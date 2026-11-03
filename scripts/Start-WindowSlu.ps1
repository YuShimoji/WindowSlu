[CmdletBinding()]
param([switch]$ShowPath)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$executable = [IO.Path]::GetFullPath((Join-Path $repoRoot 'WindowSlu/bin/Release/net10.0-windows/win-x64/publish/WindowSlu.exe'))
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw 'WindowSlu is not installed at the normal entry point. Run Update-LocalApp.ps1 with the verified publish directory.' }
if ($ShowPath) { Write-Output $executable; return }
$existing = @(Get-Process WindowSlu -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $executable })
if ($existing.Count) { $existing | Select-Object Id,Path,MainWindowTitle; return }
# This is the interactive application the user requested to open for review.
Start-Process -FilePath $executable -WorkingDirectory (Split-Path $executable -Parent) -WindowStyle Normal -PassThru | Select-Object Id,Path
