using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace RevitMCP.Handlers
{
    // ─── SearchModules ────────────────────────────────────────────────────────

    public class SearchModulesHandler
    {
        private readonly Document _doc;
        public SearchModulesHandler(Document doc) { _doc = doc; }

        public JObject Execute(JObject parameters)
        {
            var query      = parameters["query"] as JObject;
            int limit      = parameters["limit"]?.Value<int>() ?? 200;
            bool details   = parameters["includeDetails"]?.Value<bool>() ?? false;

            var collector  = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType();

            // Category filter
            var categories = (query?["categories"] as JArray)?.Select(c => c.Value<string>()).ToList();
            if (categories != null && categories.Count > 0)
            {
                var catIds = categories
                    .Select(c => GetCategoryByName(c))
                    .Where(c => c != null)
                    .Select(c => c.Id)
                    .ToList();

                if (catIds.Count > 0)
                {
                    var catFilter = new ElementMulticategoryFilter(catIds);
                    collector = collector.WherePasses(catFilter);
                }
            }

            // Bounding box filter
            var bb = query?["boundingBox"] as JObject;
            if (bb != null)
            {
                double minX = MmToFeet(bb["minX"]?.Value<double>() ?? -1e9);
                double minY = MmToFeet(bb["minY"]?.Value<double>() ?? -1e9);
                double minZ = MmToFeet(bb["minZ"]?.Value<double>() ?? -1e9);
                double maxX = MmToFeet(bb["maxX"]?.Value<double>() ?? 1e9);
                double maxY = MmToFeet(bb["maxY"]?.Value<double>() ?? 1e9);
                double maxZ = MmToFeet(bb["maxZ"]?.Value<double>() ?? 1e9);

                var outline = new Outline(new XYZ(minX, minY, minZ), new XYZ(maxX, maxY, maxZ));
                collector = collector.WherePasses(new BoundingBoxIntersectsFilter(outline));
            }

            var elements = collector.ToElements();

            // In-memory filters (family name, type name, level, param filters, text)
            string familyFilter = query?["familyName"]?.Value<string>()?.ToLower();
            string typeFilter   = query?["typeName"]?.Value<string>()?.ToLower();
            string levelFilter  = query?["levelName"]?.Value<string>()?.ToLower();
            string textSearch   = query?["textSearch"]?.Value<string>()?.ToLower();
            var paramFilters    = (query?["parameterFilters"] as JArray)?.ToObject<List<JObject>>();

            var results = new JArray();
            int count = 0;

            foreach (var elem in elements)
            {
                if (count >= limit) break;

                // Family name filter
                if (familyFilter != null)
                {
                    var famInst = elem as FamilyInstance;
                    string famName = famInst?.Symbol?.FamilyName ?? "";
                    if (!famName.ToLower().Contains(familyFilter)) continue;
                }

                // Type name filter
                if (typeFilter != null)
                {
                    if (!elem.Name.ToLower().Contains(typeFilter)) continue;
                }

                // Level filter
                if (levelFilter != null)
                {
                    var levelParam = elem.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)
                                  ?? elem.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                    if (levelParam == null) continue;
                    var level = _doc.GetElement(levelParam.AsElementId()) as Level;
                    if (level == null || !level.Name.ToLower().Contains(levelFilter)) continue;
                }

                // Parameter value filters
                if (paramFilters != null)
                {
                    bool match = true;
                    foreach (var pf in paramFilters)
                    {
                        string pName = pf["name"]?.Value<string>();
                        string op    = pf["operator"]?.Value<string>();
                        string pVal  = pf["value"]?.Value<string>() ?? "";
                        var param    = elem.LookupParameter(pName);
                        if (param == null) { match = false; break; }
                        string actual = param.AsValueString() ?? param.AsString() ?? "";
                        match = op switch
                        {
                            "equals"     => actual.Equals(pVal, StringComparison.OrdinalIgnoreCase),
                            "not_equals" => !actual.Equals(pVal, StringComparison.OrdinalIgnoreCase),
                            "contains"   => actual.ToLower().Contains(pVal.ToLower()),
                            "greater"    => double.TryParse(actual, out double av) && double.TryParse(pVal, out double pv) && av > pv,
                            "less"       => double.TryParse(actual, out double av2) && double.TryParse(pVal, out double pv2) && av2 < pv2,
                            _            => true
                        };
                        if (!match) break;
                    }
                    if (!match) continue;
                }

                // Text search
                if (textSearch != null)
                {
                    bool found = elem.Name.ToLower().Contains(textSearch);
                    if (!found)
                    {
                        foreach (Parameter p in elem.Parameters)
                        {
                            string v = p.AsValueString() ?? p.AsString() ?? "";
                            if (v.ToLower().Contains(textSearch)) { found = true; break; }
                        }
                    }
                    if (!found) continue;
                }

                var entry = new JObject
                {
                    ["id"]       = elem.Id.IntegerValue,
                    ["name"]     = elem.Name,
                    ["category"] = elem.Category?.Name ?? "Unknown",
                };

                var famInst2 = elem as FamilyInstance;
                if (famInst2 != null)
                    entry["family"] = famInst2.Symbol?.FamilyName;

                var levelP = elem.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)
                          ?? elem.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                if (levelP != null)
                    entry["level"] = (_doc.GetElement(levelP.AsElementId()) as Level)?.Name;

                if (details)
                {
                    var paramSummary = new JObject();
                    foreach (Parameter p in elem.Parameters)
                    {
                        if (p.Definition == null || p.StorageType == StorageType.None) continue;
                        string v = p.StorageType == StorageType.String ? p.AsString()
                                 : p.AsValueString() ?? p.AsDouble().ToString("F2");
                        if (!string.IsNullOrEmpty(v))
                            paramSummary[p.Definition.Name] = v;
                    }
                    entry["parameters"] = paramSummary;
                }

                results.Add(entry);
                count++;
            }

            return new JObject
            {
                ["status"] = "success",
                ["count"]  = count,
                ["elements"] = results
            };
        }

        private Category GetCategoryByName(string name)
        {
            foreach (Category cat in _doc.Settings.Categories)
                if (cat.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return cat;
            return null;
        }

        private static double MmToFeet(double mm) => mm / 304.8;
    }

    // ─── GetProjectInfo ───────────────────────────────────────────────────────

    public class GetProjectInfoHandler
    {
        private readonly Document _doc;
        public GetProjectInfoHandler(Document doc) { _doc = doc; }

        public JObject Execute(JObject parameters)
        {
            string action = parameters["action"]?.Value<string>() ?? "get";
            var info = _doc.ProjectInformation;

            if (action == "set")
            {
                var updates = parameters["updates"] as JArray;
                if (updates != null)
                {
                    using var tx = new Transaction(_doc, "MCP: Set Project Info");
                    tx.Start();
                    foreach (JObject u in updates)
                    {
                        string name  = u["name"]?.Value<string>();
                        string value = u["value"]?.Value<string>() ?? "";
                        var p = info.LookupParameter(name);
                        if (p != null && !p.IsReadOnly && p.StorageType == StorageType.String)
                            p.Set(value);
                    }
                    tx.Commit();
                }
            }

            // Count warnings
            var warnings = _doc.GetWarnings();
            var elemCount = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType().GetElementCount();

            var paramInfo = new JObject();
            foreach (Parameter p in info.Parameters)
            {
                if (p.Definition != null && p.StorageType == StorageType.String)
                    paramInfo[p.Definition.Name] = p.AsString() ?? "";
            }

            return new JObject
            {
                ["status"]       = "success",
                ["projectName"]  = info.Name,
                ["projectNumber"]= info.Number,
                ["address"]      = info.Address,
                ["clientName"]   = info.ClientName,
                ["buildingName"] = info.BuildingName,
                ["author"]       = info.Author,
                ["modelHealth"] = new JObject
                {
                    ["totalElements"] = elemCount,
                    ["warningCount"]  = warnings.Count,
                    ["filePath"]      = _doc.PathName,
                },
                ["allParameters"] = paramInfo
            };
        }
    }
}
