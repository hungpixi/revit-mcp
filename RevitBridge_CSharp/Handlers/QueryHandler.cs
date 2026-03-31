using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;

namespace RevitBridge.Handlers
{
    public static class QueryHandler
    {
        public static string Handle(UIApplication uiapp, string action, Dictionary<string, object> payload)
        {
            UIDocument uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) return "{\"status\": \"Error\", \"msg\": \"No active document in Revit\"}";
            Document doc = uidoc.Document;

            try
            {
                if (action == "get_element_ids_by_category")
                {
                    string categoryName = payload.ContainsKey("category") ? payload["category"].ToString() : "";
                    int limit = payload.ContainsKey("limit") ? Convert.ToInt32(payload["limit"]) : 100;

                    // Map category name to BuiltInCategory
                    BuiltInCategory bic = BuiltInCategory.INVALID;
                    switch (categoryName.ToLower())
                    {
                        case "lighting": case "lightingfixtures": case "lights":
                            bic = BuiltInCategory.OST_LightingFixtures; break;
                        case "electricalequipment": case "panels":
                            bic = BuiltInCategory.OST_ElectricalEquipment; break;
                        case "cabletray": case "cabletrays":
                            bic = BuiltInCategory.OST_CableTray; break;
                        case "conduit": case "conduits":
                            bic = BuiltInCategory.OST_Conduit; break;
                        case "walls":
                            bic = BuiltInCategory.OST_Walls; break;
                        case "columns": case "structuralcolumns":
                            bic = BuiltInCategory.OST_StructuralColumns; break;
                        default:
                            throw new Exception("Category không hỗ trợ: " + categoryName);
                    }

                    var collector = new FilteredElementCollector(doc)
                        .OfCategory(bic)
                        .WhereElementIsNotElementType();

                    var elements = collector.Take(limit).Select(e => new {
                        id = e.Id.IntegerValue,
                        name = e.Name,
                        category = e.Category?.Name ?? ""
                    }).ToList();

                    var result = new { status = "OK", count = elements.Count, elements = elements };
                    return JsonConvert.SerializeObject(result);
                }
                else if (action == "get_project_info")
                {
                    // Levels
                    var levels = new FilteredElementCollector(doc).OfClass(typeof(Level))
                        .Cast<Level>()
                        .Select(l => new { id = l.Id.IntegerValue, name = l.Name, elevation = l.Elevation })
                        .ToList();

                    // Count elements by category
                    int wallCount = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls).WhereElementIsNotElementType().GetElementCount();
                    int lightCount = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_LightingFixtures).WhereElementIsNotElementType().GetElementCount();
                    int trayCount = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_CableTray).WhereElementIsNotElementType().GetElementCount();
                    int conduitCount = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Conduit).WhereElementIsNotElementType().GetElementCount();
                    int panelCount = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_ElectricalEquipment).WhereElementIsNotElementType().GetElementCount();

                    var result = new {
                        status = "OK",
                        levels = levels,
                        counts = new { walls = wallCount, lights = lightCount, cableTrays = trayCount, conduits = conduitCount, panels = panelCount }
                    };
                    return JsonConvert.SerializeObject(result);
                }

                return "{\"status\": \"Error\", \"msg\": \"QueryHandler unknown action: " + action + "\"}";
            }
            catch (Exception ex)
            {
                return "{\"status\": \"Error\", \"msg\": \"" + ex.Message.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}";
            }
        }
    }
}
