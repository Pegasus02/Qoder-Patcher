[CmdletBinding()]
param(
    [string]$InstallDir = 'C:\Program Files\Qoder\Qoder CN',

    [string]$DefaultConfigPath = '',

    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:ProjectRoot = Split-Path -Parent $PSScriptRoot
$script:PatcherPath = Join-Path $PSScriptRoot 'QoderCN-OpenAI-Compatible-Patcher.ps1'
$script:ConfigsDir = Join-Path $script:ProjectRoot 'configs'
$script:RuntimeConfigPath = Join-Path $env:USERPROFILE '.qoder-cn\custom-openai-provider-v3.0.1.json'
$script:BackupRoot = Join-Path $env:LOCALAPPDATA 'QoderCNOpenAICompatiblePatcher\backups-v2'
$script:DefaultInstallDir = $InstallDir
$script:PreferredConfigPath = if ([string]::IsNullOrWhiteSpace($DefaultConfigPath)) {
    Join-Path $script:ConfigsDir 'cpa-192.168.50.241.json'
}
else {
    [IO.Path]::GetFullPath($DefaultConfigPath)
}

$script:SecretEntropy = [Text.Encoding]::UTF8.GetBytes('QoderCN-GatewayManager/3.0.1/API-Key')
$script:SecretStoreDir = Join-Path $env:LOCALAPPDATA 'QoderCNOpenAICompatiblePatcher\secrets'

function Get-SecretPath([string]$ProfilePath) {
    $normalized = [IO.Path]::GetFullPath($ProfilePath).Trim().ToUpperInvariant()
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $digest = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($normalized))
    }
    finally {
        $sha.Dispose()
    }
    $name = -join ($digest | ForEach-Object { $_.ToString('x2') })
    return Join-Path $script:SecretStoreDir ($name + '.bin')
}

function Save-StoredApiKey([string]$ProfilePath, [string]$ApiKey) {
    $secretPath = Get-SecretPath $ProfilePath
    if ([string]::IsNullOrWhiteSpace($ApiKey)) {
        if (Test-Path -LiteralPath $secretPath) { Remove-Item -LiteralPath $secretPath -Force }
        return
    }
    New-Item -ItemType Directory -Path $script:SecretStoreDir -Force | Out-Null
    $plain = [Text.Encoding]::UTF8.GetBytes($ApiKey.Trim())
    try {
        $protected = [Security.Cryptography.ProtectedData]::Protect($plain, $script:SecretEntropy, [Security.Cryptography.DataProtectionScope]::CurrentUser)
        [IO.File]::WriteAllBytes($secretPath, $protected)
    }
    finally {
        [Array]::Clear($plain, 0, $plain.Length)
    }
}

function Get-StoredApiKey([string]$ProfilePath) {
    if ([string]::IsNullOrWhiteSpace($ProfilePath)) { return '' }
    $secretPath = Get-SecretPath $ProfilePath
    if (-not (Test-Path -LiteralPath $secretPath -PathType Leaf)) { return '' }
    $plain = [Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($secretPath), $script:SecretEntropy, [Security.Cryptography.DataProtectionScope]::CurrentUser)
    try { return [Text.Encoding]::UTF8.GetString($plain) }
    finally { [Array]::Clear($plain, 0, $plain.Length) }
}
function Get-ConfigProfiles {
    $profiles = @()
    if (Test-Path -LiteralPath $script:ConfigsDir -PathType Container) {
        foreach ($file in Get-ChildItem -LiteralPath $script:ConfigsDir -Filter '*.json' -File | Sort-Object Name) {
            try {
                $raw = (Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8).Trim()
                $raw = $raw -replace '^\uFEFF', ''
                $config = $raw | ConvertFrom-Json
                $profiles += [pscustomobject]@{
                    Name = $file.Name
                    FullName = $file.FullName
                    DisplayName = [string]$config.displayName
                    Upstream = [string]$config.upstreamBaseUrl
                    UiBaseUrl = [string]$config.uiBaseUrl
                    ApiKey = (Get-StoredApiKey $file.FullName)
                    ReplaceProviderKey = if ($config.PSObject.Properties.Match('replaceProviderKey').Count -gt 0) { [string]$config.replaceProviderKey } else { 'anthropic' }
                    ReplaceProviderDisplayName = if ($config.PSObject.Properties.Match('replaceProviderDisplayName').Count -gt 0) { [string]$config.replaceProviderDisplayName } else { 'Anthropic (Claude)' }
                    ReplaceProviderIndex = if ($config.PSObject.Properties.Match('replaceProviderIndex').Count -gt 0) { [int]$config.replaceProviderIndex } else { 0 }
                    Models = @($config.models)
                    ModelCount = @($config.models).Count
                }
            }
            catch {
                # Invalid profiles are excluded from the GUI and remain diagnosable with DryRun.
            }
        }
    }
    return $profiles
}

function Get-OptionalNodePath {
    $node = Get-Command node -ErrorAction SilentlyContinue
    if ($null -ne $node) {
        return $node.Source
    }
    $bundledNode = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe'
    if (Test-Path -LiteralPath $bundledNode -PathType Leaf) {
        return $bundledNode
    }
    return $null
}

function Get-QoderExecutable([string]$Root) {
    foreach ($name in @('Qoder CN.exe', 'Qoder.exe')) {
        $candidate = Join-Path $Root $name
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }
    return $null
}

function ConvertTo-PowerShellLiteral([string]$Value) {
    return "'" + $Value.Replace("'", "''") + "'"
}

function Invoke-PatcherLocal {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Inspect', 'DryRun')]
        [string]$Action,

        [Parameter(Mandatory)]
        [string]$TargetInstallDir,

        [string]$ConfigPath
    )

    $parameters = @{
        Action = $Action
        InstallDir = $TargetInstallDir
        RuntimeConfigPath = $script:RuntimeConfigPath
        BackupRoot = $script:BackupRoot
    }
    if (-not [string]::IsNullOrWhiteSpace($ConfigPath)) {
        $parameters.ConfigPath = $ConfigPath
    }
    if ($Action -eq 'DryRun') {
        $nodePath = Get-OptionalNodePath
        if ($null -ne $nodePath) {
            $parameters.NodePath = $nodePath
        }
    }

    return (& $script:PatcherPath @parameters *>&1 | Out-String).TrimEnd()
}

function Invoke-PatcherElevated {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Apply', 'Restore')]
        [string]$Action,

        [Parameter(Mandatory)]
        [string]$TargetInstallDir,

        [string]$ConfigPath
    )

    $logPath = Join-Path ([IO.Path]::GetTempPath()) ('qoder-patcher-gui-' + [Guid]::NewGuid().ToString('N') + '.log')
    $patcherLiteral = ConvertTo-PowerShellLiteral $script:PatcherPath
    $installLiteral = ConvertTo-PowerShellLiteral $TargetInstallDir
    $runtimeConfigLiteral = ConvertTo-PowerShellLiteral $script:RuntimeConfigPath
    $backupRootLiteral = ConvertTo-PowerShellLiteral $script:BackupRoot
    $logLiteral = ConvertTo-PowerShellLiteral $logPath
    $configFragment = if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
        ''
    }
    else {
        ' -ConfigPath ' + (ConvertTo-PowerShellLiteral $ConfigPath)
    }
    $command = @"
`$ErrorActionPreference = 'Stop'
try {
    & $patcherLiteral -Action $Action -InstallDir $installLiteral$configFragment -RuntimeConfigPath $runtimeConfigLiteral -BackupRoot $backupRootLiteral *>&1 |
        Out-File -LiteralPath $logLiteral -Encoding UTF8
    exit 0
}
catch {
    (`$_ | Out-String) | Out-File -LiteralPath $logLiteral -Encoding UTF8 -Append
    exit 1
}
"@
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))

    try {
        $process = Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList @(
            '-NoProfile',
            '-ExecutionPolicy', 'Bypass',
            '-EncodedCommand', $encoded
        ) -WindowStyle Normal -Wait -PassThru
        $output = if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            (Get-Content -LiteralPath $logPath -Raw -Encoding UTF8).TrimEnd()
        }
        else {
            ''
        }
        return [pscustomobject]@{
            ExitCode = $process.ExitCode
            Output = $output
        }
    }
    finally {
        if (Test-Path -LiteralPath $logPath) {
            Remove-Item -LiteralPath $logPath -Force
        }
    }
}

if ($SelfTest) {
    if (-not (Test-Path -LiteralPath $script:PatcherPath -PathType Leaf)) {
        throw "Patcher not found: $script:PatcherPath"
    }
    $profiles = @(Get-ConfigProfiles)
    if ($profiles.Count -eq 0) {
        throw 'No valid JSON configuration profiles were found.'
    }
    foreach ($profile in $profiles) {
        if ([string]::IsNullOrWhiteSpace($profile.Upstream) -or $profile.ModelCount -le 0) {
            throw "Invalid GUI profile metadata: $($profile.FullName)"
        }
    }
    Write-Host "[OK] GUI self-test passed. Profiles: $($profiles.Count)" -ForegroundColor Green
    return
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

[Windows.Forms.Application]::EnableVisualStyles()

class ModelItemWrapper {
    [string]$Id
    [string]$DisplayName
    [psobject]$RawModel

    ModelItemWrapper([string]$id, [string]$displayName, [psobject]$raw) {
        $this.Id = $id
        $this.DisplayName = $displayName
        $this.RawModel = $raw
    }

    [string] ToString() {
        $disp = if ([string]::IsNullOrWhiteSpace($this.DisplayName) -or $this.DisplayName -eq $this.Id) {
            $this.Id
        }
        else {
            "$($this.Id) ($($this.DisplayName))"
        }
        $tags = @()
        if ($this.RawModel.PSObject.Properties.Match('reasoning').Count -gt 0 -and $this.RawModel.reasoning -eq $true) { $tags += 'Thinking' }
        if ($this.RawModel.PSObject.Properties.Match('tools').Count -gt 0 -and $this.RawModel.tools -eq $true) { $tags += 'Tools' }
        if ($this.RawModel.PSObject.Properties.Match('vision').Count -gt 0 -and $this.RawModel.vision -eq $true) { $tags += 'Vision' }
        if ($this.RawModel.PSObject.Properties.Match('maxTokensField').Count -gt 0 -and $this.RawModel.maxTokensField -eq 'max_completion_tokens') { $tags += 'o-series' }
        if ($tags.Count -gt 0) {
            return "$disp  [" + ($tags -join ', ') + "]"
        }
        return $disp
    }
}

$form = [Windows.Forms.Form]::new()
$form.Text = 'Qoder CN OpenAI-Compatible Patcher v3.0.1'
$form.StartPosition = 'CenterScreen'
$form.Size = [Drawing.Size]::new(1060, 840)
$form.MinimumSize = [Drawing.Size]::new(980, 760)
$form.Font = [Drawing.Font]::new('Segoe UI', 9)
$form.AutoScaleMode = 'Dpi'

$title = [Windows.Forms.Label]::new()
$title.Text = 'Qoder CN OpenAI-Compatible Patcher'
$title.Font = [Drawing.Font]::new('Segoe UI Semibold', 17)
$title.AutoSize = $true
$title.Location = [Drawing.Point]::new(20, 14)
$form.Controls.Add($title)

$subtitle = [Windows.Forms.Label]::new()
$subtitle.Text = 'Runtime-only patch and visual configuration manager for OpenAI-compatible gateways'
$subtitle.AutoSize = $true
$subtitle.ForeColor = [Drawing.Color]::DimGray
$subtitle.Location = [Drawing.Point]::new(22, 48)
$form.Controls.Add($subtitle)

$configGroupBox = [Windows.Forms.GroupBox]::new()
$configGroupBox.Text = 'Endpoint and Provider Configuration'
$configGroupBox.Location = [Drawing.Point]::new(20, 75)
$configGroupBox.Size = [Drawing.Size]::new(500, 310)
$configGroupBox.Anchor = 'Top, Left'
$form.Controls.Add($configGroupBox)

function New-FormInput([Windows.Forms.Control]$Parent, [string]$LabelText, [int]$Top, [string]$DefaultVal, [bool]$IsPassword = $false) {
    $lbl = [Windows.Forms.Label]::new()
    $lbl.Text = $LabelText
    $lbl.AutoSize = $true
    $lbl.Location = [Drawing.Point]::new(14, $Top + 3)
    $Parent.Controls.Add($lbl)

    $txt = [Windows.Forms.TextBox]::new()
    $txt.Text = $DefaultVal
    $txt.Location = [Drawing.Point]::new(140, $Top)
    $txt.Size = [Drawing.Size]::new(340, 25)
    $txt.Anchor = 'Top, Left, Right'
    if ($IsPassword) {
        $txt.UseSystemPasswordChar = $false
    }
    $Parent.Controls.Add($txt)
    return $txt
}

$installText = New-FormInput $configGroupBox 'Qoder CN Directory' 24 $script:DefaultInstallDir
$installText.Size = [Drawing.Size]::new(250, 25)

$browseInstallButton = [Windows.Forms.Button]::new()
$browseInstallButton.Text = 'Browse...'
$browseInstallButton.Location = [Drawing.Point]::new(398, 23)
$browseInstallButton.Size = [Drawing.Size]::new(82, 27)
$browseInstallButton.Anchor = 'Top, Right'
$configGroupBox.Controls.Add($browseInstallButton)

$profileLbl = [Windows.Forms.Label]::new()
$profileLbl.Text = 'Profile File'
$profileLbl.AutoSize = $true
$profileLbl.Location = [Drawing.Point]::new(14, 60)
$configGroupBox.Controls.Add($profileLbl)

$configCombo = [Windows.Forms.ComboBox]::new()
$configCombo.DropDownStyle = 'DropDownList'
$configCombo.Location = [Drawing.Point]::new(140, 56)
$configCombo.Size = [Drawing.Size]::new(250, 25)
$configCombo.Anchor = 'Top, Left, Right'
$configGroupBox.Controls.Add($configCombo)

$browseConfigButton = [Windows.Forms.Button]::new()
$browseConfigButton.Text = 'Open...'
$browseConfigButton.Location = [Drawing.Point]::new(398, 55)
$browseConfigButton.Size = [Drawing.Size]::new(82, 27)
$browseConfigButton.Anchor = 'Top, Right'
$configGroupBox.Controls.Add($browseConfigButton)

$displayNameText = New-FormInput $configGroupBox 'Display Name' 92 'CPA @ 192.168.50.241'
$upstreamUrlText = New-FormInput $configGroupBox 'Upstream Base URL' 128 'http://192.168.50.241:8317/v1'
$uiUrlText = New-FormInput $configGroupBox 'UI Base URL' 164 'https://192.168.50.241:8317/v1'
$apiKeyText = New-FormInput $configGroupBox 'API Key' 200 ''
$apiKeyText.UseSystemPasswordChar = $true

$replaceKeyLbl = [Windows.Forms.Label]::new()
$replaceKeyLbl.Text = 'Replace Key'
$replaceKeyLbl.AutoSize = $true
$replaceKeyLbl.Location = [Drawing.Point]::new(14, 239)
$configGroupBox.Controls.Add($replaceKeyLbl)

$replaceKeyText = [Windows.Forms.TextBox]::new()
$replaceKeyText.Text = 'anthropic'
$replaceKeyText.Location = [Drawing.Point]::new(140, 236)
$replaceKeyText.Size = [Drawing.Size]::new(120, 25)
$configGroupBox.Controls.Add($replaceKeyText)

$replaceIndexLbl = [Windows.Forms.Label]::new()
$replaceIndexLbl.Text = 'Index'
$replaceIndexLbl.AutoSize = $true
$replaceIndexLbl.Location = [Drawing.Point]::new(275, 239)
$configGroupBox.Controls.Add($replaceIndexLbl)

$replaceIndexNumeric = [Windows.Forms.NumericUpDown]::new()
$replaceIndexNumeric.Minimum = 0
$replaceIndexNumeric.Maximum = 20
$replaceIndexNumeric.Value = 0
$replaceIndexNumeric.Location = [Drawing.Point]::new(340, 236)
$replaceIndexNumeric.Size = [Drawing.Size]::new(50, 25)
$configGroupBox.Controls.Add($replaceIndexNumeric)

$testConnButton = [Windows.Forms.Button]::new()
$testConnButton.Text = 'Test Conn'
$testConnButton.Location = [Drawing.Point]::new(398, 235)
$testConnButton.Size = [Drawing.Size]::new(82, 27)
$testConnButton.Anchor = 'Top, Right'
$configGroupBox.Controls.Add($testConnButton)

$profileTipLbl = [Windows.Forms.Label]::new()
$profileTipLbl.Text = 'Enter your CPA API Key above to prevent 401 Unauthorized errors.'
$profileTipLbl.ForeColor = [Drawing.Color]::DimGray
$profileTipLbl.AutoSize = $true
$profileTipLbl.Location = [Drawing.Point]::new(14, 275)
$configGroupBox.Controls.Add($profileTipLbl)

$modelGroupBox = [Windows.Forms.GroupBox]::new()
$modelGroupBox.Text = 'Model Injection Selection (Check models to inject)'
$modelGroupBox.Location = [Drawing.Point]::new(530, 75)
$modelGroupBox.Size = [Drawing.Size]::new(490, 310)
$modelGroupBox.Anchor = 'Top, Left, Right'
$form.Controls.Add($modelGroupBox)

$modelCheckList = [Windows.Forms.CheckedListBox]::new()
$modelCheckList.CheckOnClick = $true
$modelCheckList.Location = [Drawing.Point]::new(14, 24)
$modelCheckList.Size = [Drawing.Size]::new(460, 230)
$modelCheckList.Anchor = 'Top, Bottom, Left, Right'
$modelCheckList.IntegralHeight = $false
$modelGroupBox.Controls.Add($modelCheckList)

$selectAllButton = [Windows.Forms.Button]::new()
$selectAllButton.Text = 'All'
$selectAllButton.Location = [Drawing.Point]::new(14, 265)
$selectAllButton.Size = [Drawing.Size]::new(46, 28)
$selectAllButton.Anchor = 'Bottom, Left'
$modelGroupBox.Controls.Add($selectAllButton)

$uncheckAllButton = [Windows.Forms.Button]::new()
$uncheckAllButton.Text = 'None'
$uncheckAllButton.Location = [Drawing.Point]::new(64, 265)
$uncheckAllButton.Size = [Drawing.Size]::new(50, 28)
$uncheckAllButton.Anchor = 'Bottom, Left'
$modelGroupBox.Controls.Add($uncheckAllButton)

$addModelButton = [Windows.Forms.Button]::new()
$addModelButton.Text = 'Add...'
$addModelButton.Location = [Drawing.Point]::new(118, 265)
$addModelButton.Size = [Drawing.Size]::new(60, 28)
$addModelButton.Anchor = 'Bottom, Left'
$modelGroupBox.Controls.Add($addModelButton)

$editModelButton = [Windows.Forms.Button]::new()
$editModelButton.Text = 'Edit Model...'
$editModelButton.Location = [Drawing.Point]::new(182, 265)
$editModelButton.Size = [Drawing.Size]::new(96, 28)
$editModelButton.Anchor = 'Bottom, Left'
$modelGroupBox.Controls.Add($editModelButton)

$removeModelButton = [Windows.Forms.Button]::new()
$removeModelButton.Text = 'Remove'
$removeModelButton.Location = [Drawing.Point]::new(282, 265)
$removeModelButton.Size = [Drawing.Size]::new(68, 28)
$removeModelButton.Anchor = 'Bottom, Left'
$modelGroupBox.Controls.Add($removeModelButton)

$modelCountLbl = [Windows.Forms.Label]::new()
$modelCountLbl.Text = 'Checked 0 / 0'
$modelCountLbl.AutoSize = $true
$modelCountLbl.Location = [Drawing.Point]::new(360, 271)
$modelCountLbl.ForeColor = [Drawing.Color]::Navy
$modelCountLbl.Anchor = 'Bottom, Right'
$modelGroupBox.Controls.Add($modelCountLbl)

$actionsGroup = [Windows.Forms.GroupBox]::new()
$actionsGroup.Text = 'Actions'
$actionsGroup.Location = [Drawing.Point]::new(20, 392)
$actionsGroup.Size = [Drawing.Size]::new(1000, 72)
$actionsGroup.Anchor = 'Top, Left, Right'
$form.Controls.Add($actionsGroup)

function New-ActionButton([string]$Text, [int]$X, [int]$Width) {
    $button = [Windows.Forms.Button]::new()
    $button.Text = $Text
    $button.Location = [Drawing.Point]::new($X, 24)
    $button.Size = [Drawing.Size]::new($Width, 34)
    $actionsGroup.Controls.Add($button)
    return $button
}

$saveButton = New-ActionButton 'Save Profile' 14 110
$saveButton.BackColor = [Drawing.Color]::FromArgb(235, 245, 255)

$inspectButton = New-ActionButton 'Inspect' 132 100
$dryRunButton = New-ActionButton 'Dry Run' 240 100
$applyButton = New-ActionButton 'Install / Upgrade' 348 150
$applyButton.BackColor = [Drawing.Color]::FromArgb(225, 248, 230)
$applyButton.Font = [Drawing.Font]::new('Segoe UI Semibold', 9.5)

$restoreButton = New-ActionButton 'Restore latest' 516 120
$restoreButton.BackColor = [Drawing.Color]::FromArgb(255, 241, 220)

$launchButton = New-ActionButton 'Launch Qoder CN' 634 140
$refreshButton = New-ActionButton 'Refresh' 782 95

$outputLabel = [Windows.Forms.Label]::new()
$outputLabel.Text = 'Output'
$outputLabel.AutoSize = $true
$outputLabel.Location = [Drawing.Point]::new(20, 472)
$form.Controls.Add($outputLabel)

$outputBox = [Windows.Forms.RichTextBox]::new()
$outputBox.Location = [Drawing.Point]::new(20, 495)
$outputBox.Size = [Drawing.Size]::new(1000, 260)
$outputBox.Anchor = 'Top, Bottom, Left, Right'
$outputBox.ReadOnly = $true
$outputBox.BackColor = [Drawing.Color]::FromArgb(250, 250, 250)
$outputBox.Font = [Drawing.Font]::new('Consolas', 9)
$outputBox.WordWrap = $false
$form.Controls.Add($outputBox)

$statusStrip = [Windows.Forms.StatusStrip]::new()
$statusLabel = [Windows.Forms.ToolStripStatusLabel]::new()
$statusLabel.Text = 'Ready'
$statusLabel.Spring = $true
$statusLabel.TextAlign = 'MiddleLeft'
$null = $statusStrip.Items.Add($statusLabel)
$form.Controls.Add($statusStrip)

$script:ProfilesByPath = @{}
$script:ActionButtons = @($saveButton, $inspectButton, $dryRunButton, $applyButton, $restoreButton, $launchButton, $refreshButton, $testConnButton)

function Add-Output([string]$Text, [switch]$Clear) {
    if ($Clear) {
        $outputBox.Clear()
    }
    if (-not [string]::IsNullOrWhiteSpace($Text)) {
        if ($outputBox.TextLength -gt 0) {
            $outputBox.AppendText([Environment]::NewLine + [Environment]::NewLine)
        }
        $outputBox.AppendText($Text)
        $outputBox.SelectionStart = $outputBox.TextLength
        $outputBox.ScrollToCaret()
    }
    [Windows.Forms.Application]::DoEvents()
}

function Set-Busy([bool]$Busy, [string]$Status) {
    foreach ($button in $script:ActionButtons) {
        $button.Enabled = -not $Busy
    }
    $browseInstallButton.Enabled = -not $Busy
    $browseConfigButton.Enabled = -not $Busy
    $configCombo.Enabled = -not $Busy
    $installText.Enabled = -not $Busy
    $form.UseWaitCursor = $Busy
    $statusLabel.Text = $Status
    [Windows.Forms.Application]::DoEvents()
}

function Get-SelectedConfigPath {
    if ($configCombo.SelectedIndex -lt 0) {
        return $null
    }
    return [string]$configCombo.SelectedItem
}

function Update-ModelCountLabel {
    $checked = 0
    for ($i = 0; $i -lt $modelCheckList.Items.Count; $i++) {
        if ($modelCheckList.GetItemChecked($i)) {
            $checked++
        }
    }
    $modelCountLbl.Text = "Checked $checked / $($modelCheckList.Items.Count) models"
}

function Populate-FieldsFromProfile([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return
    }
    try {
        $raw = (Get-Content -LiteralPath $Path -Raw -Encoding UTF8).Trim()
        $raw = $raw -replace '^\uFEFF', ''
        $cfg = $raw | ConvertFrom-Json
        $displayNameText.Text = if ($cfg.displayName) { [string]$cfg.displayName } else { '' }
        $upstreamUrlText.Text = if ($cfg.upstreamBaseUrl) { [string]$cfg.upstreamBaseUrl } else { '' }
        $uiUrlText.Text = if ($cfg.uiBaseUrl) { [string]$cfg.uiBaseUrl } else { '' }
        $storedApiKey = Get-StoredApiKey $Path
        if ([string]::IsNullOrWhiteSpace($storedApiKey) -and $cfg.PSObject.Properties.Match('apiKey').Count -gt 0 -and -not [string]::IsNullOrWhiteSpace([string]$cfg.apiKey)) {
            $storedApiKey = [string]$cfg.apiKey
            Save-StoredApiKey $Path $storedApiKey
            $cfg.PSObject.Properties.Remove('apiKey')
            [IO.File]::WriteAllText($Path, ($cfg | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
            Add-Output '[WARN] Migrated a legacy plaintext API Key to Windows DPAPI storage.'
        }
        $apiKeyText.Text = $storedApiKey
        $replaceKeyText.Text = if ($cfg.replaceProviderKey) { [string]$cfg.replaceProviderKey } else { 'anthropic' }
        if ($cfg.PSObject.Properties.Match('replaceProviderIndex').Count -gt 0) {
            $replaceIndexNumeric.Value = [Math]::Max(0, [int]$cfg.replaceProviderIndex)
        }

        $modelCheckList.Items.Clear()
        if ($cfg.models) {
            foreach ($m in @($cfg.models)) {
                if ($null -ne $m -and -not [string]::IsNullOrWhiteSpace($m.id)) {
                    $item = [ModelItemWrapper]::new($m.id, [string]$m.displayName, $m)
                    $idx = $modelCheckList.Items.Add($item)
                    $modelCheckList.SetItemChecked($idx, $true)
                }
            }
        }
        Update-ModelCountLabel
    }
    catch {
        Add-Output ("[ERROR] Failed to parse config profile: " + $_.Exception.Message)
    }
}

function Save-CurrentConfig([string]$TargetPath) {
    if ([string]::IsNullOrWhiteSpace($TargetPath)) {
        throw 'Target profile path is empty.'
    }

    $selectedModels = @()
    for ($i = 0; $i -lt $modelCheckList.Items.Count; $i++) {
        if ($modelCheckList.GetItemChecked($i)) {
            $wrapper = $modelCheckList.Items[$i]
            $raw = $wrapper.RawModel
            $mObj = [ordered]@{
                id = $wrapper.Id
                displayName = if ($raw.displayName) { [string]$raw.displayName } else { $wrapper.Id }
                vision = if ($raw.PSObject.Properties.Match('vision').Count -gt 0) { [bool]$raw.vision } else { $false }
                reasoning = if ($raw.PSObject.Properties.Match('reasoning').Count -gt 0) { [bool]$raw.reasoning } else { $true }
                tools = if ($raw.PSObject.Properties.Match('tools').Count -gt 0) { [bool]$raw.tools } else { $true }
                maxInputTokens = if ($raw.maxInputTokens) { [int]$raw.maxInputTokens } else { 131072 }
                maxOutputTokens = if ($raw.maxOutputTokens) { [int]$raw.maxOutputTokens } else { 32768 }
                maxTokensField = if ($raw.maxTokensField) { [string]$raw.maxTokensField } else { 'max_tokens' }
                efforts = if ($raw.efforts -and ($raw.efforts -is [System.Array] -or $raw.efforts -is [System.Collections.IList])) { @($raw.efforts) } else { @() }
                supportsDisabled = $null
            }
            $selectedModels += $mObj
        }
    }

    if ($selectedModels.Count -eq 0) {
        throw 'Please check at least one model to inject into Qoder.'
    }

    $configObj = [ordered]@{
        displayName = $displayNameText.Text.Trim()
        uiBaseUrl = $uiUrlText.Text.Trim()
        upstreamBaseUrl = $upstreamUrlText.Text.Trim()
        replaceProviderKey = if ([string]::IsNullOrWhiteSpace($replaceKeyText.Text)) { 'anthropic' } else { $replaceKeyText.Text.Trim() }
        replaceProviderDisplayName = if ([string]::IsNullOrWhiteSpace($displayNameText.Text)) { 'Anthropic (Claude)' } else { $displayNameText.Text.Trim() }
        replaceProviderIndex = [int]$replaceIndexNumeric.Value
        skipValidation = $true
        firstPayloadTimeoutMs = 180000
        streamIdleTimeoutMs = 300000
        models = $selectedModels
    }
    $json = ConvertTo-Json $configObj -Depth 10
    $parent = Split-Path -Parent $TargetPath
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    [IO.File]::WriteAllText($TargetPath, $json, [Text.UTF8Encoding]::new($false))
    Save-StoredApiKey $TargetPath $apiKeyText.Text
    return $selectedModels.Count
}

function Refresh-Profiles([string]$SelectPath) {
    $configCombo.Items.Clear()
    $script:ProfilesByPath = @{}
    foreach ($profile in @(Get-ConfigProfiles)) {
        $script:ProfilesByPath[$profile.FullName] = $profile
        $null = $configCombo.Items.Add($profile.FullName)
    }
    if (-not [string]::IsNullOrWhiteSpace($SelectPath) -and (Test-Path -LiteralPath $SelectPath -PathType Leaf)) {
        $fullPath = [IO.Path]::GetFullPath($SelectPath)
        if (-not $configCombo.Items.Contains($fullPath)) {
            $null = $configCombo.Items.Add($fullPath)
        }
        $configCombo.SelectedItem = $fullPath
    }
    elseif ($configCombo.Items.Count -gt 0) {
        $configCombo.SelectedIndex = 0
    }
    Populate-FieldsFromProfile (Get-SelectedConfigPath)
}

function Invoke-InspectUi([switch]$Clear) {
    Set-Busy $true 'Inspecting installation...'
    try {
        $result = Invoke-PatcherLocal -Action Inspect -TargetInstallDir $installText.Text.Trim()
        Add-Output $result -Clear:$Clear
        $statusLabel.Text = 'Inspection completed'
    }
    catch {
        Add-Output ("[ERROR] " + $_.Exception.Message) -Clear:$Clear
        $statusLabel.Text = 'Inspection failed'
    }
    finally {
        Set-Busy $false $statusLabel.Text
    }
}

$configCombo.Add_SelectedIndexChanged({
    Populate-FieldsFromProfile (Get-SelectedConfigPath)
})

$modelCheckList.Add_ItemCheck({
    $form.BeginInvoke([Action]{ Update-ModelCountLabel })
})

$selectAllButton.Add_Click({
    for ($i = 0; $i -lt $modelCheckList.Items.Count; $i++) {
        $modelCheckList.SetItemChecked($i, $true)
    }
    Update-ModelCountLabel
})

$uncheckAllButton.Add_Click({
    for ($i = 0; $i -lt $modelCheckList.Items.Count; $i++) {
        $modelCheckList.SetItemChecked($i, $false)
    }
    Update-ModelCountLabel
})

function Show-ModelEditorDialog([psobject]$InitialModel = $null) {
    $isEdit = $null -ne $InitialModel
    $dlg = [Windows.Forms.Form]::new()
    $dlg.Text = if ($isEdit) { "Edit Model Properties: $($InitialModel.id)" } else { 'Add New Model' }
    $dlg.Size = [Drawing.Size]::new(520, 500)
    $dlg.StartPosition = 'CenterParent'
    $dlg.FormBorderStyle = 'FixedDialog'
    $dlg.MaximizeBox = $false
    $dlg.MinimizeBox = $false
    $dlg.Font = [Drawing.Font]::new('Segoe UI', 9)

    $lblId = [Windows.Forms.Label]::new()
    $lblId.Text = 'Model ID (API 标识符，如 gpt-5.6-terra):'
    $lblId.Location = [Drawing.Point]::new(20, 16)
    $lblId.AutoSize = $true
    $dlg.Controls.Add($lblId)

    $txtId = [Windows.Forms.TextBox]::new()
    $txtId.Text = if ($isEdit) { [string]$InitialModel.id } else { '' }
    $txtId.Location = [Drawing.Point]::new(20, 36)
    $txtId.Size = [Drawing.Size]::new(460, 25)
    $dlg.Controls.Add($txtId)

    $lblName = [Windows.Forms.Label]::new()
    $lblName.Text = 'Display Name (界面显示名称，如 GPT-5.6 Terra):'
    $lblName.Location = [Drawing.Point]::new(20, 68)
    $lblName.AutoSize = $true
    $dlg.Controls.Add($lblName)

    $txtName = [Windows.Forms.TextBox]::new()
    $txtName.Text = if ($isEdit -and $InitialModel.displayName) { [string]$InitialModel.displayName } else { '' }
    $txtName.Location = [Drawing.Point]::new(20, 88)
    $txtName.Size = [Drawing.Size]::new(460, 25)
    $dlg.Controls.Add($txtName)

    $grpParams = [Windows.Forms.GroupBox]::new()
    $grpParams.Text = 'Token 与上下文配置'
    $grpParams.Location = [Drawing.Point]::new(20, 122)
    $grpParams.Size = [Drawing.Size]::new(460, 126)
    $dlg.Controls.Add($grpParams)

    $lblIn = [Windows.Forms.Label]::new()
    $lblIn.Text = 'Context Window (maxInputTokens):'
    $lblIn.Location = [Drawing.Point]::new(15, 25)
    $lblIn.AutoSize = $true
    $grpParams.Controls.Add($lblIn)

    $numIn = [Windows.Forms.NumericUpDown]::new()
    $numIn.Minimum = 1024
    $numIn.Maximum = 10000000
    $numIn.Increment = 4096
    $numIn.Value = if ($isEdit -and $InitialModel.maxInputTokens) { [int]$InitialModel.maxInputTokens } else { 131072 }
    $numIn.Location = [Drawing.Point]::new(245, 22)
    $numIn.Size = [Drawing.Size]::new(200, 25)
    $grpParams.Controls.Add($numIn)

    $lblOut = [Windows.Forms.Label]::new()
    $lblOut.Text = 'Max Output (maxOutputTokens):'
    $lblOut.Location = [Drawing.Point]::new(15, 58)
    $lblOut.AutoSize = $true
    $grpParams.Controls.Add($lblOut)

    $numOut = [Windows.Forms.NumericUpDown]::new()
    $numOut.Minimum = 512
    $numOut.Maximum = 10000000
    $numOut.Increment = 1024
    $numOut.Value = if ($isEdit -and $InitialModel.maxOutputTokens) { [int]$InitialModel.maxOutputTokens } else { 32768 }
    $numOut.Location = [Drawing.Point]::new(245, 55)
    $numOut.Size = [Drawing.Size]::new(200, 25)
    $grpParams.Controls.Add($numOut)

    $lblField = [Windows.Forms.Label]::new()
    $lblField.Text = 'Token Parameter Field Name:'
    $lblField.Location = [Drawing.Point]::new(15, 91)
    $lblField.AutoSize = $true
    $grpParams.Controls.Add($lblField)

    $cmbField = [Windows.Forms.ComboBox]::new()
    $cmbField.DropDownStyle = 'DropDownList'
    $null = $cmbField.Items.Add('max_tokens')
    $null = $cmbField.Items.Add('max_completion_tokens')
    $currField = if ($isEdit -and $InitialModel.maxTokensField) { [string]$InitialModel.maxTokensField } else { 'max_tokens' }
    $cmbField.SelectedItem = if ($currField -eq 'max_completion_tokens') { 'max_completion_tokens' } else { 'max_tokens' }
    $cmbField.Location = [Drawing.Point]::new(245, 88)
    $cmbField.Size = [Drawing.Size]::new(200, 25)
    $grpParams.Controls.Add($cmbField)

    $grpCaps = [Windows.Forms.GroupBox]::new()
    $grpCaps.Text = '模型能力特性'
    $grpCaps.Location = [Drawing.Point]::new(20, 258)
    $grpCaps.Size = [Drawing.Size]::new(460, 100)
    $dlg.Controls.Add($grpCaps)

    $chkTools = [Windows.Forms.CheckBox]::new()
    $chkTools.Text = 'Tool / Function Calling (tools)'
    $chkTools.Checked = if ($isEdit -and $InitialModel.PSObject.Properties.Match('tools').Count -gt 0) { [bool]$InitialModel.tools } else { $true }
    $chkTools.Location = [Drawing.Point]::new(20, 25)
    $chkTools.AutoSize = $true
    $grpCaps.Controls.Add($chkTools)

    $chkReason = [Windows.Forms.CheckBox]::new()
    $chkReason.Text = 'Reasoning / Thinking Mode (reasoning)'
    $chkReason.Checked = if ($isEdit -and $InitialModel.PSObject.Properties.Match('reasoning').Count -gt 0) { [bool]$InitialModel.reasoning } else { $true }
    $chkReason.Location = [Drawing.Point]::new(20, 58)
    $chkReason.AutoSize = $true
    $grpCaps.Controls.Add($chkReason)

    $chkVision = [Windows.Forms.CheckBox]::new()
    $chkVision.Text = 'Vision / Multimodal (vision)'
    $chkVision.Checked = if ($isEdit -and $InitialModel.PSObject.Properties.Match('vision').Count -gt 0) { [bool]$InitialModel.vision } else { $false }
    $chkVision.Location = [Drawing.Point]::new(260, 25)
    $chkVision.AutoSize = $true
    $grpCaps.Controls.Add($chkVision)

    $btnOk = [Windows.Forms.Button]::new()
    $btnOk.Text = if ($isEdit) { 'Update' } else { 'Add' }
    $btnOk.Location = [Drawing.Point]::new(295, 375)
    $btnOk.Size = [Drawing.Size]::new(90, 32)
    $btnOk.DialogResult = 'OK'
    $btnOk.BackColor = [Drawing.Color]::FromArgb(235, 245, 255)
    $dlg.Controls.Add($btnOk)

    $btnCancel = [Windows.Forms.Button]::new()
    $btnCancel.Text = 'Cancel'
    $btnCancel.Location = [Drawing.Point]::new(395, 375)
    $btnCancel.Size = [Drawing.Size]::new(85, 32)
    $btnCancel.DialogResult = 'Cancel'
    $dlg.Controls.Add($btnCancel)

    $dlg.AcceptButton = $btnOk
    $dlg.CancelButton = $btnCancel

    $res = $dlg.ShowDialog($form)
    if ($res -eq 'OK') {
        $idVal = $txtId.Text.Trim()
        if ([string]::IsNullOrWhiteSpace($idVal)) {
            [Windows.Forms.MessageBox]::Show($form, 'Model ID cannot be empty.', 'Validation Error', 'OK', 'Error') | Out-Null
            $dlg.Dispose()
            return $null
        }
        $nameVal = $txtName.Text.Trim()
        $modelResult = [pscustomobject]@{
            id = $idVal
            displayName = if ([string]::IsNullOrWhiteSpace($nameVal)) { $idVal } else { $nameVal }
            vision = $chkVision.Checked
            reasoning = $chkReason.Checked
            tools = $chkTools.Checked
            maxInputTokens = [int]$numIn.Value
            maxOutputTokens = [int]$numOut.Value
            maxTokensField = [string]$cmbField.SelectedItem
            efforts = @()
            supportsDisabled = $null
        }
        $dlg.Dispose()
        return $modelResult
    }
    $dlg.Dispose()
    return $null
}

$addModelButton.Add_Click({
    $newModel = Show-ModelEditorDialog $null
    if ($null -ne $newModel) {
        $newWrapper = [ModelItemWrapper]::new($newModel.id, $newModel.displayName, $newModel)
        $idx = $modelCheckList.Items.Add($newWrapper)
        $modelCheckList.SetItemChecked($idx, $true)
        $modelCheckList.SelectedIndex = $idx
        Update-ModelCountLabel
    }
})

$editModelButton.Add_Click({
    $selIdx = $modelCheckList.SelectedIndex
    if ($selIdx -lt 0) {
        [Windows.Forms.MessageBox]::Show($form, 'Please select a model from the list to edit (or double-click it).', 'Notice', 'OK', 'Information') | Out-Null
        return
    }
    $wrapper = $modelCheckList.Items[$selIdx]
    $editedModel = Show-ModelEditorDialog $wrapper.RawModel
    if ($null -ne $editedModel) {
        $isChecked = $modelCheckList.GetItemChecked($selIdx)
        $newWrapper = [ModelItemWrapper]::new($editedModel.id, $editedModel.displayName, $editedModel)
        $modelCheckList.Items[$selIdx] = $newWrapper
        $modelCheckList.SetItemChecked($selIdx, $isChecked)
        $modelCheckList.SelectedIndex = $selIdx
        Update-ModelCountLabel
    }
})

$modelCheckList.Add_DoubleClick({
    $selIdx = $modelCheckList.SelectedIndex
    if ($selIdx -ge 0) {
        $editModelButton.PerformClick()
    }
})

$removeModelButton.Add_Click({
    $selIdx = $modelCheckList.SelectedIndex
    if ($selIdx -ge 0) {
        $modelCheckList.Items.RemoveAt($selIdx)
        Update-ModelCountLabel
    }
})

$testConnButton.Add_Click({
    $url = $upstreamUrlText.Text.Trim()
    if ([string]::IsNullOrWhiteSpace($url)) {
        [Windows.Forms.MessageBox]::Show($form, 'Please enter Upstream Base URL.', 'Notice', 'OK', 'Warning') | Out-Null
        return
    }
    Set-Busy $true 'Testing connection...'
    try {
        $key = $apiKeyText.Text.Trim()
        Add-Output "[INFO] Testing upstream: $url ..."
        $modelsEndpoint = $url.TrimEnd('/') + '/models'
        $headers = @{}
        if (-not [string]::IsNullOrWhiteSpace($key)) { $headers.Authorization = "Bearer $key" }
        $resp = Invoke-RestMethod -Uri $modelsEndpoint -Headers $headers -Method Get -TimeoutSec 5 -ErrorAction Stop
        Add-Output "[OK] Upstream service responded successfully via /models."
        $statusLabel.Text = 'Upstream reachable'
        [Windows.Forms.MessageBox]::Show($form, "Upstream connection successful! Endpoint: $modelsEndpoint", 'Success', 'OK', 'Information') | Out-Null
    }
    catch {
        Add-Output ("[WARN] Test connection returned: " + $_.Exception.Message + " (For custom gateways, /chat/completions is used during actual inference).")
        $statusLabel.Text = 'Test finished'
    }
    finally {
        Set-Busy $false $statusLabel.Text
    }
})

$saveButton.Add_Click({
    $configPath = Get-SelectedConfigPath
    if ([string]::IsNullOrWhiteSpace($configPath)) {
        [Windows.Forms.MessageBox]::Show($form, 'Please select or create a profile first.', 'Notice', 'OK', 'Warning') | Out-Null
        return
    }
    try {
        $count = Save-CurrentConfig $configPath
        Add-Output "[OK] Profile saved to: $configPath ($count models checked)"
        $statusLabel.Text = 'Profile saved'
        [Windows.Forms.MessageBox]::Show($form, "Profile successfully saved!

Selected models count: $count
File: $configPath", 'Saved', 'OK', 'Information') | Out-Null
    }
    catch {
        Add-Output ("[ERROR] Save failed: " + $_.Exception.Message)
        [Windows.Forms.MessageBox]::Show($form, "Save failed: " + $_.Exception.Message, 'Error', 'OK', 'Error') | Out-Null
    }
})

$browseInstallButton.Add_Click({
    $dialog = [Windows.Forms.FolderBrowserDialog]::new()
    $dialog.Description = 'Select Qoder CN installation folder'
    $dialog.SelectedPath = $installText.Text
    if ($dialog.ShowDialog($form) -eq 'OK') {
        $installText.Text = $dialog.SelectedPath
    }
    $dialog.Dispose()
})

$browseConfigButton.Add_Click({
    $dialog = [Windows.Forms.OpenFileDialog]::new()
    $dialog.Title = 'Select profile JSON'
    $dialog.Filter = 'JSON profile (*.json)|*.json|All files (*.*)|*.*'
    $dialog.InitialDirectory = $script:ConfigsDir
    if ($dialog.ShowDialog($form) -eq 'OK') {
        Refresh-Profiles $dialog.FileName
    }
    $dialog.Dispose()
})

$refreshButton.Add_Click({
    $selected = Get-SelectedConfigPath
    Refresh-Profiles $selected
    $statusLabel.Text = 'Profiles refreshed'
})

$inspectButton.Add_Click({ Invoke-InspectUi -Clear })

$dryRunButton.Add_Click({
    $configPath = Get-SelectedConfigPath
    if ([string]::IsNullOrWhiteSpace($configPath)) {
        [Windows.Forms.MessageBox]::Show($form, 'Please select a profile first.', 'Notice', 'OK', 'Warning') | Out-Null
        return
    }
    try {
        $null = Save-CurrentConfig $configPath
    }
    catch {
        [Windows.Forms.MessageBox]::Show($form, "Config validation failed: " + $_.Exception.Message, 'Notice', 'OK', 'Warning') | Out-Null
        return
    }

    Set-Busy $true 'Running Dry Run...'
    try {
        $result = Invoke-PatcherLocal -Action DryRun -TargetInstallDir $installText.Text.Trim() -ConfigPath $configPath
        Add-Output $result -Clear
        $statusLabel.Text = 'Dry Run completed'
    }
    catch {
        Add-Output ("[ERROR] " + ($_ | Out-String)) -Clear
        $statusLabel.Text = 'Dry Run failed'
    }
    finally {
        Set-Busy $false $statusLabel.Text
    }
})

$applyButton.Add_Click({
    $configPath = Get-SelectedConfigPath
    if ([string]::IsNullOrWhiteSpace($configPath)) {
        [Windows.Forms.MessageBox]::Show($form, 'Please select a profile first.', 'Notice', 'OK', 'Warning') | Out-Null
        return
    }
    try {
        $modelCount = Save-CurrentConfig $configPath
    }
    catch {
        [Windows.Forms.MessageBox]::Show($form, "Config validation failed: " + $_.Exception.Message, 'Notice', 'OK', 'Warning') | Out-Null
        return
    }

    $answer = [Windows.Forms.MessageBox]::Show(
        $form,
        "Make sure Qoder CN is completely closed.

Ready to install/upgrade runtime patch with $modelCount selected models and API Key.

Continue?",
        'Confirm Install / Upgrade',
        'YesNo',
        'Question'
    )
    if ($answer -ne 'Yes') {
        return
    }
    Set-Busy $true 'Requesting administrator permission and applying patch...'
    try {
        Add-Output '[INFO] Please approve the Windows administrator prompt to continue...' -Clear
        $result = Invoke-PatcherElevated -Action Apply -TargetInstallDir $installText.Text.Trim() -ConfigPath $configPath
        Add-Output $result.Output
        if ($result.ExitCode -ne 0) {
            throw "Installation failed with exit code: $($result.ExitCode)."
        }
        Add-Output '[OK] Installation completed. Running final inspection...'
        $inspection = Invoke-PatcherLocal -Action Inspect -TargetInstallDir $installText.Text.Trim()
        Add-Output $inspection
        $statusLabel.Text = 'Patch installed successfully!'
        [Windows.Forms.MessageBox]::Show($form, "Patch installed successfully!

Injected $modelCount custom models.
You can now launch Qoder CN to start chatting!", 'Success', 'OK', 'Information') | Out-Null
    }
    catch {
        Add-Output ("[ERROR] " + ($_ | Out-String))
        $statusLabel.Text = 'Installation failed or was cancelled'
    }
    finally {
        Set-Busy $false $statusLabel.Text
    }
})

$restoreButton.Add_Click({
    $answer = [Windows.Forms.MessageBox]::Show(
        $form,
        "Make sure Qoder CN is closed.

Restore original official runtime from backup?",
        'Confirm Restore',
        'YesNo',
        'Warning'
    )
    if ($answer -ne 'Yes') {
        return
    }
    Set-Busy $true 'Restoring official runtime...'
    try {
        Add-Output '[INFO] Please approve the Windows administrator prompt...' -Clear
        $result = Invoke-PatcherElevated -Action Restore -TargetInstallDir $installText.Text.Trim()
        Add-Output $result.Output
        if ($result.ExitCode -ne 0) {
            throw "Restore failed with code: $($result.ExitCode)."
        }
        Add-Output '[OK] Official runtime restored successfully.'
        $statusLabel.Text = 'Restore completed'
        [Windows.Forms.MessageBox]::Show($form, 'Official runtime restored successfully.', 'Restored', 'OK', 'Information') | Out-Null
    }
    catch {
        Add-Output ("[ERROR] " + ($_ | Out-String))
        $statusLabel.Text = 'Restore failed'
    }
    finally {
        Set-Busy $false $statusLabel.Text
    }
})

$launchButton.Add_Click({
    if (Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -like '*qoder*' } | Select-Object -First 1) {
        [Windows.Forms.MessageBox]::Show($form, 'Qoder CN is already running. Close it first, then launch it from this manager so it can receive the temporary API Key.', 'Qoder Already Running', 'OK', 'Warning') | Out-Null
        return
    }
    $executable = Get-QoderExecutable $installText.Text.Trim()
    if ($null -eq $executable) {
        [Windows.Forms.MessageBox]::Show($form, 'Qoder CN executable not found in selected directory.', 'Launch Failed', 'OK', 'Error') | Out-Null
        return
    }
    $profilePath = Get-SelectedConfigPath
    $secret = Get-StoredApiKey $profilePath
    $previousConfig = [Environment]::GetEnvironmentVariable('QODER_CN_CUSTOM_PROVIDER_CONFIG', 'Process')
    $previousKey = [Environment]::GetEnvironmentVariable('QODER_CN_CUSTOM_PROVIDER_API_KEY', 'Process')
    try {
        [Environment]::SetEnvironmentVariable('QODER_CN_CUSTOM_PROVIDER_CONFIG', $script:RuntimeConfigPath, 'Process')
        [Environment]::SetEnvironmentVariable('QODER_CN_CUSTOM_PROVIDER_API_KEY', $(if ([string]::IsNullOrWhiteSpace($secret)) { $null } else { $secret }), 'Process')
        Start-Process -FilePath $executable
    }
    finally {
        [Environment]::SetEnvironmentVariable('QODER_CN_CUSTOM_PROVIDER_CONFIG', $previousConfig, 'Process')
        [Environment]::SetEnvironmentVariable('QODER_CN_CUSTOM_PROVIDER_API_KEY', $previousKey, 'Process')
    }
    $statusLabel.Text = 'Qoder CN launched'
})

$form.Add_Shown({
    Refresh-Profiles $script:PreferredConfigPath
    Invoke-InspectUi -Clear
})

$null = $form.ShowDialog()
$form.Dispose()
