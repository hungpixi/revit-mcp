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

Write-Output "🛡️ Revit Security Guardian - Chờ cửa sổ [Security - Unsigned Add-In]..."

$timeout = 120  # 2 phút
$start = Get-Date

while ((Get-Date) -lt $start.AddSeconds($timeout)) {
    $hwnd = [Win32]::FindWindow($null, "Security - Unsigned Add-In")
    if ($hwnd -ne [IntPtr]::Zero) {
        Write-Output "🎯 Tìm thấy! LEFT LEFT ENTER → Always Load"
        [Win32]::SetForegroundWindow($hwnd)
        Start-Sleep -Milliseconds 500
        
        # Focus mặc định ở "Do Not Load" (nút phải nhất)
        # LEFT LEFT → dời về "Always Load" → ENTER
        [System.Windows.Forms.SendKeys]::SendWait("{LEFT}{LEFT}{ENTER}")
        
        Write-Output "✅ Done! Add-in đã được load."
        break
    }
    Start-Sleep -Seconds 1
}
