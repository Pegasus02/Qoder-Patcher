[CmdletBinding()]
param(
    [string]$InstallDir = 'C:\Program Files\Qoder\Qoder CN',
    [switch]$RequireSignedBinary
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
$ImportReplacement = 'import*as qcv30fs from"node:fs";import*as P8e from"node:path";import*as qxA from"node:fs/promises";'

$ConverterAnchor = 'function XxA(A){'
$ConverterReplacement = @'
function qcv30cfg(){try{let h=process.env.USERPROFILE||process.env.HOME||"";let p=process.env.QODER_CN_CUSTOM_PROVIDER_CONFIG||(h?h+"/.qoder-cn/custom-openai-provider-v3.1.0.json":"");if(!p||!qcv30fs.existsSync(p)){let a000=h?h+"/.qoder-cn/custom-openai-provider-v3.0.1.json":"";let a00=h?h+"/.qoder-cn/custom-openai-provider-v3.0.json":"";let a0=h?h+"/.qoder-cn/custom-openai-provider-v2.3.json":"";let a1=h?h+"/.qoder-cn/custom-openai-provider-v2.2.json":"";let a2=h?h+"/.qoder-cn/custom-openai-provider-v2.1.json":"";let a3=h?h+"/.qoder-cn/custom-openai-provider.json":"";p=a000&&qcv30fs.existsSync(a000)?a000:a00&&qcv30fs.existsSync(a00)?a00:a0&&qcv30fs.existsSync(a0)?a0:a1&&qcv30fs.existsSync(a1)?a1:a2&&qcv30fs.existsSync(a2)?a2:a3&&qcv30fs.existsSync(a3)?a3:p}let txt=p&&qcv30fs.existsSync(p)?qcv30fs.readFileSync(p,"utf8").replace(/^\uFEFF/,"").trim():"";return txt?JSON.parse(txt):null}catch(A){return null}}function qcv30base(A){try{let e=new URL(A);if("http:"!==e.protocol&&"https:"!==e.protocol||e.username||e.password||e.search||e.hash)return;let t=e.pathname.replace(/\/+$/g,"");return t.endsWith("/chat/completions")&&(t=t.slice(0,-17)),e.pathname=t||"/",e.toString().replace(/\/$/,"")}catch{}}function qcv30url(A){try{let e=qcv30cfg();if(e&&"string"==typeof A&&new URL(e.uiBaseUrl).toString()===new URL(A).toString())return qcv30base(e.upstreamBaseUrl)??A}catch{}return A}function qcv30model(A){try{let e=qcv30cfg();if(e&&Array.isArray(e.models)){let p=e.replaceProviderKey||"anthropic",k="string"==typeof A&&A.includes("/")?A.split("/").pop():A,m=e.models.find(e=>e&&(e.id===A||e.id===k||e.displayName===A));if(m)return XxA({key:m.id,display_name:m.displayName??m.id,provider:p,model:m.id,type:"openai-compatible",parameters:{api_key:""},url:e.uiBaseUrl,format:"openai",is_vl:!0===m.vision,is_reasoning:!0===m.reasoning,max_input_tokens:Number.isInteger(m.maxInputTokens)&&m.maxInputTokens>0?m.maxInputTokens:131072,efforts:Array.isArray(m.efforts)?m.efforts:[],supports_disabled:!0===m.supportsDisabled})}}catch(A){}}function qcv30target(A){let e=qcv30cfg();if(!e||!Array.isArray(e.models))return;let t=A?.custom_model,i=A?.model_config;let rawKey=t?.model||i?.key||A?.model||("string"==typeof A?A:"")||"";let k="string"==typeof rawKey&&rawKey.includes("/")?rawKey.split("/").pop():rawKey;let n=e.models.find(m=>m&&(m.id===rawKey||m.id===k||m.displayName===rawKey));if(!n)return;let r=process.env.QODER_CN_CUSTOM_PROVIDER_API_KEY||t?.parameters?.api_key,o=qcv30base(e.upstreamBaseUrl);if("string"!=typeof r||!r.trim())throw Error("QODER_CN_PATCH_API_KEY_MISSING");if(!o)throw Error("QODER_CN_PATCH_UPSTREAM_URL_INVALID");let s=Number.isInteger(e.firstPayloadTimeoutMs)&&e.firstPayloadTimeoutMs>0?e.firstPayloadTimeoutMs:6e4,a=Number.isInteger(e.streamIdleTimeoutMs)&&e.streamIdleTimeoutMs>=0?e.streamIdleTimeoutMs:0,g=Number.isInteger(n.maxInputTokens)&&n.maxInputTokens>0?n.maxInputTokens:131072,l=Number.isInteger(n.maxOutputTokens)&&n.maxOutputTokens>0?n.maxOutputTokens:32768;return{providerId:"qoder-cn-patcher",adapter:"openai-compatible",baseUrl:o,apiKey:r,model:{modelId:n.id,displayName:n.displayName??n.id,contextWindow:g,maxOutputTokens:l,capabilities:{tools:!1!==n.tools,vision:!0===n.vision,thinking:!0===n.reasoning},maxTokensField:"max_completion_tokens"===n.maxTokensField?"max_completion_tokens":"max_tokens"},timeouts:{firstPayloadTimeoutMs:s,...a>0?{streamIdleTimeoutMs:a}:{}}}}function XxA(A){/*QODER_CN_OAI_PATCH_V3_1_0*/
'@

$ModelUrlAnchor = 'url:A.url,model:A.model,provider:A.provider'
$ModelUrlReplacement = 'url:qcv30url(A.url),model:A.model,provider:A.provider'

$CatalogAnchor = 'n=await e().getBYOKConfig(),r=t;B(pn(i,"success",{providers:n?.providers.map(A)??[]}))'
$CatalogReplacement = @'
n=await e().getBYOKConfig(),r=t;let qcp=n?.providers.map(A)??[];if(qcp.length===0){qcp=[{key:"bailian",display_name:"Alibaba Cloud Model Studio",fields:[{key:"api_key",display_name:"API Key",type:"free_input",mandatory:true}],types:[{key:"bailian",display_name:"Alibaba Cloud Model Studio",style:"bailian",models:[]}]},{key:"deepseek",display_name:"DeepSeek",fields:[{key:"api_key",display_name:"API Key",type:"free_input",mandatory:true}],types:[{key:"deepseek",display_name:"DeepSeek",style:"deepseek",models:[]}]},{key:"moonshot",display_name:"Kimi",fields:[{key:"api_key",display_name:"API Key",type:"free_input",mandatory:true}],types:[{key:"moonshot",display_name:"Kimi",style:"moonshot",models:[]}]},{key:"zhipu",display_name:"Z.ai",fields:[{key:"api_key",display_name:"API Key",type:"free_input",mandatory:true}],types:[{key:"zhipu",display_name:"Z.ai",style:"zhipu",models:[]}]},{key:"minimax",display_name:"MiniMax",fields:[{key:"api_key",display_name:"API Key",type:"free_input",mandatory:true}],types:[{key:"minimax",display_name:"MiniMax",style:"minimax",models:[]}]},{key:"qwen",display_name:"QwenCloud-China",fields:[{key:"api_key",display_name:"API Key",type:"free_input",mandatory:true}],types:[{key:"qwen",display_name:"QwenCloud-China",style:"qwen",models:[]}]},{key:"xiaomi",display_name:"Xiaomi MIMO",fields:[{key:"api_key",display_name:"API Key",type:"free_input",mandatory:true}],types:[{key:"xiaomi",display_name:"Xiaomi MIMO",style:"xiaomi",models:[]}]}];}try{let qcc=qcv30cfg();if(qcc){let qpk=qcc.replaceProviderKey||"anthropic";let qci=qcp.findIndex(q=>q.key===qpk||q.display_name===qcc.replaceProviderDisplayName);if(qci<0&&Number.isInteger(qcc.replaceProviderIndex))qci=qcc.replaceProviderIndex;let qcb=qci>=0&&qci<qcp.length?qcp[qci]:null;let qcm=(qcc.models??[]).map(q=>({key:q.id,display_name:q.displayName??q.id,is_vl:q.vision===true,is_reasoning:q.reasoning===true,format:"openai",max_input_tokens:Number.isInteger(q.maxInputTokens)&&q.maxInputTokens>0?q.maxInputTokens:131072,efforts:Array.isArray(q.efforts)?q.efforts:[],supports_disabled:q.supportsDisabled===true}));if(qcm.length>0){let customProvider={...qcb,key:qpk,display_name:qcc.displayName??"Local OpenAI Compatible",api_key_url:"",url:qcc.uiBaseUrl,fields:[{key:"api_key",display_name:"API Key",type:"free_input",mandatory:true}],types:[{key:"openai-compatible",display_name:"OpenAI Compatible",style:"openai",models:qcm}]};if(qci>=0&&qci<qcp.length){qcp[qci]=customProvider}else{qcp.unshift(customProvider)}}}}catch(qce){Q.warn("[qoder-cn-openai-patch-v3.1.0] custom provider not loaded:",qce)}B(pn(i,"success",{enabled:true,providers:qcp}))
'@

$ValidationAnchor = 'o=await r().checkBYOKModel(n.provider,n.model,{api_key:n.api_key},A,e,t);B(pn(i,"success",{success:o}))'
$ValidationReplacement = @'
o=await(async()=>{try{let qcv=qcv30cfg();if(qcv&&qcv.skipValidation!==false){let modelMatch=Array.isArray(qcv.models)&&qcv.models.some(m=>m&&m.id===n.model);let providerMatch=n.provider===(qcv.replaceProviderKey||"anthropic");let urlMatch=false;if(A&&typeof A==="string"&&qcv.uiBaseUrl){try{urlMatch=new URL(qcv.uiBaseUrl).toString()===new URL(A).toString()}catch{}}if(modelMatch||providerMatch||urlMatch)return true}}catch(qce){Q.warn("[qoder-cn-openai-patch-v3.1.0] validation override unavailable:",qce)}return r().checkBYOKModel(n.provider,n.model,{api_key:n.api_key},A,e,t)})();B(pn(i,"success",{success:o}))
'@

$InferenceRouteAnchor = 'let A=ZVe(t.model_config,()=>S?.getExternalProviderRegistry());for(;;)'
$InferenceRouteReplacement = 'let A=qcv30target(t)??ZVe(t.model_config,()=>S?.getExternalProviderRegistry());for(;;)'

$StartupBYOKAnchor = 'async function n$A(A,e){let t=e?await y8e(e):[],i=A.getSettingsBYOKModels(),n=new Map(t.map(A=>[A.key,A]));for(let A of i)n.set(A.key,A);let r=[...n.values()];'
$StartupBYOKReplacement = @'
async function n$A(A,e){let t=e?await y8e(e):[],i=A.getSettingsBYOKModels(),n=new Map(t.map(A=>[A.key,A]));for(let A of i)n.set(A.key,A);try{let qcc=qcv30cfg();if(qcc&&Array.isArray(qcc.models)){let qpk=qcc.replaceProviderKey||"anthropic";for(let m of qcc.models){if(m&&m.id){let item={key:m.id,display_name:m.displayName??m.id,provider:qpk,model:m.id,type:"openai-compatible",parameters:{api_key:""},url:qcc.uiBaseUrl,format:"openai",is_vl:!0===m.vision,is_reasoning:!0===m.reasoning,max_input_tokens:Number.isInteger(m.maxInputTokens)&&m.maxInputTokens>0?m.maxInputTokens:131072,efforts:Array.isArray(m.efforts)?m.efforts:[],supports_disabled:!0===m.supportsDisabled};n.has(m.id)||n.set(m.id,item)}}}}catch(qce){Q.warn("[qoder-cn-openai-patch-v3.1.0] startup custom models injection failed:",qce)}let r=[...n.values()];
'@

$ModelListAnchor = 'function Zdo(A,e,t){let i="function"==typeof t.getBYOKCustomModels?t.getBYOKCustomModels()??[]:[];'
$ModelListReplacement = @'
function Zdo(A,e,t){let i="function"==typeof t.getBYOKCustomModels?t.getBYOKCustomModels()??[]:[];try{let qcc=qcv30cfg();if(qcc&&Array.isArray(qcc.models)){let qpk=qcc.replaceProviderKey||"anthropic";for(let m of qcc.models){if(m&&m.id){let item={key:m.id,display_name:m.displayName??m.id,provider:qpk,model:m.id,type:"openai-compatible",parameters:{api_key:""},url:qcc.uiBaseUrl,format:"openai",is_vl:!0===m.vision,is_reasoning:!0===m.reasoning,max_input_tokens:Number.isInteger(m.maxInputTokens)&&m.maxInputTokens>0?m.maxInputTokens:131072,efforts:Array.isArray(m.efforts)?m.efforts:[],supports_disabled:!0===m.supportsDisabled};i.some(x=>x.key===m.id)||i.push(item)}}}}catch(qce){Q.warn("[qoder-cn-openai-patch-v3.1.0] Zdo injection failed:",qce)}
'@

$GetModelAnchor = 'getModel(A){return this.allModels.get(A)??this.getModelCaseInsensitive(A)??this.getModelByDisplayName(A)}'
$GetModelReplacement = 'getModel(A){return this.allModels.get(A)??this.getModelCaseInsensitive(A)??this.getModelByDisplayName(A)??qcv30model(A)}'

Write-Host '[TEST] PowerShell syntax'
$patcherRaw = Get-Content -LiteralPath $patcher -Raw -Encoding UTF8
$null = [ScriptBlock]::Create($patcherRaw)
if ($patcherRaw -notmatch 'QODER_CN_OAI_PATCH_V3_1_0' -or
    $patcherRaw -notmatch 'qcv30target' -or
    $patcherRaw -notmatch 'qcv30model' -or
    $patcherRaw -notmatch 'adapter:\"openai-compatible\"' -or
    $patcherRaw -notmatch 'StartupBYOKAnchor' -or
    $patcherRaw -notmatch 'ModelListAnchor' -or
    $patcherRaw -notmatch 'GetModelAnchor') {
    throw 'The v3.1.0 direct-route and startup-injection implementation is missing from the patcher source.'
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
if ($vi.FileVersion -ne '3.2.0.0' -or $vi.ProductName -ne 'Qoder CN Gateway Manager') {
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
if ($verContent -notmatch '^3\.2\.0') {
    throw "VERSION does not identify the v3.2.0 release (got: $verContent)."
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
        throw 'No original runtime backup is available for fixture testing.'
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

Write-Host '[OK] Project checks passed.' -ForegroundColor Green
