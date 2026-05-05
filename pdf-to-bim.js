// End-to-end: PDF binder -> extract material specs -> create Revit materials/patterns/types.
// Usage:
//   PS D:\CODE\revit-mcp> node .\pdf-to-bim.js

const path = require("path");
const net = require("net");

async function extractSpecs(pdfPath) {
  const { pathToFileURL } = require("url");
  const modUrl = pathToFileURL(
    path.resolve(__dirname, "revit-mcp", "build", "utils", "PDFExtractor.js")
  ).href;
  const { extractFromDocument } = await import(modUrl);
  return await extractFromDocument(pdfPath);
}

function sendJsonRpc({ host = "127.0.0.1", port = 8080, method, params = {} }) {
  return new Promise((resolve, reject) => {
    const socket = new net.Socket();
    socket.setNoDelay(true);

    let buf = "";
    let done = false;
    const finish = (err, value) => {
      if (done) return;
      done = true;
      try { socket.destroy(); } catch {}
      err ? reject(err) : resolve(value);
    };

    socket.on("error", (e) => finish(e));
    socket.on("data", (chunk) => {
      buf += chunk.toString("utf8");
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

    setTimeout(() => finish(new Error("Timeout waiting for Revit response (30s)")), 30_000);
  });
}

(async () => {
  const pdfPath = "D:/CODE/revit-mcp/131203-MAU NHA SO 5_BINDER.pdf";
  console.log("Reading PDF:", pdfPath);

  const extracted = await extractSpecs(pdfPath);
  console.log(
    "Extracted:",
    JSON.stringify(
      {
        pages: extracted.pageCount,
        materials: extracted.materials.length,
        doors: extracted.doors.length,
        windows: extracted.windows.length,
      },
      null,
      2
    )
  );

  // Build payload for create_brick_component
  const materials = extracted.materials.map((m) => ({
    code: m.code,
    name: m.name,
    category: m.category,
    widthMm: m.widthMm,
    heightMm: m.heightMm,
    thicknessMm: m.thicknessMm ?? 10,
    jointWidthMm: m.jointWidthMm ?? 3,
    patternType: m.patternType ?? "stack",
    colorHex: m.colorHex ?? "#CCCCCC",
    finish: m.finish ?? "",
    locations: m.locations ?? [],
    // heuristics: floor materials -> FloorType; wall materials -> WallType
    createFloorType: m.category !== "wall",
    createWallType: m.category === "wall",
  }));

  console.log("Sending to Revit: create_brick_component ...");
  const resp = await sendJsonRpc({
    method: "create_brick_component",
    params: {
      materials,
      overwriteExisting: false,
    },
  });

  console.log("Revit response:", JSON.stringify(resp, null, 2));
})();

