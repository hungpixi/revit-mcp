<div align="center">

# Revit Agent MCP Bridge

**Connect your AI Agent directly to Autodesk Revit for massive, asynchronous, and automated BIM generation.**

[![Version](https://vsmarketplacebadge.apphb.com/version/antigravity.revit-mcp.svg)]()
[![Installs](https://vsmarketplacebadge.apphb.com/installs/antigravity.revit-mcp.svg)]()
[![Ratings](https://vsmarketplacebadge.apphb.com/rating/antigravity.revit-mcp.svg)]()
[![License](https://img.shields.io/badge/License-MIT-blue.svg)]()

> Transform hour-long modeling tasks into 30-second AI prompts. Experience the first Model Context Protocol (MCP) server truly designed for the Architecture, Engineering, and Construction (AEC) industry.

</div>

---

## 🚀 Why Revit MCP Bridge?

Are you tired of UI freezes when scripting Revit? Frustrated by token-heavy AI outputs dropping mid-generation? The Revit MCP Bridge is built for **scale and autonomous reliability**.

By running a local HTTP server inside a native C# Revit Add-in, we decoupling the AI's speed from Revit's UI rendering limitations. Your AI Agent can now compile entire factories, route miles of MEP conduits, and extract 5D quantities—all while you grab a coffee.

### Core Benefits
* **Stop Wasting Time on Repetitive Drafting:** Describe your building layout to your AI. Watch the walls, cable trays, panels, and lighting fixtures populate automatically.
* **Eliminate UI Freezes & Crashes:** Our asynchronous `AICommandQueue` processes tasks safely in the background, never locking up Revit's main interface.
* **Save Prompt Tokens:** Send a single Blueprint JSON. We compile hundreds of geometries in one transaction block, saving API costs and avoiding context-window exhaustion.
* **True Headless Automation:** Built-in Security Guardians bypass unsigned add-in dialogues so your overnight batch tasks actually finish by morning.

---

## 🏗️ Feature Comparison

| Method | Revit MCP Bridge (Ours) | Standard Dynamo / Macros | Traditional API Plugins |
| :--- | :--- | :--- | :--- |
| **Trigger Mechanism** | Conversational AI (Natural Language) | Manual Graph Execution | Hardcoded C# Buttons |
| **Execution Speed** | Massive Batch Transactions | Node-by-Node (Slow) | Fast |
| **UI Blocking** | **None** (Async HTTP Queue) | Fully Blocks UI | Fully Blocks UI |
| **Token Efficiency** | **High** (Blueprint Compiler) | N/A | N/A |
| **Unattended Operation** | **Yes** (Bypasses security popups) | No | No |
| **Cross-Discipline** | Arch, Struct, HVAC, Elec, Plumbing | Segmented | Highly Specific |

---

## 🎞️ See It In Action!

*(![Insert GIF here demonstrating the AI Agent typing a prompt and the NML Factory assembling itself piece by piece in Revit's 3D View without user interaction.])*

> *The animation above shows the `animate_nml.js` script dynamically routing cable trays and populating lighting fixtures autonomously.*

---

## 🛠️ Installation

Getting started requires two steps: setting up the Node.js MCP Server and installing the native Revit Add-in.

1. **Clone the Companion Code:** Pull the [Revit MCP Bridge Repository](#) to your local machine (`d:\CODE\revit-mcp\`).
2. **Setup the Node Server:** 
   Navigate to `/RevitMCP_Node/` and run `install.bat`. This builds the TypeScript MCP interface.
3. **Install the C# Add-in:**
   Compile the Visual Studio Project in `/RevitBridge_CSharp/` (target: `.NET Framework 4.7.2` mapping to your `RevitAPI.dll`). Copy `AntigravityBridge.addin` and the compiled `.dll` to `C:\ProgramData\Autodesk\Revit\Addins\2020\`.

---

## ⚙️ Configuration Walkthrough

To connect the bridge to your IDE (like Antigravity IDE, Cursor, or Claude Desktop), add the server path to your MCP configuration file:

```json
{
  "mcpServers": {
    "revit-bim": {
      "command": "node",
      "args": [
        "C:/path/to/your/revit-mcp/RevitMCP_Node/dist/index.js"
      ]
    }
  }
}
```

### Essential Workflows
For robust operation, we recommend assigning the provided orchestration scripts:

- **Launch from Scratch:** Run `run_full_automation.ps1` to automatically kill frozen Revit instances, launch your template, and suppress the "Unsigned Add-in" dialogue using our integrated Security Guardian.
- **Troubleshooting CDP:** If your AI Agent throws a "port 9004" connection error, run the included `fix_port.ps1` skill to configure Chrome DevTools.

For comprehensive documentation on routing constraints, 4D property updates, and quantity take-offs, please see our dedicated [Usage Guide](docs/usage-guide.md) and [FAQ](docs/faq.md).

---

## 🏷️ Tags / Keywords
`revit`, `bim`, `mcp`, `ai`, `automation`, `architecture`, `engineering`, `mep`, `csharp`, `nodejs`, `claude`, `agent`
