using System;
using System.Collections.Generic;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using RevitBridge.Utils;

namespace RevitBridge.Handlers
{
    public static class ArchHandler
    {
        public static string Handle(UIApplication uiapp, string action, Dictionary<string, object> payload)
        {
            UIDocument uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) return "{\"status\": \"Error\", \"msg\": \"No active document in Revit\"}";
            Document doc = uidoc.Document;

            string statusMsg = "Action '" + action + "' handled successfully.";

            using (Transaction t = new Transaction(doc, "Antigravity AI: " + action))
            {
                t.Start();

                try
                {
                    if (action == "create_grid")
                    {
                        var stList = new System.Collections.ArrayList((System.Collections.ICollection)payload["startPt"]);
                        var enList = new System.Collections.ArrayList((System.Collections.ICollection)payload["endPt"]);
                        XYZ start = new XYZ(Convert.ToDouble(stList[0]), Convert.ToDouble(stList[1]), Convert.ToDouble(stList[2]));
                        XYZ end = new XYZ(Convert.ToDouble(enList[0]), Convert.ToDouble(enList[1]), Convert.ToDouble(enList[2]));
                        
                        Line line = Line.CreateBound(start, end);
                        Grid grid = Grid.Create(doc, line);
                        if (payload.ContainsKey("name"))
                        {
                            grid.Name = payload["name"].ToString();
                        }
                    }
                    else if (action == "create_level")
                    {
                        double elevationFeet = Convert.ToDouble(payload["elevation"]);
                        Level level = Level.Create(doc, elevationFeet);
                        if (payload.ContainsKey("name"))
                        {
                            level.Name = payload["name"].ToString();
                        }
                    }
                    else if (action == "create_wall")
                    {
                        var ptList = new System.Collections.ArrayList((System.Collections.ICollection)payload["startPt"]);
                        var endList = new System.Collections.ArrayList((System.Collections.ICollection)payload["endPt"]);
                        XYZ st = new XYZ(Convert.ToDouble(ptList[0]), Convert.ToDouble(ptList[1]), Convert.ToDouble(ptList[2]));
                        XYZ en = new XYZ(Convert.ToDouble(endList[0]), Convert.ToDouble(endList[1]), Convert.ToDouble(endList[2]));
                        
                        Line line = Line.CreateBound(st, en);
                        Level firstLevel = new FilteredElementCollector(doc).OfClass(typeof(Level)).FirstElement() as Level;
                        if (firstLevel != null)
                        {
                            Wall.Create(doc, line, firstLevel.Id, false);
                        }
                    }
                    else if (action == "place_family_instance")
                    {
                        var ptList = new System.Collections.ArrayList((System.Collections.ICollection)payload["point"]);
                        XYZ point = new XYZ(Convert.ToDouble(ptList[0]), Convert.ToDouble(ptList[1]), Convert.ToDouble(ptList[2]));

                        Level firstLevel = new FilteredElementCollector(doc).OfClass(typeof(Level)).FirstElement() as Level;

                        string categoryStr = payload.ContainsKey("category") ? payload["category"].ToString() : "Doors";
                        BuiltInCategory bic = categoryStr.ToLower() == "windows" ? BuiltInCategory.OST_Windows : BuiltInCategory.OST_Doors;

                        FamilySymbol symbolToPlace = DynamicFamilyLoader.GetOrLoadFamilySymbol(doc, bic);

                        if (symbolToPlace != null && firstLevel != null)
                        {
                            // Neu la door/window thuong can host
                            // Tam dung NewFamilyInstance voi XYZ de xem phan hoi cua Revit (trong nhieu truong hop se loi neu can Host)
                            // Neu khong co host, window/door se k hop le. 
                            doc.Create.NewFamilyInstance(point, symbolToPlace, firstLevel, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        }
                        else
                        {
                            statusMsg = "Cảnh báo: Không tìm thấy Family Mẫu cho " + categoryStr;
                        }
                    }
                    else if (action == "import_image")
                    {
                        string imagePath = payload["path"].ToString();
                        if (!System.IO.File.Exists(imagePath)) throw new Exception("Không tìm thấy tệp ảnh: " + imagePath);
                        
                        var ptList = payload.ContainsKey("point") ? new System.Collections.ArrayList((System.Collections.ICollection)payload["point"]) : null;
                        XYZ point = ptList != null && ptList.Count >= 3 ? new XYZ(Convert.ToDouble(ptList[0]), Convert.ToDouble(ptList[1]), Convert.ToDouble(ptList[2])) : XYZ.Zero;

                        ImageTypeOptions typeOptions = new ImageTypeOptions(imagePath);
                        ImageType type = ImageType.Create(doc, typeOptions);
                        
                        ImagePlacementOptions placementOptions = new ImagePlacementOptions(point, BoxPlacement.Center);
                        Autodesk.Revit.DB.ImageInstance.Create(doc, doc.ActiveView, type.Id, placementOptions);
                        
                        statusMsg = "Đã Import thành công ảnh: " + imagePath;
                    }
                    else
                    {
                        // Action not supported in this handler yet
                        t.RollBack();
                        return "{\"status\": \"Error\", \"msg\": \"ArchHandler không hỗ trợ hành động: " + action + "\"}";
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
