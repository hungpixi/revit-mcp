using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Newtonsoft.Json.Linq;

namespace RevitMCPPlugin.Commands
{
    /// <summary>
    /// Element-querying MCP commands.
    /// All linear dimensions returned in millimetres (feet × 304.8).
    /// All area dimensions returned in m² (sq-ft × 0.0929).
    /// </summary>
    public static class ElementCommands
    {
        private const double FeetToMm  = 304.8;
        private const double SqFtToM2  = 0.092903;

        // ─────────────────────────────────────────────────────────────────────
        // get_selected_elements
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns metadata for every element that is currently selected.
        ///
        /// params:
        ///   includeParameters (bool) – default false; attach parameter list
        /// </summary>
        public static JObject GetSelectedElements(UIApplication uiApp, JObject @params)
        {
            if (uiApp?.ActiveUIDocument == null)
                return Error("No active UI document");

            bool includeParams = @params["includeParameters"]?.Value<bool>() ?? false;
            Document doc = uiApp.ActiveUIDocument.Document;
            Selection sel = uiApp.ActiveUIDocument.Selection;
            ICollection<ElementId> ids = sel.GetElementIds();

            var array = new JArray();
            foreach (ElementId eid in ids)
            {
                Element elem = doc.GetElement(eid);
                if (elem == null) continue;

                JObject obj = BuildElementDetail(doc, elem, includeParams);
                array.Add(obj);
            }

            return new JObject
            {
                ["status"]  = "success",
                ["count"]   = ids.Count,
                ["elements"] = array
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // get_available_family_types
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns all FamilySymbol (family types) in the document.
        ///
        /// params:
        ///   categoryName (string) – optional BuiltInCategory filter
        ///   nameContains (string) – optional case-insensitive name substring filter
        ///   maxCount     (int)    – default 200
        /// </summary>
        public static JObject GetAvailableFamilyTypes(Document doc, JObject @params)
        {
            if (doc == null) return Error("No active document");

            string categoryFilter = @params["categoryName"]?.Value<string>();
            string nameFilter     = @params["nameContains"]?.Value<string>();
            int maxCount          = @params["maxCount"]?.Value<int>() ?? 200;

            var collector = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>();

            // Category filter
            if (!string.IsNullOrWhiteSpace(categoryFilter))
            {
                if (Enum.TryParse(categoryFilter, out BuiltInCategory bic))
                    collector = collector.Where(fs => fs.Category?.Id.IntegerValue == (int)bic);
                else
                    collector = collector.Where(fs =>
                        fs.Category?.Name.Equals(categoryFilter, StringComparison.OrdinalIgnoreCase) == true);
            }

            // Name filter
            if (!string.IsNullOrWhiteSpace(nameFilter))
            {
                collector = collector.Where(fs =>
                    (fs.FamilyName + " " + fs.Name)
                    .IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            var array = new JArray();
            int count = 0;

            foreach (FamilySymbol fs in collector)
            {
                if (count >= maxCount) break;
                array.Add(new JObject
                {
                    ["id"]           = fs.Id.IntegerValue,
                    ["familyName"]   = fs.FamilyName,
                    ["typeName"]     = fs.Name,
                    ["categoryName"] = fs.Category?.Name ?? "",
                    ["isActive"]     = fs.IsActive,
                    ["familyId"]     = fs.Family?.Id.IntegerValue ?? -1
                });
                count++;
            }

            return new JObject
            {
                ["status"] = "success",
                ["count"]  = count,
                ["types"]  = array
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // get_material_quantities
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Computes material quantities using element geometry solid volumes.
        ///
        /// params:
        ///   elementIds (array of int) – optional; if omitted, uses all model elements
        ///   categoryName (string)     – optional category filter
        /// </summary>
        public static JObject GetMaterialQuantities(Document doc, JObject @params)
        {
            if (doc == null) return Error("No active document");

            // Resolve element set
            var idArray = @params["elementIds"] as JArray;
            string categoryFilter = @params["categoryName"]?.Value<string>();

            IEnumerable<Element> elements;
            if (idArray != null && idArray.Count > 0)
            {
                elements = idArray
                    .Select(t => doc.GetElement(new ElementId(t.Value<int>())))
                    .Where(e => e != null);
            }
            else
            {
                var coll = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType();

                if (!string.IsNullOrWhiteSpace(categoryFilter) &&
                    Enum.TryParse(categoryFilter, out BuiltInCategory bic))
                {
                    coll = coll.OfCategory(bic);
                }

                elements = coll.ToElements();
            }

            var materialVolumes  = new Dictionary<string, double>(); // name → m³
            var materialAreas    = new Dictionary<string, double>(); // name → m²
            var materialElements = new Dictionary<string, int>();    // name → count

            var opts = new Options { DetailLevel = ViewDetailLevel.Fine };

            foreach (Element elem in elements)
            {
                try
                {
                    GeometryElement geom = elem.get_Geometry(opts);
                    if (geom == null) continue;

                    foreach (GeometryObject geoObj in geom)
                    {
                        ProcessGeometryObject(geoObj, doc, elem,
                            materialVolumes, materialAreas, materialElements);
                    }
                }
                catch { /* skip elements with inaccessible geometry */ }
            }

            var results = new JArray();
            foreach (var kvp in materialVolumes.OrderByDescending(x => x.Value))
            {
                results.Add(new JObject
                {
                    ["materialName"]  = kvp.Key,
                    ["volumeM3"]      = Math.Round(kvp.Value, 4),
                    ["areaM2"]        = Math.Round(materialAreas.GetValueOrDefault(kvp.Key, 0), 4),
                    ["elementCount"]  = materialElements.GetValueOrDefault(kvp.Key, 0)
                });
            }

            return new JObject
            {
                ["status"]    = "success",
                ["materials"] = results
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // ai_element_filter
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Rich filter combining category, bounding-box, visibility, and
        /// type/instance parameter criteria.
        ///
        /// params:
        ///   categoryName     (string)  – BuiltInCategory name
        ///   bbox             (object)  – { minX,minY,minZ,maxX,maxY,maxZ } in mm
        ///   paramFilters     (array)   – [{ name, value, operator }]
        ///   isInstanceOnly   (bool)    – default false
        ///   visibleInView    (bool)    – default false; restrict to active view
        ///   maxCount         (int)     – default 500
        /// </summary>
        public static JObject AiElementFilter(Document doc, JObject @params)
        {
            if (doc == null) return Error("No active document");

            string categoryFilter  = @params["categoryName"]?.Value<string>();
            bool visibleInView     = @params["visibleInView"]?.Value<bool>() ?? false;
            bool isInstanceOnly    = @params["isInstanceOnly"]?.Value<bool>() ?? false;
            int maxCount           = @params["maxCount"]?.Value<int>() ?? 500;
            var bboxParam          = @params["bbox"] as JObject;
            var paramFilters       = @params["paramFilters"] as JArray;

            // Base collector
            FilteredElementCollector collector;
            if (visibleInView && doc.ActiveView != null)
                collector = new FilteredElementCollector(doc, doc.ActiveView.Id);
            else
                collector = new FilteredElementCollector(doc);

            collector = collector.WhereElementIsNotElementType();

            // Category
            if (!string.IsNullOrWhiteSpace(categoryFilter) &&
                Enum.TryParse(categoryFilter, out BuiltInCategory bic))
            {
                collector = collector.OfCategory(bic);
            }

            // Bounding box filter
            if (bboxParam != null)
            {
                // Convert mm → feet.  Use ±1e9 ft (≈300 km) as "unbounded" defaults.
                const double BigFt = 1_000_000.0;
                double minX = bboxParam["minX"] != null ? bboxParam["minX"].Value<double>() / FeetToMm : -BigFt;
                double minY = bboxParam["minY"] != null ? bboxParam["minY"].Value<double>() / FeetToMm : -BigFt;
                double minZ = bboxParam["minZ"] != null ? bboxParam["minZ"].Value<double>() / FeetToMm : -BigFt;
                double maxX = bboxParam["maxX"] != null ? bboxParam["maxX"].Value<double>() / FeetToMm :  BigFt;
                double maxY = bboxParam["maxY"] != null ? bboxParam["maxY"].Value<double>() / FeetToMm :  BigFt;
                double maxZ = bboxParam["maxZ"] != null ? bboxParam["maxZ"].Value<double>() / FeetToMm :  BigFt;

                var outline = new Outline(new XYZ(minX, minY, minZ), new XYZ(maxX, maxY, maxZ));
                collector = collector.WherePasses(new BoundingBoxIntersectsFilter(outline));
            }

            var matched = new JArray();
            int count   = 0;

            foreach (Element elem in collector)
            {
                if (count >= maxCount) break;

                // Instance-only filter (skip type elements)
                if (isInstanceOnly && !(elem is FamilyInstance)) continue;

                // Parameter filters
                if (paramFilters != null && paramFilters.Count > 0)
                {
                    bool passAll = true;
                    foreach (JObject pf in paramFilters)
                    {
                        if (!MatchesParamFilter(elem, pf)) { passAll = false; break; }
                    }
                    if (!passAll) continue;
                }

                matched.Add(BuildElementDetail(doc, elem, false));
                count++;
            }

            return new JObject
            {
                ["status"]   = "success",
                ["count"]    = count,
                ["elements"] = matched
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // analyze_model_statistics
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns element counts grouped by category and, optionally, by level.
        ///
        /// params:
        ///   byLevel (bool) – default true
        /// </summary>
        public static JObject AnalyzeModelStatistics(Document doc, JObject @params)
        {
            if (doc == null) return Error("No active document");

            bool byLevel = @params["byLevel"]?.Value<bool>() ?? true;

            var categoryCounts = new Dictionary<string, int>();
            var levelCounts    = new Dictionary<string, Dictionary<string, int>>(); // levelName → catName → count

            var elements = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .ToElements();

            int totalCount = 0;

            foreach (Element elem in elements)
            {
                if (elem == null || elem.Category == null) continue;

                string catName = elem.Category.Name;
                if (!categoryCounts.ContainsKey(catName)) categoryCounts[catName] = 0;
                categoryCounts[catName]++;
                totalCount++;

                if (byLevel)
                {
                    string lvlName = "No Level";
                    if (elem.LevelId != null && elem.LevelId != ElementId.InvalidElementId)
                    {
                        Level lv = doc.GetElement(elem.LevelId) as Level;
                        if (lv != null) lvlName = lv.Name;
                    }

                    if (!levelCounts.ContainsKey(lvlName))
                        levelCounts[lvlName] = new Dictionary<string, int>();

                    if (!levelCounts[lvlName].ContainsKey(catName))
                        levelCounts[lvlName][catName] = 0;

                    levelCounts[lvlName][catName]++;
                }
            }

            // Top categories sorted descending
            var catArray = new JArray(
                categoryCounts
                    .OrderByDescending(x => x.Value)
                    .Select(x => new JObject { ["category"] = x.Key, ["count"] = x.Value })
            );

            var result = new JObject
            {
                ["status"]          = "success",
                ["totalElements"]   = totalCount,
                ["categoryBreakdown"] = catArray
            };

            if (byLevel)
            {
                var lvlArray = new JArray();
                foreach (var kvp in levelCounts.OrderBy(x => x.Key))
                {
                    int lvlTotal = kvp.Value.Values.Sum();
                    var catArr2 = new JArray(
                        kvp.Value
                           .OrderByDescending(x => x.Value)
                           .Select(x => new JObject { ["category"] = x.Key, ["count"] = x.Value })
                    );
                    lvlArray.Add(new JObject
                    {
                        ["levelName"] = kvp.Key,
                        ["total"]     = lvlTotal,
                        ["categories"] = catArr2
                    });
                }
                result["levelBreakdown"] = lvlArray;
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // export_room_data
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Collects all placed rooms with area, perimeter, volume, level.
        ///
        /// params:
        ///   levelName (string) – optional filter by level name
        ///   minArea   (double) – minimum area in m², default 0
        /// </summary>
        public static JObject ExportRoomData(Document doc, JObject @params)
        {
            if (doc == null) return Error("No active document");

            string levelFilter = @params["levelName"]?.Value<string>();
            double minAreaM2   = @params["minArea"]?.Value<double>() ?? 0;

            var rooms = new FilteredElementCollector(doc)
                .OfClass(typeof(SpatialElement))
                .OfType<Room>()
                .Where(r => r.Area > 0) // placed rooms only
                .ToList();

            var array = new JArray();

            foreach (Room room in rooms)
            {
                try
                {
                    // Level filter
                    if (!string.IsNullOrWhiteSpace(levelFilter))
                    {
                        Level lv = doc.GetElement(room.LevelId) as Level;
                        if (lv == null || !lv.Name.Equals(levelFilter, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }

                    double areaM2 = room.Area * SqFtToM2;
                    if (areaM2 < minAreaM2) continue;

                    double perimeterMm = room.Perimeter * FeetToMm;
                    double volumeM3    = room.Volume * SqFtToM2 * 0.3048; // ft³ → m³ approx

                    // Room center point
                    LocationPoint lp = room.Location as LocationPoint;
                    double cx = lp != null ? Math.Round(lp.Point.X * FeetToMm, 1) : 0;
                    double cy = lp != null ? Math.Round(lp.Point.Y * FeetToMm, 1) : 0;

                    // Level name
                    Level level = doc.GetElement(room.LevelId) as Level;

                    // Department / occupancy parameters (optional)
                    string dept = room.get_Parameter(BuiltInParameter.ROOM_DEPARTMENT)?.AsString() ?? "";
                    string number = room.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.AsString() ?? "";
                    string occupancy = room.get_Parameter(BuiltInParameter.ROOM_OCCUPANCY)?.AsString() ?? "";

                    array.Add(new JObject
                    {
                        ["id"]            = room.Id.IntegerValue,
                        ["name"]          = room.Name,
                        ["number"]        = number,
                        ["department"]    = dept,
                        ["occupancy"]     = occupancy,
                        ["levelName"]     = level?.Name ?? "",
                        ["levelElevationMm"] = Math.Round((level?.Elevation ?? 0) * FeetToMm, 2),
                        ["areaM2"]        = Math.Round(areaM2, 3),
                        ["perimeterMm"]   = Math.Round(perimeterMm, 1),
                        ["volumeM3"]      = Math.Round(volumeM3, 3),
                        ["centerX"]       = cx,
                        ["centerY"]       = cy
                    });
                }
                catch { /* skip inaccessible rooms */ }
            }

            return new JObject
            {
                ["status"] = "success",
                ["count"]  = array.Count,
                ["rooms"]  = array
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Internal helpers
        // ─────────────────────────────────────────────────────────────────────

        private static JObject BuildElementDetail(Document doc, Element elem, bool includeParams)
        {
            string catName   = elem.Category?.Name ?? "Unknown";
            string famName   = "";
            string typeName  = "";
            string levelName = "";

            if (doc.GetElement(elem.GetTypeId()) is ElementType et)
            {
                typeName = et.Name;
                if (et is FamilySymbol fs) famName = fs.FamilyName;
            }

            if (elem.LevelId != null && elem.LevelId != ElementId.InvalidElementId)
            {
                Level lv = doc.GetElement(elem.LevelId) as Level;
                levelName = lv?.Name ?? "";
            }

            var obj = new JObject
            {
                ["id"]           = elem.Id.IntegerValue,
                ["name"]         = elem.Name,
                ["category"]     = catName,
                ["familyName"]   = famName,
                ["typeName"]     = typeName,
                ["levelName"]    = levelName
            };

            BoundingBoxXYZ bb = elem.get_BoundingBox(null);
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

            if (elem.Location is LocationPoint lp)
            {
                obj["x"] = Math.Round(lp.Point.X * FeetToMm, 1);
                obj["y"] = Math.Round(lp.Point.Y * FeetToMm, 1);
                obj["z"] = Math.Round(lp.Point.Z * FeetToMm, 1);
            }

            if (includeParams)
            {
                var pArr = new JArray();
                foreach (Parameter p in elem.Parameters)
                {
                    try
                    {
                        if (p == null || p.Definition == null) continue;
                        pArr.Add(new JObject
                        {
                            ["name"]  = p.Definition.Name,
                            ["value"] = GetParamValueString(p),
                            ["type"]  = p.StorageType.ToString()
                        });
                    }
                    catch { }
                }
                obj["parameters"] = pArr;
            }

            return obj;
        }

        private static string GetParamValueString(Parameter p)
        {
            switch (p.StorageType)
            {
                case StorageType.String:  return p.AsString() ?? "";
                case StorageType.Double:  return p.AsDouble().ToString("G6");
                case StorageType.Integer: return p.AsInteger().ToString();
                case StorageType.ElementId:
                    return p.AsElementId()?.IntegerValue.ToString() ?? "-1";
                default: return p.AsValueString() ?? "";
            }
        }

        private static bool MatchesParamFilter(Element elem, JObject filter)
        {
            string pName  = filter["name"]?.Value<string>();
            string pValue = filter["value"]?.Value<string>();
            string op     = filter["operator"]?.Value<string>() ?? "equals";

            if (string.IsNullOrWhiteSpace(pName)) return true;

            Parameter param = elem.LookupParameter(pName);
            if (param == null) return false;

            string actual = GetParamValueString(param);

            return op.ToLower() switch
            {
                "contains"   => actual.IndexOf(pValue ?? "", StringComparison.OrdinalIgnoreCase) >= 0,
                "startswith" => actual.StartsWith(pValue ?? "", StringComparison.OrdinalIgnoreCase),
                "endswith"   => actual.EndsWith(pValue ?? "", StringComparison.OrdinalIgnoreCase),
                "gt"         => double.TryParse(actual, out double da) &&
                                double.TryParse(pValue,  out double db) && da > db,
                "lt"         => double.TryParse(actual, out double da2) &&
                                double.TryParse(pValue,  out double db2) && da2 < db2,
                _            => actual.Equals(pValue ?? "", StringComparison.OrdinalIgnoreCase)
            };
        }

        private static void ProcessGeometryObject(
            GeometryObject geoObj, Document doc, Element elem,
            Dictionary<string, double> volumes,
            Dictionary<string, double> areas,
            Dictionary<string, int>    counts)
        {
            if (geoObj is GeometryInstance gi)
            {
                foreach (GeometryObject subObj in gi.GetInstanceGeometry())
                    ProcessGeometryObject(subObj, doc, elem, volumes, areas, counts);
                return;
            }

            if (geoObj is Solid solid && solid.Volume > 0)
            {
                foreach (Face face in solid.Faces)
                {
                    ElementId matId = face.MaterialElementId;
                    string matName = "Unknown";
                    if (matId != null && matId != ElementId.InvalidElementId)
                    {
                        Material mat = doc.GetElement(matId) as Material;
                        matName = mat?.Name ?? "Unknown";
                    }

                    double faceAreaM2 = face.Area * SqFtToM2;

                    if (!areas.ContainsKey(matName))   areas[matName]  = 0;
                    if (!volumes.ContainsKey(matName))  volumes[matName] = 0;
                    if (!counts.ContainsKey(matName))   counts[matName]  = 0;

                    areas[matName]  += faceAreaM2;
                    counts[matName]++;
                }

                // Approximate volume split proportionally to face area distribution
                // (Revit doesn't expose per-material volume on a solid directly)
                double solidVolumeM3 = solid.Volume * SqFtToM2 * 0.3048;
                if (areas.Count > 0)
                {
                    double totalArea = areas.Values.Sum();
                    foreach (var key in areas.Keys.ToList())
                    {
                        volumes[key] += totalArea > 0
                            ? solidVolumeM3 * (areas[key] / totalArea)
                            : 0;
                    }
                }
            }
        }

        private static JObject Error(string msg) =>
            new JObject { ["status"] = "error", ["message"] = msg };
    }
}
