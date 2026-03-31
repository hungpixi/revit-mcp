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

Write-Output "⏳ Đợi 30 giây để Revit load UI và Active Document..."
for($i=1; $i -le 30; $i++) { Start-Sleep 1; Write-Host "." -NoNewline }
Write-Output ""

Write-Output "🎬 Bắt đầu chạy Animation Vẽ NML..."
node d:\CODE\revit-mcp\RevitMCP_Node\animate_nml.js

Write-Output "✅ Hoàn tất toàn bộ quy trình Auto 1-Click!"
