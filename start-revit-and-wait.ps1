$ErrorActionPreference = "Stop"

param(
  [string]$RevitExe = "C:\Program Files\Autodesk\Revit 2020\Revit.exe",
  [string]$ProjectPath = "",
  [string]$Host = "127.0.0.1",
  [int]$Port = 8080,
  [int]$TimeoutSec = 120
)

function Test-TcpPort {
  param([string]$Host, [int]$Port)
  try {
    $client = New-Object System.Net.Sockets.TcpClient
    $iar = $client.BeginConnect($Host, $Port, $null, $null)
    $ok = $iar.AsyncWaitHandle.WaitOne(500, $false)
    if (-not $ok) { $client.Close(); return $false }
    $client.EndConnect($iar)
    $client.Close()
    return $true
  } catch {
    return $false
  }
}

if (-not (Test-Path $RevitExe)) {
  throw "Revit.exe not found: $RevitExe"
}

# If Revit already running, don't spawn another
$running = Get-Process -Name "Revit" -ErrorAction SilentlyContinue
if (-not $running) {
  if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    Start-Process -FilePath $RevitExe | Out-Null
    Write-Host "Started Revit (no project path provided)."
  } else {
    if (-not (Test-Path $ProjectPath)) { throw "ProjectPath not found: $ProjectPath" }
    Start-Process -FilePath $RevitExe -ArgumentList @("`"$ProjectPath`"") | Out-Null
    Write-Host "Started Revit with project: $ProjectPath"
  }
} else {
  Write-Host "Revit is already running."
}

$deadline = (Get-Date).AddSeconds($TimeoutSec)
while ((Get-Date) -lt $deadline) {
  if (Test-TcpPort -Host $Host -Port $Port) {
    Write-Host "Revit MCP port is ready: $Host`:$Port"
    exit 0
  }
  Start-Sleep -Milliseconds 500
}

throw "Timed out waiting for Revit MCP port: $Host`:$Port (timeout ${TimeoutSec}s)"

