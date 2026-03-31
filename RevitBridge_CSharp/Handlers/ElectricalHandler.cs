using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using RevitBridge.Utils;

namespace RevitBridge.Handlers
{
    public static class ElectricalHandler
    {
        public static string Handle(UIApplication uiapp, string action, Dictionary<string, object> payload)
        {
            UIDocument uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) return "{\"status\": \"Error\", \"msg\": \"No active document in Revit\"}";
            Document doc = uidoc.Document;

            string statusMsg = "OK";

            using (Transaction t = new Transaction(doc, "Antigravity AI: " + action))
            {
                t.Start();
                try
                {
                    if (action == "create_cable_tray")
                    {
                        var pts = new System.Collections.ArrayList((System.Collections.ICollection)payload["points"]);
                        if (pts == null || pts.Count < 2) throw new Exception("Tạo Máng Cáp cần ít nhất 2 vector points (Start, End).");

                        Level level = new FilteredElementCollector(doc).OfClass(typeof(Level)).FirstElement() as Level;
                        CableTrayType trayType = new FilteredElementCollector(doc).OfClass(typeof(CableTrayType)).FirstElement() as CableTrayType;
                        
                        if (level == null || trayType == null) throw new Exception("Thiếu Profile CableTrayType trong Template.");

                        List<CableTray> created = new List<CableTray>();
                        
                        // Kích thước chuẩn quy đổi Feet (Ví dụ Width=300mm, Height=100mm)
                        double w = payload.ContainsKey("width") ? Convert.ToDouble(payload["width"]) / 304.8 : 300.0 / 304.8;
                        double h = payload.ContainsKey("height") ? Convert.ToDouble(payload["height"]) / 304.8 : 100.0 / 304.8;

                        for (int i = 0; i < pts.Count - 1; i++)
                        {
                            var ptList0 = new System.Collections.ArrayList((System.Collections.ICollection)pts[i]);
                            var ptList1 = new System.Collections.ArrayList((System.Collections.ICollection)pts[i+1]);
                            XYZ p0 = new XYZ(Convert.ToDouble(ptList0[0]), Convert.ToDouble(ptList0[1]), Convert.ToDouble(ptList0[2]));
                            XYZ p1 = new XYZ(Convert.ToDouble(ptList1[0]), Convert.ToDouble(ptList1[1]), Convert.ToDouble(ptList1[2]));
                            
                            CableTray tray = CableTray.Create(doc, trayType.Id, p0, p1, level.Id);
                            
                            Parameter wParam = tray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM);
                            Parameter hParam = tray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM);
                            if (wParam != null && !wParam.IsReadOnly) wParam.Set(w);
                            if (hParam != null && !hParam.IsReadOnly) hParam.Set(h);

                            created.Add(tray);
                        }

                        // Tự ghép góc Cút điện
                        for (int i = 0; i < created.Count - 1; i++)
                        {
                            MEPRoutingUtils.AutoConnectElbow(doc, created[i], created[i+1]);
                        }
                        
                        statusMsg = $"Đã rải {created.Count} máng cáp Tự Động ôm cua.";
                    }
                    else if (action == "create_conduit")
                    {
                        var pts = new System.Collections.ArrayList((System.Collections.ICollection)payload["points"]);
                        if (pts == null || pts.Count < 2) throw new Exception("Tạo Conduit cần ít nhất 2 điểm (Start, End).");

                        Level level = new FilteredElementCollector(doc).OfClass(typeof(Level)).FirstElement() as Level;
                        ConduitType conduitType = new FilteredElementCollector(doc).OfClass(typeof(ConduitType)).FirstElement() as ConduitType;

                        if (level == null || conduitType == null) throw new Exception("Thiếu ConduitType trong template. Hãy load MEP template.");

                        double diameter = payload.ContainsKey("diameter") ? Convert.ToDouble(payload["diameter"]) / 304.8 : 25.0 / 304.8; // mm → feet

                        List<Autodesk.Revit.DB.Electrical.Conduit> created = new List<Autodesk.Revit.DB.Electrical.Conduit>();
                        for (int i = 0; i < pts.Count - 1; i++)
                        {
                            var ptList0 = new System.Collections.ArrayList((System.Collections.ICollection)pts[i]);
                            var ptList1 = new System.Collections.ArrayList((System.Collections.ICollection)pts[i + 1]);
                            XYZ p0 = new XYZ(Convert.ToDouble(ptList0[0]), Convert.ToDouble(ptList0[1]), Convert.ToDouble(ptList0[2]));
                            XYZ p1 = new XYZ(Convert.ToDouble(ptList1[0]), Convert.ToDouble(ptList1[1]), Convert.ToDouble(ptList1[2]));

                            var conduit = Autodesk.Revit.DB.Electrical.Conduit.Create(doc, conduitType.Id, p0, p1, level.Id);
                            Parameter dParam = conduit.get_Parameter(BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM);
                            if (dParam != null && !dParam.IsReadOnly) dParam.Set(diameter);
                            created.Add(conduit);
                        }

                        statusMsg = $"Đã đặt {created.Count} đoạn ống luồn dây (Conduit) thành công.";
                    }
                    else if (action == "place_electrical_equipment")
                    {
                        var pts = new System.Collections.ArrayList((System.Collections.ICollection)payload["points"]);
                        Level level = new FilteredElementCollector(doc).OfClass(typeof(Level)).FirstElement() as Level;
                        
                        FamilySymbol symbol = DynamicFamilyLoader.GetOrLoadFamilySymbol(doc, BuiltInCategory.OST_ElectricalEquipment);
                        if (symbol == null) throw new Exception("Không tìm thấy Family Tủ/Thiết bị điện (Electrical Equipment) trong Library.");

                        List<FamilyInstance> created = new List<FamilyInstance>();
                        foreach (object ptObj in pts)
                        {
                            var ptList = new System.Collections.ArrayList((System.Collections.ICollection)ptObj);
                            XYZ pt = new XYZ(Convert.ToDouble(ptList[0]), Convert.ToDouble(ptList[1]), Convert.ToDouble(ptList[2]));
                            FamilyInstance inst = doc.Create.NewFamilyInstance(pt, symbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                            created.Add(inst);
                        }
                        
                        statusMsg = $"Đã đặt thành công {created.Count} Thiết bị điện.";
                    }
                    else if (action == "place_lighting_fixture")
                    {
                        Level level = new FilteredElementCollector(doc).OfClass(typeof(Level)).FirstElement() as Level;
                        FamilySymbol symbol = DynamicFamilyLoader.GetOrLoadFamilySymbol(doc, BuiltInCategory.OST_LightingFixtures);
                        if (symbol == null) throw new Exception("Không tìm thấy Family Đèn (Lighting Fixtures) trong Template. Hãy load MEP template có đèn.");

                        List<FamilyInstance> created = new List<FamilyInstance>();

                        // Mode 1: Grid pattern (đặt hàng loạt theo lưới)
                        if (payload.ContainsKey("grid"))
                        {
                            var gridObj = payload["grid"];
                            var gridDict = gridObj is Dictionary<string, object> ? (Dictionary<string, object>)gridObj 
                                : Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(gridObj.ToString());
                            
                            double originX = Convert.ToDouble(gridDict.ContainsKey("originX") ? gridDict["originX"] : 0);
                            double originY = Convert.ToDouble(gridDict.ContainsKey("originY") ? gridDict["originY"] : 0);
                            double spacingX = Convert.ToDouble(gridDict.ContainsKey("spacingX") ? gridDict["spacingX"] : 10);
                            double spacingY = Convert.ToDouble(gridDict.ContainsKey("spacingY") ? gridDict["spacingY"] : 10);
                            int countX = Convert.ToInt32(gridDict.ContainsKey("countX") ? gridDict["countX"] : 5);
                            int countY = Convert.ToInt32(gridDict.ContainsKey("countY") ? gridDict["countY"] : 5);
                            double elev = Convert.ToDouble(gridDict.ContainsKey("elevation") ? gridDict["elevation"] : 15);

                            for (int ix = 0; ix < countX; ix++)
                            {
                                for (int iy = 0; iy < countY; iy++)
                                {
                                    double x = originX + ix * spacingX;
                                    double y = originY + iy * spacingY;
                                    XYZ pt = new XYZ(x, y, elev);
                                    FamilyInstance inst = doc.Create.NewFamilyInstance(pt, symbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                    created.Add(inst);
                                }
                            }
                        }
                        // Mode 2: Điểm riêng lẻ
                        else if (payload.ContainsKey("points"))
                        {
                            var pts = new System.Collections.ArrayList((System.Collections.ICollection)payload["points"]);
                            double defaultElev = payload.ContainsKey("elevation") ? Convert.ToDouble(payload["elevation"]) : 15;
                            foreach (object ptObj in pts)
                            {
                                var ptList = new System.Collections.ArrayList((System.Collections.ICollection)ptObj);
                                double z = ptList.Count >= 3 ? Convert.ToDouble(ptList[2]) : defaultElev;
                                XYZ pt = new XYZ(Convert.ToDouble(ptList[0]), Convert.ToDouble(ptList[1]), z);
                                FamilyInstance inst = doc.Create.NewFamilyInstance(pt, symbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                created.Add(inst);
                            }
                        }
                        else throw new Exception("Cần 'grid' hoặc 'points' trong payload.");

                        statusMsg = $"Đã đặt thành công {created.Count} đèn chiếu sáng.";
                    }
                    else if (action == "create_electrical_circuit")
                    {
                        string sysTypeStr = payload.ContainsKey("systemType") ? payload["systemType"].ToString() : "Lighting";
                        ElectricalSystemType sysType = sysTypeStr.ToLower() == "power" 
                            ? ElectricalSystemType.PowerCircuit 
                            : ElectricalSystemType.PowerCircuit; // Revit 2020 uses PowerCircuit

                        // Collect fixture element IDs
                        var idsRaw = new System.Collections.ArrayList((System.Collections.ICollection)payload["fixtureIds"]);
                        List<ElementId> ids = new List<ElementId>();
                        foreach (var idObj in idsRaw)
                        {
                            ids.Add(new ElementId(Convert.ToInt32(idObj)));
                        }

                        if (ids.Count == 0) throw new Exception("fixtureIds trống, cần ít nhất 1 element.");

                        // Create electrical system from the collected IDs
                        ElectricalSystem circuit = ElectricalSystem.Create(doc, ids, sysType);

                        // Connect to panel if specified
                        if (payload.ContainsKey("panelId"))
                        {
                            int panelIdInt = Convert.ToInt32(payload["panelId"]);
                            FamilyInstance panel = doc.GetElement(new ElementId(panelIdInt)) as FamilyInstance;
                            if (panel != null)
                            {
                                circuit.SelectPanel(panel);
                            }
                        }

                        statusMsg = $"Đã tạo mạch điện với {ids.Count} thiết bị. Circuit ID: {circuit.Id.IntegerValue}";
                    }
                    else
                    {
                        t.RollBack();
                        return "{\"status\": \"Error\", \"msg\": \"ElectricalHandler No Action Mapped: " + action + "\"}";
                    }
                    t.Commit();
                }
                catch (Exception ex)
                {
                    if (t.HasStarted()) t.RollBack();
                    return "{\"status\": \"Error\", \"msg\": \"" + ex.Message.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}";
                }
            }
            return "{\"status\": \"OK\", \"msg\": \"" + statusMsg + "\"}";
        }
    }
}
