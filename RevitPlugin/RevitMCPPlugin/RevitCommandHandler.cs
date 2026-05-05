using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCP.Handlers;

namespace RevitMCPPlugin
{
    /// <summary>
    /// IExternalEventHandler that bridges background TCP threads with the Revit
    /// main thread via Revit's ExternalEvent mechanism.
    ///
    /// Flow:
    ///   1. Background thread calls HandleRequestAsync(request).
    ///   2. Request is enqueued + ExternalEvent.Raise() is called.
    ///   3. Revit calls Execute() on the main thread.
    ///   4. Execute() dequeues items, dispatches to handlers, sets TCS results.
    ///   5. HandleRequestAsync awaits the TCS and returns the serialised response.
    /// </summary>
    public class RevitCommandHandler : IExternalEventHandler
    {
        // ─── Inner type ───────────────────────────────────────────────────────

        private class RequestItem
        {
            public JObject Request { get; set; }
            public TaskCompletionSource<string> Tcs { get; set; }
        }

        // ─── State ────────────────────────────────────────────────────────────

        private readonly ConcurrentQueue<RequestItem> _queue = new ConcurrentQueue<RequestItem>();

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Called from any background thread.  Enqueues the request, raises the
        /// ExternalEvent and asynchronously awaits the result.
        /// </summary>
        public async Task<string> HandleRequestAsync(JObject request)
        {
            var tcs = new TaskCompletionSource<string>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            _queue.Enqueue(new RequestItem { Request = request, Tcs = tcs });

            // Ask Revit to schedule a call to Execute() on the main thread
            ExternalEventRequest result = App.RevitEvent.Raise();
            if (result != ExternalEventRequest.Accepted)
            {
                App.Log($"ExternalEvent.Raise returned {result} for method {request["method"]}");
                // Still wait — Revit may process it on a subsequent idle
            }

            // Wait up to 120 seconds; cancel automatically on timeout
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120)))
            {
                cts.Token.Register(
                    () => tcs.TrySetCanceled(),
                    useSynchronizationContext: false);

                try
                {
                    return await tcs.Task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return MakeError(
                        request["id"]?.Value<string>(),
                        "Request timed out after 120 s");
                }
                catch (Exception ex)
                {
                    return MakeError(
                        request["id"]?.Value<string>(),
                        $"Unexpected await error: {ex.Message}");
                }
            }
        }

        // ─── IExternalEventHandler ────────────────────────────────────────────

        /// <summary>
        /// Called by Revit on the main thread.  Drains every pending request.
        /// </summary>
        public void Execute(UIApplication uiApp)
        {
            // Keep the event alive while there are items to process
            while (_queue.TryDequeue(out RequestItem item))
            {
                try
                {
                    Document doc = uiApp.ActiveUIDocument?.Document;
                    string response = Dispatch(uiApp, doc, item.Request);
                    item.Tcs.TrySetResult(response);
                }
                catch (Exception ex)
                {
                    App.Log($"Execute unhandled: {ex.Message}\n{ex.StackTrace}");
                    item.Tcs.TrySetResult(
                        MakeError(item.Request["id"]?.Value<string>(), ex.Message));
                }
            }

            // If more items arrived while we were processing, raise again
            if (!_queue.IsEmpty)
            {
                App.RevitEvent.Raise();
            }
        }

        public string GetName() => "RevitMCPPlugin.CommandHandler";

        // ─── Dispatch ─────────────────────────────────────────────────────────

        private string Dispatch(UIApplication uiApp, Document doc, JObject req)
        {
            string id     = req["id"]?.Value<string>() ?? "0";
            string method = req["method"]?.Value<string>() ?? "";
            JObject @params = req["params"] as JObject ?? new JObject();

            App.Log($"Dispatch [{id}]: {method}");

            try
            {
                JObject resultObj = method switch
                {
                    // ── Diagnostics ─────────────────────────────────────────
                    "ping"                    => new JObject { ["pong"] = true },
                    "say_hello"               => SayHello(uiApp),

                    // ── View ────────────────────────────────────────────────
                    "get_current_view_info"        => Commands.ViewCommands.GetCurrentViewInfo(doc),
                    "get_current_view_elements"    => Commands.ViewCommands.GetCurrentViewElements(doc, @params),

                    // ── Elements ────────────────────────────────────────────
                    "get_selected_elements"        => Commands.ElementCommands.GetSelectedElements(uiApp, @params),
                    "get_available_family_types"   => Commands.ElementCommands.GetAvailableFamilyTypes(doc, @params),
                    "get_material_quantities"      => Commands.ElementCommands.GetMaterialQuantities(doc, @params),
                    "ai_element_filter"            => Commands.ElementCommands.AiElementFilter(doc, @params),
                    "analyze_model_statistics"     => Commands.ElementCommands.AnalyzeModelStatistics(doc, @params),
                    "export_room_data"             => Commands.ElementCommands.ExportRoomData(doc, @params),

                    // ── Create ──────────────────────────────────────────────
                    "create_point_based_element"       => Commands.CreateCommands.CreatePointBased(doc, @params),
                    "create_line_based_element"        => Commands.CreateCommands.CreateLineBased(doc, @params),
                    "create_surface_based_element"     => Commands.CreateCommands.CreateSurfaceBased(doc, @params),
                    "create_grid"                      => Commands.CreateCommands.CreateGrid(doc, @params),
                    "create_level"                     => Commands.CreateCommands.CreateLevel(doc, @params),
                    "create_room"                      => Commands.CreateCommands.CreateRoom(doc, @params),
                    "create_structural_framing_system" => Commands.CreateCommands.CreateStructuralFraming(doc, @params),

                    // ── Operate ─────────────────────────────────────────────
                    "delete_element"  => Commands.OperateCommands.DeleteElement(doc, @params),
                    "operate_element" => Commands.OperateCommands.OperateElement(uiApp, doc, @params),
                    "color_elements"  => Commands.OperateCommands.ColorElements(doc, @params),
                    "tag_all_walls"   => Commands.OperateCommands.TagAllWalls(doc, @params),
                    "tag_all_rooms"   => Commands.OperateCommands.TagAllRooms(doc, @params),

                    // ── Code execution ──────────────────────────────────────
                    "send_code_to_revit" => Commands.CodeExecutionCommand.Execute(doc, @params),

                    // ── Extended handlers (RevitMCP.Handlers) ───────────────
                    _ => new CommandRouter(doc).Dispatch(method, @params)
                };

                return MakeSuccess(id, resultObj);
            }
            catch (Exception ex)
            {
                App.Log($"Handler error [{method}]: {ex.Message}\n{ex.StackTrace}");
                return MakeError(id, ex.Message);
            }
        }

        // ─── Built-in helpers ─────────────────────────────────────────────────

        private static JObject SayHello(UIApplication uiApp)
        {
            string version = uiApp.Application.VersionName;
            System.Windows.Forms.MessageBox.Show(
                "Hello from RevitMCP!\nConnection successful.",
                "RevitMCP",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Information);

            return new JObject
            {
                ["message"] = "Hello from Revit! Connection is working.",
                ["revitVersion"] = version
            };
        }

        // ─── JSON-RPC envelope helpers ────────────────────────────────────────

        private static string MakeSuccess(string id, JObject result) =>
            JsonConvert.SerializeObject(new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"]      = id,
                ["result"]  = result
            }, Formatting.None);

        private static string MakeError(string id, string msg) =>
            JsonConvert.SerializeObject(new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"]      = id,
                ["error"]   = new JObject
                {
                    ["code"]    = -32603,
                    ["message"] = msg
                }
            }, Formatting.None);
    }
}
