# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.2] - 2026-03-31

### Added
- `AutoDismissFailuresPreprocessor` in `App.cs` — auto-dismiss all Revit warnings during Transaction commits
- `DialogBoxShowing` event handler — auto-override TaskDialog, MessageBox, and generic dialogs with OK result
- `FailuresProcessing` event handler — auto-delete warning-level failures to prevent UI blocking
- Ribbon UI tab "AI AGENT" with "Bridge Controls" panel and status button (`DummyCommand`)
- `revit_security_bypass.ps1` — auto-click "Always Load" on Revit unsigned add-in security dialog using `{LEFT}{LEFT}{ENTER}` keystroke automation
- `run_full_automation.ps1` — one-click automation script: kills old Revit → starts security guard → launches Revit with template → runs NML animation
- `animate_nml.js` — NML factory animation script for automated drawing demonstration
- `gen_nml_blueprint.js` — JSON blueprint generator for NML factory electrical model

### Changed
- HTTP listener timeout increased from 60s to 120s for long-running blueprint compilations
- `open_new_project` action now auto-discovers template paths from multiple fallback locations (`US Metric/Electrical-Default_Metric.rte`, `DefaultMetric.rte`, `Construction-Default_Metric.rte`)
- `App.cs` startup flow now includes auto-dismiss hooks before starting the web server

### Fixed
- `revit_security_bypass.ps1` — fixed PowerShell comment syntax (`//` → `#`) and added missing `Add-Type -AssemblyName System.Windows.Forms`

## [2.0.1] - 2026-03-31

### Added
- `BlueprintCompiler.cs` — batch-compile entire buildings from a single JSON blueprint file in one `TransactionGroup`
- `compile_blueprint_to_revit` MCP tool with 5-minute timeout for large factory models
- Headless project generation: create Revit project from template in background without UI document
- Virtual ID mapping system (`virtualIdMap`) for cross-referencing blueprint elements (e.g. panels ↔ circuits)
- Blueprint support for: Levels, Grids, Walls, Columns, Cable Trays, Conduits, Equipment, Lighting, Circuits
- Auto-routing with elbow fittings for cable trays and conduits within blueprint compilation
- `GeometryUtils.cs` — `SnapToGrid()` utility for aligning cable tray endpoints to grid precision
- `SaveAsOptions` with `OverwriteExistingFile` for headless file output

### Changed
- `BIMCommandHandler.cs` — added `build_from_blueprint` action routing to `BlueprintCompiler`
- `index.ts` — added `compile_blueprint_to_revit` tool definition with `jsonPath` parameter

## [2.0.0] - 2026-03-30

### Added
- `ElectricalHandler.cs` — full MEP electrical modeling:
  - `create_cable_tray`: multi-segment cable tray routing with auto-sizing (width/height in mm → feet)
  - `create_conduit`: conduit routing with configurable diameter
  - `place_electrical_equipment`: batch-place electrical panels with `DynamicFamilyLoader`
  - `place_lighting_fixture`: two modes — grid pattern (mass placement) and individual point placement
  - `create_electrical_circuit`: create `ElectricalSystem` circuits and connect to panels
- `HvacHandler.cs` — HVAC duct routing with auto-elbow fitting connections
- `PlumbingHandler.cs` — plumbing pipe creation
- `StructHandler.cs` — structural columns and framing placement
- `VDCHandler.cs` — 5D quantity extraction (BIM→QTO) with token-optimized JSON output and 4D schedule parameter updates
- `ArchHandler.cs` — expanded architectural tools: `create_grid`, `create_level`, `create_wall`, `place_family_instance`, `import_image`
- `QueryHandler.cs` — element query tools: `get_element_ids_by_category`, `get_project_info`, `get_levels`
- `DynamicFamilyLoader.cs` — auto-discover and activate `FamilySymbol` by `BuiltInCategory` with fallback logic
- `MEPRoutingUtils.cs` — `AutoConnectElbow()` utility for automatic fitting insertion between adjacent MEP curves
- `execute_pipeline` MCP tool — macro pipeline for chaining multiple API/shortcut steps in sequence
- `execute_revit_command` MCP tool — trigger any Revit ribbon command by `CommandId`
- `shortcuts.json` — shortcut code-to-command mapping database
- `logger.ts` — structured macro execution logging to file
- `FileHandler.cs` — `create_project_from_template`, `save_project`, `close_project` actions
- MCP tools: `get_element_ids_by_category`, `get_project_info`, `extract_5d_quantities`, `update_4d_schedule`, `place_lighting_fixture`, `create_electrical_circuit`

### Changed
- `BIMCommandHandler.cs` — refactored from monolithic handler to modular dispatcher routing actions to specialized handlers (Arch, HVAC, Plumbing, Struct, Electrical, VDC, Query, File)
- JSON protocol upgraded to structured `{"action": "xxx", "payload": {...}}` format (v2.0)
- `index.ts` — expanded from 4 tools to 16 tools covering full BIM lifecycle

### Removed
- Legacy inline command parsing (raw string matching without `action` field)

## [1.0.0] - 2026-03-29

### Added
- Initial MCP Server (`index.ts`) with `@modelcontextprotocol/sdk` stdio transport
- C# Revit Add-in (`App.cs`) with `IExternalApplication` lifecycle
- `BIMCommandHandler.cs` with `IExternalEventHandler` for thread-safe Revit API access
- `AICommandQueue` — `ConcurrentQueue`-based async command pipeline with `TaskCompletionSource` for HTTP response pairing
- HTTP local web server on `localhost:5050` for Node.js ↔ Revit communication
- `AntigravityBridge.addin` manifest for Revit 2020
- Basic MCP tools:
  - `open_new_project` — open Revit new project dialog
  - `create_wall` — draw wall by start/end points (feet)
  - `get_levels` — query project levels
  - `place_family_instance` — place door/window family at point
- Jest test configuration (`jest.config.js`, `index.test.ts`)
- TypeScript build pipeline (`tsconfig.json`)
- `install.bat` for Node.js dependency setup
