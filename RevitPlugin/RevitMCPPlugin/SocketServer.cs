using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace RevitMCPPlugin
{
    /// <summary>
    /// Async TCP server that listens on the configured port, accepts multiple
    /// concurrent clients, reads newline-delimited or complete JSON objects,
    /// dispatches them to RevitCommandHandler and writes the response back.
    /// </summary>
    public class SocketServer
    {
        private readonly int _port;
        private TcpListener _listener;
        private volatile bool _running;
        private CancellationTokenSource _cts;

        public SocketServer(int port)
        {
            _port = port;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start();
            _running = true;
            Task.Run(() => AcceptLoop(_cts.Token));
            App.Log($"SocketServer listening on 127.0.0.1:{_port}");
        }

        public void Stop()
        {
            _running = false;
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }
            App.Log("SocketServer stopped");
        }

        private async Task AcceptLoop(CancellationToken ct)
        {
            while (_running && !ct.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    client.NoDelay = true;
                    client.ReceiveTimeout = 0; // no read timeout — connection kept alive
                    _ = Task.Run(() => HandleClient(client, ct), ct);
                }
                catch (ObjectDisposedException)
                {
                    // Listener was stopped — exit gracefully
                    break;
                }
                catch (Exception ex) when (!_running)
                {
                    App.Log($"AcceptLoop stopped: {ex.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    App.Log($"AcceptLoop error: {ex.Message}");
                }
            }
        }

        private async Task HandleClient(TcpClient client, CancellationToken ct)
        {
            string remoteEp = client.Client?.RemoteEndPoint?.ToString() ?? "unknown";
            App.Log($"Client connected: {remoteEp}");

            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    var buffer = new byte[65536];
                    var sb = new StringBuilder();

                    while (!ct.IsCancellationRequested && client.Connected)
                    {
                        int bytesRead;
                        try
                        {
                            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }

                        if (bytesRead == 0) break; // client closed connection

                        sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));

                        // Process all complete JSON objects in the buffer
                        while (true)
                        {
                            string buffered = sb.ToString();
                            int leadingWhitespace = CountLeadingWhitespace(buffered);
                            if (leadingWhitespace == buffered.Length)
                            {
                                sb.Clear();
                                break;
                            }

                            if (leadingWhitespace > 0)
                            {
                                sb.Remove(0, leadingWhitespace);
                            }

                            string accumulated = sb.ToString();

                            JObject request = TryParseJson(accumulated, out int consumed);
                            if (request == null) break; // incomplete JSON — wait for more

                            sb.Remove(0, consumed);

                            // Dispatch and send response
                            string response;
                            try
                            {
                                response = await App.CommandHandler.HandleRequestAsync(request);
                            }
                            catch (Exception ex)
                            {
                                App.Log($"HandleRequestAsync error: {ex.Message}");
                                string rid = request["id"]?.Value<string>();
                                response = MakeError(rid, $"Internal error: {ex.Message}");
                            }

                            // Append newline delimiter so clients can split responses easily
                            byte[] responseBytes = Encoding.UTF8.GetBytes(response + "\n");
                            try
                            {
                                await stream.WriteAsync(responseBytes, 0, responseBytes.Length, ct);
                                await stream.FlushAsync(ct);
                            }
                            catch (Exception ex)
                            {
                                App.Log($"Write error: {ex.Message}");
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log($"Client {remoteEp} error: {ex.Message}");
            }

            App.Log($"Client disconnected: {remoteEp}");
        }

        /// <summary>
        /// Attempts to parse a complete JSON object from <paramref name="raw"/>.
        /// Returns null if the JSON is incomplete; sets <paramref name="consumed"/>
        /// to the number of characters that form the parsed object (so the caller
        /// can remove them from the accumulation buffer).
        /// </summary>
        private static JObject TryParseJson(string raw, out int consumed)
        {
            consumed = 0;

            // Fast-path: try a newline-delimited message first
            int newlineIdx = raw.IndexOf('\n');
            if (newlineIdx >= 0)
            {
                string line = raw.Substring(0, newlineIdx).Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    try
                    {
                        var obj = JObject.Parse(line);
                        consumed = newlineIdx + 1;
                        return obj;
                    }
                    catch { /* not valid JSON on its own — fall through to brace counting */ }
                }
                else
                {
                    consumed = newlineIdx + 1;
                    return null; // empty line; caller will loop again
                }
            }

            // Brace-counting approach for non-newline-terminated streams
            if (!raw.TrimStart().StartsWith("{")) return null;

            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];

                if (escaped) { escaped = false; continue; }
                if (c == '\\' && inString) { escaped = true; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (inString) continue;

                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        string candidate = raw.Substring(0, i + 1);
                        try
                        {
                            var obj = JObject.Parse(candidate);
                            consumed = i + 1;
                            return obj;
                        }
                        catch
                        {
                            return null; // malformed JSON
                        }
                    }
                }
            }

            return null; // JSON not yet complete
        }

        private static int CountLeadingWhitespace(string raw)
        {
            int count = 0;
            while (count < raw.Length && char.IsWhiteSpace(raw[count]))
            {
                count++;
            }

            return count;
        }

        private static string MakeError(string id, string msg)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"]      = id,
                ["error"]   = new JObject
                {
                    ["code"]    = -32603,
                    ["message"] = msg
                }
            }.ToString(Newtonsoft.Json.Formatting.None);
        }
    }
}
