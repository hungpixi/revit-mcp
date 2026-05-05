using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace RevitMCP.Handlers
{
    // ─── GetCurrentViewElements ───────────────────────────────────────────────
    /// <summary>
    /// Gets all elements in the current/active view.
    /// Parameters:
    ///   - elementCategories (string[]): Filter by category (e.g., ["Walls", "Doors"])
    ///   - includeProperties (bool): Include element properties
    ///   - limit (int): Maximum elements to return
    /// </summary>
    public class GetCurrentViewElementsHandler
    {
        private readonly Document _doc;

        public GetCurrentViewElementsHandler(Document doc) { _doc = doc; }

        public JObject Execute(JObject parameters)
        {
            try
            {
                var categories = parameters["elementCategories"] as JArray;
                bool includeProps = parameters["includeProperties"]?.Value<bool>() ?? false;
                int limit = parameters["limit"]?.Value<int>() ?? 1000;

                // Get current view
                View currentView = _doc.ActiveView;
                if (currentView == null)
                    return Error("No active view found");

                var categoryNames = categories?.Cast<JToken>()
                    .Select(t => t.Value<string>())
                    .ToList() ?? new List<string>();

                var elements = new JArray();
                int count = 0;

                // Filter and collect elements
                var collector = new FilteredElementCollector(_doc, currentView.Id)
                    .WhereElementIsNotElementType();

                foreach (Element elem in collector)
                {
                    if (count >= limit) break;

                    // Filter by category if specified
                    if (categoryNames.Count > 0)
                    {
                        string catName = elem.Category?.Name ?? "";
                        if (!categoryNames.Contains(catName))
                            continue;
                    }

                    var elemData = new JObject
                    {
                        ["elementId"] = elem.Id.IntegerValue,
                        ["name"] = elem.Name,
                        ["category"] = elem.Category?.Name ?? "Unknown",
                        ["type"] = elem.GetType().Name
                    };

                    if (includeProps)
                    {
                        var props = new JObject();
                        foreach (Parameter param in elem.Parameters)
                        {
                            if (param.HasValue)
                            {
                                try
                                {
                                    props[param.Definition.Name] = param.AsValueString();
                                }
                                catch { }
                            }
                        }
                        elemData["properties"] = props;
                    }

                    elements.Add(elemData);
                    count++;
                }

                return Success(new JObject
                {
                    ["viewName"] = currentView.Name,
                    ["elementCount"] = count,
                    ["totalCount"] = collector.Count(),
                    ["categories"] = categoryNames.Count > 0 ? 
                        JArray.FromObject(categoryNames) : "All",
                    ["elements"] = elements,
                    ["limited"] = count >= limit
                });
            }
            catch (Exception ex)
            {
                return Error($"Error getting view elements: {ex.Message}");
            }
        }
    }

    // ─── GetSelectedElements ───────────────────────────────────────────────────
    /// <summary>
    /// Gets information about currently selected elements.
    /// Parameters:
    ///   - includeGeometry (bool): Include geometry information
    ///   - includeParameters (bool): Include all parameters
    /// </summary>
    public class GetSelectedElementsHandler
    {
        private readonly Document _doc;

        public GetSelectedElementsHandler(Document doc) { _doc = doc; }

        public JObject Execute(JObject parameters)
        {
            try
            {
                bool includeGeom = parameters["includeGeometry"]?.Value<bool>() ?? false;
                bool includeParams = parameters["includeParameters"]?.Value<bool>() ?? false;

                var selectedElements = new JArray();
                ICollection<ElementId> selected = _doc.Selection.GetElementIds();

                if (selected.Count == 0)
                    return Error("No elements selected");

                foreach (ElementId elemId in selected)
                {
                    Element elem = _doc.GetElement(elemId);
                    if (elem == null) continue;

                    var elemData = new JObject
                    {
                        ["elementId"] = elem.Id.IntegerValue,
                        ["name"] = elem.Name,
                        ["category"] = elem.Category?.Name ?? "Unknown",
                        ["type"] = elem.GetType().Name,
                        ["level"] = GetElementLevel(elem)
                    };

                    if (includeGeom)
                    {
                        try
                        {
                            GeometryElement geom = elem.get_Geometry(new Options());
                            elemData["geometry"] = new JObject
                            {
                                ["boundingBox"] = ExtractBoundingBox(geom),
                                ["hasGeometry"] = geom != null && geom.Count > 0
                            };
                        }
                        catch { }
                    }

                    if (includeParams)
                    {
                        var props = new JObject();
                        foreach (Parameter param in elem.Parameters)
                        {
                            if (param.HasValue)
                            {
                                try
                                {
                                    props[param.Definition.Name] = param.AsValueString();
                                }
                                catch { }
                            }
                        }
                        elemData["parameters"] = props;
                    }

                    selectedElements.Add(elemData);
                }

                return Success(new JObject
                {
                    ["selectionCount"] = selectedElements.Count,
                    ["selectedElements"] = selectedElements
                });
            }
            catch (Exception ex)
            {
                return Error($"Error getting selected elements: {ex.Message}");
            }
        }

        private string GetElementLevel(Element elem)
        {
            if (elem is FamilyInstance fi)
                return fi.LevelId != ElementId.InvalidElementId ? 
                    _doc.GetElement(fi.LevelId)?.Name ?? "" : "";

            if (elem is Wall wall)
                return wall.LevelId != ElementId.InvalidElementId ? 
                    _doc.GetElement(wall.LevelId)?.Name ?? "" : "";

            return "";
        }

        private JObject ExtractBoundingBox(GeometryElement geom)
        {
            if (geom == null) return null;

            BoundingBoxXYZ bbox = geom.GetBoundingBox();
            if (bbox == null) return null;

            return new JObject
            {
                ["min"] = new JObject
                {
                    ["x"] = Math.Round(bbox.Min.X * 304.8, 2),
                    ["y"] = Math.Round(bbox.Min.Y * 304.8, 2),
                    ["z"] = Math.Round(bbox.Min.Z * 304.8, 2)
                },
                ["max"] = new JObject
                {
                    ["x"] = Math.Round(bbox.Max.X * 304.8, 2),
                    ["y"] = Math.Round(bbox.Max.Y * 304.8, 2),
                    ["z"] = Math.Round(bbox.Max.Z * 304.8, 2)
                }
            };
        }
    }

    // ─── GetElementProperties ──────────────────────────────────────────────────
    /// <summary>
    /// Gets detailed properties of a specific element.
    /// Parameters:
    ///   - elementId (int): ID of element to query
    ///   - includeSharedParameters (bool): Include shared parameters
    ///   - propertyNames (string[]): Specific properties to retrieve
    /// </summary>
    public class GetElementPropertiesHandler
    {
        private readonly Document _doc;

        public GetElementPropertiesHandler(Document doc) { _doc = doc; }

        public JObject Execute(JObject parameters)
        {
            try
            {
                int elemId = parameters["elementId"]?.Value<int>() ?? -1;
                bool includeShared = parameters["includeSharedParameters"]?.Value<bool>() ?? false;
                var propNames = parameters["propertyNames"] as JArray;

                if (elemId <= 0)
                    return Error("elementId is required");

                Element elem = _doc.GetElement(new ElementId(elemId));
                if (elem == null)
                    return Error($"Element with ID {elemId} not found");

                var properties = new JObject();
                var specifiedProps = propNames?.Cast<JToken>()
                    .Select(t => t.Value<string>())
                    .ToList() ?? new List<string>();

                foreach (Parameter param in elem.Parameters)
                {
                    // Filter by specified property names if provided
                    if (specifiedProps.Count > 0 && !specifiedProps.Contains(param.Definition.Name))
                        continue;

                    // Filter shared parameters if not requested
                    if (param.IsShared && !includeShared)
                        continue;

                    if (param.HasValue)
                    {
                        try
                        {
                            properties[param.Definition.Name] = new JObject
                            {
                                ["value"] = param.AsValueString(),
                                ["type"] = param.StorageType.ToString(),
                                ["isShared"] = param.IsShared,
                                ["isReadOnly"] = param.IsReadOnly
                            };
                        }
                        catch { }
                    }
                }

                return Success(new JObject
                {
                    ["elementId"] = elemId,
                    ["elementName"] = elem.Name,
                    ["elementType"] = elem.GetType().Name,
                    ["category"] = elem.Category?.Name ?? "Unknown",
                    ["propertyCount"] = properties.Count,
                    ["properties"] = properties
                });
            }
            catch (Exception ex)
            {
                return Error($"Error getting element properties: {ex.Message}");
            }
        }
    }

    // ─── AnalyzeModelStatistics ───────────────────────────────────────────────
    /// <summary>
    /// Analyzes comprehensive BIM model statistics.
    /// Parameters:
    ///   - includeByCategory (bool): Break down by category
    ///   - includeByLevel (bool): Break down by level
    ///   - includeVolumes (bool): Calculate volumes
    /// </summary>
    public class AnalyzeModelStatisticsHandler
    {
        private readonly Document _doc;

        public AnalyzeModelStatisticsHandler(Document doc) { _doc = doc; }

        public JObject Execute(JObject parameters)
        {
            try
            {
                bool byCategory = parameters["includeByCategory"]?.Value<bool>() ?? true;
                bool byLevel = parameters["includeByLevel"]?.Value<bool>() ?? true;
                bool calcVolumes = parameters["includeVolumes"]?.Value<bool>() ?? false;

                var collector = new FilteredElementCollector(_doc)
                    .WhereElementIsNotElementType();

                int totalElements = collector.Count();
                var categoryStats = new JObject();
                var levelStats = new JObject();
                double totalVolume = 0;

                // Category-based analysis
                if (byCategory)
                {
                    var elemsByCategory = collector
                        .GroupBy(e => e.Category?.Name ?? "Uncategorized")
                        .OrderByDescending(g => g.Count());

                    foreach (var group in elemsByCategory)
                    {
                        categoryStats[group.Key] = new JObject
                        {
                            ["count"] = group.Count(),
                            ["percentage"] = Math.Round((group.Count() * 100.0) / totalElements, 2)
                        };
                    }
                }

                // Level-based analysis
                if (byLevel)
                {
                    var levels = new FilteredElementCollector(_doc)
                        .OfClass(typeof(Level))
                        .Cast<Level>()
                        .OrderBy(l => l.Elevation);

                    foreach (Level level in levels)
                    {
                        var elementsOnLevel = collector
                            .Where(e => GetElementLevelId(e) == level.Id)
                            .Count();

                        levelStats[level.Name] = new JObject
                        {
                            ["elevation"] = Math.Round(level.Elevation * 304.8, 2),
                            ["elementCount"] = elementsOnLevel
                        };
                    }
                }

                // Volume calculation
                if (calcVolumes)
                {
                    var solids = collector
                        .Where(e => e is FamilyInstance || e is Wall || e is Floor)
                        .Take(100);

                    foreach (Element elem in solids)
                    {
                        try
                        {
                            GeometryElement geom = elem.get_Geometry(new Options());
                            totalVolume += CalculateVolume(geom);
                        }
                        catch { }
                    }
                }

                return Success(new JObject
                {
                    ["totalElements"] = totalElements,
                    ["totalVolume"] = Math.Round(totalVolume, 2),
                    ["statistics"] = new JObject
                    {
                        ["byCategory"] = byCategory ? categoryStats : null,
                        ["byLevel"] = byLevel ? levelStats : null
                    },
                    ["timestamp"] = DateTime.Now.ToString("o")
                });
            }
            catch (Exception ex)
            {
                return Error($"Error analyzing model: {ex.Message}");
            }
        }

        private ElementId GetElementLevelId(Element elem)
        {
            if (elem is FamilyInstance fi)
                return fi.LevelId;
            if (elem is Wall wall)
                return wall.LevelId;
            if (elem is Floor floor)
                return floor.LevelId;
            return ElementId.InvalidElementId;
        }

        private double CalculateVolume(GeometryElement geom)
        {
            double volume = 0;
            if (geom == null) return volume;

            foreach (GeometryObject obj in geom)
            {
                if (obj is Solid solid)
                {
                    volume += solid.Volume;
                }
            }

            return volume;
        }
    }

    // ─── Utility Methods ──────────────────────────────────────────────────────

    private static JObject Success(JObject data)
    {
        data["success"] = true;
        return data;
    }

    private static JObject Error(string message)
    {
        return new JObject
        {
            ["success"] = false,
            ["error"] = message
        };
    }
}
