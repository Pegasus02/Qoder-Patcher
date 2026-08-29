[CmdletBinding()]
param(
    [string]$InstallDir = 'C:\Program Files\Qoder\Qoder CN'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$patcher = Join-Path $projectRoot 'src\QoderCN-OpenAI-Compatible-Patcher.ps1'
$config = Join-Path $projectRoot 'configs\cpa-192.168.50.241.json'
$runtimeRelativePath = 'resources\app.asar.unpacked\node_modules\@qoder-ai\qoder-cn-agent-sdk\dist\_worker\qoder-worker-runtime.obf.mjs'
$installedRuntime = Join-Path $InstallDir $runtimeRelativePath
$installedAsar = Join-Path $InstallDir 'resources\app.asar'
$originalRuntimeSha256 = '7348879D488DC22CCA1FC8138C3182233637F78BEA210652701B3463B6D3F655'

Write-Host '[TEST] PowerShell syntax'
$null = [ScriptBlock]::Create((Get-Content -LiteralPath $patcher -Raw))

Write-Host '[TEST] JSON configuration'
$raw = Get-Content -LiteralPath $config -Raw -Encoding UTF8
$null = $raw | ConvertFrom-Json
if ($raw -match '"(?:api[_-]?key|access[_-]?token|authorization)"\s*:') {
    throw 'Tracked configuration contains a secret-like property.'
}

Write-Host '[TEST] Patcher DryRun'
$dryRunInstallDir = $InstallDir
$fixtureRoot = $null

if ((Get-FileHash -LiteralPath $installedRuntime -Algorithm SHA256).Hash -ne $originalRuntimeSha256) {
    Write-Host '[INFO] Installed runtime is patched; building a temporary original-runtime fixture.'
    $backupRoot = Join-Path $env:LOCALAPPDATA 'QoderCNOpenAICompatiblePatcher\backups-v2'
    $manifestFile = Get-ChildItem -LiteralPath $backupRoot -Filter manifest.json -File -Recurse -ErrorAction Stop |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $manifestFile) {
        throw 'No v2 backup is available for the DryRun fixture.'
    }
    $manifest = Get-Content -LiteralPath $manifestFile.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    if ((Get-FileHash -LiteralPath $manifest.runtimeBackup -Algorithm SHA256).Hash -ne $originalRuntimeSha256) {
        throw 'The newest v2 backup is not the supported original runtime.'
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('qoder-patcher-project-test-' + [Guid]::NewGuid().ToString('N'))
    $fixtureRuntime = Join-Path $fixtureRoot $runtimeRelativePath
    $fixtureAsar = Join-Path $fixtureRoot 'resources\app.asar'
    New-Item -ItemType Directory -Path (Split-Path -Parent $fixtureRuntime) -Force | Out-Null
    Copy-Item -LiteralPath $manifest.runtimeBackup -Destination $fixtureRuntime
    Copy-Item -LiteralPath $installedAsar -Destination $fixtureAsar
    $dryRunInstallDir = $fixtureRoot
}

try {
    & $patcher -Action DryRun -InstallDir $dryRunInstallDir -ConfigPath $config
}
finally {
    if ($null -ne $fixtureRoot -and (Test-Path -LiteralPath $fixtureRoot)) {
        $resolvedFixture = [IO.Path]::GetFullPath($fixtureRoot)
        $resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
        if (-not $resolvedFixture.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase) -or
            -not ([IO.Path]::GetFileName($resolvedFixture)).StartsWith('qoder-patcher-project-test-', [StringComparison]::Ordinal)) {
            throw "Refusing to remove unexpected fixture path: $resolvedFixture"
        }
        Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
    }
}

Write-Host '[OK] Project checks passed.' -ForegroundColor Green
