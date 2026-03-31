using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using RevitBridge.Utils;

namespace RevitBridge.Handlers
{
    public static class HvacHandler
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
                    if (action == "create_duct")
                    {
                        var pts = new System.Collections.ArrayList((System.Collections.ICollection)payload["points"]);
                        if (pts == null || pts.Count < 2) throw new Exception("Mảng 'points' phải có ít nhất 2 XYZ");

                        List<XYZ> xyzs = new List<XYZ>();
                        foreach (object ptObj in pts)
                        {
                            var ptList = new System.Collections.ArrayList((System.Collections.ICollection)ptObj);
                            xyzs.Add(new XYZ(Convert.ToDouble(ptList[0]), Convert.ToDouble(ptList[1]), Convert.ToDouble(ptList[2])));
                        }

                        // Lấy hệ thống System Type (Supply Air làm mặc định)
                        MechanicalSystemType systemType = new FilteredElementCollector(doc)
                            .OfClass(typeof(MechanicalSystemType))
                            .Cast<MechanicalSystemType>()
                            .FirstOrDefault(s => s.SystemClassification == MEPSystemClassification.SupplyAir) 
                            ?? new FilteredElementCollector(doc).OfClass(typeof(MechanicalSystemType)).FirstElement() as MechanicalSystemType;

                        // Tìm DuctType hợp lý
                        DuctType ductType = new FilteredElementCollector(doc)
                            .OfClass(typeof(DuctType))
                            .Cast<DuctType>()
                            .FirstOrDefault(d => d.FamilyName.ToLower().Contains("rect") || d.FamilyName.Contains("Hình chữ nhật"))
                            ?? new FilteredElementCollector(doc).OfClass(typeof(DuctType)).FirstElement() as DuctType;

                        Level firstLevel = new FilteredElementCollector(doc).OfClass(typeof(Level)).FirstElement() as Level;

                        if (ductType == null || systemType == null || firstLevel == null)
                        {
                            throw new Exception("Không thể tạo Duct: Thiếu DuctType, SystemType, hoặc Level trong dự án");
                        }

                        List<Duct> createdDucts = new List<Duct>();

                        // Feet conversion
                        double w = payload.ContainsKey("width") ? Convert.ToDouble(payload["width"]) / 304.8 : 300 / 304.8;
                        double h = payload.ContainsKey("height") ? Convert.ToDouble(payload["height"]) / 304.8 : 300 / 304.8;

                        for (int i = 0; i < xyzs.Count - 1; i++)
                        {
                            Duct newDuct = Duct.Create(doc, systemType.Id, ductType.Id, firstLevel.Id, xyzs[i], xyzs[i+1]);
                            
                            // Edit Parameter if exist
                            Parameter wParam = newDuct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
                            Parameter hParam = newDuct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
                            if (wParam != null && !wParam.IsReadOnly) wParam.Set(w);
                            if (hParam != null && !hParam.IsReadOnly) hParam.Set(h);

                            createdDucts.Add(newDuct);
                        }

                        // Tự động kết nối hai nhánh bằng tính năng Routing Utility mới tạo của ta
                        for (int i = 0; i < createdDucts.Count - 1; i++)
                        {
                            MEPRoutingUtils.AutoConnectElbow(doc, createdDucts[i], createdDucts[i+1]);
                        }
                        
                        statusMsg = "Đã rải " + createdDucts.Count + " đoạn ống gió kín mạc (Hệ: " + systemType.Name + ")";
                    }
                    else
                    {
                        t.RollBack();
                        return "{\"status\": \"Error\", \"msg\": \"HvacHandler chưa hỗ trợ lệnh: " + action + "\"}";
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
