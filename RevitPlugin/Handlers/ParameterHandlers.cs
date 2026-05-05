using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace RevitMCP.Handlers
{
    // ─── GetElementParameters ─────────────────────────────────────────────────

    public class GetElementParametersHandler
    {
        private readonly Document _doc;
        public GetElementParametersHandler(Document doc) { _doc = doc; }

        public JObject Execute(JObject parameters)
        {
            var ids = (parameters["elementIds"] as JArray)?.Select(t => t.Value<int>()).ToList();
            if (ids == null || ids.Count == 0) return Error("No elementIds provided");

            var filterNames = (parameters["parameterNames"] as JArray)
                ?.Select(t => t.Value<string>().ToLower()).ToHashSet();
            bool includeType = parameters["includeTypeParameters"]?.Value<bool>() ?? true;

            var results = new JArray();

            foreach (int eid in ids)
            {
                var elem = _doc.GetElement(new ElementId(eid));
                if (elem == null) { results.Add(new JObject { ["elementId"] = eid, ["error"] = "not found" }); continue; }

                var paramList = new JArray();
                CollectParams(elem.Parameters, paramList, filterNames, "instance");

                if (includeType)
                {
                    var typeElem = _doc.GetElement(elem.GetTypeId());
                    if (typeElem != null)
                        CollectParams(typeElem.Parameters, paramList, filterNames, "type");
                }

                results.Add(new JObject
                {
                    ["elementId"] = eid,
                    ["category"]  = elem.Category?.Name ?? "Unknown",
                    ["name"]      = elem.Name,
                    ["parameters"] = paramList
                });
            }

            return new JObject { ["status"] = "success", ["elements"] = results };
        }

        private void CollectParams(ParameterSet ps, JArray result, HashSet<string> filter, string scope)
        {
            foreach (Parameter p in ps)
            {
                if (p.Definition == null) continue;
                if (filter != null && !filter.Contains(p.Definition.Name.ToLower())) continue;

                string valStr;
                try
                {
                    valStr = p.StorageType switch
                    {
                        StorageType.String    => p.AsString() ?? "",
                        StorageType.Double    => p.AsDouble().ToString("F4"),
                        StorageType.Integer   => p.AsInteger().ToString(),
                        StorageType.ElementId => p.AsElementId().IntegerValue.ToString(),
                        _                     => ""
                    };
                }
                catch { valStr = ""; }

                result.Add(new JObject
                {
                    ["name"]        = p.Definition.Name,
                    ["value"]       = valStr,
                    ["storageType"] = p.StorageType.ToString(),
                    ["isReadOnly"]  = p.IsReadOnly,
                    ["scope"]       = scope,
                    ["isShared"]    = p.IsShared,
                });
            }
        }

        private static JObject Error(string msg) => new JObject { ["status"] = "error", ["message"] = msg };
    }

    // ─── SetElementParameters ─────────────────────────────────────────────────

    public class SetElementParametersHandler
    {
        private readonly Document _doc;
        public SetElementParametersHandler(Document doc) { _doc = doc; }

        public JObject Execute(JObject parameters)
        {
            var updates = parameters["updates"] as JArray;
            if (updates == null || updates.Count == 0) return Error("No updates provided");

            var results = new JArray();

            using (var tx = new Transaction(_doc, "MCP: Set Element Parameters"))
            {
                tx.Start();

                foreach (JObject update in updates)
                {
                    int eid = update["elementId"]?.Value<int>() ?? -1;
                    var elem = _doc.GetElement(new ElementId(eid));
                    if (elem == null) { results.Add(new JObject { ["elementId"] = eid, ["status"] = "error", ["message"] = "not found" }); continue; }

                    var setResults = new JArray();
                    foreach (JObject pUpdate in update["parameters"] as JArray ?? new JArray())
                    {
                        string pName = pUpdate["name"]?.Value<string>();
                        if (pName == null) continue;

                        var param = elem.LookupParameter(pName);
                        if (param == null) { setResults.Add(new JObject { ["name"] = pName, ["status"] = "not found" }); continue; }
                        if (param.IsReadOnly) { setResults.Add(new JObject { ["name"] = pName, ["status"] = "read-only" }); continue; }

                        try
                        {
                            var val = pUpdate["value"];
                            switch (param.StorageType)
                            {
                                case StorageType.String:  param.Set(val?.Value<string>() ?? ""); break;
                                case StorageType.Double:  param.Set(val?.Value<double>() ?? 0); break;
                                case StorageType.Integer: param.Set(val?.Value<int>() ?? 0);    break;
                                case StorageType.ElementId:
                                    param.Set(new ElementId(val?.Value<int>() ?? -1)); break;
                            }
                            setResults.Add(new JObject { ["name"] = pName, ["status"] = "set" });
                        }
                        catch (Exception ex)
                        {
                            setResults.Add(new JObject { ["name"] = pName, ["status"] = "error", ["message"] = ex.Message });
                        }
                    }

                    results.Add(new JObject { ["elementId"] = eid, ["parametersSet"] = setResults });
                }

                tx.Commit();
            }

            return new JObject { ["status"] = "success", ["results"] = results };
        }

        private static JObject Error(string msg) => new JObject { ["status"] = "error", ["message"] = msg };
    }
}
