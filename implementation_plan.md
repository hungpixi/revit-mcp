# Chiến Lược Triển Khai: Tự Động Hóa 100% Khởi Tạo Dự Án MEP (Hệ Thống Điện NML)

Mục tiêu: Đưa MCP từ một chatbot gọi lệnh trở thành một AI "BIM Modeler Điện". Dựa trên dữ liệu cung cấp `1.BaiTapMau_Dien.pdf` (Dự án Nhà máy NML - 4 Tầng - bao gồm Trunking, Tủ Điện, Đèn chiếu sáng, Đèn Khẩn cấp, Công tắc), hệ thống MCP sẽ tiến hành số hóa tài liệu này và phát sinh mô hình Revit tương ứng bằng các lệnh API quy mô lớn.

## User Review Required

> [!CAUTION]
> Tự động dựng toàn bộ mô hình 4 tầng từ PDF là một bài toán rất phức tạp (cỡ Đồ án Tốt nghiệp / Commercial Plugin). Chúng ta không thể kỳ vọng AI nhìn phát vẽ được ngay mà phải chia thành 4 phân đoạn (Phase) xây dựng Engine thật chi tiết.
> Anh hãy review 4 Phân đoạn dưới đây xem đã phù hợp và "cover" đủ scope dự án mẫu NML chưa nhé. Nếu đồng ý, ta sẽ chạy Phase 1 trước.

## Proposed Architecture: Automated MEP Generation

Kế hoạch sẽ được tách thành các "vùng" tính năng.

### Phase 1: Mở Rộng API C# (Trunking & Schedule/View)
Hiện tại `BIMCommandHandler.cs` mới vẽ được Duct (Ống gió) và Wall (Tường). Để dựng hệ thống điện y như PDF, ta cần bổ sung các hàm API C# nội tại vào thư mục `Handlers/`:
1. **[NEW] `ElectricalHandler.cs`**:
   - `create_cable_tray`: Định tuyến Máng cáp/Trunking dựa trên chuỗi Vector3. Tự động sinh ra `CableTrayType` (System Family) với size yêu cầu (VD: 200x100) mà không cần user tốn công Duplicate nghiệm.
   - `place_electrical_fixture`: Đặt Đèn (Lighting), Tủ điện (Panel), Công tắc. Tích hợp tính năng **Auto-Fallback Family**:
     + Bước 1: Quét và duplicate một Type có sẵn nếu trùng Category.
     + Bước 2: Nếu Project trống trơn, tự động dò tìm và Load Component từ mảng thư mục mặc định của Autodesk (`C:\ProgramData\Autodesk\RVT 2020\Libraries`).
     + Bước 3: Nếu máy tính không có cả Library, hệ thống nội suy sử dụng API `DirectShape` để nặn ra một "hình hộp vật lý" đúng chuẩn kích thước có mang Category Cơ Điện (Electrical/Lighting) để anh vẫn có mô hình và bảng thống kê hợp lệ.
2. **[NEW] `ScheduleHandler.cs`**:
   - `create_schedule_view`: Tự động tạo bảng Thống kê khối lượng (như Thống Kê Đèn ở trang cuối của PDF) và thả vào bản vẽ.

### Phase 2: PDF Parsing -> Trí Tuệ Không Gian (JSON Topology)
Em với tư cách là Antigravity sẽ dùng thị giác máy tính đọc từng trang của PDF, để chuyển hóa vị trí "Đèn", "Tủ Điền", "Đường đi Trunking" thành dữ liệu thuật toán JSON.
- Ví dụ mô hình Tầng 1:
```json
{
  "level": "Tầng 1",
  "panels": [
    {"name": "DB-WH", "location": [0, 84000]}
  ],
  "trunking": {
    "main_route": [[0, 84000, 2800], [32000, 84000, 2800]]
  },
  "lighting_grid": {
    "type": "Den_HQ_AmTran_T12 2x36W", 
    "count": 194, 
    "bounds": [[0,0], [32000, 84000]]
  }
}
```

### Phase 3: MCP Pipeline Builder (Node.js)
Dựa vào tệp JSON trên, AI sẽ gọi cái tool `execute_pipeline` (mà anh em mình vừa code hôm nay).
Nhưng thay vì gọi 3 step, AI sẽ đẩy một Array khoảng **200 steps** vào Node.js.
- Step 1->50: Vẽ 50 đoạn Trunking cho toàn nhà máy.
- Step 51->150: Đặt các lưới (Grid) hệ thống Đèn Chiếu Sáng, Đèn Khẩn Cấp.
- Step 151->190: Rải các thiết bị Công Tắc và Tủ điện tại vị trí các vách.
- Step 191->200: Gọi lệnh tạo Bảng thống kê khối lượng của toàn bộ thiết bị vừa rải ra.

Node.js MCP sẽ liên tục bắn lệnh qua port 5050 vào Revit trong khoãng 2-3 phút, và anh sẽ ngồi nhìn màn hình Revit tự mọc lên Hệ thống Máng Cáp và Đèn chi chít hệt như trang PDF.

## Open Questions

> [!TIP]
> **Đã Cập Nhật Theo Yêu Cầu:** MCP đã được nâng cấp chiến lược thành "Absolute Automation". Anh không cần phải chuẩn bị Project Template hay gánh vác việc Load Family. Hệ thống sẽ tự động quét Library gốc của máy tính hoặc nội suy hình khối (API DirectShape) nếu máy tính anh thiếu thư viện. Tất cả vì mục tiêu: Chỉ cần anh ra lệnh, AI làm mọi thứ.

**Câu hỏi duy nhất cho anh:**
Về cao độ (Z-axis): Bản đồ PDF (Mặt bằng) chỉ cho tọa độ XY. Anh có muốn thiết lập mặc định Z cho Máng cáp là +2800mm (so với sàn) và Đèn là gắn sát trần không? Nếu anh Ok, em sẽ chốt thông số độ cao này vào code C#.

## Verification Plan

- Viết file `ElectricalHandler.cs` -> Bổ sung vào Project.
- Gọi một Macro test khoảng 10 bóng đèn và 1 đoạn máng cáp để Revit chạy thử trên 1 project trống.
