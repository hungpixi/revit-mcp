$ErrorActionPreference = "SilentlyContinue"

Write-Output "🛑 Dọn dẹp Revit cũ để khởi động lại sạch sẽ..."
Stop-Process -Name Revit -Force
Start-Sleep -Seconds 3

Write-Output "🛡️ Bật Guard tự động click Always Load..."
Start-Job -FilePath "d:\CODE\revit-mcp\revit_security_bypass.ps1"

Write-Output "🚀 Khởi động Revit 2020 kèm File Template chuẩn..."
$RvtExe = "C:\Program Files\Autodesk\Revit 2020\Revit.exe"
$TemplateFile = "C:\ProgramData\Autodesk\RVT 2020\Templates\US Imperial\Electrical-Default.rte"

Start-Process $RvtExe -ArgumentList "`"$TemplateFile`""

Write-Output "⏳ Đợi Port 5050 active (Revit Bridge)..."
$timeout = 60
$start = Get-Date

while ((Get-Date) -lt $start.AddSeconds($timeout)) {
    $port = netstat -ano | findstr :5050 | findstr LISTENING
    if ($port) {
        Write-Output "✅ Port 5050 ĐÃ MOỞ. Revit Add-in sẵn sàng nhận lệnh!"
        exit 0
    }
    Start-Sleep -Seconds 2
}

Write-Output "❌ Lỗi: Không thể kết nối tới Revit sau 60s."
exit 1
