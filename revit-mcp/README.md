# Revit MCP Server - AI-Powered BIM Automation

[![MseeP.ai Security Assessment Badge](https://mseep.net/pr/revit-mcp-revit-mcp-badge.png)](https://mseep.ai/app/revit-mcp-revit-mcp)
[![npm version](https://badge.fury.io/js/revit-mcp.svg)](https://badge.fury.io/js/revit-mcp)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Node.js Version](https://img.shields.io/badge/node-%3E%3D18.0.0-brightgreen)](https://nodejs.org/)

**English** | [简体中文](README_zh.md)

> 🚀 **Revolutionary AI Integration for Autodesk Revit** - Transform your BIM workflow with intelligent automation powered by Claude AI and MCP protocol.

## 🌟 What is Revit MCP?

**Revit MCP (Model Context Protocol)** is a cutting-edge integration that bridges the gap between AI language models and Autodesk Revit's powerful BIM capabilities. This server enables AI assistants like Claude to directly interact with Revit projects, automating complex BIM tasks through natural language commands.

### 🎯 Key Benefits

- **🤖 AI-Driven BIM Automation**: Let AI handle repetitive Revit tasks
- **⚡ Natural Language Commands**: "Create a 3m wall on level 1" becomes reality
- **🔄 Real-time Collaboration**: AI works alongside human designers
- **📊 Data-Driven Insights**: Extract and analyze BIM data intelligently
- **🏗️ Parametric Design**: Generate families and elements dynamically

## ✨ Features

### 🏗️ Core BIM Operations
- **Element Management**: Create, modify, delete, and query Revit elements
- **Level & Grid Creation**: Automated structural framework setup
- **Room & Space Analysis**: Intelligent space planning and tagging
- **Material Quantification**: Automated material takeoffs and reporting

### 🎨 CAD Integration
- **DWG Import**: Seamless CAD file processing and conversion
- **Layer Analysis**: Intelligent CAD layer interpretation
- **Block Conversion**: Transform CAD blocks into parametric Revit families
- **Entity Extraction**: Extract lines, blocks, and annotations from CAD

### 📊 Data & Analytics
- **Model Statistics**: Comprehensive BIM model analysis
- **Database Integration**: SQLite-powered project data storage
- **Export Capabilities**: Generate reports and documentation
- **Performance Metrics**: Real-time processing analytics

### 🔧 Advanced Tools
- **Code Execution**: Run AI-generated Revit API scripts
- **Batch Operations**: Process multiple elements simultaneously
- **Error Handling**: Robust validation and recovery mechanisms
- **WebSocket Communication**: Real-time bidirectional data flow

## 🚀 Quick Start

### Prerequisites
- **Node.js 18+**
- **Autodesk Revit 2020+**
- **[Revit MCP Plugin](https://github.com/revit-mcp/revit-mcp-plugin)** (Required for Revit integration)

### Installation

#### 1. Clone & Install
```bash
git clone https://github.com/hungpixi/revit-mcp.git
cd revit-mcp
npm install
```

#### 2. Build the Server
```bash
npm run build
```

#### 3. Configure Claude Desktop

Edit `claude_desktop_config.json`:

```json
{
    "mcpServers": {
        "revit-mcp": {
            "command": "node",
            "args": ["C:\\path\\to\\revit-mcp\\build\\index.js"]
        }
    }
}
```

#### 4. Launch Revit Plugin

Install and run the companion [Revit MCP Plugin](https://github.com/revit-mcp/revit-mcp-plugin) in your Revit environment.

## ✅ Revit 2020 Deliverable Progress (WiseBIM @CAD → BIM)

This repository is being actively adapted and tested against **Autodesk Revit 2020** to deliver an end-to-end automation pipeline:

- **PDF binder → Materials/Types**: parse finish/spec codes (FT/GT/P) and create Revit materials/types.
- **DWG CAD → Multi-level model**: link DWGs, extract real CAD layers/entities, generate walls/openings, then QA + documentation + exports.

### Current status (as of 2026-05-05)

- **Revit 2020 add-in connectivity**: ✅ `ping` and `say_hello` confirmed.
- **PDF-to-BIM**: ✅ materials/types created successfully (evidence captured locally).
- **Levels**: ✅ Basement/Floor1/Floor2/Floor3/Roof created.
- **CAD linking**: ✅ native `link_cad_files` command implemented for Revit 2020 (requires Revit restart to load updated DLLs).
- **CAD extraction**: ✅ `get_cad_entities` and `get_layers` upgraded to read **real GraphicsStyle layer names** and output coordinates in **mm**.

### Quick refs

- **Level mapping + CAD pack summary**: see [`QUICK_SUMMARY.md`](../QUICK_SUMMARY.md)
- **CAD integration notes**: see [`PHASE1.5_INTEGRATION_GUIDE.md`](../PHASE1.5_INTEGRATION_GUIDE.md)

## 🧠 Design reasoning (token-efficient automation)

The system is intentionally split so the AI uses tokens only where it adds value:

- **Deterministic ops (no AI)**:
  - link CAD (`link_cad_files`)
  - extract layers/entities (`get_layers`, `get_cad_entities`)
  - create Revit elements (`create_*`)
  - export/schedules (`create_schedule`, `create_view_sheet`, `export_model`)
- **AI-assisted ops (minimal tokens)**:
  - map CAD layer naming → wall/opening classification
  - infer door/window mapping when CAD blocks are inconsistent
  - generate QA checks (counts, outliers) and propose fixes

This keeps runs repeatable and cheap: once the layer→element mapping is stable, the pipeline is effectively “no-AI”.

## 🤖 Toward 100% automation (no manual Revit clicks)

Why you still sometimes open Revit manually: the add-in’s MCP socket exists **only after Revit starts and loads the add-in**.

The roadmap to fully automated runs on Windows:

1. **Launcher (outside Revit)**: start `Revit.exe`, optionally open a project, then wait for the MCP port to be ready.
2. **Pipeline runner**: execute the full workflow via JSON-RPC scripts:
   - `link_cad_files` → `get_layers/get_cad_entities` → `create_line_based_element` (walls) → openings → QA → docs/export.
3. **Optimization**: cache mapping results to avoid repeated exploration prompts (token savings).

### Included helper: start Revit and wait for MCP port

From repo root:

```powershell
cd D:\\CODE\\revit-mcp
.\\start-revit-and-wait.ps1
```

Or open a specific RVT:

```powershell
.\\start-revit-and-wait.ps1 -ProjectPath \"D:\\path\\to\\your.rvt\"
```

## 📖 Usage Guide

### 🤖 AI Commands Examples

Once connected, you can use natural language commands with Claude:

```
"Create a new level called 'Roof Level' at 3000mm above Level 1"

"Extract all door blocks from the linked CAD file A-701-DANHSÁCHCỬAĐI.dwg"

"Generate material quantities report for concrete elements"

"Create parametric door family from CAD block D01 with width 2200mm"

"Tag all rooms on the current floor plan with automatic numbering"
```

### 🔧 MCP Tools Overview

The server provides 40+ specialized tools organized by functionality:

#### 📐 CAD Processing Tools
- `get_file_info` - Project metadata and linked files
- `get_cad_entities` - Extract CAD entities (lines, blocks, text)
- `get_layers` - Analyze CAD layer structure
- `get_cad_block_info` - Door/window block information
- `convert_cad_to_family` - Convert CAD blocks to Revit families

#### 🏗️ BIM Creation Tools
- `create_level` - Generate building levels
- `create_grid` - Create structural grids
- `create_room` - Automated room creation
- `create_structural_framing_system` - Framing systems
- `create_line_based_element` - Lines, walls, beams
- `create_point_based_element` - Columns, furniture
- `create_surface_based_element` - Floors, roofs, ceilings

#### 📊 Analysis & Query Tools
- `analyze_model_statistics` - Comprehensive model analysis
- `get_current_view_elements` - View-specific element queries
- `get_selected_elements` - Selected element information
- `get_material_quantities` - Material takeoff reports
- `query_stored_data` - Database queries

#### 🎨 Visualization Tools
- `color_elements` - Element highlighting and coloring
- `tag_all_rooms` - Automatic room tagging
- `tag_all_walls` - Wall tagging and annotation
- `ai_element_filter` - AI-powered element filtering

#### 💾 Data Management Tools
- `store_project_data` - Project data persistence
- `store_room_data` - Room data storage
- `export_room_data` - Room data export
- `search_modules` - Module discovery
- `use_module` - Module utilization

#### ⚙️ Advanced Tools
- `send_code_to_revit` - Execute custom Revit API scripts
- `modify_element` - Element modification
- `delete_element` - Element removal
- `operate_element` - Complex element operations

## 🏭 Development Pipeline

### Architecture Overview

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Claude AI     │────│   MCP Server    │────│  Revit Plugin   │
│                 │    │  (Node.js)      │    │  (C# .NET)      │
│ Natural Language│    │                 │    │                 │
│   Commands      │    │ • Tool Registry │    │ • Command       │
│                 │    │ • WebSocket     │    │   Execution     │
│                 │    │ • Validation    │    │ • API Bridge    │
└─────────────────┘    │ • Database      │    └─────────────────┘
                       └─────────────────┘             │
                                                      │
                                               ┌─────────────────┐
                                               │   Autodesk      │
                                               │     Revit       │
                                               │   (BIM Model)   │
                                               └─────────────────┘
```

### Development Workflow

#### Phase 1: CAD Import & Analysis ✅
- ✅ CAD file detection and validation
- ✅ Entity extraction (lines, blocks, text)
- ✅ Layer analysis and filtering
- ✅ Block information parsing
- ✅ Family conversion pipeline

#### Phase 2: BIM Element Creation
- ⏳ Level and grid generation
- ⏳ Structural element creation
- ⏳ Room and space planning
- ⏳ Material assignment

#### Phase 3: Advanced Automation
- ⏳ Sheet generation and documentation
- ⏳ PDF export capabilities
- ⏳ Batch processing optimization
- ⏳ Custom script execution

### Code Quality Pipeline

```mermaid
graph TD
    A[Feature Request] --> B[Design Review]
    B --> C[Implementation]
    C --> D[Unit Tests]
    D --> E[Integration Tests]
    E --> F[Code Review]
    F --> G[Merge to Main]
    G --> H[Release Build]
    H --> I[Documentation Update]
```

## 🧪 Testing Strategy

### Test Categories

#### Unit Tests
```bash
npm run test:unit
```
- Tool parameter validation
- Database operations
- Utility functions
- Error handling

#### Integration Tests
```bash
npm run test:integration
```
- MCP protocol communication
- WebSocket connections
- Revit plugin interaction
- Database persistence

#### Mock Testing
```bash
node test-mcp-server.js
```
- Offline development testing
- API contract validation
- Response format verification

### Test Coverage

| Component | Coverage | Status |
|-----------|----------|--------|
| Tool Registry | 95% | ✅ |
| WebSocket Service | 90% | ✅ |
| Database Layer | 85% | ✅ |
| Validation Schemas | 100% | ✅ |
| Error Handling | 80% | 🔄 |

### Performance Benchmarks

- **Tool Registration**: < 100ms
- **WebSocket Latency**: < 50ms
- **Database Query**: < 200ms
- **CAD Processing**: < 2s per file
- **Memory Usage**: < 150MB

## 🔧 Configuration

### Environment Variables

```bash
# Server Configuration
PORT=8080
HOST=localhost

# Database Configuration
DB_PATH=./data/revit-mcp.db

# Logging Configuration
LOG_LEVEL=info
LOG_FILE=./logs/revit-mcp.log

# Revit Integration
REVIT_PLUGIN_PORT=8081
REVIT_TIMEOUT=30000
```

### Advanced Configuration

#### Custom Tool Registration
```typescript
import { registerCustomTool } from './tools/registry';

registerCustomTool({
  name: 'custom_analysis',
  description: 'Custom BIM analysis tool',
  parameters: {
    // Zod schema for validation
  },
  handler: async (params) => {
    // Implementation
  }
});
```

#### Database Schema Customization
```sql
-- Custom tables for project-specific data
CREATE TABLE custom_elements (
  id INTEGER PRIMARY KEY,
  element_id TEXT,
  custom_properties JSON,
  created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

## 🤝 Contributing

We welcome contributions from the BIM and AI communities!

### Development Setup

1. **Fork the repository**
2. **Create feature branch**
   ```bash
   git checkout -b feature/amazing-enhancement
   ```
3. **Install dependencies**
   ```bash
   npm install
   ```
4. **Run tests**
   ```bash
   npm test
   ```
5. **Build and validate**
   ```bash
   npm run build
   node test-mcp-server.js
   ```

### Contribution Guidelines

- **Code Style**: Follow TypeScript best practices
- **Testing**: 80%+ test coverage required
- **Documentation**: Update README for new features
- **Commits**: Use conventional commit format
- **PR Review**: All changes require review

### Adding New Tools

1. **Create tool file** in `src/tools/`
2. **Implement Zod schema** for validation
3. **Add error handling** and logging
4. **Write unit tests**
5. **Update documentation**

## 📚 Documentation

- **[API Reference](./docs/api.md)** - Complete tool specifications
- **[Plugin Guide](https://github.com/revit-mcp/revit-mcp-plugin)** - Revit plugin setup
- **[Examples](./examples/)** - Sample usage patterns
- **[Troubleshooting](./docs/troubleshooting.md)** - Common issues and solutions

## 🏢 Use Cases

### Architectural Design
- Rapid prototyping of design alternatives
- Automated code compliance checking
- Material optimization analysis

### Construction Management
- Automated quantity takeoffs
- Clash detection and resolution
- Construction sequencing optimization

### Facility Management
- Space utilization analysis
- Maintenance scheduling
- Energy performance modeling

### Education & Training
- Interactive BIM learning experiences
- Automated model validation
- Design critique and feedback

## 🌐 Community

- **Discord**: [Join our community](https://discord.gg/cGzUGurq)
- **QQ Group**: [792379482](http://qm.qq.com/cgi-bin/qm/qr?_wv=1027&k=kLnQiFVtYBytHm7R58KFoocd3mzU_9DR&authKey=fyXDOBmXP7FMkXAWjddWZumblxKJH7ZycYyLp40At3t9%2FOfSZyVO7zyYgIROgSHF&noverify=0&group_code=792379482)
- **GitHub Issues**: [Report bugs](https://github.com/hungpixi/revit-mcp/issues)
- **Discussions**: [Share ideas](https://github.com/hungpixi/revit-mcp/discussions)

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- **Autodesk Revit** - The foundation of modern BIM
- **Anthropic Claude** - Revolutionary AI capabilities
- **MCP Community** - Protocol innovation
- **Open Source Contributors** - Community-driven development

---

**Made with ❤️ for the BIM and AI communities**

*Transforming how we design, build, and manage the built environment*
