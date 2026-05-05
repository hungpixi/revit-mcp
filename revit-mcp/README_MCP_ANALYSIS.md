# 🏢 WiseBIM MCP - Dự Án Tự Động Hóa BIM Toàn Diện
## Mẫu Nhà Số 5 - Khu Nhà Ở 9B4, 9B8

> **Status**: 🚀 Phase 1 Development (CAD Import Tools)  
> **Last Updated**: May 5, 2026  
> **Architecture**: MCP Server (Node.js/TypeScript) + Revit Plugin

---

## 📋 Table of Contents

- [Project Overview](#project-overview)
- [CAD Structure Analysis](#cad-structure-analysis)
- [System Architecture](#system-architecture)
- [Tools Implementation Progress](#tools-implementation-progress)
- [Phase 1: CAD Import Tools](#phase-1-cad-import-tools)
- [Installation & Setup](#installation--setup)
- [Usage Guide](#usage-guide)
- [Project Roadmap](#project-roadmap)

---

## 🎯 Project Overview

### Mục Tiêu

Xây dựng hệ thống **MCP (Model Context Protocol)** tự chủ để tự động hóa toàn bộ quy trình BIM từ giai đoạn **M** (Model) → **C** (Coordinate) → **P** (Publish) cho dự án:

- **Dự Án**: Mẫu Nhà Số 5
- **Vị Trí**: Khu Nhà Ở 9B4, 9B8
- **CAD Input**: 33 bản vẽ (8.93 MB)
- **Mục Tiêu**: Tự động chuyển CAD 2D → Revit 3D BIM Model

### Lợi Ích

✅ Làm chủ hoàn toàn dữ liệu  
✅ Giảm thiểu sai sót thủ công  
✅ Tái sử dụng quy trình cho các dự án khác  
✅ Tối ưu hóa hiệu suất làm việc nhóm  
✅ Tích hợp AI/LLM (Claude) cho kiến nghị thiết kế

---

## 📊 CAD Structure Analysis

### 📁 CAD Files Overview

```
Total Files: 33
Total Size: 8.93 MB
Format: AutoCAD DWG
Location: /CAD
```

### 📐 Files by Category

#### **1. Floor Plans (Mặt Bằng)** - 6 files
Các mặt bằng từ hầm đến mái với thông tin toàn bộ các tầng:

| File | Level | Elevation | Description |
|------|-------|-----------|-------------|
| A-101-MẶTBẰNGHẦM.dwg | Basement | -1500mm | Hầm: Garage, Kho, Bể tự hoại |
| A-102-MẶTBẰNGTẦNG1.dwg | Level 1 | +1200mm (FFL) | Tầng 1: Phòng khách, Bếp, Phòng ăn |
| A-103-MẶTBẰNGTẦNG2.dwg | Level 2 | +4600mm | Tầng 2: Phòng ngủ 01, 02, Sảnh chung |
| A-104-MẶTBẰNGTẦNG3.dwg | Level 3 | +8000mm | Tầng 3: Phòng ngủ 01, 02, Hành lang |
| A-105-MẶTBẰNGMÁI.dwg | Roof | +11400mm | Mái: Sân thượng, Bồn nước |
| A-000-DANHMỤCBẢNVẼ.dwg | Index | N/A | Danh mục 33 bản vẽ |

#### **2. Elevations & Sections (Mặt Đứng & Mặt Cắt)** - 5 files
Hình chiếu kiến trúc từ các phương hướng khác nhau:

| File | Type | Coverage |
|------|------|----------|
| A-201-MẶTĐỨNGTRỤCB-A&B-A.dwg | Elevation | Trục B-A & B-A |
| A-202-MẶTĐỨNGTRỤC4-1.dwg | Elevation | Trục 4-1 |
| A-203-MẶTĐỨNGTRỤC1-4.dwg | Elevation | Trục 1-4 |
| A-301-MẶTCẮTA-A.dwg | Section | Cắt A-A |
| A-302-MẶTCẮTB-B.dwg | Section | Cắt B-B |

#### **3. Ceiling & Lighting Plans** - 2 files
Mặt bằng trần đèn với vị trí thiết bị chiếu sáng:

- A-401-MẶTBẰNGTRẦNĐÈNTẦNGHẦM&1.dwg
- A-403-MẶTBẰNGTRẦNĐÈNTẦNG2&3.dwg

#### **4. Finishing Plans** - 2 files
Mặt bằng hoàn thiện sàn với chỉ định vật liệu:

- A-501-MẶTBẰNGHOÀNTHIỆNSÀNTẦNGHẦM&TẦNG1.dwg
  - FT-01: Gạch Ceramic 600x600 (Phòng khách, Phòng ăn, Sảnh)
  - FT-02: Gạch Ceramic 300x300 (WC, chống trượt)
  - FT-03: Sàn ốp gỗ (Phòng ngủ)
  - FT-06: Sàn bê tông thô (Hầm, Ramp)

- A-502-MẶTBẰNGHOÀNTHIỆNSÀNTẦNG2&3.dwg

#### **5. Stair Details (Chi Tiết Thang)** - 4 files
Chi tiết vẽ các bậc thang với kích thước và vật liệu:

- A-601-MBKHAITRIỂNTHANGST1.dwg (Stair 1 Plan)
- A-602-MẶTBẰNGKHAITRIỂNTHANGST2.dwg (Stair 2 Plan)
- A-603-MẶTCẮTKHAITRIỂNTHANGST2.dwg (Stair 2 Section)
- A-604-KHAITRIỂNBẬCCẤPST3.dwg (Stair 3 Rise/Tread)
- A-605-KHAITRIỂNBẬCCẤPST4.dwg (Stair 4 Rise/Tread)

#### **6. Bathroom & Balcony Details** - 4 files
Chi tiết hoàn thiện các khu vực đặc biệt:

| File | Type | Coverage |
|------|------|----------|
| A-606-KHAITRIỂNVỆSINHWC1.dwg | Detail | WC1 - Phòng vệ sinh 1 |
| A-607-KHAITRIỂNVỆSINHWC2.dwg | Detail | WC2 - Phòng vệ sinh 2 |
| A-609-CHITIẾTBANCÔNG1.dwg | Detail | Balcony 1 (Ban công 1) |
| A-610-CHITIẾTBANCÔNG2.dwg | Detail | Balcony 2 (Ban công 2) |

#### **7. Ramp & MEP Details** - 3 files
Chi tiết hạ tầng và hệ thống cơ điện nước:

| File | Element | Slope | Description |
|------|---------|-------|-------------|
| A-608-KHAITRIỂNRAMDỐC.dwg | Ramp | 18-19% | Ramp dốc hầm với hệ rãnh thoát nước |
| A-801-CHITIẾTRẢNHTHUNƯỚC.dwg | Drainage | - | Rãnh thu nước & hệ thống ngầm |

#### **8. Louver & Sun Control (Chi Tiết Louver)** - 3 files
Hệ thống louver chắn nắng tại mặt đứng:

| File | Louver Type | Material | Spacing | Description |
|------|-------------|----------|---------|-------------|
| A-611-CHITIẾTLOUVER1,2.dwg | LV1, LV2 | Thép vuông | 100mm | Louver tầng dưới |
| A-612-CHITIẾTLOUVER5,6.dwg | LV5, LV6 | Thép vuông | 100mm | Louver tầng trên |
| A-613-CHITIẾTLOUVER.dwg | LV Generic | Thép vuông | 100mm | Louver chi tiết tổng |

Tất cả louver: **Thép vuông chống rỉ 50×100 @100mm**

#### **9. Door & Window Schedules** - 3 files
Danh sách chi tiết tất cả cửa và cửa sổ:

| File | Type | Quantity | Codes |
|------|------|----------|-------|
| A-701-DANHSÁCHCỬAĐI.dwg | Door Schedule | 11 types | D01 → D11 |
| A-702-DANHSÁCHCỦASỔ1.dwg | Window Schedule | 5 types | W1 → W5 |
| A-703-DANHSÁCHCỬASỔ2.dwg | Window Schedule | 5 types | W6 → W10 |

**Expected Blocks** (từ CAD):

🚪 **Door Blocks**: D01, D02, D03, D04, D05, D06, D07, D08, D09, D10, D11

🪟 **Window Blocks**: W1, W2, W3, W4, W5, W6, W7, W8, W9, W10

☀️ **Louver Blocks**: LV1, LV2, LV3, LV4, LV5, LV6

---

## 🏗️ System Architecture

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────┐
│  Claude AI / LLM Client                                 │
│  (MCP Protocol - stdio)                                  │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│  revit-mcp Server (Node.js/TypeScript)                 │
│  ├─ 20+ Tools for Revit Automation                      │
│  ├─ Database Layer (SQLite)                             │
│  ├─ Connection Manager (WebSocket)                      │
│  └─ Dynamic Tool Registry                               │
└────────────────────┬────────────────────────────────────┘
                     │ (WebSocket: localhost:8080)
┌────────────────────▼────────────────────────────────────┐
│  Revit Plugin (C# Addon)                                │
│  ├─ CAD Linking & Import                                │
│  ├─ Element Creation/Modification                       │
│  ├─ Parameter Management                                │
│  ├─ Export/Import Data                                  │
│  └─ Dynamo Script Execution                             │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│  Revit Application (2024+)                              │
│  ├─ 3D BIM Model                                        │
│  ├─ Schedules & Data Tables                             │
│  ├─ Sheet & View Management                             │
│  └─ PDF/Export Services                                 │
└─────────────────────────────────────────────────────────┘
```

### Three-Phase Implementation (M-C-P)

```
PHASE 1: MODEL (Giai Đoạn M)
├─ CAD Import & Analysis
├─ Automatic Level/Grid Creation
├─ Wall Generation from CAD
├─ Door/Window Family Creation
└─ 20 New Tools ✅ IN PROGRESS

PHASE 2: COORDINATE (Giai Đoạn C)
├─ Shared Coordinates Management
├─ Interference Check (Clash Detection)
├─ Element Verification
├─ MEP Coordination
└─ 8-10 Dynamo Scripts (Coming Soon)

PHASE 3: PUBLISH (Giai Đoạn P)
├─ Automatic Sheet Generation
├─ Schedule Export (Excel/CSV)
├─ Batch PDF Export
├─ Version Management
└─ Data Round-trip (Excel ↔ Revit)
```

---

## 🔧 Tools Implementation Progress

### Current Status: **Phase 1 - CAD Import Tools** ✅

**Total Tools in revit-mcp**: 30+ existing + 20 new = 50+ total

### GROUP 1: CAD IMPORT (5 tools) ✅ COMPLETED

#### 1. **get_cad_entities** 
Lấy các entities (đường, khối, text, ...) từ file CAD liên kết.

```typescript
Tool: "get_cad_entities"
Parameters:
  - linkedCADFileName: string (optional) - Tên file CAD
  - entityTypes: ["LINE", "POLYLINE", "CIRCLE", "BLOCK", ...]
  - layerFilter: ["Tường", "Cửa", "Cột"] (optional)
  - includeMetadata: boolean (default: true)
  
Returns:
  - List of CAD entities with coordinates, properties
  - Layer info, entity type, geometry data
```

**Use Case**: Phân tích cấu trúc CAD trước khi tạo Revit elements.

#### 2. **get_file_info**
Lấy thông tin chi tiết về project Revit hiện tại.

```typescript
Tool: "get_file_info"
Parameters:
  - includeLinkedFiles: boolean (default: true)
  - includePerformanceMetrics: boolean (default: false)
  
Returns:
  - File path, version, central file location
  - List of linked CAD/Revit files
  - Element count, file size, warnings
```

**Use Case**: Kiểm tra tính toàn vẹn của project trước khi xuất bản.

#### 3. **get_layers**
Lấy danh sách tất cả layers từ CAD file liên kết.

```typescript
Tool: "get_layers"
Parameters:
  - linkedCADFileName: string (optional)
  - includeUnusedLayers: boolean (default: false)
  - sortBy: "name" | "entityCount" | "color" (default: "name")
  
Returns:
  - Layer name, color, linetype, lineweight
  - Entity count per layer
  - Organized by sort preference
```

**Use Case**: Hiểu cấu trúc CAD layers để thiết lập mapping tới Revit wall types, door families, etc.

#### 4. **get_cad_block_info**
Lấy thông tin chi tiết về CAD blocks (cho cửa D01-D11, cửa sổ W1-W10, louver LV1-LV6).

```typescript
Tool: "get_cad_block_info"
Parameters:
  - linkedCADFileName: string (optional)
  - blockNameFilter: string (e.g., "D*", "W*")
  - includeInstances: boolean (default: true)
  - includeAttributes: boolean (default: true)
  
Returns:
  - Block definition info
  - All block instances (location, rotation, scale)
  - Block attributes and properties
  - Ready for conversion to Revit families
```

**Use Case**: Tạo mapping CAD blocks → Revit families (D01→Door Family D01, W1→Window Family W1, etc.)

#### 5. **convert_cad_to_family**
Tự động chuyển đổi CAD block thành Revit family.

```typescript
Tool: "convert_cad_to_family"
Parameters:
  - linkedCADFileName: string (required)
  - blockName: string (e.g., "D01", "W1", "LV6")
  - familyType: "DOOR" | "WINDOW" | "GENERIC" | "FURNITURE" | "LOUVER"
  - familyName: string
  - width/height/depth: number (mm) (optional)
  - preserveCADLayers: boolean (default: true)
  - makeParametric: boolean (default: true)
  
Returns:
  - New Revit family created
  - Family category assigned
  - Parametric constraints set
  - Ready for placement in model
```

**Use Case**: Batch convert 11 door blocks + 10 window blocks + 6 louver blocks into parametric Revit families.

---

### Coming Soon: GROUP 2-5 (15 more tools)

#### GROUP 2: LEVEL/GRID (4 tools)
- `create_levels_from_data` - Tạo Levels từ array dữ liệu
- `create_grids_from_data` - Tạo Grids từ array dữ liệu  
- `get_levels_info` - Query thông tin tất cả Levels
- `get_grids_info` - Query thông tin tất cả Grids

#### GROUP 3: ELEMENT QUERY (5 tools)
- `query_walls_by_layer` - Query tường theo layer
- `query_doors_by_family` - Query cửa theo family
- `query_windows_by_family` - Query cửa sổ theo family
- `query_elements_in_view` - Query phần tử trong view
- `query_element_parameters` - Lấy parameters của element

#### GROUP 4: EXPORT/IMPORT (5 tools)
- `export_schedules_to_excel` - Schedules → Excel
- `import_excel_data` - Excel → Parameters (Round-trip)
- `export_elements_to_csv` - Elements → CSV
- `export_room_schedule` - Room schedule tổng hợp
- `export_door_schedule` - Door schedule tổng hợp

#### GROUP 5: SHEET & PDF (3 tools)
- `create_sheets_from_data` - Tạo Sheets tự động
- `export_to_pdf_batch` - Export PDF hàng loạt
- `manage_sheet_set` - Quản lý Sheet Set

---

## 📦 Installation & Setup

### Prerequisites

```
✓ Node.js 18+
✓ npm or yarn
✓ Revit 2024+ (for plugin)
✓ Autodesk Revit Python Shell (optional, for advanced scripts)
```

### Step 1: Clone Repository

```bash
cd d:\CODE
git clone https://github.com/mcp-servers-for-revit/revit-mcp.git
cd revit-mcp/revit-mcp
```

### Step 2: Install Dependencies

```bash
npm install
```

### Step 3: Build TypeScript

```bash
npm run build
```

Output: `build/index.js` (MCP Server executable)

### Step 4: Configure Claude Desktop

Edit: `%APPDATA%\Code\User\globalStorage\GitHub.copilot-chat\claude_desktop_config.json`

```json
{
  "mcpServers": {
    "revit-mcp": {
      "command": "node",
      "args": ["D:\\CODE\\revit-mcp\\revit-mcp\\build\\index.js"]
    }
  }
}
```

### Step 5: Install Revit Plugin

Copy Revit plugin files to:
```
%APPDATA%\Autodesk\Revit\Addins\2024\
```

Plugin must listen on `localhost:8080` (WebSocket)

### Step 6: Test Connection

In Claude desktop:
```
@revit say_hello message:"Test MCP Connection"
```

Expected response: Dialog in Revit showing "Test MCP Connection"

---

## 💡 Usage Guide

### Example 1: Analyze CAD Structure

```
User: Analyze the CAD files for the Mẫu Nhà project
Claude:
  1. Calls: get_file_info() → Get linked CAD files
  2. Calls: get_layers() → Get layer structure
  3. Calls: get_cad_block_info(blockNameFilter: "D*") → Get door blocks
  4. Returns: Comprehensive CAD analysis
```

### Example 2: Create Door Family from CAD

```
User: Convert door block D01 from CAD to Revit family
Claude:
  1. Calls: get_cad_block_info(blockName: "D01")
  2. Extracts: Size 2200x1050, material, attributes
  3. Calls: convert_cad_to_family(
       blockName: "D01",
       familyType: "DOOR",
       width: 2200,
       height: 1050
     )
  4. Returns: New Revit family "D01_Door"
```

### Example 3: Extract Floor Information

```
User: What entities are on the "Tường" layer in the basement floor plan?
Claude:
  1. Calls: get_cad_entities(
       linkedCADFileName: "A-101-MẶTBẰNGHẦM.dwg",
       layerFilter: ["Tường"]
     )
  2. Returns: All wall lines with coordinates & length
  3. Ready for automatic wall creation in Revit
```

---

## 🚀 Project Roadmap

### ✅ Phase 1: CAD Import (Week 1-2)
- [x] Design 20 new tools
- [x] Implement GROUP 1: CAD Import (5 tools)
- [ ] Build Revit plugin handlers for these 5 tools
- [ ] Test with actual CAD files (Mẫu Nhà Số 5)

**Deliverable**: Extract CAD geometry ready for Revit conversion

### 📋 Phase 2: Model Building (Week 3-4)
- [ ] Implement GROUP 2: Level/Grid Creation (4 tools)
- [ ] Implement GROUP 3: Element Query (5 tools)
- [ ] Write 8-10 Dynamo scripts:
  - `CAD2Revit_Walls.dyn` - Convert 2 lines → Wall with thickness
  - `CAD2Revit_Doors.dyn` - Place doors from CAD blocks
  - `CAD2Revit_Windows.dyn` - Place windows from CAD blocks
  - `CreateFamilies.dyn` - Batch create door/window families
  - `TagElements.dyn` - Auto number doors, rooms
  - `WallProperties.dyn` - Assign wall types (P1, P3, P4, P5)
  - `GenerateSchedules.dyn` - Create material schedules
  - `InterferenceCheck.dyn` - Detect clashes

**Deliverable**: Complete 3D BIM model from CAD

### 📊 Phase 3: Coordinate & Publish (Week 5-6)
- [ ] Implement GROUP 4: Export/Import (5 tools)
- [ ] Implement GROUP 5: Sheet & PDF (3 tools)
- [ ] Shared Coordinates management
- [ ] Clash detection & reporting
- [ ] Batch PDF export
- [ ] Excel round-trip data sync

**Deliverable**: Complete documented drawings + data exports

---

## 📄 CAD File Mapping Reference

### Wall Types (from plan.md)

| Layer | Type | Description | Thickness | Material Stack |
|-------|------|-------------|-----------|-----------------|
| Tường_Ngoài | Exterior | Sơn trắng (P1) | 200mm | Gạch 80×80×180 + Vữa + Sơn |
| Tường_Xám | Exterior | Sơn xám (P3) | 200mm | Gạch 80×80×180 + Vữa + Sơn xám |
| Tường_Đá | Exterior | Ốp đá chẻ (P4) | 250mm | Gạch + Đá chẻ lớp ngoài |
| Tường_Ron | Interior | Kẻ ron 15@300 (P5) | 150mm | Gạch + Ron |
| Vách_Nhẹ | Interior | Vách nhẹ | 100-150mm | Khung thép + Vật liệu lấp |

### Door Types (from A-701)

| Code | Name | Size (W×H) | Material | Use |
|------|------|-----------|----------|-----|
| D01 | Main Door | 2200×1650 | Gỗ/Kính | Ban công |
| D02 | Sliding Door | 2400×2000 | Kính | Phòng khách |
| D03-D05 | Bedroom Door | 1000×2100 | Gỗ | Phòng ngủ |
| D06-D08 | Bathroom Door | 700×2100 | Gỗ | WC |
| D09-D11 | Closet Door | 600×2100 | Gỗ | Tủ/Hành lang |

### Window Types (from A-702, A-703)

| Code | Style | Size (W×H) | Glass | Use |
|------|-------|-----------|-------|-----|
| W1-W3 | Fixed | Variable | Tempered | Living area |
| W4-W6 | Sliding | Variable | Double glazed | Bedroom |
| W7-W8 | Casement | Variable | Tempered | Kitchen |
| W9-W10 | High window | Variable | Fixed | Ventilation |

### Louver Types (from A-611, A-612)

| Code | Type | Material | Size | Spacing | Location |
|------|------|----------|------|---------|----------|
| LV1-LV2 | Horizontal | Steel 50×100 | Variable | 100mm | Facade lower |
| LV3-LV4 | Horizontal | Steel 50×100 | Variable | 100mm | Facade middle |
| LV5-LV6 | Horizontal | Steel 50×100 | Variable | 100mm | Facade upper |

---

## 🔗 Related Documents

- **plan.md** - Chi tiết phân tích kiến trúc & kỹ thuật dự án
- **PDF** - 131203-MAU NHA SO 5_BINDER.pdf - Bản vẽ render & visualization
- **CAD folder** - 33 file DWG (8.93 MB) - Bộ vẽ công trình

---

## 📞 Support & Contribution

### Questions?
- Check Claude AI in VS Code for MCP tool documentation
- Review existing tools in `src/tools/` folder
- Read individual tool files for parameter details

### Want to Extend?
1. Create new file: `src/tools/my_new_tool.ts`
2. Follow pattern from existing tools
3. Add Zod schema for validation
4. Use `withRevitConnection()` to communicate with plugin
5. Auto-registers on server startup

### Example: Adding a New Tool

```typescript
// src/tools/my_tool.ts
import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerMyToolTool(server: McpServer) {
  server.tool(
    "my_tool_name",
    "Description of what the tool does",
    {
      param1: z.string().describe("Description of param1"),
      param2: z.number().optional(),
    },
    async (args) => {
      try {
        const response = await withRevitConnection(async (client) => {
          return await client.sendCommand("revit_command_name", args);
        });
        return {
          content: [{ type: "text", text: JSON.stringify(response, null, 2) }],
        };
      } catch (error) {
        return {
          content: [{ type: "text", text: `Error: ${error.message}` }],
          isError: true,
        };
      }
    }
  );
}
```

Run `npm run build` → Auto-registers!

---

## 📚 Architecture Details

### Tool Registry Pattern

```
revit-mcp (Server)
├── index.ts
│   └── Calls registerTools()
├── tools/register.ts
│   └── Dynamically loads all tool files
│       ├── get_cad_entities.ts (register✓)
│       ├── get_file_info.ts (register✓)
│       ├── get_layers.ts (register✓)
│       ├── get_cad_block_info.ts (register✓)
│       ├── convert_cad_to_family.ts (register✓)
│       ├── create_level.ts (existing)
│       ├── create_grid.ts (existing)
│       └── ... (30+ more tools)
│
└── utils/
    ├── ConnectionManager.ts
    │   └── withRevitConnection(operation)
    ├── SocketClient.ts
    │   └── WebSocket to Revit plugin
    └── CADAnalyzer.ts (NEW)
        └── Analyze CAD structure
```

### Data Flow

```
Claude: "Convert door D01 from CAD to family"
  ↓
MCP Server (Node.js)
  ├─ parse: convert_cad_to_family { blockName: "D01" }
  ├─ validate: Zod schema checks
  ├─ execute: withRevitConnection()
  │   └─ SocketClient.sendCommand("convert_cad_to_family", params)
  │       ↓ (WebSocket localhost:8080)
  ├─ Revit Plugin (C#)
  │   ├─ Find block "D01" in linked CAD
  │   ├─ Extract geometry & attributes
  │   ├─ Create new Door family
  │   ├─ Add parametric constraints
  │   └─ Return family ID
  │       ↑ (WebSocket response)
  ├─ Format response
  └─ Return to Claude
      "Family D01 created successfully"
```

---

## 🎓 Learning Path

1. **Start**: Understand CAD structure (this README)
2. **Explore**: Use `get_cad_entities`, `get_layers`, `get_cad_block_info` tools
3. **Analyze**: Study CAD → Revit mapping requirements
4. **Design**: Plan wall types, family parameters, schedules
5. **Implement**: Use remaining tools to build Revit model
6. **Optimize**: Write Dynamo scripts for batch operations
7. **Publish**: Export schedules, PDFs, and documentation

---

## 📝 Notes

- All units in mm (millimeters)
- Shared coordinates origin: (0, 0, 0) at Trục 1 × Trục A intersection
- Elevation reference: Nền đường (Ground level = 0)
- Building heights: Basement (-1500) to Roof (+11400) = 12,900mm total
- All tools follow async/await pattern for non-blocking I/O
- WebSocket connection pooling with 5-second timeout

---

**Version**: 0.1.0 (Phase 1 - CAD Import)  
**Last Updated**: May 5, 2026  
**Status**: 🚀 Active Development
