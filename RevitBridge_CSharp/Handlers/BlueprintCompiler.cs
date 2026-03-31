using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using RevitBridge.Utils;

namespace RevitBridge.Handlers
{
    public static class BlueprintCompiler
    {
        public static string Compile(UIApplication uiapp, string jsonFilePath)
        {
            if (!File.Exists(jsonFilePath))
            {
                throw new Exception($"Blueprint file not found: {jsonFilePath}");
            }

            string jsonContent = File.ReadAllText(jsonFilePath);
            JObject blueprint = JObject.Parse(jsonContent);

            Document doc = null;
            bool isBackgroundDoc = false;
            string savePath = "";

            if (blueprint["project"] != null && blueprint["project"]["template"] != null && blueprint["project"]["save_path"] != null)
            {
                string tmpl = blueprint["project"]["template"].ToString();
                savePath = blueprint["project"]["save_path"].ToString();
                
                if (!File.Exists(tmpl))
                    throw new Exception("Không tìm thấy Template Revit tại: " + tmpl);

                doc = uiapp.Application.NewProjectDocument(tmpl);
                isBackgroundDoc = true;
            }
            else
            {
                UIDocument uidoc = uiapp.ActiveUIDocument;
                if (uidoc == null) return "{\"status\": \"Error\", \"msg\": \"Không tìm thấy file Revit UI đang mở, và chưa cấu hình template trong JSON!\"}";
                doc = uidoc.Document;
            }

            // Dictionary caching logic IDs (e.g. "Panel_A") to Revit ElementIds
            Dictionary<string, ElementId> virtualIdMap = new Dictionary<string, ElementId>();
            // Dictionary mapping Level names to Level objects
            Dictionary<string, Level> levelMap = new Dictionary<string, Level>();

            int countLevels = 0, countGrids = 0, countWalls = 0, countCols = 0;
            int countTrays = 0, countConduits = 0, countEquip = 0, countLights = 0, countCircuits = 0;

            using (TransactionGroup transGroup = new TransactionGroup(doc, "Compile Blueprint"))
            {
                transGroup.Start();

                try
                {
                    // 1. Build Levels
                    if (blueprint["levels"] != null)
                    {
                        using (Transaction t = new Transaction(doc, "Build Levels"))
                        {
                            t.Start();
                            foreach (var item in blueprint["levels"])
                            {
                                string name = item["name"]?.ToString();
                                double elevation = Convert.ToDouble(item["elevation"]);
                                
                                // Check if exists
                                Level existing = new FilteredElementCollector(doc).OfClass(typeof(Level))
                                    .Cast<Level>().FirstOrDefault(l => l.Name == name);
                                
                                if (existing != null)
                                {
                                    levelMap[name] = existing;
                                }
                                else
                                {
                                    Level newLevel = Level.Create(doc, elevation);
                                    newLevel.Name = name;
                                    levelMap[name] = newLevel;
                                    countLevels++;
                                }
                            }
                            t.Commit();
                        }
                    }
                    
                    // Default Level if none provided
                    Level defaultLevel = levelMap.Values.FirstOrDefault() ?? new FilteredElementCollector(doc).OfClass(typeof(Level)).FirstElement() as Level;

                    // 2. Build Grids
                    if (blueprint["grids"] != null)
                    {
                        using (Transaction t = new Transaction(doc, "Build Grids"))
                        {
                            t.Start();
                            foreach (var item in blueprint["grids"])
                            {
                                XYZ st = ParseXYZ(item["start"]);
                                XYZ en = ParseXYZ(item["end"]);
                                Line line = Line.CreateBound(st, en);
                                Grid grid = Grid.Create(doc, line);
                                if (item["name"] != null) grid.Name = item["name"].ToString();
                                countGrids++;
                            }
                            t.Commit();
                        }
                    }

                    // 3. Build Walls
                    if (blueprint["walls"] != null)
                    {
                        using (Transaction t = new Transaction(doc, "Build Walls"))
                        {
                            t.Start();
                            foreach (var item in blueprint["walls"])
                            {
                                XYZ st = ParseXYZ(item["start"]);
                                XYZ en = ParseXYZ(item["end"]);
                                Line line = Line.CreateBound(st, en);
                                Level wallLvl = GetLevel(item["level"], levelMap, defaultLevel);
                                Wall.Create(doc, line, wallLvl.Id, false);
                                countWalls++;
                            }
                            t.Commit();
                        }
                    }

                    // 4. Build Columns
                    if (blueprint["columns"] != null)
                    {
                        using (Transaction t = new Transaction(doc, "Build Columns"))
                        {
                            t.Start();
                            FamilySymbol colSymbol = DynamicFamilyLoader.GetOrLoadFamilySymbol(doc, BuiltInCategory.OST_StructuralColumns);
                            if (colSymbol != null)
                            {
                                foreach (var item in blueprint["columns"])
                                {
                                    XYZ pt = ParseXYZ(item["point"]);
                                    Level lvl = GetLevel(item["level"], levelMap, defaultLevel);
                                    doc.Create.NewFamilyInstance(pt, colSymbol, lvl, Autodesk.Revit.DB.Structure.StructuralType.Column);
                                    countCols++;
                                }
                            }
                            t.Commit();
                        }
                    }

                    // 5. Build MEP: Cable Trays
                    if (blueprint["cable_trays"] != null)
                    {
                        using (Transaction t = new Transaction(doc, "Build Cable Trays"))
                        {
                            t.Start();
                            ElementId trayTypeId = new FilteredElementCollector(doc).OfClass(typeof(CableTrayType)).FirstElementId();
                            foreach (var item in blueprint["cable_trays"])
                            {
                                Level lvl = GetLevel(item["level"], levelMap, defaultLevel);
                                
                                JArray routeObj = item["route"] as JArray;
                                if (routeObj != null && routeObj.Count >= 2)
                                {
                                    List<CableTray> createdTrays = new List<CableTray>();
                                    for (int i = 0; i < routeObj.Count - 1; i++)
                                    {
                                        XYZ st = ParseXYZ(routeObj[i]);
                                        XYZ en = ParseXYZ(routeObj[i + 1]);
                                        
                                        // Thêm Smart Snap cho Máng cáp
                                        st = GeometryUtils.SnapToGrid(st);
                                        en = GeometryUtils.SnapToGrid(en);

                                        CableTray tray = CableTray.Create(doc, trayTypeId, st, en, lvl.Id);
                                        
                                        if (item["width"] != null)
                                            tray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM).Set(Convert.ToDouble(item["width"]) / 304.8);
                                        if (item["height"] != null)
                                            tray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM).Set(Convert.ToDouble(item["height"]) / 304.8);
                                        
                                        createdTrays.Add(tray);
                                        countTrays++;
                                    }
                                    
                                    // Auto-Routing: Nối Fitting (Co)
                                    doc.Regenerate(); // Cần tái tạo hình học trước khi lấy Connector
                                    for (int i = 0; i < createdTrays.Count - 1; i++)
                                    {
                                        XYZ sharedPt = ParseXYZ(routeObj[i + 1]);
                                        Connector c1 = GetClosestConnector(createdTrays[i], sharedPt);
                                        Connector c2 = GetClosestConnector(createdTrays[i + 1], sharedPt);
                                        if (c1 != null && c2 != null)
                                            doc.Create.NewElbowFitting(c1, c2);
                                    }
                                }
                                else if (item["start"] != null && item["end"] != null) 
                                {
                                    XYZ st = ParseXYZ(item["start"]);
                                    XYZ en = ParseXYZ(item["end"]);
                                    CableTray tray = CableTray.Create(doc, trayTypeId, st, en, lvl.Id);
                                    if (item["width"] != null) tray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM).Set(Convert.ToDouble(item["width"]) / 304.8);
                                    if (item["height"] != null) tray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM).Set(Convert.ToDouble(item["height"]) / 304.8);
                                    countTrays++;
                                }
                            }
                            t.Commit();
                        }
                    }

                    // 6. Build MEP: Conduits
                    if (blueprint["conduits"] != null)
                    {
                        using (Transaction t = new Transaction(doc, "Build Conduits"))
                        {
                            t.Start();
                            ElementId conduitTypeId = new FilteredElementCollector(doc).OfClass(typeof(ConduitType)).FirstElementId();
                            foreach (var item in blueprint["conduits"])
                            {
                                Level lvl = GetLevel(item["level"], levelMap, defaultLevel);
                                JArray routeObj = item["route"] as JArray;
                                if (routeObj != null && routeObj.Count >= 2)
                                {
                                    List<Conduit> createdConduits = new List<Conduit>();
                                    for (int i = 0; i < routeObj.Count - 1; i++)
                                    {
                                        XYZ st = ParseXYZ(routeObj[i]);
                                        XYZ en = ParseXYZ(routeObj[i + 1]);
                                        Conduit cond = Conduit.Create(doc, conduitTypeId, st, en, lvl.Id);
                                        createdConduits.Add(cond);
                                        countConduits++;
                                    }
                                    
                                    doc.Regenerate();
                                    for (int i = 0; i < createdConduits.Count - 1; i++)
                                    {
                                        XYZ sharedPt = ParseXYZ(routeObj[i + 1]);
                                        Connector c1 = GetClosestConnector(createdConduits[i], sharedPt);
                                        Connector c2 = GetClosestConnector(createdConduits[i + 1], sharedPt);
                                        if (c1 != null && c2 != null)
                                            doc.Create.NewElbowFitting(c1, c2);
                                    }
                                }
                                else if (item["start"] != null && item["end"] != null)
                                {
                                    XYZ st = ParseXYZ(item["start"]);
                                    XYZ en = ParseXYZ(item["end"]);
                                    Conduit.Create(doc, conduitTypeId, st, en, lvl.Id);
                                    countConduits++;
                                }
                            }
                            t.Commit();
                        }
                    }

                    // 7. Build Equipment (Panels)
                    if (blueprint["equipment"] != null)
                    {
                        using (Transaction t = new Transaction(doc, "Build Equipment"))
                        {
                            t.Start();
                            FamilySymbol equipSymbol = DynamicFamilyLoader.GetOrLoadFamilySymbol(doc, BuiltInCategory.OST_ElectricalEquipment);
                            if (equipSymbol != null)
                            {
                                foreach (var item in blueprint["equipment"])
                                {
                                    XYZ pt = ParseXYZ(item["point"]);
                                    Level lvl = GetLevel(item["level"], levelMap, defaultLevel);
                                    FamilyInstance inst = doc.Create.NewFamilyInstance(pt, equipSymbol, lvl, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                    
                                    string vId = item["id"]?.ToString();
                                    if (!string.IsNullOrEmpty(vId)) virtualIdMap[vId] = inst.Id;
                                    
                                    countEquip++;
                                }
                            }
                            t.Commit();
                        }
                    }

                    // 8. Build Lighting Fixtures
                    if (blueprint["lighting"] != null)
                    {
                        using (Transaction t = new Transaction(doc, "Build Lighting"))
                        {
                            t.Start();
                            FamilySymbol lightSymbol = DynamicFamilyLoader.GetOrLoadFamilySymbol(doc, BuiltInCategory.OST_LightingFixtures);
                            if (lightSymbol != null)
                            {
                                foreach (var item in blueprint["lighting"])
                                {
                                    XYZ pt = ParseXYZ(item["point"]);
                                    Level lvl = GetLevel(item["level"], levelMap, defaultLevel);
                                    FamilyInstance inst = doc.Create.NewFamilyInstance(pt, lightSymbol, lvl, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                    
                                    string vId = item["id"]?.ToString();
                                    if (!string.IsNullOrEmpty(vId)) virtualIdMap[vId] = inst.Id;

                                    countLights++;
                                }
                            }
                            t.Commit();
                        }
                    }

                    // 9. Build Circuits
                    if (blueprint["circuits"] != null)
                    {
                        using (Transaction t = new Transaction(doc, "Build Circuits"))
                        {
                            t.Start();
                            foreach (var item in blueprint["circuits"])
                            {
                                string panelIdStr = item["panel"]?.ToString();
                                JArray fixturesArray = item["fixtures"] as JArray;
                                
                                if (fixturesArray != null && fixturesArray.Count > 0)
                                {
                                    List<ElementId> fixtureIds = new List<ElementId>();
                                    foreach (var fToken in fixturesArray)
                                    {
                                        string fIdStr = fToken.ToString();
                                        if (virtualIdMap.ContainsKey(fIdStr))
                                        {
                                            fixtureIds.Add(virtualIdMap[fIdStr]);
                                        }
                                    }

                                    if (fixtureIds.Count > 0)
                                    {
                                        ElectricalSystemType sysType = ElectricalSystemType.PowerCircuit;
                                        if (item["type"] != null && item["type"].ToString().ToLower() == "lighting")
                                        {
                                            // Revit 2020 sometimes forces PowerCircuit for generic systems, but we try:
                                            sysType = ElectricalSystemType.PowerCircuit; 
                                        }

                                        ElectricalSystem circuit = ElectricalSystem.Create(doc, fixtureIds, sysType);
                                        
                                        // Connect to panel
                                        if (!string.IsNullOrEmpty(panelIdStr) && virtualIdMap.ContainsKey(panelIdStr))
                                        {
                                            FamilyInstance panel = doc.GetElement(virtualIdMap[panelIdStr]) as FamilyInstance;
                                            if (panel != null)
                                            {
                                                try {
                                                    circuit.SelectPanel(panel);
                                                } catch {
                                                    // Bỏ qua lỗi khác biệt hệ thống điện áp giữa Tủ và Đèn (Distribution System Mismatch)
                                                    // Mạch vẫn được tạo thành công để người dùng nối tay sau nếu Tempate lệch chuẩn.
                                                }
                                            }
                                        }
                                        countCircuits++;
                                    }
                                }
                            }
                            t.Commit();
                        }
                    }

                    transGroup.Assimilate(); // Merge all transactions into one Undo step
                }
                catch (Exception ex)
                {
                    transGroup.RollBack();
                    if (isBackgroundDoc && doc != null) doc.Close(false);
                    throw new Exception("Lỗi khi Compiler Blueprint: " + ex.Message);
                }
            }
            
            if (isBackgroundDoc)
            {
                // Headless Generation requires saving and opening in UI after compilation
                try 
                {
                    SaveAsOptions opt = new SaveAsOptions { OverwriteExistingFile = true };
                    doc.SaveAs(savePath, opt);
                    doc.Close(false);
                    // uiapp.OpenAndActivateDocument(savePath); // Bỏ comment này do gây DEADLOCK Revit UI khi đang chạy dưới IExternalEventHandler
                }
                catch (Exception e)
                {
                    throw new Exception("Đã gen file rvt nhưng gặp lỗi khi Lưu ra đĩa hoặc OpenAndActivate: " + e.Message);
                }
            }

            string summary = $"Blueprint compiled successfully. Created: {countLevels} Levels, {countGrids} Grids, {countWalls} Walls, {countCols} Columns, {countTrays} Trays, {countConduits} Conduits, {countEquip} Panels, {countLights} Lights, {countCircuits} Circuits.";
            
            return "{\"status\": \"OK\", \"msg\": \"" + summary + "\"}";
        }

        private static XYZ ParseXYZ(JToken token)
        {
            if (token == null || !token.HasValues) return XYZ.Zero;
            var arr = token.ToObject<double[]>();
            double z = arr.Length > 2 ? arr[2] : 0;
            return new XYZ(arr[0], arr[1], z);
        }

        private static Level GetLevel(JToken levelNameToken, Dictionary<string, Level> map, Level defaultLevel)
        {
            if (levelNameToken == null) return defaultLevel;
            string name = levelNameToken.ToString();
            return map.ContainsKey(name) ? map[name] : defaultLevel;
        }

        private static Connector GetClosestConnector(MEPCurve curve, XYZ targetPt)
        {
            Connector closest = null;
            double minDist = double.MaxValue;
            if (curve.ConnectorManager == null) return null;
            
            foreach (Connector conn in curve.ConnectorManager.Connectors)
            {
                double d = conn.Origin.DistanceTo(targetPt);
                if (d < minDist)
                {
                    minDist = d;
                    closest = conn;
                }
            }
            return closest;
        }
    }
}
