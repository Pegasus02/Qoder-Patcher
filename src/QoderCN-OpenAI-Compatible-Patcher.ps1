[CmdletBinding()]
param(
    [ValidateSet('Inspect', 'DryRun', 'Apply', 'Restore')]
    [string]$Action = 'Inspect',

    [string]$InstallDir = 'C:\Program Files\Qoder\Qoder CN',

    [string]$ConfigPath = '',

    [string]$RuntimeConfigPath = (Join-Path $env:USERPROFILE '.qoder-cn\custom-openai-provider-v2.json'),

    [string]$BackupRoot = (Join-Path $env:LOCALAPPDATA 'QoderCNOpenAICompatiblePatcher\backups-v2'),

    [string]$BackupId
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$PatchMarker = 'QODER_CN_OAI_PATCH_V2'
$LegacyPatchMarker = 'QODER_CN_OAI_PATCH_V1'
$RuntimeRelativePath = 'resources\app.asar.unpacked\node_modules\@qoder-ai\qoder-cn-agent-sdk\dist\_worker\qoder-worker-runtime.obf.mjs'
$AsarRelativePath = 'resources\app.asar'
$SupportedRuntimeSha256 = '7348879d488dc22cca1fc8138c3182233637f78bea210652701b3463b6d3f655'
$SupportedAsarSha256 = '8f7429f5e0efd4850663fae438cf1340feda7e86ec2392d0d7820ee22699a941'

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'configs\cpa-192.168.50.241.json'
}

$ImportAnchor = 'import*as P8e from"node:path";import*as qxA from"node:fs/promises";'
$ImportReplacement = 'import*as qcv2fs from"node:fs";import*as P8e from"node:path";import*as qxA from"node:fs/promises";'

$ConverterAnchor = 'function XxA(A){'
$ConverterReplacement = @'
function qcv2cfg(){try{return JSON.parse(qcv2fs.readFileSync(process.env.QODER_CN_CUSTOM_PROVIDER_CONFIG||process.env.USERPROFILE+"/.qoder-cn/custom-openai-provider-v2.json","utf8"))}catch(A){return null}}function qcv2url(A){try{let e=qcv2cfg();if(e&&"string"==typeof A&&new URL(e.uiBaseUrl).toString()===new URL(A).toString())return e.upstreamBaseUrl}catch{}return A}function XxA(A){/*QODER_CN_OAI_PATCH_V2*/
'@

$ModelUrlAnchor = 'url:A.url,model:A.model,provider:A.provider'
$ModelUrlReplacement = 'url:qcv2url(A.url),model:A.model,provider:A.provider'

$CatalogAnchor = 'n=await e().getBYOKConfig(),r=t;B(pn(i,"success",{providers:n?.providers.map(A)??[]}))'
$CatalogReplacement = @'
n=await e().getBYOKConfig(),r=t;let qcp=n?.providers.map(A)??[];try{let qcc=qcv2cfg();if(!qcc)throw Error("QODER_CN_PATCH_CONFIG_UNAVAILABLE");let qci=Number.isInteger(qcc.replaceProviderIndex)?qcc.replaceProviderIndex:qcp.findIndex(q=>q.key===qcc.replaceProviderKey||q.display_name===qcc.replaceProviderDisplayName);if(qci<0||qci>=qcp.length)throw Error("QODER_CN_PATCH_REPLACEMENT_PROVIDER_NOT_FOUND");let qcb=qcp[qci],qcm=(qcc.models??[]).map(q=>({key:q.id,display_name:q.displayName??q.id,is_vl:q.vision===true,is_reasoning:q.reasoning===true,format:"openai",max_input_tokens:q.maxInputTokens??131072,efforts:q.efforts??[],supports_disabled:q.supportsDisabled===true}));if(!qcm.length)throw Error("QODER_CN_PATCH_MODELS_EMPTY");qcp[qci]={...qcb,display_name:qcc.displayName??"Local OpenAI Compatible",url:qcc.uiBaseUrl,fields:[{key:"api_key",display_name:"API Key",type:"free_input",mandatory:true}],types:[{key:"openai-compatible",display_name:"OpenAI Compatible",style:"openai",models:qcm}]}}catch(qce){Q.warn("[qoder-cn-openai-patch-v2] custom provider not loaded:",qce)}B(pn(i,"success",{providers:qcp}))
'@

$ValidationAnchor = 'o=await r().checkBYOKModel(n.provider,n.model,{api_key:n.api_key},A,e,t);B(pn(i,"success",{success:o}))'
$ValidationReplacement = @'
o=await(async()=>{try{let qcv=qcv2cfg();if(qcv&&qcv.skipValidation!==false&&new URL(qcv.uiBaseUrl).toString()===new URL(A).toString())return true}catch(qce){Q.warn("[qoder-cn-openai-patch-v2] validation override unavailable:",qce)}return r().checkBYOKModel(n.provider,n.model,{api_key:n.api_key},A,e,t)})();B(pn(i,"success",{success:o}))
'@

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
    $raw = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    if ($raw -match '"(?:api[_-]?key|access[_-]?token|authorization)"\s*:') {
        throw 'Configuration must not contain API keys, access tokens, or authorization values.'
    }
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
    if ($uiUri.AbsolutePath.TrimEnd('/') -ne $upstreamUri.AbsolutePath.TrimEnd('/')) {
        throw 'uiBaseUrl and upstreamBaseUrl must use the same path; normally only the scheme differs.'
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
    return [pscustomobject]@{
        RuntimeSha256 = Get-FileSha256Hex $RuntimePath
        AsarSha256 = $asarSha256
        RuntimePatched = $text.IndexOf($PatchMarker, [StringComparison]::Ordinal) -ge 0
        LegacyRuntimePatched = $text.IndexOf($LegacyPatchMarker, [StringComparison]::Ordinal) -ge 0
        ImportAnchorCount = Get-OccurrenceCount $text $ImportAnchor
        ConverterAnchorCount = Get-OccurrenceCount $text $ConverterAnchor
        ModelUrlAnchorCount = Get-OccurrenceCount $text $ModelUrlAnchor
        CatalogAnchorCount = Get-OccurrenceCount $text $CatalogAnchor
        ValidationAnchorCount = Get-OccurrenceCount $text $ValidationAnchor
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
        throw 'The v2 runtime patch is already installed.'
    }
    if ($text.IndexOf($LegacyPatchMarker, [StringComparison]::Ordinal) -ge 0) {
        throw 'The legacy v1 runtime patch is present. Restore v1 before applying v2.'
    }
    $text = Replace-ExactlyOnce $text $ImportAnchor $ImportReplacement 'node:fs import'
    $text = Replace-ExactlyOnce $text $ConverterAnchor $ConverterReplacement 'BYOK model converter'
    $text = Replace-ExactlyOnce $text $ModelUrlAnchor $ModelUrlReplacement 'BYOK inference URL'
    $text = Replace-ExactlyOnce $text $CatalogAnchor $CatalogReplacement 'BYOK catalog'
    $text = Replace-ExactlyOnce $text $ValidationAnchor $ValidationReplacement 'BYOK validation'
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
        patchVersion = 2
    }
    $manifestPath = Join-Path $dir 'manifest.json'
    [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
    return [pscustomobject]@{ Id = $id; Directory = $dir; ManifestPath = $manifestPath }
}

function Get-BackupManifest {
    if (-not (Test-Path -LiteralPath $BackupRoot -PathType Container)) {
        throw "No v2 backups found in: $BackupRoot"
    }
    if (-not [string]::IsNullOrWhiteSpace($BackupId)) {
        $path = Join-Path (Join-Path $BackupRoot $BackupId) 'manifest.json'
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Backup not found: $BackupId"
        }
        return Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    $manifestFile = Get-ChildItem -LiteralPath $BackupRoot -Filter manifest.json -File -Recurse |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $manifestFile) {
        throw "No v2 backup manifests found in: $BackupRoot"
    }
    return Get-Content -LiteralPath $manifestFile.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Install-RuntimePatch([string]$RuntimePath) {
    $temp = "$RuntimePath.qoder-openai-patch-v2.tmp"
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

$runtimePath = Join-Path $InstallDir $RuntimeRelativePath
$asarPath = Join-Path $InstallDir $AsarRelativePath

switch ($Action) {
    'Inspect' {
        $state = Get-TargetState $runtimePath $asarPath
        Write-Host ''
        Write-Host 'Qoder CN OpenAI-compatible runtime patch v2' -ForegroundColor White
        Write-Host "  Install directory   : $([IO.Path]::GetFullPath($InstallDir))"
        Write-Host "  Runtime patched     : $($state.RuntimePatched)"
        Write-Host "  Legacy patch present: $($state.LegacyRuntimePatched)"
        Write-Host "  app.asar untouched  : $($state.AppAsarUnmodified)"
        Write-Host "  Runtime SHA-256     : $($state.RuntimeSha256)"
        Write-Host "  app.asar SHA-256    : $($state.AsarSha256)"
        Write-Host "  Required anchors    : import=$($state.ImportAnchorCount), converter=$($state.ConverterAnchorCount), url=$($state.ModelUrlAnchorCount), catalog=$($state.CatalogAnchorCount), validation=$($state.ValidationAnchorCount)"
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
        if ($state.RuntimePatched -or $state.LegacyRuntimePatched) {
            throw 'A runtime patch is already present; DryRun expects the original runtime.'
        }
        if ($state.RuntimeSha256 -ne $SupportedRuntimeSha256 -or -not $state.AppAsarUnmodified) {
            throw 'This Qoder CN build does not match the tested 0.1.2 / runtime 1.1.31 baseline.'
        }
        if ($state.ImportAnchorCount -ne 1 -or $state.ConverterAnchorCount -ne 1 -or
            $state.ModelUrlAnchorCount -ne 1 -or $state.CatalogAnchorCount -ne 1 -or
            $state.ValidationAnchorCount -ne 1) {
            throw 'One or more required anchors are missing or ambiguous.'
        }
        $temp = Join-Path ([IO.Path]::GetTempPath()) ("qoder-runtime-v2-{0}.mjs" -f [Guid]::NewGuid().ToString('N'))
        try {
            Patch-Runtime $runtimePath $temp
            Write-Success 'DryRun passed. The runtime can be patched without modifying the installation.'
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
        if ($state.RuntimePatched) {
            $configParent = Split-Path -Parent $RuntimeConfigPath
            New-Item -ItemType Directory -Path $configParent -Force | Out-Null
            Copy-Item -LiteralPath $ConfigPath -Destination $RuntimeConfigPath -Force
            Write-Success 'The v2 patch was already installed; configuration was updated.'
            break
        }
        if ($state.LegacyRuntimePatched) {
            throw 'The legacy v1 runtime patch is present. Restore it before applying v2.'
        }
        if ($state.RuntimeSha256 -ne $SupportedRuntimeSha256 -or -not $state.AppAsarUnmodified) {
            throw 'This Qoder CN build does not match the tested 0.1.2 / runtime 1.1.31 baseline.'
        }
        if ($state.ImportAnchorCount -ne 1 -or $state.ConverterAnchorCount -ne 1 -or
            $state.ModelUrlAnchorCount -ne 1 -or $state.CatalogAnchorCount -ne 1 -or
            $state.ValidationAnchorCount -ne 1) {
            throw 'Required code anchors do not match this Qoder CN build; refusing to patch.'
        }

        Write-Info 'Creating a runtime-only backup. app.asar will not be modified.'
        $backup = New-Backup $runtimePath $asarPath
        try {
            Install-RuntimePatch $runtimePath
            $configParent = Split-Path -Parent $RuntimeConfigPath
            New-Item -ItemType Directory -Path $configParent -Force | Out-Null
            Copy-Item -LiteralPath $ConfigPath -Destination $RuntimeConfigPath -Force
            $newState = Get-TargetState $runtimePath $asarPath
            if (-not $newState.RuntimePatched -or -not $newState.AppAsarUnmodified) {
                throw 'Post-install verification failed.'
            }
            Write-Success "Runtime-only patch installed. Backup ID: $($backup.Id)"
            Write-Host "Configuration: $RuntimeConfigPath"
            Write-Host 'app.asar was verified unchanged.'
        }
        catch {
            Write-Host '[ERROR] v2 patch failed; restoring the runtime backup.' -ForegroundColor Red
            Copy-Item -LiteralPath (Join-Path $backup.Directory 'qoder-worker-runtime.obf.mjs') -Destination $runtimePath -Force
            throw
        }
    }

    'Restore' {
        Assert-QoderClosed $InstallDir
        $manifest = Get-BackupManifest
        if ([int]$manifest.patchVersion -ne 2) {
            throw 'The selected backup is not a v2 runtime-only backup.'
        }
        if (-not (Test-Path -LiteralPath $manifest.runtimeBackup -PathType Leaf)) {
            throw 'Runtime backup referenced by the manifest is missing.'
        }
        if ((Get-FileSha256Hex ([string]$manifest.runtimeBackup)) -ne [string]$manifest.runtimeSha256) {
            throw 'Runtime backup hash verification failed.'
        }
        Copy-Item -LiteralPath $manifest.runtimeBackup -Destination ([string]$manifest.runtimePath) -Force
        Write-Success "Restored Qoder CN runtime from v2 backup: $($manifest.backupId)"
        Write-Host 'app.asar was never modified by v2.'
        Write-Host "The harmless configuration file was retained: $RuntimeConfigPath"
    }
}
