Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
    using System;
    using System.Runtime.InteropServices;
    public class Win32 {
        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
    }
"@

Write-Output "[GUARD] Waiting for Revit Security Dialog..."
$timeout = 180
$start = Get-Date

while ((Get-Date) -lt $start.AddSeconds($timeout)) {
    # Scan for the specific Revit 'Unsigned Add-in' window title
    $hwnd = [Win32]::FindWindow($null, "Security - Unsigned Add-In")
    if ($hwnd -ne 0) {
        Write-Output "[MATCH] Found Security Dialog. Auto-clicking 'Always Load'..."
        [Win32]::SetForegroundWindow($hwnd)
        Start-Sleep -Milliseconds 500
        # Tab/Arrow sequence to select 'Always Load' and hit Enter
        [System.Windows.Forms.SendKeys]::SendWait("{LEFT}{LEFT}{ENTER}")
        Start-Sleep -Seconds 2
    }
    
    # Check if the AI Bridge port 5050 is active yet
    $port = netstat -ano | findstr :5050 | findstr LISTENING
    if ($port) {
        Write-Output "[OK] REVID BRIDGE PORT 5050 IS NOW ACTIVE."
        break
    }
    
    Start-Sleep -Seconds 1
}
