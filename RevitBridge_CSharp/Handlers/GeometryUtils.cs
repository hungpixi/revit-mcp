using System;
using Autodesk.Revit.DB;

namespace RevitBridge.Handlers
{
    public static class GeometryUtils
    {
        // Độ chia lưới mặc định (ví dụ 0.1 feet ~ 3cm) giúp tránh lỗi lẻ tọa độ
        public const double GRID_SNAP = 0.1;

        /// <summary>
        /// Làm tròn tọa độ về lưới chuẩn để Revit không báo lỗi Connector skewed.
        /// </summary>
        public static XYZ SnapToGrid(XYZ point)
        {
            return new XYZ(
                Math.Round(point.X / GRID_SNAP) * GRID_SNAP,
                Math.Round(point.Y / GRID_SNAP) * GRID_SNAP,
                Math.Round(point.Z / GRID_SNAP) * GRID_SNAP
            );
        }

        /// <summary>
        /// Ép vector về hướng 90 độ gần nhất (Hữu ích cho Tường/Máng cáp)
        /// </summary>
        public static XYZ OrthoSnap(XYZ start, XYZ end)
        {
            XYZ vec = end - start;
            double dx = Math.Abs(vec.X);
            double dy = Math.Abs(vec.Y);

            if (dx > dy)
                return new XYZ(start.X + vec.X, start.Y, start.Z);
            else
                return new XYZ(start.X, start.Y + vec.Y, start.Z);
        }
    }
}
