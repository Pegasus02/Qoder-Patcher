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

$ImportAnchor = 'import*as P8e from"node:path";import*as qxA from"node:fs/promises";'
$ImportReplacement = 'import*as qcv21fs from"node:fs";import*as P8e from"node:path";import*as qxA from"node:fs/promises";'

$ConverterAnchor = 'function XxA(A){'
$ConverterReplacement = @'
function qcv21cfg(){try{return JSON.parse(qcv21fs.readFileSync(process.env.QODER_CN_CUSTOM_PROVIDER_CONFIG||process.env.USERPROFILE+"/.qoder-cn/custom-openai-provider-v2.1.json","utf8"))}catch(A){return null}}function qcv21base(A){try{let e=new URL(A);if("http:"!==e.protocol&&"https:"!==e.protocol||e.username||e.password||e.search||e.hash)return;let t=e.pathname.replace(/\/+$/g,"");return t.endsWith("/chat/completions")&&(t=t.slice(0,-17)),e.pathname=t||"/",e.toString().replace(/\/$/,"")}catch{}}function qcv21url(A){try{let e=qcv21cfg();if(e&&"string"==typeof A&&new URL(e.uiBaseUrl).toString()===new URL(A).toString())return qcv21base(e.upstreamBaseUrl)??A}catch{}return A}function qcv21target(A){let t=A?.custom_model,i=A?.model_config;if(!t||"custom_model"!==i?.key)return;let e=qcv21cfg();if(!e)throw Error("QODER_CN_PATCH_CONFIG_UNAVAILABLE");let n=Array.isArray(e.models)?e.models.find(A=>A&&A.id===t.model):void 0;if(!n)return;let r=t.parameters?.api_key,o=qcv21base(e.upstreamBaseUrl);if("string"!=typeof r||!r.trim())throw Error("QODER_CN_PATCH_API_KEY_MISSING");if(!o)throw Error("QODER_CN_PATCH_UPSTREAM_URL_INVALID");let s=Number.isInteger(e.firstPayloadTimeoutMs)&&e.firstPayloadTimeoutMs>0?e.firstPayloadTimeoutMs:6e4,a=Number.isInteger(e.streamIdleTimeoutMs)&&e.streamIdleTimeoutMs>=0?e.streamIdleTimeoutMs:0,g=Number.isInteger(n.maxInputTokens)&&n.maxInputTokens>0?n.maxInputTokens:131072,l=Number.isInteger(n.maxOutputTokens)&&n.maxOutputTokens>0?n.maxOutputTokens:32768;return{providerId:"qoder-cn-patcher",adapter:"openai-compatible",baseUrl:o,apiKey:r,model:{modelId:t.model,displayName:n.displayName??t.model,contextWindow:g,maxOutputTokens:l,capabilities:{tools:!1!==n.tools,vision:!0===n.vision,thinking:!0===n.reasoning},maxTokensField:"max_completion_tokens"===n.maxTokensField?"max_completion_tokens":"max_tokens"},timeouts:{firstPayloadTimeoutMs:s,...a>0?{streamIdleTimeoutMs:a}:{}}}}function XxA(A){/*QODER_CN_OAI_PATCH_V2_1*/
'@

$ModelUrlAnchor = 'url:A.url,model:A.model,provider:A.provider'
$ModelUrlReplacement = 'url:qcv21url(A.url),model:A.model,provider:A.provider'

$CatalogAnchor = 'n=await e().getBYOKConfig(),r=t;B(pn(i,"success",{providers:n?.providers.map(A)??[]}))'
$CatalogReplacement = @'
n=await e().getBYOKConfig(),r=t;let qcp=n?.providers.map(A)??[];try{let qcc=qcv21cfg();if(!qcc)throw Error("QODER_CN_PATCH_CONFIG_UNAVAILABLE");let qci=Number.isInteger(qcc.replaceProviderIndex)?qcc.replaceProviderIndex:qcp.findIndex(q=>q.key===qcc.replaceProviderKey||q.display_name===qcc.replaceProviderDisplayName);if(qci<0||qci>=qcp.length)throw Error("QODER_CN_PATCH_REPLACEMENT_PROVIDER_NOT_FOUND");let qcb=qcp[qci],qcm=(qcc.models??[]).map(q=>({key:q.id,display_name:q.displayName??q.id,is_vl:q.vision===true,is_reasoning:q.reasoning===true,format:"openai",max_input_tokens:q.maxInputTokens??131072,efforts:q.efforts??[],supports_disabled:q.supportsDisabled===true}));if(!qcm.length)throw Error("QODER_CN_PATCH_MODELS_EMPTY");qcp[qci]={...qcb,display_name:qcc.displayName??"Local OpenAI Compatible",url:qcc.uiBaseUrl,fields:[{key:"api_key",display_name:"API Key",type:"free_input",mandatory:true}],types:[{key:"openai-compatible",display_name:"OpenAI Compatible",style:"openai",models:qcm}]}}catch(qce){Q.warn("[qoder-cn-openai-patch-v2.1] custom provider not loaded:",qce)}B(pn(i,"success",{providers:qcp}))
'@

$ValidationAnchor = 'o=await r().checkBYOKModel(n.provider,n.model,{api_key:n.api_key},A,e,t);B(pn(i,"success",{success:o}))'
$ValidationReplacement = @'
o=await(async()=>{try{let qcv=qcv21cfg();if(qcv&&qcv.skipValidation!==false&&new URL(qcv.uiBaseUrl).toString()===new URL(A).toString())return true}catch(qce){Q.warn("[qoder-cn-openai-patch-v2.1] validation override unavailable:",qce)}return r().checkBYOKModel(n.provider,n.model,{api_key:n.api_key},A,e,t)})();B(pn(i,"success",{success:o}))
'@

$InferenceRouteAnchor = 'let A=ZVe(t.model_config,()=>S?.getExternalProviderRegistry());for(;;)'
$InferenceRouteReplacement = 'let A=qcv21target(t)??ZVe(t.model_config,()=>S?.getExternalProviderRegistry());for(;;)'

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

Write-Host '[TEST] Native C# GUI build and compilation'
$buildScript = Join-Path $projectRoot 'build.ps1'
if (-not (Test-Path -LiteralPath $buildScript -PathType Leaf)) {
    throw 'build.ps1 is missing.'
}
& $buildScript
$builtExe = Join-Path $projectRoot 'bin\QoderCN-Patcher.exe'
if (-not (Test-Path -LiteralPath $builtExe -PathType Leaf)) {
    throw 'The compiled binary bin\QoderCN-Patcher.exe was not created.'
}

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
    $originalText = $null

    if (Test-Path -LiteralPath $backupRoot -PathType Container) {
        $manifestFile = Get-ChildItem -LiteralPath $backupRoot -Filter manifest.json -File -Recurse -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($null -ne $manifestFile) {
            $manifest = Get-Content -LiteralPath $manifestFile.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
            if ((Get-FileHash -LiteralPath $manifest.runtimeBackup -Algorithm SHA256).Hash -eq $originalRuntimeSha256) {
                $originalText = [IO.File]::ReadAllText($manifest.runtimeBackup, [Text.Encoding]::UTF8)
            }
        }
    }

    if ($null -eq $originalText) {
        $unpatched = $installedRuntimeText.
            Replace($ImportReplacement, $ImportAnchor).
            Replace($ConverterReplacement, $ConverterAnchor).
            Replace($ModelUrlReplacement, $ModelUrlAnchor).
            Replace($CatalogReplacement, $CatalogAnchor).
            Replace($ValidationReplacement, $ValidationAnchor).
            Replace($InferenceRouteReplacement, $InferenceRouteAnchor)
        $originalText = $unpatched
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('qoder-patcher-project-test-' + [Guid]::NewGuid().ToString('N'))
    $fixtureRuntime = Join-Path $fixtureRoot $runtimeRelativePath
    $fixtureAsar = Join-Path $fixtureRoot 'resources\app.asar'
    New-Item -ItemType Directory -Path (Split-Path -Parent $fixtureRuntime) -Force | Out-Null
    [IO.File]::WriteAllText($fixtureRuntime, $originalText, [Text.UTF8Encoding]::new($false))
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
