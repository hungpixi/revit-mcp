# Phase 1.5: Revit Plugin Integration Guide

## 📋 Overview

Phase 1.5 implements the C# handlers in the Revit plugin to support the 5 new CAD import tools from Phase 1. This document provides comprehensive integration instructions and testing procedures.

## 🏗️ Architecture

```
┌─────────────────────────┐
│   MCP Server (Node.js)  │
│                         │
│  • get_file_info        │
│  • get_cad_entities     │
│  • get_layers           │
│  • get_cad_block_info   │
│  • convert_cad_to_family│
└────────────┬────────────┘
             │ WebSocket (localhost:8080)
             ▼
┌─────────────────────────┐
│  Revit Plugin (C#/.NET) │
│                         │
│  CommandRouter          │
│  ├─ CADImportHandlers   │
│  │  ├─ GetFileInfo      │
│  │  ├─ GetCADEntities   │
│  │  ├─ GetLayers        │
│  │  ├─ GetCADBlockInfo  │
│  │  └─ ConvertCADFamily │
│  └─ [Other Handlers]    │
│                         │
│  SocketService          │
└────────────┬────────────┘
             │ Revit API
             ▼
┌─────────────────────────┐
│  Autodesk Revit         │
│  (BIM Model)            │
└─────────────────────────┘
```

## 📦 Handler Classes

### 1. **GetFileInfoHandler**
Retrieves Revit project metadata and linked CAD files.

**Parameters:**
```csharp
{
  "includeLinkedFiles": true  // bool
}
```

**Returns:**
```json
{
  "success": true,
  "filePath": "D:\\Projects\\MauNha5.rvt",
  "title": "Mẫu Nhà Số 5",
  "version": "2024",
  "linkedCADFiles": ["A-101-*.dwg", "A-102-*.dwg"],
  "elementCount": 1250,
  "fileSize": "45 MB"
}
```

**Key Implementation Details:**
- Uses `RevitLinkInstance` for linked Revit files
- Uses `ImportInstance` for CAD/DWG imports
- Queries `ProjectInformation` for project metadata
- Calculates file size from file system

---

### 2. **GetCADEntitiesHandler**
Extracts entities (lines, blocks, text) from linked CAD files.

**Parameters:**
```csharp
{
  "linkedCADFileName": "A-102-MẶTBẰNGTẦNG1.dwg",
  "entityTypes": ["LINE", "BLOCK", "TEXT"],
  "layerFilter": ["Tường", "Cửa"]
}
```

**Returns:**
```json
{
  "success": true,
  "fileName": "A-102-MẶTBẰNGTẦNG1.dwg",
  "entities": [
    {
      "type": "LINE",
      "layer": "Tường",
      "start": {"x": 0, "y": 0},
      "end": {"x": 6000, "y": 0}
    },
    {
      "type": "BLOCK",
      "layer": "Cửa",
      "name": "D01",
      "position": {"x": 1000, "y": 0}
    }
  ],
  "totalEntities": 245
}
```

**Key Implementation Details:**
- Accesses CAD geometry via `ImportInstance.GetGeometryObject()`
- Iterates through `GeometryElement` objects
- Extracts `Line`, `Arc`, `Curve` primitives
- Supports layer filtering

---

### 3. **GetLayersHandler**
Analyzes CAD layer structure and properties.

**Parameters:**
```csharp
{
  "linkedCADFileName": "A-102-MẶTBẰNGTẦNG1.dwg",
  "includeUnusedLayers": false
}
```

**Returns:**
```json
{
  "success": true,
  "fileName": "A-102-MẶTBẰNGTẦNG1.dwg",
  "layers": [
    {
      "name": "Tường",
      "entityCount": 45,
      "color": "Red",
      "visible": true,
      "locked": false
    },
    {
      "name": "Cửa",
      "entityCount": 12,
      "color": "Blue",
      "visible": true,
      "locked": false
    }
  ],
  "layerCount": 4
}
```

**Key Implementation Details:**
- Maps common Vietnam BIM layers
- Color coding by layer type
- Visibility and lock status tracking
- Entity count per layer

---

### 4. **GetCADBlockInfoHandler**
Extracts door/window/louver block information from CAD.

**Parameters:**
```csharp
{
  "linkedCADFileName": "A-701-DANHSÁCHCỬAĐI.dwg",
  "blockNameFilter": "D*",  // Wildcard pattern
  "includeInstances": true
}
```

**Returns:**
```json
{
  "success": true,
  "fileName": "A-701-DANHSÁCHCỬAĐI.dwg",
  "blocks": [
    {
      "name": "D01",
      "type": "Door",
      "size": {
        "width": 2200,
        "height": 1650
      },
      "instances": [
        {
          "position": {"x": 1000, "y": 0},
          "rotation": 0
        }
      ]
    }
  ],
  "blockCount": 2
}
```

**Supported Block Patterns:**
- **Doors**: D01-D11 (widths: 1000-2400mm, height: 1650-2100mm)
- **Windows**: W1-W10 (widths: 1000-2500mm, height: 1000-1500mm)
- **Louvers**: LV1-LV6 (widths: 3000-6000mm, height: 500-800mm)

**Key Implementation Details:**
- Wildcard pattern matching (D*, W*, LV*)
- Block dimension extraction
- Instance placement and rotation
- Optional instance details

---

### 5. **ConvertCADToFamilyHandler**
Converts CAD blocks to parametric Revit families.

**Parameters:**
```csharp
{
  "linkedCADFileName": "A-701-DANHSÁCHCỬAĐI.dwg",
  "blockName": "D01",
  "familyType": "DOOR",          // DOOR, WINDOW, LOUVER
  "familyName": "D01_Door_Family",
  "width": 2200,                 // mm
  "height": 1650                 // mm
}
```

**Returns:**
```json
{
  "success": true,
  "familyName": "D01_Door_Family",
  "familyId": "123456",
  "category": "DOOR",
  "blockName": "D01",
  "dimensions": {
    "width": 2200,
    "height": 1650
  },
  "message": "Family D01_Door_Family created successfully from CAD block D01"
}
```

**Key Implementation Details:**
- Uses family templates from Revit installation
- Creates parametric dimensions
- Loads family into active document
- Returns family ID for placement
- Transaction-based with rollback on error

---

## 🔧 Integration Steps

### Step 1: Update CommandRouter
The `CADImportHandlers.cs` file adds 5 new handler classes. Update `CommandRouter.cs` to register them:

```csharp
public JObject Dispatch(string method, JObject parameters)
{
    return method switch
    {
        // New handlers
        "get_file_info"         => new GetFileInfoHandler(_doc).Execute(parameters),
        "get_cad_entities"      => new GetCADEntitiesHandler(_doc).Execute(parameters),
        "get_layers"            => new GetLayersHandler(_doc).Execute(parameters),
        "get_cad_block_info"    => new GetCADBlockInfoHandler(_doc).Execute(parameters),
        "convert_cad_to_family" => new ConvertCADToFamilyHandler(_doc).Execute(parameters),
        
        // Existing handlers...
    };
}
```

### Step 2: Compile Revit Plugin
```bash
cd RevitPlugin
dotnet build --configuration Release
```

### Step 3: Deploy to Revit Addins
Copy compiled DLL to Revit addins folder:
```
C:\Users\<Username>\AppData\Roaming\Autodesk\Revit\Addins\2024\
```

### Step 4: Verify WebSocket Connection
Check that the Revit plugin's SocketService:
- Listens on `localhost:8080`
- Properly routes commands from MCP server
- Returns JSON responses correctly

## 🧪 Testing Procedures

### Test 1: get_file_info
```csharp
// Open a Revit project with linked CAD files
var testParams = new JObject
{
    ["includeLinkedFiles"] = true
};

var handler = new GetFileInfoHandler(document);
var result = handler.Execute(testParams);

// Expected: Project info + linked CAD file list
Assert.IsTrue(result["success"].Value<bool>());
Assert.IsNotNull(result["linkedCADFiles"]);
```

### Test 2: get_cad_entities
```csharp
var testParams = new JObject
{
    ["linkedCADFileName"] = "A-102-MẶTBẰNGTẦNG1.dwg",
    ["entityTypes"] = JArray.FromObject(new[] { "LINE", "BLOCK" }),
    ["layerFilter"] = JArray.FromObject(new[] { "Tường" })
};

var handler = new GetCADEntitiesHandler(document);
var result = handler.Execute(testParams);

// Expected: Array of extracted entities
Assert.IsTrue(result["success"].Value<bool>());
Assert.IsTrue(result["totalEntities"].Value<int>() > 0);
```

### Test 3: get_layers
```csharp
var testParams = new JObject
{
    ["linkedCADFileName"] = "A-102-MẶTBẰNGTẦNG1.dwg",
    ["includeUnusedLayers"] = false
};

var handler = new GetLayersHandler(document);
var result = handler.Execute(testParams);

// Expected: Layer information with entity counts
Assert.IsTrue(result["success"].Value<bool>());
Assert.IsTrue(result["layerCount"].Value<int>() > 0);
```

### Test 4: get_cad_block_info
```csharp
var testParams = new JObject
{
    ["linkedCADFileName"] = "A-701-DANHSÁCHCỬAĐI.dwg",
    ["blockNameFilter"] = "D*",
    ["includeInstances"] = true
};

var handler = new GetCADBlockInfoHandler(document);
var result = handler.Execute(testParams);

// Expected: Door blocks with instances
Assert.IsTrue(result["success"].Value<bool>());
Assert.IsTrue(result["blockCount"].Value<int>() > 0);
```

### Test 5: convert_cad_to_family
```csharp
var testParams = new JObject
{
    ["linkedCADFileName"] = "A-701-DANHSÁCHCỬAĐI.dwg",
    ["blockName"] = "D01",
    ["familyType"] = "DOOR",
    ["familyName"] = "D01_Door_Family",
    ["width"] = 2200,
    ["height"] = 1650
};

var handler = new ConvertCADToFamilyHandler(document);
var result = handler.Execute(testParams);

// Expected: New family created and loaded
Assert.IsTrue(result["success"].Value<bool>());
Assert.IsNotNull(result["familyId"]);
```

## 📊 Integration Checklist

- [ ] Copy `CADImportHandlers.cs` to `RevitPlugin/Handlers/`
- [ ] Update `CommandRouter.cs` with new handler registrations
- [ ] Compile Revit plugin project
- [ ] Deploy DLL to Revit Addins folder
- [ ] Test with sample Revit project containing CAD imports
- [ ] Verify WebSocket communication
- [ ] Test all 5 handlers with sample parameters
- [ ] Validate JSON response format
- [ ] Check error handling and rollback mechanisms
- [ ] Document any environment-specific configurations

## 🐛 Troubleshooting

### Issue: "ImportInstance not found"
**Solution**: Ensure the CAD file is properly imported as `ImportInstance`, not as a `RevitLinkInstance`.

### Issue: "Family template not found"
**Solution**: Verify the correct Revit version and update the template path in `GetFamilyTemplate()` method.

### Issue: WebSocket timeout
**Solution**: Check firewall settings and ensure the Revit plugin's SocketService is running on port 8080.

### Issue: "Cannot access geometry element"
**Solution**: Ensure the CAD import is visible and not hidden. Use `ImportInstance.IsVisible` to check.

## 📈 Next Steps

Phase 1.5 is complete with these 5 handlers. The next phases include:

**Phase 2**: Implement GROUP 2-5 tools (15+ more tools)
- Level & Grid creation tools
- Element query and analysis tools
- Export and PDF generation tools

**Phase 3**: Write Dynamo scripts
- CAD2Revit_Walls.dyn
- CAD2Revit_Doors.dyn
- CAD2Revit_Windows.dyn
- And 5-7 more specialized scripts

## 📚 References

- [Autodesk Revit API Documentation](https://www.autodesk.com/developer/revit)
- [Revit SDK Samples](https://github.com/Autodesk/revit-ifc)
- [ImportInstance Class Reference](https://www.revitapidocs.com/2024/65402aff-1b4e-482b-8a0f-c2e5a5861788.htm)
- [GeometryElement Class Reference](https://www.revitapidocs.com/2024/f28bed0f-ab77-4edb-a0c0-96a9b2f03f54.htm)

---

**Last Updated**: May 5, 2026
**Status**: Phase 1.5 Implementation Complete ✅
