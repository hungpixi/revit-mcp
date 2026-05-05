// Ping RevitMCPPlugin via TCP JSON-RPC (localhost:8080).
// Usage:
//   node .\revit-ping.js

const net = require("net");

function sendJsonRpc({ host = "127.0.0.1", port = 8080, method, params = {} }) {
  return new Promise((resolve, reject) => {
    const socket = new net.Socket();
    socket.setNoDelay(true);

    let buf = "";
    let done = false;

    const finish = (err, value) => {
      if (done) return;
      done = true;
      try {
        socket.destroy();
      } catch {}
      err ? reject(err) : resolve(value);
    };

    socket.on("error", (e) => finish(e));
    socket.on("data", (chunk) => {
      buf += chunk.toString("utf8");
      // Plugin appends '\n' after each response.
      const idx = buf.indexOf("\n");
      if (idx >= 0) {
        const line = buf.slice(0, idx).trim();
        if (!line) return;
        try {
          const obj = JSON.parse(line);
          finish(null, obj);
        } catch (e) {
          finish(new Error(`Failed to parse response JSON: ${e.message}\n${line}`));
        }
      }
    });

    socket.connect(port, host, () => {
      const id = `${Date.now()}`;
      const msg = JSON.stringify({ jsonrpc: "2.0", id, method, params });
      socket.write(msg + "\n");
    });

    setTimeout(() => finish(new Error("Timeout waiting for Revit response (10s)")), 10_000);
  });
}

(async () => {
  try {
    const ping = await sendJsonRpc({ method: "ping" });
    console.log("PING:", ping);

    const hello = await sendJsonRpc({ method: "say_hello" });
    console.log("SAY_HELLO:", hello);
  } catch (e) {
    console.error("FAILED:", e.message || e);
    process.exit(1);
  }
})();

