using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace RevitMCPPlugin.Commands
{
    /// <summary>
    /// View-related MCP commands.
    /// All linear dimensions are returned in millimetres (feet × 304.8).
    /// </summary>
    public static class ViewCommands
    {
        private const double FeetToMm = 304.8;

        // ─────────────────────────────────────────────────────────────────────
        // get_current_view_info
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns metadata about the document's active view:
        /// type, name, scale, associated level, and crop-region bounding box.
        /// </summary>
        public static JObject GetCurrentViewInfo(Document doc)
        {
            if (doc == null)
                return Error("No active document");

            View activeView = doc.ActiveView;
            if (activeView == null)
                return Error("No active view");

            var info = new JObject
            {
                ["viewId"]   = activeView.Id.IntegerValue,
                ["name"]     = activeView.Name,
                ["viewType"] = activeView.ViewType.ToString(),
                ["scale"]    = activeView.Scale,
                ["detailLevel"] = activeView.DetailLevel.ToString(),
                ["disciplineFilter"] = activeView.Discipline.ToString()
            };

            // Associated level (floor plan / ceiling plan / structural plan)
            if (activeView is ViewPlan viewPlan)
            {
                Level level = doc.GetElement(viewPlan.GenLevel?.Id ?? ElementId.InvalidElementId) as Level;
                if (level != null)
                {
                    info["levelId"]        = level.Id.IntegerValue;
                    info["levelName"]      = level.Name;
                    info["levelElevationMm"] = Math.Round(level.Elevation * FeetToMm, 2);
                }
            }
            else if (activeView is View3D view3d)
            {
                info["is3D"]     = true;
                info["isLocked"] = view3d.IsLocked;
            }
            else if (activeView is ViewSection viewSection)
            {
                info["isSection"] = true;
            }

            // Crop region
            if (activeView.CropBoxActive)
            {
                BoundingBoxXYZ crop = activeView.CropBox;
                info["cropBox"] = new JObject
                {
                    ["minX"] = Math.Round(crop.Min.X * FeetToMm, 2),
                    ["minY"] = Math.Round(crop.Min.Y * FeetToMm, 2),
                    ["minZ"] = Math.Round(crop.Min.Z * FeetToMm, 2),
                    ["maxX"] = Math.Round(crop.Max.X * FeetToMm, 2),
                    ["maxY"] = Math.Round(crop.Max.Y * FeetToMm, 2),
                    ["maxZ"] = Math.Round(crop.Max.Z * FeetToMm, 2),
                };
            }

            // Sheet info if the view is placed on a sheet
            var viewports = new FilteredElementCollector(doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .Where(vp => vp.ViewId == activeView.Id)
                .ToList();

            if (viewports.Count > 0)
            {
                var vp = viewports[0];
                var sheet = doc.GetElement(vp.SheetId) as ViewSheet;
                if (sheet != null)
                {
                    info["sheetId"]     = sheet.Id.IntegerValue;
                    info["sheetNumber"] = sheet.SheetNumber;
                    info["sheetName"]   = sheet.Name;
                }
            }

            return new JObject { ["status"] = "success", ["view"] = info };
        }

        // ─────────────────────────────────────────────────────────────────────
        // get_current_view_elements
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns all (or filtered) elements visible in the active view.
        ///
        /// params:
        ///   categoryName   (string)  – optional BuiltInCategory name filter
        ///   includeHidden  (bool)    – default false; include elements hidden in view
        ///   maxCount       (int)     – default 500; cap returned list
        /// </summary>
        public static JObject GetCurrentViewElements(Document doc, JObject @params)
        {
            if (doc == null)
                return Error("No active document");

            View activeView = doc.ActiveView;
            if (activeView == null)
                return Error("No active view");

            string categoryFilter = @params["categoryName"]?.Value<string>();
            bool includeHidden    = @params["includeHidden"]?.Value<bool>() ?? false;
            int maxCount          = @params["maxCount"]?.Value<int>() ?? 500;

            // Build collector scoped to the active view
            FilteredElementCollector collector = includeHidden
                ? new FilteredElementCollector(doc)
                : new FilteredElementCollector(doc, activeView.Id);

            collector = collector.WhereElementIsNotElementType();

            // Optional category filter — BuiltInCategory enum parse first, else string match post-collect
            bool useCategoryNameMatch = false;
            if (!string.IsNullOrWhiteSpace(categoryFilter))
            {
                if (Enum.TryParse(categoryFilter, out BuiltInCategory bic))
                {
                    collector = collector.OfCategory(bic);
                }
                else
                {
                    // Cannot use a quick filter; iterate all and match by display name
                    useCategoryNameMatch = true;
                }
            }

            var elements = new JArray();
            int count = 0;

            foreach (Element elem in collector)
            {
                if (count >= maxCount) break;
                if (elem == null) continue;

                // Category name string-match (fallback when BuiltInCategory parse fails)
                if (useCategoryNameMatch &&
                    elem.Category?.Name.Equals(categoryFilter, StringComparison.OrdinalIgnoreCase) != true)
                    continue;

                try
                {
                    var eObj = BuildElementSummary(doc, elem, activeView);
                    elements.Add(eObj);
                    count++;
                }
                catch { /* skip malformed elements */ }
            }

            return new JObject
            {
                ["status"]    = "success",
                ["viewId"]    = activeView.Id.IntegerValue,
                ["viewName"]  = activeView.Name,
                ["count"]     = count,
                ["elements"]  = elements
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static JObject BuildElementSummary(Document doc, Element elem, View view)
        {
            string categoryName = elem.Category?.Name ?? "Unknown";
            string familyName   = "";
            string typeName     = "";
            string levelName    = "";

            // Family / type info
            if (doc.GetElement(elem.GetTypeId()) is ElementType et)
            {
                typeName = et.Name;
                if (et is FamilySymbol fs)
                    familyName = fs.FamilyName;
            }

            // Level
            ElementId levelId = elem.LevelId;
            if (levelId != null && levelId != ElementId.InvalidElementId)
            {
                Level lv = doc.GetElement(levelId) as Level;
                levelName = lv?.Name ?? "";
            }

            var obj = new JObject
            {
                ["id"]           = elem.Id.IntegerValue,
                ["name"]         = elem.Name,
                ["category"]     = categoryName,
                ["familyName"]   = familyName,
                ["typeName"]     = typeName,
                ["levelName"]    = levelName,
            };

            // Bounding box in mm
            BoundingBoxXYZ bb = elem.get_BoundingBox(view);
            if (bb != null)
            {
                obj["bbox"] = new JObject
                {
                    ["minX"] = Math.Round(bb.Min.X * FeetToMm, 1),
                    ["minY"] = Math.Round(bb.Min.Y * FeetToMm, 1),
                    ["minZ"] = Math.Round(bb.Min.Z * FeetToMm, 1),
                    ["maxX"] = Math.Round(bb.Max.X * FeetToMm, 1),
                    ["maxY"] = Math.Round(bb.Max.Y * FeetToMm, 1),
                    ["maxZ"] = Math.Round(bb.Max.Z * FeetToMm, 1),
                };
            }

            // Location point (if available)
            if (elem.Location is LocationPoint lp)
            {
                obj["locationX"] = Math.Round(lp.Point.X * FeetToMm, 1);
                obj["locationY"] = Math.Round(lp.Point.Y * FeetToMm, 1);
                obj["locationZ"] = Math.Round(lp.Point.Z * FeetToMm, 1);
            }
            else if (elem.Location is LocationCurve lc)
            {
                XYZ mid = lc.Curve.Evaluate(0.5, true);
                obj["locationX"] = Math.Round(mid.X * FeetToMm, 1);
                obj["locationY"] = Math.Round(mid.Y * FeetToMm, 1);
                obj["locationZ"] = Math.Round(mid.Z * FeetToMm, 1);
            }

            return obj;
        }

        private static JObject Error(string msg) =>
            new JObject { ["status"] = "error", ["message"] = msg };
    }
}
