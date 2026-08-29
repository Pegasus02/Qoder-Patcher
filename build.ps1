param(
    [switch]$Sign,
    [string]$CertificateThumbprint = '',
    [switch]$CreateDevelopmentCertificate
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = $PSScriptRoot
Set-Location $ProjectRoot

$cscCandidates = @(
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe',
    'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
)

$csc = $null
foreach ($candidate in $cscCandidates) {
    if (Test-Path -LiteralPath $candidate -PathType Leaf) {
        $csc = $candidate
        break
    }
}

if ($null -eq $csc) {
    throw '.NET Framework csc.exe compiler not found.'
}

$binDir = Join-Path $ProjectRoot 'bin'
if (-not (Test-Path -LiteralPath $binDir -PathType Container)) {
    New-Item -ItemType Directory -Path $binDir -Force | Out-Null
}

$outExe = Join-Path $binDir 'QoderCN-Patcher.exe'
$manifest = Join-Path $ProjectRoot 'src-native\app.manifest'
$icon = Join-Path $ProjectRoot 'src-native\app.ico'
$refs = '/r:System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll,System.Web.Extensions.dll,System.Security.dll'
$files = Get-ChildItem -Path (Join-Path $ProjectRoot 'src-native') -Filter '*.cs' -Recurse | Select-Object -ExpandProperty FullName
$tempOut = Join-Path $binDir ("QoderCN-Patcher-{0}.tmp.exe" -f [Guid]::NewGuid().ToString('N').Substring(0, 8))

Write-Host "Compiling $outExe ..." -ForegroundColor Cyan
& $csc /nologo /target:winexe /platform:anycpu /optimize+ "/out:$tempOut" "/win32manifest:$manifest" "/win32icon:$icon" $refs $files

if ($LASTEXITCODE -ne 0) {
    throw "csc.exe compilation failed with exit code $LASTEXITCODE"
}

try {
    Move-Item -LiteralPath $tempOut -Destination $outExe -Force -ErrorAction Stop
}
catch {
    if (Test-Path -LiteralPath $tempOut) {
        Remove-Item -LiteralPath $tempOut -Force -ErrorAction SilentlyContinue
    }
    throw "Unable to update $outExe because QoderCN-Patcher.exe is currently running. Please close the running application window and run build.cmd again."
}

Write-Host "[SUCCESS] Compiled $outExe" -ForegroundColor Green
Write-Host "Signing requested: $([bool]$Sign)" -ForegroundColor DarkGray

if ($Sign) {
    $signScript = Join-Path $ProjectRoot 'scripts\Sign-Binary.ps1'
    if (Test-Path -LiteralPath $signScript -PathType Leaf) {
        $signArguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $signScript, '-BinaryPath', $outExe)
        if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) { $signArguments += @('-CertificateThumbprint', $CertificateThumbprint) }
        if ($CreateDevelopmentCertificate) { $signArguments += '-CreateDevelopmentCertificate' }
        & powershell.exe $signArguments
        if ($LASTEXITCODE -ne 0) { throw "Signing failed with exit code $LASTEXITCODE" }
    }
}
