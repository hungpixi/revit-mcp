// Link DWG files from ./CAD into current Revit document (native link_cad_files),
// then list ImportInstance names via get_file_info.
//
// Usage:
//   node .\cad-link-and-list.js

const { spawnSync } = require("child_process");

function call(method, paramsObj) {
  const json = JSON.stringify(paramsObj);
  const r = spawnSync(process.execPath, ["./revit-call.js", method, "--stdin"], {
    cwd: __dirname,
    input: json,
    encoding: "utf8",
  });
  return { code: r.status, out: r.stdout, err: r.stderr };
}

function callTool(tool, paramsObj) {
  const json = JSON.stringify(paramsObj);
  const r = spawnSync(process.execPath, ["./revit-call.js", tool, "--stdin"], {
    cwd: __dirname,
    input: json,
    encoding: "utf8",
  });
  return { code: r.status, out: r.stdout, err: r.stderr };
}

function listDwgFiles(dir) {
  const fs = require("fs");
  const path = require("path");
  return fs
    .readdirSync(dir)
    .filter((f) => f.toLowerCase().endsWith(".dwg"))
    .map((f) => path.join(dir, f));
}

function linkCadFiles(filePaths) {
  return callTool("link_cad_files", {
    filePaths,
    placement: "origin",
    skipIfAlreadyLinked: true,
  });
}

// Fallback: SendCode expects: { code: "...", parameters: [] }
function sendCode(code, parameters = []) {
  return callTool("send_code_to_revit", { code, parameters });
}

const code = `
using System;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

// parameters[0] = CAD folder absolute path
string cadDir = parameters != null && parameters.Count > 0 ? (parameters[0]?.ToString() ?? "") : "";
if (string.IsNullOrWhiteSpace(cadDir) || !Directory.Exists(cadDir))
  return new JObject{["status"]="error",["message"]="CAD directory not found: "+cadDir};

string[] dwgs = Directory.GetFiles(cadDir, "*.dwg");
if (dwgs.Length == 0)
  return new JObject{["status"]="error",["message"]="No DWG files found in: "+cadDir};

int linked = 0;
var results = new JArray();

using (var tx = new Transaction(doc, "MCP: Link CAD DWGs"))
{
  tx.Start();
  foreach (var dwgPath in dwgs)
  {
    try
    {
      string name = Path.GetFileName(dwgPath);
      // Avoid linking duplicates by checking existing ImportInstance names
      bool exists = new FilteredElementCollector(doc)
        .OfClass(typeof(ImportInstance))
        .Cast<ImportInstance>()
        .Any(ii => (ii.Name ?? "").IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
      if (exists)
      {
        results.Add(new JObject{["file"]=name,["status"]="skipped",["reason"]="already linked"});
        continue;
      }

      var opts = new DWGImportOptions();
      opts.Placement = ImportPlacement.Origin;
      opts.Unit = ImportUnit.Default;
      ElementId id;
      bool ok = doc.Link(dwgPath, opts, doc.ActiveView, out id);
      if (ok)
      {
        linked++;
        results.Add(new JObject{["file"]=name,["status"]="linked",["importId"]=id.IntegerValue});
      }
      else
      {
        results.Add(new JObject{["file"]=name,["status"]="error",["message"]="doc.Link returned false"});
      }
    }
    catch (Exception ex)
    {
      results.Add(new JObject{["file"]=Path.GetFileName(dwgPath),["status"]="error",["message"]=ex.Message});
    }
  }
  tx.Commit();
}

return new JObject{["status"]="success",["linked"]=linked,["results"]=results};
`;

const cadDir = "D:\\\\CODE\\\\revit-mcp\\\\CAD";
let r1;
try {
  const filePaths = listDwgFiles(cadDir);
  r1 = linkCadFiles(filePaths);
} catch (e) {
  r1 = sendCode(code, [cadDir]);
}
process.stdout.write(r1.out || "");
process.stderr.write(r1.err || "");

const r2 = callTool("get_file_info", { includeLinkedFiles: true, includePerformanceMetrics: true });
process.stdout.write(r2.out || "");
process.stderr.write(r2.err || "");

