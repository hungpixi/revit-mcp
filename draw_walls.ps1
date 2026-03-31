
$proc = Get-Process -Name "Revit" -ErrorAction SilentlyContinue
if ($proc) {
    Add-Type @"
      using System;
      using System.Runtime.InteropServices;
      public class Win32 {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
      }
"@
    [Win32]::SetForegroundWindow($proc.MainWindowHandle)
}

Start-Job { Invoke-RestMethod -Uri "http://localhost:5050/api/ai-command/" -Method Post -Body '{"action": "open_new_project"}' -ContentType "application/json" }
Start-Sleep -Seconds 3

Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Start-Sleep -Seconds 7

$wall1 = '{"action": "create_wall", "payload": {"startPt": [0,0,0], "endPt": [0,20,0], "levelId": "Level 1" }}'
$wall2 = '{"action": "create_wall", "payload": {"startPt": [0,20,0], "endPt": [25,20,0], "levelId": "Level 1" }}'
$wall3 = '{"action": "create_wall", "payload": {"startPt": [25,20,0], "endPt": [25,0,0], "levelId": "Level 1" }}'
$wall4 = '{"action": "create_wall", "payload": {"startPt": [25,0,0], "endPt": [0,0,0], "levelId": "Level 1" }}'

Invoke-RestMethod -Uri "http://localhost:5050/api/ai-command/" -Method Post -Body $wall1 -ContentType "application/json"
Invoke-RestMethod -Uri "http://localhost:5050/api/ai-command/" -Method Post -Body $wall2 -ContentType "application/json"
Invoke-RestMethod -Uri "http://localhost:5050/api/ai-command/" -Method Post -Body $wall3 -ContentType "application/json"
Invoke-RestMethod -Uri "http://localhost:5050/api/ai-command/" -Method Post -Body $wall4 -ContentType "application/json"

