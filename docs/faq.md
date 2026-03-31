# Frequently Asked Questions (FAQ)

Find quick answers to common questions about using the Revit MCP Bridge and Antigravity Agent.

### Q: Why isn't the AI creating any elements when I ask it to?
**A:** Check the `run_full_automation.ps1` script first to see if Revit launched. If Revit is open, ensure there isn't a modal popup blocking the UI thread. The C# Add-In cannot execute queued API transactions if Revit is stuck asking for permission, missing fonts, or warning you about overlapping lines.

### Q: How do I get around the "Unsigned Add-in" error?
**A:** Our system uses a background PowerShell watcher (`revit_guard.ps1` or `revit_security_bypass.ps1`) to automatically click "Always Load". Run this watcher **before** launching Revit in headless mode to guarantee success.

### Q: Does this support Revit 2021, 2022, 2023, or 2024?
**A:** The base `.csproj` is mapped to Revit 2020 (`C:\Program Files\Autodesk\Revit 2020`). You can upgrade it by changing the `.NET` Framework Target (usually 4.8 for newer Revits) and updating the `HintPath` references for `RevitAPI.dll` and `RevitAPIUI.dll`. The C# `Transaction` system is generally backwards-compatible.

### Q: How can I debug connection errors?
**A:** Open `%LOCALAPPDATA%\Autodesk\Revit\Autodesk Revit 2020\Journals\`. The Revit AI Add-In logs critical boot errors directly to the journal.
On the Node.js side, verify `http://localhost:5050` is active. Use `axios` or standard `curl` requests to `/api/project-info` to test the bridge manually.

### Q: Where do I fix the CDP port 9004 issue for the Agent extension?
**A:** You need to explicitly pass the debug flag when starting your Chrome/Edge browser for the IDE extension to communicate. See the [Troubleshooting Guide](troubleshooting.md#1-ai-agent-disconnection-cdp-port-9004-error).

### Q: I exceeded my Claude/OpenAI token quota using the architectural tools.
**A:** For factory-scale generation, **do not** use conversational prompts to build 500 cable tray segments one by one. Use the Blueprint Compiler. Pass a JSON configuration to `compile_blueprint_to_revit`, allowing the Add-in to process 500 segments instantly in one fast `Transaction` without draining your MCP Server interaction tokens.

### Q: Can I run this in true "Headless" mode?
**A:** Yes. `create_project_from_template` operates purely in memory if a UI Document hasn't been instantiated yet. You can compile a blueprint and save it out to an RVT file, entirely automated.
