using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;

namespace RevitBridge.Handlers
{
    public static class VDCHandler
    {
        public static string HandleExtract(UIApplication uiapp, Dictionary<string, object> payload)
        {
            UIDocument uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) return "{\"error\":\"No active document\"}";
            Document doc = uidoc.Document;

            // Đọc Token optimization limit (mặc định 50 để tránh tràn Context Window của LLM)
            int limit = payload.ContainsKey("limit") ? Convert.ToInt32(payload["limit"]) : 50;
            string categoryName = payload.ContainsKey("categoryName") ? payload["categoryName"].ToString() : "";

            var collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .WhereElementIsViewIndependent();

            List<object> resultList = new List<object>();
            int count = 0;

            foreach (Element e in collector)
            {
                if (e.Category != null && (string.IsNullOrEmpty(categoryName) || e.Category.Name == categoryName))
                {
                    // Trích xuất Volume Parameter cho 5D Costing (Nếu có)
                    double volume = 0.0;
                    Parameter volParam = e.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
                    if (volParam != null && volParam.HasValue)
                    {
                        volume = volParam.AsDouble() * 0.0283168; // Convert sq feet to CBM
                    }

                    // Tối ưu Lean JSON: Chỉ lấy id, tên, và tham số bóc tách
                    resultList.Add(new {
                        id = e.Id.IntegerValue,
                        name = e.Name,
                        volumeCBM = Math.Round(volume, 2)
                    });

                    count++;
                    if (count >= limit) break; // Token Optimization Hard Limit!
                }
            }

            var resultDict = new Dictionary<string, object> {
                {"category", categoryName},
                {"count", count},
                {"data", resultList}
            };
            
            return JsonConvert.SerializeObject(resultDict);
        }

        public static string HandleUpdate(UIApplication uiapp, Dictionary<string, object> payload)
        {
            UIDocument uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) return "{\"error\":\"No active document\"}";
            Document doc = uidoc.Document;

            int elementId = Convert.ToInt32(payload["elementId"]);
            string paramName = payload["parameterName"].ToString();
            string value = payload["value"].ToString();

            Element e = doc.GetElement(new ElementId(elementId));
            if (e == null) return "{\"error\":\"Element not found\"}";

            using (Transaction t = new Transaction(doc, "Antigravity AI: Update 4D/5D Data"))
            {
                t.Start();
                Parameter p = e.LookupParameter(paramName);
                if (p == null) 
                {
                    // Thử tìm trong BuiltIn nếu ko có Custom
                    if (paramName.ToLower() == "comments") p = e.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                    else if (paramName.ToLower() == "mark") p = e.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                }

                if (p != null && !p.IsReadOnly)
                {
                    p.Set(value);
                    t.Commit();
                    return "{\"status\":\"Success\", \"elementId\":" + elementId + ", \"parameter\":\"" + paramName + "\", \"value\":\"" + value + "\"}";
                }
                else
                {
                    t.RollBack();
                    return "{\"error\":\"Parameter not found or read-only\"}";
                }
            }
        }
    }
}
