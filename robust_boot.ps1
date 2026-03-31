$ErrorActionPreference = "SilentlyContinue"

Write-Output "[STOP] Killing frozen Revit instances..."
Stop-Process -Name Revit -Force
Start-Sleep -Seconds 2

$RvtExe = "C:\Program Files\Autodesk\Revit 2020\Revit.exe"
$Template = "C:\ProgramData\Autodesk\RVT 2020\Templates\US Imperial\Electrical-Default.rte"

Write-Output "[START] Launching Revit with Template..."
Start-Process $RvtExe -ArgumentList "`"$Template`""

Write-Output "[GUARD] Starting Security Guardian (Bypass Modal)..."
Start-Process powershell.exe -ArgumentList "-ExecutionPolicy Bypass -File `"d:\CODE\revit-mcp\revit_guard.ps1`"" -WindowStyle Normal

Write-Output "[WAIT] Waiting for API Port 5050 to become active..."
