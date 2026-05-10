using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace RevitMCP.Handlers
{
    // ─── CreateViewSheet ──────────────────────────────────────────────────────

    public class CreateViewSheetHandler
    {
        private readonly Document _doc;
        public CreateViewSheetHandler(Document doc) { _doc = doc; }

        public JObject Execute(JObject parameters)
        {
            var sheets = parameters["sheets"] as JArray;
            if (sheets == null || sheets.Count == 0) return Error("No sheets provided");

            var results = new JArray();

            using (var tx = new Transaction(_doc, "MCP: Create View Sheets"))
            {
                tx.Start();

                // Get default title block
                var titleBlocks = new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .ToList();

                foreach (JObject sheetSpec in sheets)
                {
                    try
                    {
                        string number   = sheetSpec["sheetNumber"]?.Value<string>() ?? "A-000";
                        string name     = sheetSpec["sheetName"]?.Value<string>() ?? "New Sheet";
                        int? titleBlockId = sheetSpec["titleBlockTypeId"]?.Value<int?>();

                        FamilySymbol titleBlock;
                        if (titleBlockId.HasValue)
                            titleBlock = _doc.GetElement(new ElementId(titleBlockId.Value)) as FamilySymbol;
                        else
                            titleBlock = titleBlocks.FirstOrDefault();

                        if (titleBlock == null || !titleBlock.IsActive)
                            titleBlock?.Activate();

                        ViewSheet sheet = titleBlock != null
                            ? ViewSheet.Create(_doc, titleBlock.Id)
                            : ViewSheet.Create(_doc, ElementId.InvalidElementId);

                        sheet.SheetNumber = number;
                        sheet.Name = name;

                        var viewResults = new JArray();

                        // Place views on sheet
                        var viewsArray = sheetSpec["views"] as JArray;
                        if (viewsArray != null)
                        {
                            foreach (JObject vs in viewsArray)
                            {
                                int viewId = vs["viewId"]?.Value<int>() ?? -1;
                                var view = _doc.GetElement(new ElementId(viewId)) as View;
                                if (view == null || view is ViewSheet) continue;

                                // Override scale if specified
                                int? scale = vs["scale"]?.Value<int?>();
                                if (scale.HasValue && scale > 0)
                                    view.Scale = scale.Value;

                                // Center position or provided coords
                                XYZ center;
                                double? cx = vs["centerX"]?.Value<double?>();
                                double? cy = vs["centerY"]?.Value<double?>();

                                if (cx.HasValue && cy.HasValue)
                                    center = new XYZ(cx.Value / 304.8, cy.Value / 304.8, 0);
                                else
                                    center = new XYZ(0.5, 0.35, 0); // ~A1 center

                                var vp = Viewport.Create(_doc, sheet.Id, view.Id, center);
                                viewResults.Add(new JObject
                                {
                                    ["viewId"]     = viewId,
                                    ["viewportId"] = vp.Id.IntegerValue,
                                    ["status"]     = "placed"
                                });
                            }
                        }

                        results.Add(new JObject
                        {
                            ["sheetNumber"] = number,
                            ["sheetId"]     = sheet.Id.IntegerValue,
                            ["status"]      = "created",
                            ["views"]       = viewResults
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new JObject
                        {
                            ["sheetNumber"] = sheetSpec["sheetNumber"]?.Value<string>(),
                            ["status"]      = "error",
                            ["message"]     = ex.Message
                        });
                    }
                }

                tx.Commit();
            }

            return new JObject { ["status"] = "success", ["sheets"] = results };
        }

        private static JObject Error(string msg) => new JObject { ["status"] = "error", ["message"] = msg };
    }

    // ─── CreateSchedule ───────────────────────────────────────────────────────

    public class CreateScheduleHandler
    {
        private readonly Document _doc;
        public CreateScheduleHandler(Document doc) { _doc = doc; }

        public JObject Execute(JObject parameters)
        {
            string scheduleName = parameters["scheduleName"]?.Value<string>() ?? "MCP Schedule";
            string categoryName = parameters["category"]?.Value<string>() ?? "Walls";
            var fieldNames      = (parameters["fields"] as JArray)?.Select(f => f.Value<string>()).ToList() ?? new List<string>();
            bool showTotals     = parameters["showTotals"]?.Value<bool>() ?? false;
            bool isItemized     = parameters["isItemized"]?.Value<bool>() ?? true;

            // Resolve category
            BuiltInCategory bic = ResolveCategoryName(categoryName);

            using (var tx = new Transaction(_doc, "MCP: Create Schedule"))
            {
                tx.Start();

                var schedule = ViewSchedule.CreateSchedule(_doc, new ElementId(bic));
                schedule.Name = scheduleName;

                var def = schedule.Definition;
                def.IsItemized = isItemized;

                // Add fields
                var availableFields = def.GetSchedulableFields();
                foreach (string fieldName in fieldNames)
                {
                    var sf = availableFields.FirstOrDefault(f =>
                        f.GetName(_doc).Equals(fieldName, StringComparison.OrdinalIgnoreCase));
                    if (sf != null)
                    {
                        var field = def.AddField(sf);
                        if (showTotals && sf.FieldType == ScheduleFieldType.Instance)
                        {
                            field.DisplayType = ScheduleFieldDisplayType.Totals;
                        }
                    }
                }

                // Filters / Sort / Group-by
                // Revit 2020 schedule filter/sort APIs differ by field id types.
                // For stability, we currently skip these optional enhancements.

                tx.Commit();

                return new JObject
                {
                    ["status"]     = "success",
                    ["scheduleId"] = schedule.Id.IntegerValue,
                    ["name"]       = schedule.Name,
                    ["fieldCount"] = def.GetFieldCount()
                };
            }
        }

        private BuiltInCategory ResolveCategoryName(string name) => name.ToLower() switch
        {
            "walls"               => BuiltInCategory.OST_Walls,
            "floors"              => BuiltInCategory.OST_Floors,
            "doors"               => BuiltInCategory.OST_Doors,
            "windows"             => BuiltInCategory.OST_Windows,
            "rooms"               => BuiltInCategory.OST_Rooms,
            "structural framing"  => BuiltInCategory.OST_StructuralFraming,
            "structural columns"  => BuiltInCategory.OST_StructuralColumns,
            "ceilings"            => BuiltInCategory.OST_Ceilings,
            "roofs"               => BuiltInCategory.OST_Roofs,
            "stairs"              => BuiltInCategory.OST_Stairs,
            "furniture"           => BuiltInCategory.OST_Furniture,
            "materials"           => BuiltInCategory.OST_Materials,
            _                     => BuiltInCategory.OST_Walls
        };
    }

    // ─── DetectClashes ────────────────────────────────────────────────────────

    public class DetectClashesHandler
    {
        private readonly Document _doc;
        public DetectClashesHandler(Document doc) { _doc = doc; }

        public JObject Execute(JObject parameters)
        {
            var setASpec  = parameters["setA"] as JObject;
            var setBSpec  = parameters["setB"] as JObject;
            int limit     = parameters["limitResults"]?.Value<int>() ?? 100;
            string type   = parameters["clashType"]?.Value<string>() ?? "hard";
            double clearance = parameters["clearanceMm"]?.Value<double>() ?? 0;

            var setA = GetElements(setASpec);
            var setB = GetElements(setBSpec);

            if (setA.Count == 0 || setB.Count == 0)
                return Error("One or both element sets are empty");

            var clashes = new JArray();

            foreach (var elemA in setA)
            {
                if (clashes.Count >= limit) break;
                var solidA = GetSolid(elemA);
                if (solidA == null) continue;

                foreach (var elemB in setB)
                {
                    if (clashes.Count >= limit) break;
                    if (elemA.Id == elemB.Id) continue;

                    var solidB = GetSolid(elemB);
                    if (solidB == null) continue;

                    double volume = 0;
                    bool isClash;
                    if (type == "hard")
                    {
                        volume = GetIntersectionVolume(solidA, solidB);
                        isClash = volume > 1e-10;
                    }
                    else
                    {
                        isClash = IsWithinClearance(elemA, elemB, clearance / 304.8);
                    }

                    if (isClash)
                    {
                        var bbA = elemA.get_BoundingBox(null);
                        XYZ center = bbA != null ? (bbA.Min + bbA.Max) / 2 : XYZ.Zero;

                        clashes.Add(new JObject
                        {
                            ["elementA_Id"]       = elemA.Id.IntegerValue,
                            ["elementA_Name"]     = elemA.Name,
                            ["elementA_Category"] = elemA.Category?.Name,
                            ["elementB_Id"]       = elemB.Id.IntegerValue,
                            ["elementB_Name"]     = elemB.Name,
                            ["elementB_Category"] = elemB.Category?.Name,
                            ["clashType"]         = type,
                            ["intersectVolumeCm3"] = Math.Round(volume * 28316.8, 2),
                            ["locationX"]         = Math.Round(center.X * 304.8, 0),
                            ["locationY"]         = Math.Round(center.Y * 304.8, 0),
                            ["locationZ"]         = Math.Round(center.Z * 304.8, 0),
                        });
                    }
                }
            }

            return new JObject
            {
                ["status"]      = "success",
                ["clashCount"]  = clashes.Count,
                ["clashType"]   = type,
                ["clashes"]     = clashes
            };
        }

        private List<Element> GetElements(JObject spec)
        {
            var results = new List<Element>();
            if (spec == null) return results;

            var ids = spec["elementIds"] as JArray;
            if (ids != null)
            {
                foreach (var id in ids)
                    results.Add(_doc.GetElement(new ElementId(id.Value<int>())));
                return results.Where(e => e != null).ToList();
            }

            var catNames = (spec["categories"] as JArray)?.Select(c => c.Value<string>()).ToList();
            if (catNames == null) return results;

            var catIds = catNames
                .Select(GetCategoryId)
                .Where(id => id != ElementId.InvalidElementId)
                .ToList();

            if (catIds.Count > 0)
            {
                results.AddRange(new FilteredElementCollector(_doc)
                    .WherePasses(new ElementMulticategoryFilter(catIds))
                    .WhereElementIsNotElementType()
                    .ToElements());
            }

            return results;
        }

        private Solid GetSolid(Element elem)
        {
            try
            {
                var opts = new Options { DetailLevel = ViewDetailLevel.Fine };
                var geo  = elem.get_Geometry(opts);
                foreach (var obj in geo)
                {
                    if (obj is Solid s && s.Volume > 0) return s;
                    if (obj is GeometryInstance gi)
                        foreach (var inner in gi.GetInstanceGeometry())
                            if (inner is Solid s2 && s2.Volume > 0) return s2;
                }
            }
            catch { }
            return null;
        }

        private double GetIntersectionVolume(Solid solidA, Solid solidB)
        {
            try
            {
                var intersection = BooleanOperationsUtils.ExecuteBooleanOperation(
                    solidA, solidB, BooleanOperationsType.Intersect);
                return intersection?.Volume ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        private bool IsWithinClearance(Element a, Element b, double clearanceFt)
        {
            var bbA = a.get_BoundingBox(null);
            var bbB = b.get_BoundingBox(null);
            if (bbA == null || bbB == null) return false;

            return bbA.Min.X - clearanceFt < bbB.Max.X &&
                   bbA.Max.X + clearanceFt > bbB.Min.X &&
                   bbA.Min.Y - clearanceFt < bbB.Max.Y &&
                   bbA.Max.Y + clearanceFt > bbB.Min.Y &&
                   bbA.Min.Z - clearanceFt < bbB.Max.Z &&
                   bbA.Max.Z + clearanceFt > bbB.Min.Z;
        }

        private ElementId GetCategoryId(string name)
        {
            foreach (Category cat in _doc.Settings.Categories)
                if (cat.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return cat.Id;
            return ElementId.InvalidElementId;
        }

        private static JObject Error(string msg) => new JObject { ["status"] = "error", ["message"] = msg };
    }
}
