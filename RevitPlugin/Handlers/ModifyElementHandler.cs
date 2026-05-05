using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace RevitMCP.Handlers
{
    public class ModifyElementHandler
    {
        private readonly Document _doc;
        public ModifyElementHandler(Document doc) { _doc = doc; }

        public JObject Execute(JObject parameters)
        {
            var modifications = parameters["modifications"] as JArray;
            if (modifications == null) return Error("No modifications array");

            var results = new JArray();

            using (var tx = new Transaction(_doc, "MCP: Modify Elements"))
            {
                tx.Start();

                foreach (JObject mod in modifications)
                {
                    int eid = mod["elementId"]?.Value<int>() ?? -1;
                    var elem = _doc.GetElement(new ElementId(eid));
                    if (elem == null) { results.Add(ElementNotFound(eid)); continue; }

                    try
                    {
                        // Move
                        var move = mod["move"] as JObject;
                        if (move != null)
                        {
                            double dx = MmToFeet(move["x"]?.Value<double>() ?? 0);
                            double dy = MmToFeet(move["y"]?.Value<double>() ?? 0);
                            double dz = MmToFeet(move["z"]?.Value<double>() ?? 0);
                            ElementTransformUtils.MoveElement(_doc, elem.Id, new XYZ(dx, dy, dz));
                        }

                        // Rotate
                        double? rotateDeg = mod["rotateDegrees"]?.Value<double?>();
                        if (rotateDeg.HasValue)
                        {
                            var bbox = elem.get_BoundingBox(null);
                            if (bbox != null)
                            {
                                XYZ center = (bbox.Min + bbox.Max) / 2;
                                Line axis = Line.CreateBound(center, center + XYZ.BasisZ);
                                ElementTransformUtils.RotateElement(
                                    _doc, elem.Id, axis,
                                    rotateDeg.Value * Math.PI / 180.0
                                );
                            }
                        }

                        // Change type
                        int? typeId = mod["typeId"]?.Value<int?>();
                        if (typeId.HasValue)
                        {
                            var newType = _doc.GetElement(new ElementId(typeId.Value));
                            if (newType != null) elem.ChangeTypeId(newType.Id);
                        }

                        // Offset parameter
                        double? offset = mod["offset"]?.Value<double?>();
                        if (offset.HasValue)
                        {
                            var p = elem.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM)
                                 ?? elem.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
                            p?.Set(MmToFeet(offset.Value));
                        }

                        // Set parameters
                        var paramArray = mod["parameters"] as JArray;
                        if (paramArray != null)
                            SetParameters(elem, paramArray);

                        results.Add(new JObject { ["elementId"] = eid, ["status"] = "modified" });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new JObject { ["elementId"] = eid, ["status"] = "error", ["message"] = ex.Message });
                    }
                }

                tx.Commit();
            }

            return new JObject { ["status"] = "success", ["results"] = results };
        }

        private void SetParameters(Element elem, JArray paramArray)
        {
            foreach (JObject p in paramArray)
            {
                string pName = p["name"]?.Value<string>();
                if (pName == null) continue;

                var param = elem.LookupParameter(pName);
                if (param == null || param.IsReadOnly) continue;

                var val = p["value"];
                switch (param.StorageType)
                {
                    case StorageType.String:
                        param.Set(val?.Value<string>() ?? "");
                        break;
                    case StorageType.Double:
                        if (double.TryParse(val?.ToString(), out double d))
                            param.Set(d);
                        break;
                    case StorageType.Integer:
                        if (int.TryParse(val?.ToString(), out int i))
                            param.Set(i);
                        break;
                    case StorageType.ElementId:
                        if (int.TryParse(val?.ToString(), out int rid))
                            param.Set(new ElementId(rid));
                        break;
                }
            }
        }

        private static double MmToFeet(double mm) => mm / 304.8;
        private static JObject Error(string msg) => new JObject { ["status"] = "error", ["message"] = msg };
        private static JObject ElementNotFound(int id) => new JObject { ["elementId"] = id, ["status"] = "error", ["message"] = "Element not found" };
    }
}
