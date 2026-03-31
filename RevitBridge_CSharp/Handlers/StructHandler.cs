using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitBridge.Utils;

namespace RevitBridge.Handlers
{
    public static class StructHandler
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
                    if (action == "create_structural_column")
                    {
                        var pts = new System.Collections.ArrayList((System.Collections.ICollection)payload["points"]);
                        Level level = new FilteredElementCollector(doc).OfClass(typeof(Level)).FirstElement() as Level;
                        
                        FamilySymbol symbol = DynamicFamilyLoader.GetOrLoadFamilySymbol(doc, BuiltInCategory.OST_StructuralColumns);
                        if (symbol == null) throw new Exception("Không tìm thấy Family Móng Cột Thép/Bê Tông (Structural Column) trong Library.");

                        List<FamilyInstance> created = new List<FamilyInstance>();
                        foreach (object ptObj in pts)
                        {
                            var ptList = new System.Collections.ArrayList((System.Collections.ICollection)ptObj);
                            XYZ pt = new XYZ(Convert.ToDouble(ptList[0]), Convert.ToDouble(ptList[1]), Convert.ToDouble(ptList[2]));
                            FamilyInstance inst = doc.Create.NewFamilyInstance(pt, symbol, level, StructuralType.Column);
                            created.Add(inst);
                        }
                        
                        statusMsg = $"Đã đặt thành công {created.Count} Cột Kết cấu.";
                    }
                    else if (action == "create_structural_framing")
                    {
                        var pts = new System.Collections.ArrayList((System.Collections.ICollection)payload["points"]);
                        if (pts == null || pts.Count < 2) throw new Exception("Cần >= 2 points để nối Dầm.");
                        
                        Level level = new FilteredElementCollector(doc).OfClass(typeof(Level)).FirstElement() as Level;
                        
                        FamilySymbol symbol = DynamicFamilyLoader.GetOrLoadFamilySymbol(doc, BuiltInCategory.OST_StructuralFraming);
                        if (symbol == null) throw new Exception("Lỗi Data System - Không tìm thấy Dầm/Kèo (Structural Framing).");

                        List<FamilyInstance> created = new List<FamilyInstance>();
                        for (int i = 0; i < pts.Count - 1; i++)
                        {
                            var ptList0 = new System.Collections.ArrayList((System.Collections.ICollection)pts[i]);
                            var ptList1 = new System.Collections.ArrayList((System.Collections.ICollection)pts[i+1]);
                            XYZ p0 = new XYZ(Convert.ToDouble(ptList0[0]), Convert.ToDouble(ptList0[1]), Convert.ToDouble(ptList0[2]));
                            XYZ p1 = new XYZ(Convert.ToDouble(ptList1[0]), Convert.ToDouble(ptList1[1]), Convert.ToDouble(ptList1[2]));
                            
                            Line curve = Line.CreateBound(p0, p1);
                            FamilyInstance inst = doc.Create.NewFamilyInstance(curve, symbol, level, StructuralType.Beam);
                            created.Add(inst);
                        }
                        
                        statusMsg = $"Đã kẻ nhánh {created.Count} Dầm/Kèo Thép liên tục đi qua cấp điểm.";
                    }
                    else
                    {
                        t.RollBack();
                        return "{\"status\": \"Error\", \"msg\": \"StructHandler chưa có route cho lệnh: " + action + "\"}";
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
