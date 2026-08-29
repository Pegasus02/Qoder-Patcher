[CmdletBinding()]
param(
    [ValidateSet('Inspect', 'DryRun', 'Apply', 'Restore')]
    [string]$Action = 'Inspect',

    [string]$InstallDir = 'C:\Program Files\Qoder\Qoder CN',

    [string]$ConfigPath = '',

    [string]$RuntimeConfigPath = (Join-Path $env:USERPROFILE '.qoder-cn\custom-openai-provider-v3.1.0.json'),

    [string]$BackupRoot = (Join-Path $env:LOCALAPPDATA 'QoderCNOpenAICompatiblePatcher\backups-v2'),

    [string]$BackupId,

    [string]$NodePath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$PatchMarker = 'QODER_CN_OAI_PATCH_V3_1_0'
$PreviousPatchMarkers = @('QODER_CN_OAI_PATCH_V3_0_1', 'QODER_CN_OAI_PATCH_V3_0', 'QODER_CN_OAI_PATCH_V2_3', 'QODER_CN_OAI_PATCH_V2_2', 'QODER_CN_OAI_PATCH_V2_1', 'QODER_CN_OAI_PATCH_V2*/')
$LegacyPatchMarker = 'QODER_CN_OAI_PATCH_V1'
$RuntimeRelativePath = 'resources\app.asar.unpacked\node_modules\@qoder-ai\qoder-cn-agent-sdk\dist\_worker\qoder-worker-runtime.obf.mjs'
$AsarRelativePath = 'resources\app.asar'
$SupportedRuntimeSha256 = '7348879d488dc22cca1fc8138c3182233637f78bea210652701b3463b6d3f655'
$SupportedAsarSha256 = '8f7429f5e0efd4850663fae438cf1340feda7e86ec2392d0d7820ee22699a941'

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'configs\cpa-192.168.50.241.json'
}

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
n=await e().getBYOKConfig(),r=t;let qcp=n?.providers.map(A)??[];if(qcp.length===0){qcp=[{key:"bailian",display_name:"Alibaba Cloud Model Studio",fields:[{key:"api_key",display_name:"API Key",type:"free_input",mandatory:true}],types:[{key:"bailian",display_name:"Alibaba Cloud Model Studio",style:"bailian",models:[]}]},{key:"deepseek",display_name:"DeepSeek",fields:[{key:"api_key",display_name:"API Key",type:"free_input",mandatory:true}],types:[{key:"deepseek",display_name:"DeepSeek",style:"deepseek",models:[]}]},{key:"moonshot",display_name:"Kimi",fields:[{key:"api_key",display_name:"API Key",type:"free_input",mandatory:true}],types:[{key:"moonshot",display_name:"Kimi",style:"moonshot",models:[]}]},{key:"zhipu",display_name:"Z.ai",fields:[{key:"api_key",display_name:"API Key",type:"free_input",mandatory:true}],types:[{key:"zhipu",display_name:"Z.ai",style:"zhipu",models:[]}]},{key:"minimax",display_name:"MiniMax",fields:[{key:"api_key",display_name:"API Key",type:"free_input",mandatory:true}],types:[{key:"minimax",display_name:"MiniMax",style:"minimax",models:[]}]},{key:"qwen",display_name:"QwenCloud-China",fields:[{key:"api_key",display_name:"API Key",type:"free_input",mandatory:true}],types:[{key:"qwen",display_name:"QwenCloud-China",style:"qwen",models:[]}]},{key:"xiaomi",display_name:"Xiaomi MIMO",fields:[{key:"api_key",display_name:"API Key",type:"free_input",mandatory:true}],types:[{key:"xiaomi",display_name:"Xiaomi MIMO",style:"xiaomi",models:[]}]}];}try{let qcc=qcv30cfg();if(qcc){let qpk=qcc.replaceProviderKey||"anthropic";let qci=qcp.findIndex(q=>q.key===qpk||q.display_name===qcc.replaceProviderDisplayName);if(qci<0&&Number.isInteger(qcc.replaceProviderIndex))qci=qcc.replaceProviderIndex;let qcb=qci>=0&&qci<qcp.length?qcp[qci]:null;let qcm=(qcc.models??[]).map(q=>({key:q.id,display_name:q.displayName??q.id,is_vl:q.vision===true,is_reasoning:q.reasoning===true,format:"openai",max_input_tokens:Number.isInteger(q.maxInputTokens)&&q.maxInputTokens>0?q.maxInputTokens:131072,efforts:Array.isArray(q.efforts)?q.efforts:[],supports_disabled:q.supportsDisabled===true}));if(qcm.length>0){let customProvider={...qcb,key:qpk,display_name:qcc.displayName??"Local OpenAI Compatible",api_key_url:"",url:qcc.uiBaseUrl,fields:[{key:"api_key",display_name:"API Key",type:"free_input",mandatory:true}],types:[{key:"openai-compatible",display_name:"OpenAI Compatible",style:"openai",models:qcm}]};if(qci>=0&&qci<qcp.length){qcp[qci]=customProvider}else{qcp.unshift(customProvider)}}}}catch(qce){Q.warn("[qoder-cn-openai-patch-v3.0.1] custom provider not loaded:",qce)}B(pn(i,"success",{enabled:true,providers:qcp}))
'@

$ValidationAnchor = 'o=await r().checkBYOKModel(n.provider,n.model,{api_key:n.api_key},A,e,t);B(pn(i,"success",{success:o}))'
$ValidationReplacement = @'
o=await(async()=>{try{let qcv=qcv30cfg();if(qcv&&qcv.skipValidation!==false){let modelMatch=Array.isArray(qcv.models)&&qcv.models.some(m=>m&&m.id===n.model);let providerMatch=n.provider===(qcv.replaceProviderKey||"anthropic");let urlMatch=false;if(A&&typeof A==="string"&&qcv.uiBaseUrl){try{urlMatch=new URL(qcv.uiBaseUrl).toString()===new URL(A).toString()}catch{}}if(modelMatch||providerMatch||urlMatch)return true}}catch(qce){Q.warn("[qoder-cn-openai-patch-v3.0.1] validation override unavailable:",qce)}return r().checkBYOKModel(n.provider,n.model,{api_key:n.api_key},A,e,t)})();B(pn(i,"success",{success:o}))
'@

$InferenceRouteAnchor = 'let A=ZVe(t.model_config,()=>S?.getExternalProviderRegistry());for(;;)'
$InferenceRouteReplacement = 'let A=qcv30target(t)??ZVe(t.model_config,()=>S?.getExternalProviderRegistry());for(;;)'

$StartupBYOKAnchor = 'async function n$A(A,e){let t=e?await y8e(e):[],i=A.getSettingsBYOKModels(),n=new Map(t.map(A=>[A.key,A]));for(let A of i)n.set(A.key,A);let r=[...n.values()];'
$StartupBYOKReplacement = @'
async function n$A(A,e){let t=e?await y8e(e):[],i=A.getSettingsBYOKModels(),n=new Map(t.map(A=>[A.key,A]));for(let A of i)n.set(A.key,A);try{let qcc=qcv30cfg();if(qcc&&Array.isArray(qcc.models)){let qpk=qcc.replaceProviderKey||"anthropic";for(let m of qcc.models){if(m&&m.id){let item={key:m.id,display_name:m.displayName??m.id,provider:qpk,model:m.id,type:"openai-compatible",parameters:{api_key:""},url:qcc.uiBaseUrl,format:"openai",is_vl:!0===m.vision,is_reasoning:!0===m.reasoning,max_input_tokens:Number.isInteger(m.maxInputTokens)&&m.maxInputTokens>0?m.maxInputTokens:131072,efforts:Array.isArray(m.efforts)?m.efforts:[],supports_disabled:!0===m.supportsDisabled};n.has(m.id)||n.set(m.id,item)}}}}catch(qce){Q.warn("[qoder-cn-openai-patch-v3.0.1] startup custom models injection failed:",qce)}let r=[...n.values()];
'@

$ModelListAnchor = 'function Zdo(A,e,t){let i="function"==typeof t.getBYOKCustomModels?t.getBYOKCustomModels()??[]:[];'
$ModelListReplacement = @'
function Zdo(A,e,t){let i="function"==typeof t.getBYOKCustomModels?t.getBYOKCustomModels()??[]:[];try{let qcc=qcv30cfg();if(qcc&&Array.isArray(qcc.models)){let qpk=qcc.replaceProviderKey||"anthropic";for(let m of qcc.models){if(m&&m.id){let item={key:m.id,display_name:m.displayName??m.id,provider:qpk,model:m.id,type:"openai-compatible",parameters:{api_key:""},url:qcc.uiBaseUrl,format:"openai",is_vl:!0===m.vision,is_reasoning:!0===m.reasoning,max_input_tokens:Number.isInteger(m.maxInputTokens)&&m.maxInputTokens>0?m.maxInputTokens:131072,efforts:Array.isArray(m.efforts)?m.efforts:[],supports_disabled:!0===m.supportsDisabled};i.some(x=>x.key===m.id)||i.push(item)}}}}catch(qce){Q.warn("[qoder-cn-openai-patch-v3.0.1] Zdo injection failed:",qce)}
'@

$GetModelAnchor = 'getModel(A){return this.allModels.get(A)??this.getModelCaseInsensitive(A)??this.getModelByDisplayName(A)}'
$GetModelReplacement = 'getModel(A){return this.allModels.get(A)??this.getModelCaseInsensitive(A)??this.getModelByDisplayName(A)??qcv30model(A)}'

function Write-Info([string]$Message) {
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

function Write-Success([string]$Message) {
    Write-Host "[OK]   $Message" -ForegroundColor Green
}

function Get-FileSha256Hex([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-PropertyValue([object]$Object, [string]$Name, $DefaultValue = $null) {
    if ($null -eq $Object) {
        return $DefaultValue
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $DefaultValue
    }
    return $property.Value
}

function Get-OccurrenceCount([string]$Text, [string]$Value) {
    if ([string]::IsNullOrEmpty($Value)) {
        return 0
    }
    $count = 0
    $offset = 0
    while (($index = $Text.IndexOf($Value, $offset, [StringComparison]::Ordinal)) -ge 0) {
        $count++
        $offset = $index + $Value.Length
    }
    return $count
}

function Get-NodeExecutable {
    if (-not [string]::IsNullOrWhiteSpace($NodePath)) {
        if (-not (Test-Path -LiteralPath $NodePath -PathType Leaf)) {
            throw "Node.js executable not found: $NodePath"
        }
        return [IO.Path]::GetFullPath($NodePath)
    }
    $command = Get-Command node -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }
    return $null
}

function Replace-ExactlyOnce([string]$Text, [string]$OldValue, [string]$NewValue, [string]$Description) {
    $count = Get-OccurrenceCount $Text $OldValue
    if ($count -ne 1) {
        throw "$Description anchor count is $count; expected exactly one. This Qoder CN version is not supported."
    }
    return $Text.Replace($OldValue, $NewValue)
}

function Test-Configuration([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Configuration file not found: $Path"
    }
    $raw = (Get-Content -LiteralPath $Path -Raw -Encoding UTF8).Trim()
    $raw = $raw -replace '^\uFEFF', ''
    $config = $raw | ConvertFrom-Json

    $uiBaseUrl = [string](Get-PropertyValue $config 'uiBaseUrl')
    $upstreamBaseUrl = [string](Get-PropertyValue $config 'upstreamBaseUrl')
    if ([string]::IsNullOrWhiteSpace($uiBaseUrl)) {
        throw 'uiBaseUrl is required.'
    }
    if ([string]::IsNullOrWhiteSpace($upstreamBaseUrl)) {
        throw 'upstreamBaseUrl is required.'
    }
    $uiUri = $null
    $upstreamUri = $null
    if (-not [Uri]::TryCreate($uiBaseUrl, [UriKind]::Absolute, [ref]$uiUri) -or $uiUri.Scheme -ne 'https') {
        throw 'uiBaseUrl must be an absolute HTTPS URL accepted by the unmodified Qoder desktop app.'
    }
    if (-not [Uri]::TryCreate($upstreamBaseUrl, [UriKind]::Absolute, [ref]$upstreamUri) -or $upstreamUri.Scheme -notin @('http', 'https')) {
        throw 'upstreamBaseUrl must be an absolute HTTP or HTTPS URL.'
    }
    foreach ($uriInfo in @(
        [pscustomobject]@{ Name = 'uiBaseUrl'; Uri = $uiUri },
        [pscustomobject]@{ Name = 'upstreamBaseUrl'; Uri = $upstreamUri }
    )) {
        if (-not [string]::IsNullOrEmpty($uriInfo.Uri.UserInfo) -or
            -not [string]::IsNullOrEmpty($uriInfo.Uri.Query) -or
            -not [string]::IsNullOrEmpty($uriInfo.Uri.Fragment)) {
            throw "$($uriInfo.Name) must not contain credentials, a query string, or a fragment."
        }
    }

    foreach ($timeoutName in @('firstPayloadTimeoutMs', 'streamIdleTimeoutMs')) {
        $timeout = Get-PropertyValue $config $timeoutName
        if ($null -ne $timeout) {
            $parsedTimeout = 0L
            if (-not [int64]::TryParse([string]$timeout, [ref]$parsedTimeout) -or
                ($timeoutName -eq 'firstPayloadTimeoutMs' -and $parsedTimeout -le 0) -or
                ($timeoutName -eq 'streamIdleTimeoutMs' -and $parsedTimeout -lt 0)) {
                throw "$timeoutName has an invalid value."
            }
        }
    }

    $models = @(Get-PropertyValue $config 'models')
    if ($models.Count -eq 0) {
        throw 'At least one model is required.'
    }
    $ids = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($model in $models) {
        $id = [string](Get-PropertyValue $model 'id')
        if ([string]::IsNullOrWhiteSpace($id)) {
            throw 'Every model requires a non-empty id.'
        }
        if (-not $ids.Add($id)) {
            throw "Duplicate model id: $id"
        }
        $maxInputTokens = Get-PropertyValue $model 'maxInputTokens'
        if ($null -ne $maxInputTokens -and [int64]$maxInputTokens -le 0) {
            throw "maxInputTokens must be positive for model: $id"
        }
        $maxOutputTokens = Get-PropertyValue $model 'maxOutputTokens'
        if ($null -ne $maxOutputTokens -and [int64]$maxOutputTokens -le 0) {
            throw "maxOutputTokens must be positive for model: $id"
        }
        $maxTokensField = [string](Get-PropertyValue $model 'maxTokensField')
        if (-not [string]::IsNullOrWhiteSpace($maxTokensField) -and
            $maxTokensField -notin @('max_tokens', 'max_completion_tokens')) {
            throw "maxTokensField must be max_tokens or max_completion_tokens for model: $id"
        }
    }

    $replaceProviderIndex = Get-PropertyValue $config 'replaceProviderIndex'
    $replaceProviderKey = [string](Get-PropertyValue $config 'replaceProviderKey')
    $replaceProviderDisplayName = [string](Get-PropertyValue $config 'replaceProviderDisplayName')
    if ($null -eq $replaceProviderIndex -and
        [string]::IsNullOrWhiteSpace($replaceProviderKey) -and
        [string]::IsNullOrWhiteSpace($replaceProviderDisplayName)) {
        throw 'Set replaceProviderIndex, replaceProviderKey, or replaceProviderDisplayName.'
    }
    if ($null -ne $replaceProviderIndex -and [int]$replaceProviderIndex -lt 0) {
        throw 'replaceProviderIndex cannot be negative.'
    }
    return $config
}

function Get-TargetState([string]$RuntimePath, [string]$AsarPath) {
    if (-not (Test-Path -LiteralPath $RuntimePath -PathType Leaf)) {
        throw "Qoder worker runtime not found: $RuntimePath"
    }
    if (-not (Test-Path -LiteralPath $AsarPath -PathType Leaf)) {
        throw "Qoder app.asar not found: $AsarPath"
    }
    $text = [IO.File]::ReadAllText($RuntimePath, [Text.Encoding]::UTF8)
    $asarSha256 = Get-FileSha256Hex $AsarPath
    $runtimePatched = $text.IndexOf($PatchMarker, [StringComparison]::Ordinal) -ge 0
    $previousPatched = $false
    foreach ($m in $PreviousPatchMarkers) {
        if (-not $runtimePatched -and $text.IndexOf($m, [StringComparison]::Ordinal) -ge 0) {
            $previousPatched = $true
            break
        }
    }
    return [pscustomobject]@{
        RuntimeSha256 = Get-FileSha256Hex $RuntimePath
        AsarSha256 = $asarSha256
        RuntimePatched = $runtimePatched
        PreviousRuntimePatched = $previousPatched
        LegacyRuntimePatched = $text.IndexOf($LegacyPatchMarker, [StringComparison]::Ordinal) -ge 0
        ImportAnchorCount = Get-OccurrenceCount $text $ImportAnchor
        ConverterAnchorCount = Get-OccurrenceCount $text $ConverterAnchor
        ModelUrlAnchorCount = Get-OccurrenceCount $text $ModelUrlAnchor
        CatalogAnchorCount = Get-OccurrenceCount $text $CatalogAnchor
        ValidationAnchorCount = Get-OccurrenceCount $text $ValidationAnchor
        InferenceRouteAnchorCount = Get-OccurrenceCount $text $InferenceRouteAnchor
        StartupBYOKAnchorCount = Get-OccurrenceCount $text $StartupBYOKAnchor
        ModelListAnchorCount = Get-OccurrenceCount $text $ModelListAnchor
        GetModelAnchorCount = Get-OccurrenceCount $text $GetModelAnchor
        AppAsarUnmodified = $asarSha256 -eq $SupportedAsarSha256
    }
}

function Assert-QoderClosed([string]$Root) {
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    $running = Get-Process -ErrorAction SilentlyContinue | Where-Object {
        try {
            $_.Path -and ([IO.Path]::GetFullPath($_.Path).StartsWith($rootFull, [StringComparison]::OrdinalIgnoreCase))
        }
        catch {
            $false
        }
    }
    if ($running) {
        throw 'Close Qoder CN before applying or restoring the v2 patch.'
    }
}

function Patch-Runtime([string]$SourcePath, [string]$DestinationPath) {
    $text = [IO.File]::ReadAllText($SourcePath, [Text.Encoding]::UTF8)
    if ($text.IndexOf($PatchMarker, [StringComparison]::Ordinal) -ge 0) {
        throw 'The v3.0.1 runtime patch is already installed.'
    }
    foreach ($m in $PreviousPatchMarkers) {
        if ($text.IndexOf($m, [StringComparison]::Ordinal) -ge 0) {
            throw 'An older v2.x runtime patch is present. Upgrade must start from its verified original backup.'
        }
    }
    if ($text.IndexOf($LegacyPatchMarker, [StringComparison]::Ordinal) -ge 0) {
        throw 'The legacy v1 runtime patch is present. Restore v1 before applying v3.0.1.'
    }
    $text = Replace-ExactlyOnce $text $ImportAnchor $ImportReplacement 'node:fs import'
    $text = Replace-ExactlyOnce $text $ConverterAnchor $ConverterReplacement 'BYOK model converter'
    $text = Replace-ExactlyOnce $text $ModelUrlAnchor $ModelUrlReplacement 'BYOK inference URL'
    $text = Replace-ExactlyOnce $text $CatalogAnchor $CatalogReplacement 'BYOK catalog'
    $text = Replace-ExactlyOnce $text $ValidationAnchor $ValidationReplacement 'BYOK validation'
    $text = Replace-ExactlyOnce $text $InferenceRouteAnchor $InferenceRouteReplacement 'direct inference route'
    $text = Replace-ExactlyOnce $text $StartupBYOKAnchor $StartupBYOKReplacement 'startup BYOK injection'
    $text = Replace-ExactlyOnce $text $ModelListAnchor $ModelListReplacement 'model list injection'
    $text = Replace-ExactlyOnce $text $GetModelAnchor $GetModelReplacement 'catalog getModel resolution'
    [IO.File]::WriteAllText($DestinationPath, $text, [Text.UTF8Encoding]::new($false))
}

function New-Backup([string]$RuntimePath, [string]$AsarPath) {
    $id = (Get-Date).ToString('yyyyMMdd-HHmmss') + '-' + ([Guid]::NewGuid().ToString('N').Substring(0, 8))
    $dir = Join-Path $BackupRoot $id
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $runtimeBackup = Join-Path $dir 'qoder-worker-runtime.obf.mjs'
    Copy-Item -LiteralPath $RuntimePath -Destination $runtimeBackup
    $manifest = [ordered]@{
        backupId = $id
        createdAt = (Get-Date).ToString('o')
        installDir = [IO.Path]::GetFullPath($InstallDir)
        runtimePath = [IO.Path]::GetFullPath($RuntimePath)
        runtimeBackup = $runtimeBackup
        runtimeSha256 = Get-FileSha256Hex $RuntimePath
        appAsarPath = [IO.Path]::GetFullPath($AsarPath)
        appAsarSha256 = Get-FileSha256Hex $AsarPath
        patchVersion = '3.0.1'
    }
    $manifestPath = Join-Path $dir 'manifest.json'
    [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
    return [pscustomobject]@{ Id = $id; Directory = $dir; ManifestPath = $manifestPath }
}

function Get-BackupManifest {
    if (-not (Test-Path -LiteralPath $BackupRoot -PathType Container)) {
        throw "No v2/v3 backups found in: $BackupRoot"
    }
    if (-not [string]::IsNullOrWhiteSpace($BackupId)) {
        if ([IO.Path]::GetFileName($BackupId) -ne $BackupId) {
            throw 'Invalid backup identifier.'
        }
        $path = Join-Path (Join-Path $BackupRoot $BackupId) 'manifest.json'
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Backup not found: $BackupId"
        }
        $specificManifest = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
        if ([IO.Path]::GetFullPath([string]$specificManifest.installDir).TrimEnd('\') -ne [IO.Path]::GetFullPath($InstallDir).TrimEnd('\')) {
            throw 'The selected backup belongs to a different Qoder CN installation.'
        }
        return $specificManifest
    }
    $manifestFiles = Get-ChildItem -LiteralPath $BackupRoot -Filter manifest.json -File -Recurse |
        Sort-Object LastWriteTime -Descending
    foreach ($manifestFile in $manifestFiles) {
        try {
            $candidate = Get-Content -LiteralPath $manifestFile.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
            if ([IO.Path]::GetFullPath([string]$candidate.installDir).TrimEnd('\') -eq [IO.Path]::GetFullPath($InstallDir).TrimEnd('\')) {
                return $candidate
            }
        }
        catch { }
    }
    throw "No backup belongs to this Qoder CN installation: $InstallDir"
}

function Get-OriginalRuntimeBackupManifest {
    if (-not (Test-Path -LiteralPath $BackupRoot -PathType Container)) {
        throw "No runtime backups found in: $BackupRoot"
    }
    $manifests = Get-ChildItem -LiteralPath $BackupRoot -Filter manifest.json -File -Recurse |
        Sort-Object LastWriteTime -Descending
    foreach ($manifestFile in $manifests) {
        try {
            $manifest = Get-Content -LiteralPath $manifestFile.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
            if ([IO.Path]::GetFullPath([string]$manifest.installDir).TrimEnd('\') -eq [IO.Path]::GetFullPath($InstallDir).TrimEnd('\') -and
                (Test-Path -LiteralPath $manifest.runtimeBackup -PathType Leaf) -and
                (Get-FileSha256Hex ([string]$manifest.runtimeBackup)) -eq $SupportedRuntimeSha256) {
                return $manifest
            }
        }
        catch {
            Write-Host "[WARN] Ignoring invalid backup manifest: $($manifestFile.FullName)" -ForegroundColor Yellow
        }
    }
    throw 'No verified original runtime backup is available for the upgrade.'
}

function Install-RuntimePatch([string]$RuntimePath) {
    $temp = "$RuntimePath.qoder-openai-patch-v3.0.tmp"
    try {
        Patch-Runtime $RuntimePath $temp
        Move-Item -LiteralPath $temp -Destination $RuntimePath -Force
    }
    finally {
        if (Test-Path -LiteralPath $temp) {
            Remove-Item -LiteralPath $temp -Force
        }
    }
}

function Upgrade-V2RuntimePatch([string]$RuntimePath) {
    $manifest = Get-OriginalRuntimeBackupManifest
    $patchedTemp = "$RuntimePath.qoder-openai-patch-v3.0.tmp"
    $rollbackTemp = "$RuntimePath.qoder-openai-patch-v2.rollback"
    try {
        Copy-Item -LiteralPath $RuntimePath -Destination $rollbackTemp -Force
        Patch-Runtime ([string]$manifest.runtimeBackup) $patchedTemp
        Move-Item -LiteralPath $patchedTemp -Destination $RuntimePath -Force
        $installedText = [IO.File]::ReadAllText($RuntimePath, [Text.Encoding]::UTF8)
        if ($installedText.IndexOf($PatchMarker, [StringComparison]::Ordinal) -lt 0) {
            throw 'The upgraded Runtime does not contain the v3.0.1 marker.'
        }
    }
    catch {
        if (Test-Path -LiteralPath $rollbackTemp -PathType Leaf) {
            Copy-Item -LiteralPath $rollbackTemp -Destination $RuntimePath -Force
        }
        throw
    }
    finally {
        foreach ($tempPath in @($patchedTemp, $rollbackTemp)) {
            if (Test-Path -LiteralPath $tempPath) {
                Remove-Item -LiteralPath $tempPath -Force
            }
        }
    }
    return [string]$manifest.backupId
}

$runtimePath = Join-Path $InstallDir $RuntimeRelativePath
$asarPath = Join-Path $InstallDir $AsarRelativePath

switch ($Action) {
    'Inspect' {
        $state = Get-TargetState $runtimePath $asarPath
        Write-Host ''
        Write-Host 'Qoder CN OpenAI-compatible runtime patch v3.0.1' -ForegroundColor White
        Write-Host "  Install directory   : $([IO.Path]::GetFullPath($InstallDir))"
        Write-Host "  Runtime patched     : $($state.RuntimePatched)"
        Write-Host "  v2.x patch present  : $($state.PreviousRuntimePatched)"
        Write-Host "  v1 patch present    : $($state.LegacyRuntimePatched)"
        Write-Host "  app.asar untouched  : $($state.AppAsarUnmodified)"
        Write-Host "  Runtime SHA-256     : $($state.RuntimeSha256)"
        Write-Host "  app.asar SHA-256    : $($state.AsarSha256)"
        Write-Host "  Required anchors    : import=$($state.ImportAnchorCount), converter=$($state.ConverterAnchorCount), url=$($state.ModelUrlAnchorCount), catalog=$($state.CatalogAnchorCount), validation=$($state.ValidationAnchorCount), route=$($state.InferenceRouteAnchorCount), startup=$($state.StartupBYOKAnchorCount), modelList=$($state.ModelListAnchorCount), getModel=$($state.GetModelAnchorCount)"
        if (Test-Path -LiteralPath $RuntimeConfigPath -PathType Leaf) {
            try {
                $null = Test-Configuration $RuntimeConfigPath
                Write-Host "  Runtime config      : valid ($RuntimeConfigPath)"
            }
            catch {
                Write-Host "  Runtime config      : invalid ($($_.Exception.Message))" -ForegroundColor Yellow
            }
        }
        else {
            Write-Host "  Runtime config      : not installed ($RuntimeConfigPath)"
        }
    }

    'DryRun' {
        $null = Test-Configuration $ConfigPath
        $state = Get-TargetState $runtimePath $asarPath
        if ($state.RuntimePatched -or $state.PreviousRuntimePatched -or $state.LegacyRuntimePatched) {
            throw 'A runtime patch is already present; DryRun expects the original runtime.'
        }
        if ($state.RuntimeSha256 -ne $SupportedRuntimeSha256 -or -not $state.AppAsarUnmodified) {
            throw 'This Qoder CN build does not match the tested 0.1.2 / runtime 1.1.31 baseline.'
        }
        if ($state.ImportAnchorCount -ne 1 -or $state.ConverterAnchorCount -ne 1 -or
            $state.ModelUrlAnchorCount -ne 1 -or $state.CatalogAnchorCount -ne 1 -or
            $state.ValidationAnchorCount -ne 1 -or $state.InferenceRouteAnchorCount -ne 1 -or
            $state.StartupBYOKAnchorCount -ne 1 -or $state.ModelListAnchorCount -ne 1 -or
            $state.GetModelAnchorCount -ne 1) {
            throw 'One or more required anchors are missing or ambiguous.'
        }
        $temp = Join-Path ([IO.Path]::GetTempPath()) ("qoder-runtime-v3.0.1-{0}.mjs" -f [Guid]::NewGuid().ToString('N'))
        try {
            Patch-Runtime $runtimePath $temp
            $patchedText = [IO.File]::ReadAllText($temp, [Text.Encoding]::UTF8)
            if ((Get-OccurrenceCount $patchedText $PatchMarker) -ne 1 -or
                (Get-OccurrenceCount $patchedText $InferenceRouteReplacement) -ne 1 -or
                (Get-OccurrenceCount $patchedText 'function qcv30target(A)') -ne 1 -or
                (Get-OccurrenceCount $patchedText 'function qcv30model(A)') -ne 1 -or
                (Get-OccurrenceCount $patchedText 'Zdo injection failed') -ne 1) {
                throw 'Patched runtime verification failed: direct-route or catalog injection is incomplete.'
            }
            $node = Get-NodeExecutable
            if ($null -ne $node) {
                & $node --check $temp
                if ($LASTEXITCODE -ne 0) {
                    throw 'Patched runtime failed the Node.js syntax check.'
                }
                Write-Success 'Patched runtime passed the Node.js syntax check.'
            }
            else {
                Write-Host '[WARN] Node.js is unavailable; skipped JavaScript syntax validation.' -ForegroundColor Yellow
            }
            Write-Success 'DryRun passed. The v3.0.1 direct-route and session-resume patch can be generated without modifying the installation.'
            Write-Host "Patched test SHA-256: $(Get-FileSha256Hex $temp)"
        }
        finally {
            if (Test-Path -LiteralPath $temp) {
                Remove-Item -LiteralPath $temp -Force
            }
        }
    }

    'Apply' {
        Assert-QoderClosed $InstallDir
        $null = Test-Configuration $ConfigPath
        $state = Get-TargetState $runtimePath $asarPath
        if ($state.RuntimePatched -or $state.PreviousRuntimePatched) {
            if (-not $state.AppAsarUnmodified) {
                throw 'app.asar no longer matches the tested build; refusing to upgrade.'
            }
            Write-Info 'Upgrading/refreshing the installed runtime from its verified original backup.'
            $configParent = Split-Path -Parent $RuntimeConfigPath
            New-Item -ItemType Directory -Path $configParent -Force | Out-Null
            $cleanJson = (Get-Content -LiteralPath $ConfigPath -Raw -Encoding UTF8).Trim()
            [IO.File]::WriteAllText($RuntimeConfigPath, $cleanJson, [Text.UTF8Encoding]::new($false))
            $originalBackupId = Upgrade-V2RuntimePatch $runtimePath
            $newState = Get-TargetState $runtimePath $asarPath
            if (-not $newState.RuntimePatched -or -not $newState.AppAsarUnmodified) {
                throw 'Post-upgrade verification failed.'
            }
            Write-Success "Upgraded to v3.0.1. Original backup ID: $originalBackupId"
            Write-Host "Configuration: $RuntimeConfigPath"
            break
        }
        if ($state.LegacyRuntimePatched) {
            throw 'The legacy v1 runtime patch is present. Restore it before applying v3.0.1.'
        }
        if ($state.RuntimeSha256 -ne $SupportedRuntimeSha256 -or -not $state.AppAsarUnmodified) {
            throw 'This Qoder CN build does not match the tested 0.1.2 / runtime 1.1.31 baseline.'
        }
        if ($state.ImportAnchorCount -ne 1 -or $state.ConverterAnchorCount -ne 1 -or
            $state.ModelUrlAnchorCount -ne 1 -or $state.CatalogAnchorCount -ne 1 -or
            $state.ValidationAnchorCount -ne 1 -or $state.InferenceRouteAnchorCount -ne 1 -or
            $state.StartupBYOKAnchorCount -ne 1 -or $state.ModelListAnchorCount -ne 1 -or
            $state.GetModelAnchorCount -ne 1) {
            throw 'Required code anchors do not match this Qoder CN build; refusing to patch.'
        }

        Write-Info 'Creating a runtime-only backup. app.asar will not be modified.'
        $backup = New-Backup $runtimePath $asarPath
        try {
            Install-RuntimePatch $runtimePath
            $configParent = Split-Path -Parent $RuntimeConfigPath
            New-Item -ItemType Directory -Path $configParent -Force | Out-Null
            $cleanJson = (Get-Content -LiteralPath $ConfigPath -Raw -Encoding UTF8).Trim()
            [IO.File]::WriteAllText($RuntimeConfigPath, $cleanJson, [Text.UTF8Encoding]::new($false))
            $newState = Get-TargetState $runtimePath $asarPath
            if (-not $newState.RuntimePatched -or -not $newState.AppAsarUnmodified) {
                throw 'Post-install verification failed.'
            }
            Write-Success "Runtime-only v3.0.1 patch installed. Backup ID: $($backup.Id)"
            Write-Host "Configuration: $RuntimeConfigPath"
            Write-Host 'app.asar was verified unchanged.'
        }
        catch {
            Write-Host '[ERROR] v3.0.1 patch failed; restoring the runtime backup.' -ForegroundColor Red
            Copy-Item -LiteralPath (Join-Path $backup.Directory 'qoder-worker-runtime.obf.mjs') -Destination $runtimePath -Force
            throw
        }
    }

    'Restore' {
        Assert-QoderClosed $InstallDir
        $manifest = Get-BackupManifest
        if ([string]$manifest.patchVersion -notin @('2', '2.1', '2.2', '2.3', '3.0', '3.0.1', '3')) {
            throw 'The selected backup is not a supported v2/v3 runtime-only backup.'
        }
        if (-not (Test-Path -LiteralPath $manifest.runtimeBackup -PathType Leaf)) {
            throw 'Runtime backup referenced by the manifest is missing.'
        }
        if ((Get-FileSha256Hex ([string]$manifest.runtimeBackup)) -ne [string]$manifest.runtimeSha256) {
            throw 'Runtime backup hash verification failed.'
        }
        if ([IO.Path]::GetFullPath([string]$manifest.runtimePath) -ne [IO.Path]::GetFullPath($runtimePath)) {
            throw 'Backup target path does not match the selected Qoder installation.'
        }
        Copy-Item -LiteralPath $manifest.runtimeBackup -Destination ([string]$manifest.runtimePath) -Force
        Write-Success "Restored Qoder CN runtime from backup: $($manifest.backupId)"
        Write-Host 'app.asar was never modified by v2.x/v3.x.'
        Write-Host "The harmless configuration file was retained: $RuntimeConfigPath"
    }
}
