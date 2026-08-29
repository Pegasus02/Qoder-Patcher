param(
    [switch]$RunAfterBuild
)

$ErrorActionPreference = 'Stop'

$script:ProjectRoot = if (-not [string]::IsNullOrEmpty($PSScriptRoot)) { $PSScriptRoot } else { [IO.Directory]::GetCurrentDirectory() }
$script:BinDir = [IO.Path]::Combine($script:ProjectRoot, 'bin')
$script:OutExe = [IO.Path]::Combine($script:BinDir, 'QoderCN-Patcher.exe')
$script:TempOut = [IO.Path]::Combine($script:BinDir, ("QoderCN-Patcher-" + [Guid]::NewGuid().ToString('N').Substring(0, 8) + ".exe"))
$script:Manifest = [IO.Path]::Combine($script:ProjectRoot, 'src\gui\app.manifest')
$script:RootExe = [IO.Path]::Combine($script:ProjectRoot, 'QoderCN-Patcher.exe')
$script:StrayBinConfigs = [IO.Path]::Combine($script:BinDir, 'configs')

$CscCandidates = @(
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe',
    'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
)

$Csc = $null
foreach ($cand in $CscCandidates) {
    if (Test-Path -LiteralPath $cand -PathType Leaf) {
        $Csc = $cand
        break
    }
}

if ($null -eq $Csc) {
    throw 'Could not find csc.exe in Windows .NET Framework directory.'
}

if (-not (Test-Path -LiteralPath $script:BinDir -PathType Container)) {
    $null = New-Item -ItemType Directory -Path $script:BinDir -Force
}

$Sources = @(
    (Join-Path $script:ProjectRoot 'src\gui\Program.cs'),
    (Join-Path $script:ProjectRoot 'src\gui\PatcherCore.cs'),
    (Join-Path $script:ProjectRoot 'src\gui\MainForm.cs')
)

$Refs = @(
    'System.dll',
    'System.Core.dll',
    'System.Drawing.dll',
    'System.Windows.Forms.dll',
    'System.Web.Extensions.dll'
)

$CscArgs = @(
    '/target:winexe',
    "/out:$script:TempOut",
    "/win32manifest:$script:Manifest",
    '/platform:anycpu',
    '/optimize+',
    '/utf8output',
    '/nologo'
)

foreach ($r in $Refs) {
    $CscArgs += "/r:$r"
}
foreach ($s in $Sources) {
    $CscArgs += $s
}

Write-Host '[BUILD] Compiling QoderCN-Patcher.exe...' -ForegroundColor Cyan
Write-Host "[BUILD] Compiler: $Csc" -ForegroundColor DarkGray
Write-Host "[BUILD] Output:   $script:OutExe" -ForegroundColor DarkGray

& $Csc $CscArgs
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code: $LASTEXITCODE"
}

# Move compiled output to bin directory
try {
    [IO.File]::Copy($script:TempOut, $script:OutExe, $true)
    [IO.File]::Delete($script:TempOut)
}
catch {
    if (Test-Path -LiteralPath $script:TempOut) {
        Remove-Item -LiteralPath $script:TempOut -Force -ErrorAction SilentlyContinue
    }
    Write-Host "[WARN] bin\QoderCN-Patcher.exe is currently running and locked by Windows. Please close the open patcher window to update the binary file." -ForegroundColor Yellow
}

if ([IO.File]::Exists($script:OutExe)) {
    try {
        Copy-Item -LiteralPath $script:OutExe -Destination $script:RootExe -Force
        Write-Host "[OK] Root shortcut synced: $script:RootExe" -ForegroundColor Green
    }
    catch {
        Write-Host "[WARN] Could not update root QoderCN-Patcher.exe because it is currently running." -ForegroundColor Yellow
    }
}

# Clean stray bin/configs if present
if ([IO.Directory]::Exists($script:StrayBinConfigs)) {
    try {
        [IO.Directory]::Delete($script:StrayBinConfigs, $true)
    }
    catch { }
}

$fileInfo = Get-Item -LiteralPath $script:OutExe
Write-Host "[OK] Build succeeded: $($script:OutExe) ($([math]::Round($fileInfo.Length / 1KB, 2)) KB)" -ForegroundColor Green

if ($RunAfterBuild) {
    Start-Process -FilePath $script:RootExe
}
