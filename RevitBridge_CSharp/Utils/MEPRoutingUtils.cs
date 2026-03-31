using System;
using Autodesk.Revit.DB;

namespace RevitBridge.Utils
{
    public static class MEPRoutingUtils
    {
        // Tự động tìm 2 đầu nối gần nhất giữa hai đoạn ống MEP (Pipe/Duct) và đặt Fitting (Elbow)
        public static void AutoConnectElbow(Document doc, MEPCurve curve1, MEPCurve curve2)
        {
            try
            {
                ConnectorManager cm1 = curve1.ConnectorManager;
                ConnectorManager cm2 = curve2.ConnectorManager;

                Connector close1 = null;
                Connector close2 = null;
                double minDist = double.MaxValue;

                foreach (Connector conn1 in cm1.Connectors)
                {
                    foreach (Connector conn2 in cm2.Connectors)
                    {
                        double d = conn1.Origin.DistanceTo(conn2.Origin);
                        if (d < minDist)
                        {
                            minDist = d;
                            close1 = conn1;
                            close2 = conn2;
                        }
                    }
                }

                // Nếu 2 đầu mút khoảng cách xấp xỉ 0 (chạm nhau) -> Thêm Cút (Elbow Fitting)
                if (close1 != null && close2 != null && minDist < 0.1) 
                {
                    doc.Create.NewElbowFitting(close1, close2);
                }
            }
            catch (Exception)
            {
                // Bỏ qua nếu có thư viện Fitting khuyết thiếu
            }
        }
    }
}
