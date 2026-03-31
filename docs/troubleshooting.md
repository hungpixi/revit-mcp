# Troubleshooting Guide

This document assists in resolving common issues encountered while running the Revit MCP Bridge.

## Table of Contents
1. [AI Agent Disconnection](#1-ai-agent-disconnection-cdp-port-9004-error)
2. [Revit Frozen at Startup](#2-revit-frozen-at-startup-security-guard-fails)
3. [Macro Timeout Errors](#3-macro-timeout-errors)
4. [Quota & Token Monitoring](#4-quota--token-monitoring)

---

### 1. AI Agent Disconnection (CDP Port 9004 Error)
**Symptom:**
The Antigravity Multi-Purpose Agent disconnects, throwing an error: *"Multi Purpose Agent could not connect to CDP port 9004"*.

**Root Cause:**
The IDE or browser extension requires Chrome DevTools Protocol metrics to be running on port 9004, but the target browser launched without the debug flag.

**Resolution:**
We have included a specific automation skill for this. Run the Antigravity CDP Port Fix:
```powershell
# Manually run if needed
d:\CODE\revit-mcp\skills\antigravity-cdp-fix\fix_port.ps1
```
Or ensure your Antigravity IDE launcher includes the flag: `--remote-debugging-port=9004`.

---

### 2. Revit Frozen at Startup (Security Guard Fails)
**Symptom:**
`run_full_automation.ps1` hangs at *"Waiting for API Port 5050 to become active..."*. Opening the Revit UI reveals the "Security - Unsigned Add-In" dialog box is waiting for user input.

**Root Cause:**
By default, Revit prompts the user for confirmation when a dynamically loaded `.dll` lacks a recognized publisher certificate. If `revit_guard.ps1` or `revit_security_bypass.ps1` fails to catch it, the process deadlocks.

**Resolution:**
1. Manually force close Revit: `Stop-Process -Name Revit -Force`
2. Run the dedicated guard script to verify the window targeting:
   ```powershell
   .\revit_security_bypass.ps1
   ```
3. The script monitors for the specific Window Title `Security - Unsigned Add-In`. Ensure your OS language is English, otherwise, the Window Title might be localized (e.g., "Sécurité" in French), which causes `FindWindow` to fail.

---

### 3. Macro Timeout Errors
**Symptom:**
Node.js throws an `HTTP Error [action_name]: timeout of 30000ms exceeded`.

**Root Cause:**
Complex API operations like building hundreds of cable trays or aligning conduit elbows can take several minutes.

**Resolution:**
The base timeout has been increased to 120s for `compile_blueprint_to_revit` in `index.ts`. If running massive factory diagrams, increase the Axios timeout in your Node script:
```javascript
const response = await axios.post(API_URL, payload, { timeout: 300000 }); // 5 minutes
```
Note: The Revit C# server puts the request on an `AICommandQueue` and answers immediately for simple async tasks, but synchronous compilation tasks deliberately block until finished to report accurate `elementId` maps back to the AI.

---

### 4. Quota & Token Monitoring
**Symptom:**
The AI refuses to execute commands, loops infinitely, or requests are dropped.

**Root Cause:**
Sending heavy geometric representations (like hundreds of XYZ vectors) directly via Markdown prompts exhausts token quotas quickly.

**Resolution:**
You must use the **Blueprint Compiler (JSON)** approach rather than individual tool calls. Instead of `create_wall` 50 times (consuming high output tokens), format the 50 walls locally into `nml_factory.json` and call `compile_blueprint_to_revit` once.

When verifying the model, do not query all elements. Use `extract_5d_quantities` with a strict `limit: 50` parameter to prevent overflowing the AI's context window.
