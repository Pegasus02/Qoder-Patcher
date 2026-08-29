[CmdletBinding()]
param(
    [string]$InstallDir = 'C:\Program Files\Qoder\Qoder CN'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$patcher = Join-Path $projectRoot 'src\QoderCN-OpenAI-Compatible-Patcher.ps1'
$gui = Join-Path $projectRoot 'src\QoderCN-Patcher-GUI.ps1'
$guiLauncher = Join-Path $projectRoot 'Launch-QoderCN-Patcher-GUI.cmd'
$config = Join-Path $projectRoot 'configs\cpa-192.168.50.241.json'
$runtimeRelativePath = 'resources\app.asar.unpacked\node_modules\@qoder-ai\qoder-cn-agent-sdk\dist\_worker\qoder-worker-runtime.obf.mjs'
$installedRuntime = Join-Path $InstallDir $runtimeRelativePath
$installedAsar = Join-Path $InstallDir 'resources\app.asar'
$originalRuntimeSha256 = '7348879D488DC22CCA1FC8138C3182233637F78BEA210652701B3463B6D3F655'
$versionFile = Join-Path $projectRoot 'VERSION'
$installedRuntimeText = [IO.File]::ReadAllText($installedRuntime, [Text.Encoding]::UTF8)
$originalRuntimeSource = $installedRuntime
$bundledNode = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe'

Write-Host '[TEST] PowerShell syntax'
$patcherRaw = Get-Content -LiteralPath $patcher -Raw
$null = [ScriptBlock]::Create($patcherRaw)
if ($patcherRaw -notmatch 'QODER_CN_OAI_PATCH_V2_1' -or
    $patcherRaw -notmatch 'qcv21target' -or
    $patcherRaw -notmatch 'adapter:\"openai-compatible\"') {
    throw 'The v2.1 direct-route implementation is missing from the patcher source.'
}

Write-Host '[TEST] GUI syntax and self-test'
if (-not (Test-Path -LiteralPath $guiLauncher -PathType Leaf)) {
    throw 'The double-click GUI launcher is missing.'
}
$guiRaw = Get-Content -LiteralPath $gui -Raw
$null = [ScriptBlock]::Create($guiRaw)
if ($guiRaw -notmatch 'Invoke-PatcherElevated' -or
    $guiRaw -notmatch 'Install / Upgrade' -or
    $guiRaw -notmatch 'Restore latest') {
    throw 'The GUI does not expose the required patch operations.'
}
& $gui -SelfTest

Write-Host '[TEST] JavaScript URL normalization helper'
$urlHelperMatch = [regex]::Match(
    $patcherRaw,
    'function qcv21base\(A\)\{try\{.*?\}catch\{\}\}',
    [Text.RegularExpressions.RegexOptions]::CultureInvariant
)
if (-not $urlHelperMatch.Success) {
    throw 'Could not extract qcv21base from the patcher source.'
}
if (Test-Path -LiteralPath $bundledNode -PathType Leaf) {
    $urlTestPath = Join-Path ([IO.Path]::GetTempPath()) ('qoder-patcher-url-test-' + [Guid]::NewGuid().ToString('N') + '.mjs')
    try {
        $urlTestScript = $urlHelperMatch.Value + @'

const cases = [
  ["http://192.168.50.241:8317/v1", "http://192.168.50.241:8317/v1"],
  ["http://192.168.50.241:8317/v1/", "http://192.168.50.241:8317/v1"],
  ["http://192.168.50.241:8317/v1/chat/completions", "http://192.168.50.241:8317/v1"],
  ["https://localhost:11434/v1/", "https://localhost:11434/v1"]
];
for (const [input, expected] of cases) {
  const actual = qcv21base(input);
  if (actual !== expected) throw new Error(`${input} => ${actual}; expected ${expected}`);
}
if (qcv21base("http://localhost:8000/v1?token=secret") !== undefined) {
  throw new Error("URL query strings must be rejected");
}
'@
        [IO.File]::WriteAllText($urlTestPath, $urlTestScript, [Text.UTF8Encoding]::new($false))
        & $bundledNode $urlTestPath
        if ($LASTEXITCODE -ne 0) {
            throw 'qcv21base behavior test failed.'
        }
    }
    finally {
        if (Test-Path -LiteralPath $urlTestPath) {
            Remove-Item -LiteralPath $urlTestPath -Force
        }
    }
}
else {
    Write-Host '[WARN] Bundled Node.js is unavailable; skipped URL helper behavior test.' -ForegroundColor Yellow
}

Write-Host '[TEST] Project version'
if ((Get-Content -LiteralPath $versionFile -Raw).Trim() -ne '2.1.1-experimental') {
    throw 'VERSION does not identify the v2.1.1 experimental release.'
}

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
    $originalRuntimeSource = [string]$manifest.runtimeBackup

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('qoder-patcher-project-test-' + [Guid]::NewGuid().ToString('N'))
    $fixtureRuntime = Join-Path $fixtureRoot $runtimeRelativePath
    $fixtureAsar = Join-Path $fixtureRoot 'resources\app.asar'
    New-Item -ItemType Directory -Path (Split-Path -Parent $fixtureRuntime) -Force | Out-Null
    Copy-Item -LiteralPath $manifest.runtimeBackup -Destination $fixtureRuntime
    Copy-Item -LiteralPath $installedAsar -Destination $fixtureAsar
    $dryRunInstallDir = $fixtureRoot
}

try {
    $dryRunParameters = @{
        Action = 'DryRun'
        InstallDir = $dryRunInstallDir
        ConfigPath = $config
    }
    if (Test-Path -LiteralPath $bundledNode -PathType Leaf) {
        $dryRunParameters.NodePath = $bundledNode
    }
    & $patcher @dryRunParameters
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

if ($installedRuntimeText.Contains('QODER_CN_OAI_PATCH_V2*/') -and
    -not $installedRuntimeText.Contains('QODER_CN_OAI_PATCH_V2_1')) {
    Write-Host '[TEST] Transactional v2 to v2.1 upgrade and Restore'
    $upgradeRoot = Join-Path ([IO.Path]::GetTempPath()) ('qoder-patcher-upgrade-test-' + [Guid]::NewGuid().ToString('N'))
    $upgradeRuntime = Join-Path $upgradeRoot $runtimeRelativePath
    $upgradeAsar = Join-Path $upgradeRoot 'resources\app.asar'
    $upgradeBackupRoot = Join-Path $upgradeRoot 'backups'
    $upgradeBackupId = 'original-fixture'
    $upgradeBackupDir = Join-Path $upgradeBackupRoot $upgradeBackupId
    $upgradeRuntimeBackup = Join-Path $upgradeBackupDir 'qoder-worker-runtime.obf.mjs'
    $upgradeConfigPath = Join-Path $upgradeRoot 'runtime-config.json'
    try {
        New-Item -ItemType Directory -Path (Split-Path -Parent $upgradeRuntime) -Force | Out-Null
        New-Item -ItemType Directory -Path $upgradeBackupDir -Force | Out-Null
        Copy-Item -LiteralPath $installedRuntime -Destination $upgradeRuntime
        Copy-Item -LiteralPath $installedAsar -Destination $upgradeAsar
        Copy-Item -LiteralPath $originalRuntimeSource -Destination $upgradeRuntimeBackup
        $upgradeManifest = [ordered]@{
            backupId = $upgradeBackupId
            createdAt = (Get-Date).ToString('o')
            installDir = $upgradeRoot
            runtimePath = $upgradeRuntime
            runtimeBackup = $upgradeRuntimeBackup
            runtimeSha256 = $originalRuntimeSha256.ToLowerInvariant()
            appAsarPath = $upgradeAsar
            appAsarSha256 = (Get-FileHash -LiteralPath $upgradeAsar -Algorithm SHA256).Hash.ToLowerInvariant()
            patchVersion = 2
        }
        [IO.File]::WriteAllText(
            (Join-Path $upgradeBackupDir 'manifest.json'),
            ($upgradeManifest | ConvertTo-Json -Depth 5),
            [Text.UTF8Encoding]::new($false)
        )

        & $patcher -Action Apply -InstallDir $upgradeRoot -ConfigPath $config `
            -RuntimeConfigPath $upgradeConfigPath -BackupRoot $upgradeBackupRoot
        $upgradedText = [IO.File]::ReadAllText($upgradeRuntime, [Text.Encoding]::UTF8)
        if (-not $upgradedText.Contains('QODER_CN_OAI_PATCH_V2_1') -or
            $upgradedText.Contains('QODER_CN_OAI_PATCH_V2*/')) {
            throw 'The temporary v2 fixture was not upgraded cleanly to v2.1.'
        }

        & $patcher -Action Restore -InstallDir $upgradeRoot -RuntimeConfigPath $upgradeConfigPath `
            -BackupRoot $upgradeBackupRoot -BackupId $upgradeBackupId
        if ((Get-FileHash -LiteralPath $upgradeRuntime -Algorithm SHA256).Hash -ne $originalRuntimeSha256) {
            throw 'Restore did not recover the original Runtime in the upgrade fixture.'
        }
    }
    finally {
        if (Test-Path -LiteralPath $upgradeRoot) {
            $resolvedUpgradeRoot = [IO.Path]::GetFullPath($upgradeRoot)
            $resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
            if (-not $resolvedUpgradeRoot.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase) -or
                -not ([IO.Path]::GetFileName($resolvedUpgradeRoot)).StartsWith('qoder-patcher-upgrade-test-', [StringComparison]::Ordinal)) {
                throw "Refusing to remove unexpected upgrade fixture path: $resolvedUpgradeRoot"
            }
            Remove-Item -LiteralPath $resolvedUpgradeRoot -Recurse -Force
        }
    }
}

Write-Host '[OK] Project checks passed.' -ForegroundColor Green
