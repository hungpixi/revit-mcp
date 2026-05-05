// Wrapper to run the MCP test script from repo root.
// Usage:
//   PS D:\CODE\revit-mcp> node .\test-mcp-server.js

const { spawnSync } = require("child_process");
const path = require("path");

const subdir = path.join(__dirname, "revit-mcp");
const script = path.join(subdir, "test-mcp-server.js");

const result = spawnSync(process.execPath, [script], {
  cwd: subdir,
  stdio: "inherit",
});

process.exit(result.status ?? 1);

