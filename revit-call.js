// Generic JSON-RPC caller for RevitMCPPlugin (localhost:8080).
// Usage examples:
//   node .\revit-call.js ping
//   node .\revit-call.js get_file_info "{\"includeLinkedFiles\":true}"
//
// Notes:
// - Second arg is a JSON string for params (optional).

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

      // Prefer newline-delimited frames if present.
      const nl = buf.indexOf("\n");
      if (nl >= 0) {
        const line = buf.slice(0, nl).trim();
        if (!line) return;
        try {
          finish(null, JSON.parse(line));
        } catch (e) {
          finish(new Error(`Failed to parse response JSON: ${e.message}\n${line}`));
        }
        return;
      }

      // If the plugin sends a raw JSON object (no newline), detect completion via brace counting.
      const s = buf.trimStart();
      if (!s.startsWith("{")) return;

      let depth = 0;
      let inString = false;
      let escaped = false;

      for (let i = 0; i < s.length; i++) {
        const c = s[i];
        if (escaped) {
          escaped = false;
          continue;
        }
        if (c === "\\" && inString) {
          escaped = true;
          continue;
        }
        if (c === "\"") {
          inString = !inString;
          continue;
        }
        if (inString) continue;
        if (c === "{") depth++;
        else if (c === "}") {
          depth--;
          if (depth === 0) {
            const candidate = s.slice(0, i + 1);
            try {
              finish(null, JSON.parse(candidate));
            } catch (e) {
              finish(new Error(`Failed to parse response JSON: ${e.message}\n${candidate}`));
            }
            return;
          }
        }
      }
    });

    socket.connect(port, host, () => {
      const id = `${Date.now()}`;
      const msg = JSON.stringify({ jsonrpc: "2.0", id, method, params });
      socket.write(msg + "\n");
    });

    setTimeout(() => finish(new Error("Timeout waiting for Revit response (30s)")), 30_000);
  });
}

(async () => {
  const method = process.argv[2];
  if (!method) {
    console.error("Usage: node revit-call.js <method> [paramsJson]");
    process.exit(2);
  }

  let params = {};
  if (process.argv[3] && process.argv[3] !== "--stdin") {
    try {
      params = JSON.parse(process.argv[3]);
    } catch (e) {
      console.error("Invalid params JSON:", e.message);
      process.exit(2);
    }
  } else if (process.argv[3] === "--stdin") {
    const chunks = [];
    for await (const c of process.stdin) chunks.push(c);
    const raw = Buffer.concat(chunks).toString("utf8").trim();
    if (raw) {
      try {
        params = JSON.parse(raw);
      } catch (e) {
        console.error("Invalid params JSON from stdin:", e.message);
        process.exit(2);
      }
    }
  }

  try {
    const resp = await sendJsonRpc({ method, params });
    console.log(JSON.stringify(resp, null, 2));
  } catch (e) {
    console.error("FAILED:", e.message || e);
    process.exit(1);
  }
})();

