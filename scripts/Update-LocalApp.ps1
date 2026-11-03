[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PublishDirectory,
    [string]$ReceiptDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$source = [IO.Path]::GetFullPath($(if ([IO.Path]::IsPathRooted($PublishDirectory)) { $PublishDirectory } else { Join-Path $repoRoot $PublishDirectory }))
$destination = [IO.Path]::GetFullPath((Join-Path $repoRoot 'WindowSlu/bin/Release/net10.0-windows/win-x64/publish'))
$sourceExe = Join-Path $source 'WindowSlu.exe'
$destinationExe = Join-Path $destination 'WindowSlu.exe'
if (-not (Test-Path -LiteralPath $sourceExe -PathType Leaf)) { throw "Publish executable not found: $sourceExe" }
if ($source -eq $destination) { throw 'The source must be a separately verified publish directory.' }
$running = @(Get-Process WindowSlu -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $destinationExe })
if ($running.Count) { throw 'The normal WindowSlu is running. Keep its state; close it normally before replacing its executable.' }

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backup = Join-Path $repoRoot ('artifacts/local-app-backups/' + $stamp + '-' + [guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
if (-not $ReceiptDirectory) { $ReceiptDirectory = Join-Path $repoRoot 'artifacts/local-app' }
$ReceiptDirectory = [IO.Path]::GetFullPath($ReceiptDirectory)
New-Item -ItemType Directory -Path $ReceiptDirectory -Force | Out-Null
$profile = Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'WindowSlu'
$profileBefore = @()
if (Test-Path -LiteralPath $profile) {
    $profileBackup = Join-Path $backup 'profile'
    New-Item -ItemType Directory -Path $profileBackup -Force | Out-Null
    foreach ($file in Get-ChildItem -LiteralPath $profile -File) {
        Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $profileBackup $file.Name)
        $profileBefore += [pscustomobject]@{ path=$file.FullName; sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash }
    }
}

# The app is published as a self-contained single file. Preserve other files at
# the existing entry point and replace only the new bundle and its symbols.
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$files = @()
foreach ($name in @('WindowSlu.exe','WindowSlu.pdb')) {
    $inputPath = Join-Path $source $name
    if (-not (Test-Path -LiteralPath $inputPath -PathType Leaf)) { continue }
    $outputPath = Join-Path $destination $name
    if (Test-Path -LiteralPath $outputPath) { Copy-Item -LiteralPath $outputPath -Destination (Join-Path $backup $name) }
    Copy-Item -LiteralPath $inputPath -Destination $outputPath -Force
    $expected = (Get-FileHash -LiteralPath $inputPath -Algorithm SHA256).Hash
    $actual = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash
    if ($expected -ne $actual) { throw "Installed file hash mismatch: $name. Previous files remain in $backup" }
    $files += [pscustomobject]@{name=$name;source=$inputPath;destination=$outputPath;sha256=$actual}
}

$shell = New-Object -ComObject WScript.Shell
$shortcuts = @()
foreach ($entry in @(
    @{name='start-menu';path=(Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs/WindowSlu.lnk')},
    @{name='desktop';path=(Join-Path ([Environment]::GetFolderPath('Desktop')) 'WindowSlu.lnk')}
)) {
    if (Test-Path -LiteralPath $entry.path) { Copy-Item -LiteralPath $entry.path -Destination (Join-Path $backup ($entry.name+'.lnk')) }
    $shortcut = $shell.CreateShortcut($entry.path)
    $shortcut.TargetPath = $destinationExe
    $shortcut.Arguments = ''
    $shortcut.WorkingDirectory = $destination
    $shortcut.IconLocation = $destinationExe + ',0'
    $shortcut.Description = 'WindowSlu'
    $shortcut.Save()
    $readback = $shell.CreateShortcut($entry.path)
    if ($readback.TargetPath -ne $destinationExe -or -not (Test-Path -LiteralPath $readback.TargetPath)) { throw 'Shortcut readback failed.' }
    $shortcuts += [pscustomobject]@{path=$entry.path;target=$readback.TargetPath;workingDirectory=$readback.WorkingDirectory}
}
foreach ($file in $profileBefore) {
    if ((Get-FileHash -LiteralPath $file.path -Algorithm SHA256).Hash -ne $file.sha256) { throw "Profile changed during installation; do not overwrite it: $($file.path)" }
}
if (Test-Path -LiteralPath $profile) {
    $profileAfter = @(Get-ChildItem -LiteralPath $profile -File)
    if ($profileAfter.Count -ne $profileBefore.Count) { throw 'The profile file set changed during installation. Preserve it and review the backup.' }
}
$receipt = [pscustomobject]@{
    installedAt=[DateTimeOffset]::Now.ToString('o');source=$source;executable=$destinationExe
    backup=$backup;profile=$profile;profileUnchanged=$true;profileBefore=$profileBefore
    files=$files;shortcuts=$shortcuts;started=$false
}
$receiptPath = Join-Path $ReceiptDirectory 'install-receipt.json'
$receipt | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $receiptPath -Encoding utf8
$receipt | Select-Object executable,backup,profileUnchanged,@{n='receipt';e={$receiptPath}} | ConvertTo-Json
