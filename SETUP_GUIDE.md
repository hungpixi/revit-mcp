# 🛠️ SETUP GUIDE: Revit MCP Bridge (From Zero to Working)

Dự án này không thể "Chạy ngay lập tức" sau khi `git clone` vì nó phụ thuộc vào môi trường máy tính cục bộ của anh (Phiên bản Revit, Đường dẫn cài đặt). Dưới đây là 4 bước để làm nó hoạt động:

## 1. Môi trường bắt buộc (Prerequisites)
- **Autodesk Revit 2020** (Nếu dùng bản khác, hãy đổi Reference DLL trong file `.csproj`).
- **Visual Studio 2022** hoặc `.NET SDK 4.7.2` trở lên.
- **Node.js v18+**.
- **GitHub CLI** (Đã đăng nhập).

---

## 2. Các bước triển khai (Step-by-Step)

### Bước 1: Build Revit Bridge (C#)
1. Mở Folder `RevitBridge_CSharp`.
2. Build project để tạo ra file `RevitBridge.dll`.
   ```powershell
   dotnet build RevitBridge.csproj -c Release
   ```
3. Copy file `bin/Release/net472/RevitBridge.dll` vào đúng thư mục Add-ins của Revit:
   - `C:\ProgramData\Autodesk\Revit\Addins\2020\`

### Bước 2: Cài đặt Manifest (.addin)
1. Trong thư mục `RevitBridge_CSharp`, anh sẽ thấy file `AntigravityBridge.addin`.
2. Copy nó vào cùng thư mục với file `.dll` (`C:\ProgramData\Autodesk\Revit\Addins\2020\`).
3. **Lưu ý quan trọng**: Mở file `.addin` bằng Notepad và sửa đường dẫn `<Assembly>` trỏ đúng vào file `.dll` anh vừa copy.

### Bước 3: Build MCP Server (Node.js)
1. Mở Command Prompt trong thư mục `RevitMCP_Node`.
2. Chạy lệnh cài đặt:
   ```bash
   npm install
   npm run build
   ```

### Bước 4: Khởi động hệ thống
1. Mở Revit 2020. Nếu có hỏi "Unsigned Add-in", chọn **Always Load**.
2. Kiểm tra log hoặc cửa sổ Command: Nếu Revit hiện "Bridge: Online" là thành công.
3. Chạy lệnh `node dist/index.js` để bật MCP Server kết nối với AI (Claude/Cursor).

---

## 3. Các lỗi thường gặp (Troubleshooting)

- **Revit bị treo (Not Responding) khi khởi động**: Do xung đột giữa Popup `TaskDialog` và Event tắt dialog tự động. Em đã fix trong bản mới nhất của `App.cs`, hãy build lại DLL mới.
- **Port 5050 bị chiếm dụng**: Kiểm tra `netstat -ano | findstr :5050`. Kết thúc process đang chiếm port này.
- **Thiếu Reference**: Nếu build lỗi, vào `.csproj` sửa lại `<HintPath>` trỏ đúng vào thư mục cài Revit trên máy anh (Thường là `C:\Program Files\Autodesk\Revit 2020\RevitAPI.dll`).

---

> [!TIP]
> **Dùng tool tự động**: Anh có thể chạy `run_full_automation.ps1` ngoài thư mục gốc. Nó sẽ tự động tắt Revit cũ, bật Revit mới và bypass bảo mật cho anh.
