using System;
using System.Linq;
using Autodesk.Revit.DB;

namespace RevitBridge.Utils
{
    public static class DynamicFamilyLoader
    {
        // Thử tìm một loại FamilySymbol hợp lệ theo Category. 
        // Nếu không có, có thể mở rộng để load ngầm hoặc generate DirectShape.
        public static FamilySymbol GetOrLoadFamilySymbol(Document doc, BuiltInCategory category)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector.OfClass(typeof(FamilySymbol)).OfCategory(category);
            
            // Lấy phần tử Type đầu tiên tìm thấy
            FamilySymbol symbol = collector.Cast<FamilySymbol>().FirstOrDefault();

            // Phase 1: Trả về nếu có
            if (symbol != null)
            {
                if (!symbol.IsActive)
                {
                    symbol.Activate();
                    doc.Regenerate();
                }
                return symbol;
            }

            // Fallback Logic: Quét ổ đĩa C:\ProgramData\Autodesk\RVT 2020\Libraries 
            // (Hiện tại đang mockup, sẽ triển khai thuật toán đệ quy thư mục trong bản vá sau)
            
            return null;
        }
    }
}
