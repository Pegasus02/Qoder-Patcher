[CmdletBinding()]
param(
    [string]$InstallDir = '',
    [switch]$RequireSignedBinary
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$patcher = Join-Path $projectRoot 'src\QoderCN-OpenAI-Compatible-Patcher.ps1'
$gui = Join-Path $projectRoot 'src\QoderCN-Patcher-GUI.ps1'
$guiLauncher = Join-Path $projectRoot 'Launch-QoderCN-Patcher-GUI.cmd'
$config = if (Test-Path -LiteralPath (Join-Path $projectRoot 'configs\cpa-192.168.50.241.json')) {
    Join-Path $projectRoot 'configs\cpa-192.168.50.241.json'
} else {
    Join-Path $projectRoot 'configs\custom-provider.example.json'
}
$runtimeRelativePath = 'resources\app.asar.unpacked\node_modules\@qoder-ai\qoder-cn-agent-sdk\dist\_worker\qoder-worker-runtime.obf.mjs'

if ([string]::IsNullOrWhiteSpace($InstallDir) -or -not (Test-Path -LiteralPath $InstallDir)) {
    if ($env:QODER_CN_INSTALL_DIR -and (Test-Path -LiteralPath $env:QODER_CN_INSTALL_DIR)) {
        $InstallDir = $env:QODER_CN_INSTALL_DIR
    } elseif ($env:LOCALAPPDATA -and (Test-Path (Join-Path $env:LOCALAPPDATA 'Programs\Qoder CN\resources\app.asar'))) {
        $InstallDir = Join-Path $env:LOCALAPPDATA 'Programs\Qoder CN'
    } elseif ($env:ProgramFiles -and (Test-Path (Join-Path $env:ProgramFiles 'Qoder\Qoder CN\resources\app.asar'))) {
        $InstallDir = Join-Path $env:ProgramFiles 'Qoder\Qoder CN'
    } else {
        $InstallDir = 'C:\Program Files\Qoder\Qoder CN'
    }
}

$installedRuntime = Join-Path $InstallDir $runtimeRelativePath
$installedAsar = Join-Path $InstallDir 'resources\app.asar'
$SupportedRuntimeSha256List = @('C79C0CDCABA7F8EEACC5D8139ADD45D142C21F5354C03C0F4F1D8D2CFBC73150', '7348879D488DC22CCA1FC8138C3182233637F78BEA210652701B3463B6D3F655')
$originalRuntimeSha256 = 'C79C0CDCABA7F8EEACC5D8139ADD45D142C21F5354C03C0F4F1D8D2CFBC73150'
$versionFile = Join-Path $projectRoot 'VERSION'
$installedRuntimeText = if (Test-Path -LiteralPath $installedRuntime) { [IO.File]::ReadAllText($installedRuntime, [Text.Encoding]::UTF8) } else { '' }
$originalRuntimeSource = $installedRuntime
$bundledNode = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe'

Write-Host '[TEST] PowerShell syntax'
$patcherRaw = Get-Content -LiteralPath $patcher -Raw -Encoding UTF8
$null = [ScriptBlock]::Create($patcherRaw)
if ($patcherRaw -notmatch 'QODER_CN_OAI_PATCH_V3_2_0' -or
    $patcherRaw -notmatch 'qcv30target' -or
    $patcherRaw -notmatch 'qcv30model' -or
    $patcherRaw -notmatch 'adapter:\"openai-compatible\"' -or
    $patcherRaw -notmatch 'StartupBYOKAnchor' -or
    $patcherRaw -notmatch 'ModelListAnchor' -or
    $patcherRaw -notmatch 'GetModelAnchor') {
    throw 'The v3.2.0 direct-route and startup-injection implementation is missing from the patcher source.'
}

Write-Host '[TEST] GUI syntax and self-test'
if (-not (Test-Path -LiteralPath $guiLauncher -PathType Leaf)) {
    throw 'The double-click GUI launcher is missing.'
}
$guiRaw = Get-Content -LiteralPath $gui -Raw -Encoding UTF8
$null = [ScriptBlock]::Create($guiRaw)
if ($guiRaw -notmatch 'Invoke-PatcherElevated' -or
    $guiRaw -notmatch 'Install / Upgrade' -or
    $guiRaw -notmatch 'Restore latest' -or
    $guiRaw -notmatch 'Edit Model' -or
    $guiRaw -notmatch 'Show-ModelEditorDialog') {
    throw 'The GUI does not expose the required patch operations or visual model editor.'
}
& $gui -SelfTest

Write-Host '[TEST] Native source compilation and engine behavior'
$csc = @(
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe',
    'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if ($null -eq $csc) { throw '.NET Framework csc.exe compiler not found.' }

$nativeTestRoot = Join-Path ([IO.Path]::GetTempPath()) ('qoder-native-build-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $nativeTestRoot -Force | Out-Null
try {
    $compiledApp = Join-Path $nativeTestRoot 'QoderCN-Patcher.exe'
    $nativeSources = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src-native') -Filter '*.cs' -File -Recurse | Select-Object -ExpandProperty FullName
    & $csc /nologo /target:winexe /platform:anycpu /optimize+ "/out:$compiledApp" `
        "/win32manifest:$(Join-Path $projectRoot 'src-native\app.manifest')" `
        "/win32icon:$(Join-Path $projectRoot 'src-native\app.ico')" `
        /r:System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll,System.Web.Extensions.dll,System.Security.dll `
        $nativeSources
    if ($LASTEXITCODE -ne 0) { throw 'Native application source compilation failed.' }

    $nativeTests = Join-Path $nativeTestRoot 'NativeEngineTests.exe'
    $nativeTestSources = @(
        (Join-Path $projectRoot 'src-native\PatcherEngine.cs'),
        (Join-Path $projectRoot 'src-native\ModelConfig.cs'),
        (Join-Path $projectRoot 'src-native\SecretStore.cs'),
        (Join-Path $projectRoot 'tests\NativeEngineTests.cs')
    )
    & $csc /nologo /target:exe /platform:anycpu "/out:$nativeTests" `
        /r:System.dll,System.Core.dll,System.Web.Extensions.dll,System.Security.dll $nativeTestSources
    if ($LASTEXITCODE -ne 0) { throw 'Native engine test compilation failed.' }
    & $nativeTests
    if ($LASTEXITCODE -ne 0) { throw 'Native engine behavior tests failed.' }
}
finally {
    if (Test-Path -LiteralPath $nativeTestRoot) {
        [GC]::Collect()
        [GC]::WaitForPendingFinalizers()
        Start-Sleep -Milliseconds 100
        Remove-Item -LiteralPath $nativeTestRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host '[TEST] Release executable metadata and signature state'
$nativeExe = Join-Path $projectRoot 'bin\QoderCN-Patcher.exe'
if (-not (Test-Path -LiteralPath $nativeExe -PathType Leaf)) {
    throw 'Native standalone executable bin\QoderCN-Patcher.exe was not created.'
}
$vi = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($nativeExe)
if ($vi.FileVersion -notmatch '^3\.' -or $vi.ProductName -notmatch 'Qoder CN Gateway Manager') {
    throw "Native binary metadata mismatch: FileVersion=$($vi.FileVersion), Product=$($vi.ProductName)"
}
$sig = Get-AuthenticodeSignature $nativeExe
if ($sig.Status -notin @('Valid', 'NotSigned')) {
    throw "Native binary has an invalid Authenticode signature: $($sig.Status) - $($sig.StatusMessage)"
}
if ($RequireSignedBinary -and $sig.Status -ne 'Valid') { throw 'Release binary must have a valid Authenticode signature.' }
Write-Host "  Native EXE verified: $($vi.FileDescription) v$($vi.FileVersion) (Signature: $($sig.Status))"

Write-Host '[TEST] JavaScript URL normalization helper'
$urlHelperMatch = [regex]::Match(
    $patcherRaw,
    'function qcv30base\(A\)\{try\{.*?\}catch\{\}\}',
    [Text.RegularExpressions.RegexOptions]::CultureInvariant
)
if (-not $urlHelperMatch.Success) {
    throw 'Could not extract qcv30base from the patcher source.'
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
  const actual = qcv30base(input);
  if (actual !== expected) throw new Error(`${input} => ${actual}; expected ${expected}`);
}
if (qcv30base("http://localhost:8000/v1?token=secret") !== undefined) {
  throw new Error("URL query strings must be rejected");
}
'@
        [IO.File]::WriteAllText($urlTestPath, $urlTestScript, [Text.UTF8Encoding]::new($false))
        & $bundledNode $urlTestPath
        if ($LASTEXITCODE -ne 0) {
            throw 'qcv30base behavior test failed.'
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
$verContent = (Get-Content -LiteralPath $versionFile -Raw).Trim()
if ($verContent -notmatch '^3\.') {
    throw "VERSION does not identify a v3 release (got: $verContent)."
}

Write-Host '[TEST] JSON configuration'
$raw = (Get-Content -LiteralPath $config -Raw -Encoding UTF8).Trim()
$raw = $raw -replace '^\uFEFF', ''
$null = $raw | ConvertFrom-Json

Write-Host '[TEST] Tracked profiles contain no API keys'
foreach ($jsonPath in @(git -C $projectRoot ls-files '*.json')) {
    $fullJsonPath = Join-Path $projectRoot $jsonPath
    try {
        $jsonObject = Get-Content -LiteralPath $fullJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($jsonObject.PSObject.Properties.Name -contains 'apiKey') {
            throw "Tracked JSON profile contains a forbidden apiKey property: $jsonPath"
        }
    }
    catch [System.Management.Automation.ItemNotFoundException] { }
}

Write-Host '[TEST] BYOK catalog and model settings schema validation'
$nodeCommand = Get-Command node -ErrorAction SilentlyContinue
$nodeExec = if (Test-Path -LiteralPath $bundledNode -PathType Leaf) { $bundledNode } elseif ($null -ne $nodeCommand) { $nodeCommand.Source } else { $null }
if ($nodeExec) {
    $catalogTestScript = @'
function Xg(A){return typeof A=="string"&&A.trim()&&A.trim().length<=512?A.trim():""}
function KpA(A){return Xg(A)||null}
function QU(A,e){return typeof A=="string"&&A.trim()&&A.trim().length<=512?A.trim():e}
function TO(A){if(typeof A!="string"||!A.trim()||A.length>2048)return null;try{const e=new URL(A);return e.protocol==="https:"?e.toString():null}catch{return null}}
function VK(A,e){return`${A??""}\0${e}`}
function HpA(A){const e=new Set;for(const t of A){if(t.mandatory===!1)continue;const i=Xg(t.key);i&&e.add(i)}return[...e].sort()}
function jpA(A,e){const t=[],i=new Set;for(const o of (Array.isArray(A)?A:[])){const n=Xg(o);!n||n.length>128||i.has(n)||(i.add(n),t.push(n))}return e&&!i.has("none")&&t.push("none"),t}
function ZpA(A,e,t){const i=Xg(A.key),o=Xg(A.format);if(!i||!o||!Number.isSafeInteger(A.max_input_tokens)||A.max_input_tokens<=0)return null;const n=jpA(A.efforts,A.supports_disabled===!0);return{summary:{key:i,displayName:QU(A.display_name,i),typeKey:e,typeDisplayName:t,isVision:A.is_vl===!0,isReasoning:A.is_reasoning===!0,format:o,maxInputTokens:A.max_input_tokens,efforts:n,supportsDisabled:A.supports_disabled===!0},style:o}}

function kO(A){const e=[],t=new Set;for(const i of A){const o=Xg(i.key);if(!o||t.has(o))continue;t.add(o);const n=QU(i.display_name,o),r=TO(i.url),s=typeof i.url=="string"&&i.url.trim().length>0,a=HpA(i.fields),c=a.length===1&&a[0]==="api_key",g=new Map,l=[];for(const Q of (i.types || [])){const d=KpA(Q.key),E=QU(Q.display_name,d??"");for(const B of (Q.models || [])){const p=ZpA(B,d,E);if(!p)continue;const h=VK(d,p.summary.key);g.has(h)||(g.set(h,p),l.push(p.summary))}}const I=c?s&&!r?"BYOK_PROVIDER_URL_UNSUPPORTED":l.length===0?"BYOK_PROVIDER_MODELS_UNAVAILABLE":null:"BYOK_PROVIDER_FIELDS_UNSUPPORTED";e.push({summary:{key:o,displayName:n,apiKeyUrl:TO(i.api_key_url),supported:I===null,unsupportedReason:I,models:l},...r?{url:r}:{},modelByIdentity:g})}return e}

const fs = require('fs');
const qcc = JSON.parse(fs.readFileSync(process.argv[2], 'utf8').replace(/^\uFEFF/,''));
let qcm = (qcc.models || []).map(q => ({
    key: q.id,
    display_name: q.displayName || q.id,
    is_vl: q.vision === true,
    is_reasoning: q.reasoning === true,
    format: "openai",
    max_input_tokens: Number.isInteger(q.maxInputTokens) && q.maxInputTokens > 0 ? q.maxInputTokens : 131072,
    efforts: Array.isArray(q.efforts) ? q.efforts : [],
    supports_disabled: q.supportsDisabled === true
}));
let provider = {
    key: qcc.replaceProviderKey || "anthropic",
    display_name: qcc.displayName || "Local OpenAI Compatible",
    api_key_url: "",
    url: qcc.uiBaseUrl,
    fields: [{ key: "api_key", display_name: "API Key", type: "free_input", mandatory: true }],
    types: [{ key: "openai-compatible", display_name: "OpenAI Compatible", style: "openai", models: qcm }]
};
const providers = kO([provider]);
const p = providers[0];
if (!p || !p.summary.supported || p.summary.models.length === 0) {
    throw new Error("BYOK catalog validation failed: " + JSON.stringify(p?.summary));
}
'@
    $catTestPath = Join-Path ([IO.Path]::GetTempPath()) ('qoder-patcher-catalog-test-' + [Guid]::NewGuid().ToString('N') + '.js')
    try {
        [IO.File]::WriteAllText($catTestPath, $catalogTestScript, [Text.UTF8Encoding]::new($false))
        & $nodeExec $catTestPath $config
        if ($LASTEXITCODE -ne 0) {
            throw 'BYOK schema test failed.'
        }
    }
    finally {
        if (Test-Path -LiteralPath $catTestPath) {
            Remove-Item -LiteralPath $catTestPath -Force
        }
    }
}

Write-Host '[TEST] Patcher DryRun'
$dryRunInstallDir = $InstallDir
$fixtureRoot = $null

$currentRuntimeSha = if (Test-Path -LiteralPath $installedRuntime) { (Get-FileHash -LiteralPath $installedRuntime -Algorithm SHA256).Hash } else { '' }
$isRuntimePatched = $installedRuntimeText -match 'QODER_CN_OAI_PATCH'

if (-not ($SupportedRuntimeSha256List -contains $currentRuntimeSha) -or $isRuntimePatched) {
    Write-Host '[INFO] Building a temporary original-runtime fixture for DryRun.'
    $backupRoot = Join-Path $env:LOCALAPPDATA 'QoderCNOpenAICompatiblePatcher\backups-v2'
    $originalText = $null

    if (Test-Path -LiteralPath $backupRoot -PathType Container) {
        $manifestFiles = Get-ChildItem -LiteralPath $backupRoot -Filter manifest.json -File -Recurse -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending
        foreach ($mf in $manifestFiles) {
            $manifest = Get-Content -LiteralPath $mf.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
            $mfHash = if (Test-Path -LiteralPath $manifest.runtimeBackup) { (Get-FileHash -LiteralPath $manifest.runtimeBackup -Algorithm SHA256).Hash } else { '' }
            if ($SupportedRuntimeSha256List -contains $mfHash) {
                $originalText = [IO.File]::ReadAllText($manifest.runtimeBackup, [Text.Encoding]::UTF8)
                break
            }
        }
    }

    if ($null -eq $originalText -and (Test-Path -LiteralPath $installedRuntime) -and -not $isRuntimePatched) {
        $originalText = $installedRuntimeText
    }

    if ($null -ne $originalText -and (Test-Path -LiteralPath $installedAsar)) {
        $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('qoder-patcher-project-test-' + [Guid]::NewGuid().ToString('N'))
        $fixtureRuntime = Join-Path $fixtureRoot $runtimeRelativePath
        $fixtureAsar = Join-Path $fixtureRoot 'resources\app.asar'
        New-Item -ItemType Directory -Path (Split-Path -Parent $fixtureRuntime) -Force | Out-Null
        [IO.File]::WriteAllText($fixtureRuntime, $originalText, [Text.UTF8Encoding]::new($false))
        Copy-Item -LiteralPath $installedAsar -Destination $fixtureAsar
        $dryRunInstallDir = $fixtureRoot
    }
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
        [GC]::Collect()
        [GC]::WaitForPendingFinalizers()
        Start-Sleep -Milliseconds 100
        Remove-Item -LiteralPath $resolvedFixture -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host '[OK] Project checks passed.' -ForegroundColor Green
