using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using RevitBridge.Utils;

namespace RevitBridge.Handlers
{
    public static class PlumbingHandler
    {
        public static string Handle(UIApplication uiapp, string action, Dictionary<string, object> payload)
        {
            UIDocument uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) return "{\"status\": \"Error\", \"msg\": \"No active document in Revit\"}";
            Document doc = uidoc.Document;

            string statusMsg = "OK";

            using (Transaction t = new Transaction(doc, "Antigravity AI: " + action))
            {
                t.Start();

                try
                {
                    if (action == "create_pipe")
                    {
                        var pts = new System.Collections.ArrayList((System.Collections.ICollection)payload["points"]);
                        if (pts == null || pts.Count < 2) throw new Exception("Mảng đường đi 'points' mảng phải có n>1 đỉnh.");

                        List<XYZ> xyzs = new List<XYZ>();
                        foreach (object ptObj in pts)
                        {
                            var ptList = new System.Collections.ArrayList((System.Collections.ICollection)ptObj);
                            xyzs.Add(new XYZ(Convert.ToDouble(ptList[0]), Convert.ToDouble(ptList[1]), Convert.ToDouble(ptList[2])));
                        }

                        // Ưu tiên Hệ thống nước lạnh (DomesticColdWater) hoặc Nước thải (Sanitary)
                        PipingSystemType systemType = new FilteredElementCollector(doc)
                            .OfClass(typeof(PipingSystemType))
                            .Cast<PipingSystemType>()
                            .FirstOrDefault(s => s.SystemClassification == MEPSystemClassification.Sanitary 
                                              || s.SystemClassification == MEPSystemClassification.DomesticColdWater) 
                            ?? new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType)).FirstElement() as PipingSystemType;

                        // Tìm PipeType hiện hành
                        PipeType pipeType = new FilteredElementCollector(doc)
                            .OfClass(typeof(PipeType))
                            .FirstElement() as PipeType;

                        Level firstLevel = new FilteredElementCollector(doc).OfClass(typeof(Level)).FirstElement() as Level;

                        if (pipeType == null || systemType == null || firstLevel == null)
                        {
                            throw new Exception("Lỗi Data System - Tệp Revit này thiếu SystemTemplate cho Piping.");
                        }

                        List<Pipe> createdPipes = new List<Pipe>();
                        double diameter = payload.ContainsKey("diameter") ? (Convert.ToDouble(payload["diameter"]) / 304.8) : (50.0 / 304.8); 

                        for (int i = 0; i < xyzs.Count - 1; i++)
                        {
                            Pipe newPipe = Pipe.Create(doc, systemType.Id, pipeType.Id, firstLevel.Id, xyzs[i], xyzs[i+1]);
                            
                            Parameter dParam = newPipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                            if (dParam != null && !dParam.IsReadOnly) dParam.Set(diameter);

                            createdPipes.Add(newPipe);
                        }

                        // Tính năng Routing tự nối Fititing
                        for (int i = 0; i < createdPipes.Count - 1; i++)
                        {
                            MEPRoutingUtils.AutoConnectElbow(doc, createdPipes[i], createdPipes[i+1]);
                        }
                        
                        statusMsg = "Rải thành công " + createdPipes.Count + " đoạn ống nước (Hệ: " + systemType.Name + ").";
                    }
                    else
                    {
                        t.RollBack();
                        return "{\"status\": \"Error\", \"msg\": \"PlumbingHandler chưa xử lý action: " + action + "\"}";
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
