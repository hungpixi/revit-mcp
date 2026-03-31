# Revit MCP Usage Guide

Welcome to the Antigravity Revit MCP Bridge documentation. This guide explains how to use the automated Revit integration pipeline.

## Overview
The Revit MCP (Model Context Protocol) Bridge allows an AI Agent (like the Antigravity Multi-Purpose Agent) to natively control Autodesk Revit. It bypasses Revit's UI-thread limitations by running an asynchronous HTTP server directly inside the Revit process, receiving batch JSON commands, and executing them safely within a `TransactionGroup`.

## Installation & Setup

### Prerequisites
1. **Autodesk Revit 2020** (or compatible versions) installed on `C:\Program Files\Autodesk\Revit 2020\Revit.exe`.
2. **Node.js** (v18+) installed.
3. **PowerShell 5.1+** for automation scripts.

### 1. Build the Node.js MCP Server
Navigate to the Node directory and run the install script:
```bat
cd RevitMCP_Node
install.bat
```
This installs NPM dependencies and builds the TypeScript codebase into the `dist/` directory.

### 2. Build the C# Add-in
Ensure the internal `HintPath` references in `RevitBridge_CSharp/RevitBridge.csproj` point correctly to your Revit installation directory.
Open the project in Visual Studio or use MSBuild:
```bat
cd RevitBridge_CSharp
msbuild RevitBridge.csproj /p:Configuration=Release
```

### 3. Deploy the Manifest
Copy the `AntigravityBridge.addin` file and the compiled `RevitBridge.dll` to your Revit Add-ins folder:
`%APPDATA%\Autodesk\Revit\Addins\2020\`

## Configuration

### MCP Configuration
To connect this tool to your Antigravity IDE or Claude platform, configure the MCP server settings:
```json
{
  "mcpServers": {
    "revit-bim": {
      "command": "node",
      "args": ["d:/CODE/revit-mcp/RevitMCP_Node/dist/index.js"]
    }
  }
}
```

## Running the Pipeline

You can run individual scripts or use the orchestrator to manage the entire lifecycle.

### Full Orchestration (`run_full_automation.ps1`)
This provides a "One-Click" automation experience:
1. Opens PowerShell as Administrator (if required).
2. Kills any frozen Revit instances (`Stop-Process -Name Revit -Force`).
3. Starts the Headless Revit instance loading a metric/imperial template.
4. Starts the **Security Guardian** (`revit_guard.ps1`) to bypass the "Unsigned Add-in" popup automatically.
5. Polls for port `5050` listening.
6. Starts the Node.js Macro/Automation process (e.g., `animate_nml.js`).

**Command:**
```powershell
.\run_full_automation.ps1
```

### Queue Mode & The Blueprint Compiler
Instead of sending hundreds of sequential requests—which causes UI freezing and high token consumption—this system uses **Blueprint Compilation**.

Generate a complete JSON architecture describing the entire factory (like `nml_factory_full.json`), then pass the path to the `compile_blueprint_to_revit` tool.

The C# Bridge will queue this request locally and execute it as a massive `TransactionGroup`, ensuring robust execution without breaking the connection.

### Example Animation (`animate_nml.js`)
If you wish to see the drawing happening live (to record a demo), use the `animate_nml.js` script. It parses the JSON blueprint and sends line-by-line API requests with small `SetTimeout` delays to visualize the modeling process.
