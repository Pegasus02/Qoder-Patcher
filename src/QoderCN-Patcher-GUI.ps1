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
$script:RuntimeConfigPath = Join-Path $env:USERPROFILE '.qoder-cn\custom-openai-provider-v2.1.json'
$script:BackupRoot = Join-Path $env:LOCALAPPDATA 'QoderCNOpenAICompatiblePatcher\backups-v2'
$script:DefaultInstallDir = $InstallDir
$script:PreferredConfigPath = if ([string]::IsNullOrWhiteSpace($DefaultConfigPath)) {
    Join-Path $script:ConfigsDir 'cpa-192.168.50.241.json'
}
else {
    [IO.Path]::GetFullPath($DefaultConfigPath)
}

function Get-ConfigProfiles {
    $profiles = @()
    if (Test-Path -LiteralPath $script:ConfigsDir -PathType Container) {
        foreach ($file in Get-ChildItem -LiteralPath $script:ConfigsDir -Filter '*.json' -File | Sort-Object Name) {
            try {
                $raw = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
                if ($raw -match '"(?:api[_-]?key|access[_-]?token|authorization)"\s*:') {
                    continue
                }
                $config = $raw | ConvertFrom-Json
                $profiles += [pscustomobject]@{
                    Name = $file.Name
                    FullName = $file.FullName
                    DisplayName = [string]$config.displayName
                    Upstream = [string]$config.upstreamBaseUrl
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
        throw 'No valid, secret-free JSON configuration profiles were found.'
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

$form = [Windows.Forms.Form]::new()
$form.Text = 'Qoder CN OpenAI-Compatible Patcher v2.1'
$form.StartPosition = 'CenterScreen'
$form.Size = [Drawing.Size]::new(980, 720)
$form.MinimumSize = [Drawing.Size]::new(900, 650)
$form.Font = [Drawing.Font]::new('Segoe UI', 9)
$form.AutoScaleMode = 'Dpi'

$title = [Windows.Forms.Label]::new()
$title.Text = 'Qoder CN OpenAI-Compatible Patcher'
$title.Font = [Drawing.Font]::new('Segoe UI Semibold', 18)
$title.AutoSize = $true
$title.Location = [Drawing.Point]::new(22, 18)
$form.Controls.Add($title)

$subtitle = [Windows.Forms.Label]::new()
$subtitle.Text = 'Runtime-only patch for OpenAI-compatible services. API keys are entered and stored in Qoder CN.'
$subtitle.AutoSize = $true
$subtitle.ForeColor = [Drawing.Color]::DimGray
$subtitle.Location = [Drawing.Point]::new(25, 57)
$form.Controls.Add($subtitle)

$pathsGroup = [Windows.Forms.GroupBox]::new()
$pathsGroup.Text = 'Installation and configuration'
$pathsGroup.Location = [Drawing.Point]::new(22, 88)
$pathsGroup.Size = [Drawing.Size]::new(920, 155)
$pathsGroup.Anchor = 'Top, Left, Right'
$form.Controls.Add($pathsGroup)

$installLabel = [Windows.Forms.Label]::new()
$installLabel.Text = 'Qoder CN folder'
$installLabel.AutoSize = $true
$installLabel.Location = [Drawing.Point]::new(16, 31)
$pathsGroup.Controls.Add($installLabel)

$installText = [Windows.Forms.TextBox]::new()
$installText.Text = $script:DefaultInstallDir
$installText.Location = [Drawing.Point]::new(145, 27)
$installText.Size = [Drawing.Size]::new(650, 25)
$installText.Anchor = 'Top, Left, Right'
$pathsGroup.Controls.Add($installText)

$browseInstallButton = [Windows.Forms.Button]::new()
$browseInstallButton.Text = 'Browse...'
$browseInstallButton.Location = [Drawing.Point]::new(807, 26)
$browseInstallButton.Size = [Drawing.Size]::new(95, 28)
$browseInstallButton.Anchor = 'Top, Right'
$pathsGroup.Controls.Add($browseInstallButton)

$configLabel = [Windows.Forms.Label]::new()
$configLabel.Text = 'Provider profile'
$configLabel.AutoSize = $true
$configLabel.Location = [Drawing.Point]::new(16, 72)
$pathsGroup.Controls.Add($configLabel)

$configCombo = [Windows.Forms.ComboBox]::new()
$configCombo.DropDownStyle = 'DropDownList'
$configCombo.Location = [Drawing.Point]::new(145, 68)
$configCombo.Size = [Drawing.Size]::new(540, 25)
$configCombo.Anchor = 'Top, Left, Right'
$pathsGroup.Controls.Add($configCombo)

$browseConfigButton = [Windows.Forms.Button]::new()
$browseConfigButton.Text = 'Other JSON...'
$browseConfigButton.Location = [Drawing.Point]::new(695, 67)
$browseConfigButton.Size = [Drawing.Size]::new(105, 28)
$browseConfigButton.Anchor = 'Top, Right'
$pathsGroup.Controls.Add($browseConfigButton)

$openConfigButton = [Windows.Forms.Button]::new()
$openConfigButton.Text = 'Open folder'
$openConfigButton.Location = [Drawing.Point]::new(807, 67)
$openConfigButton.Size = [Drawing.Size]::new(95, 28)
$openConfigButton.Anchor = 'Top, Right'
$pathsGroup.Controls.Add($openConfigButton)

$profileInfo = [Windows.Forms.Label]::new()
$profileInfo.Text = 'No profile selected.'
$profileInfo.AutoEllipsis = $true
$profileInfo.Location = [Drawing.Point]::new(145, 106)
$profileInfo.Size = [Drawing.Size]::new(757, 34)
$profileInfo.ForeColor = [Drawing.Color]::DimGray
$profileInfo.Anchor = 'Top, Left, Right'
$pathsGroup.Controls.Add($profileInfo)

$actionsGroup = [Windows.Forms.GroupBox]::new()
$actionsGroup.Text = 'Actions'
$actionsGroup.Location = [Drawing.Point]::new(22, 253)
$actionsGroup.Size = [Drawing.Size]::new(920, 78)
$actionsGroup.Anchor = 'Top, Left, Right'
$form.Controls.Add($actionsGroup)

function New-ActionButton([string]$Text, [int]$X, [int]$Width) {
    $button = [Windows.Forms.Button]::new()
    $button.Text = $Text
    $button.Location = [Drawing.Point]::new($X, 29)
    $button.Size = [Drawing.Size]::new($Width, 32)
    $actionsGroup.Controls.Add($button)
    return $button
}

$inspectButton = New-ActionButton 'Inspect' 16 105
$dryRunButton = New-ActionButton 'Dry Run' 131 105
$applyButton = New-ActionButton 'Install / Upgrade' 246 150
$restoreButton = New-ActionButton 'Restore latest' 406 135
$launchButton = New-ActionButton 'Launch Qoder CN' 551 145
$refreshButton = New-ActionButton 'Refresh profiles' 706 140

$applyButton.BackColor = [Drawing.Color]::FromArgb(225, 245, 232)
$restoreButton.BackColor = [Drawing.Color]::FromArgb(255, 241, 220)

$outputLabel = [Windows.Forms.Label]::new()
$outputLabel.Text = 'Output'
$outputLabel.AutoSize = $true
$outputLabel.Location = [Drawing.Point]::new(22, 345)
$form.Controls.Add($outputLabel)

$outputBox = [Windows.Forms.RichTextBox]::new()
$outputBox.Location = [Drawing.Point]::new(22, 369)
$outputBox.Size = [Drawing.Size]::new(920, 255)
$outputBox.Anchor = 'Top, Bottom, Left, Right'
$outputBox.ReadOnly = $true
$outputBox.BackColor = [Drawing.Color]::FromArgb(250, 250, 250)
$outputBox.Font = [Drawing.Font]::new('Consolas', 9)
$outputBox.WordWrap = $false
$form.Controls.Add($outputBox)

$safetyLabel = [Windows.Forms.Label]::new()
$safetyLabel.Text = 'Close Qoder CN before Install / Upgrade or Restore. Windows will request administrator approval.'
$safetyLabel.AutoSize = $true
$safetyLabel.ForeColor = [Drawing.Color]::FromArgb(145, 90, 20)
$safetyLabel.Location = [Drawing.Point]::new(22, 638)
$safetyLabel.Anchor = 'Bottom, Left'
$form.Controls.Add($safetyLabel)

$statusStrip = [Windows.Forms.StatusStrip]::new()
$statusLabel = [Windows.Forms.ToolStripStatusLabel]::new()
$statusLabel.Text = 'Ready'
$statusLabel.Spring = $true
$statusLabel.TextAlign = 'MiddleLeft'
$null = $statusStrip.Items.Add($statusLabel)
$form.Controls.Add($statusStrip)

$script:ProfilesByPath = @{}
$script:ActionButtons = @($inspectButton, $dryRunButton, $applyButton, $restoreButton, $launchButton, $refreshButton)

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
    $openConfigButton.Enabled = -not $Busy
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

function Update-ProfileInfo {
    $path = Get-SelectedConfigPath
    if ($null -eq $path -or -not $script:ProfilesByPath.ContainsKey($path)) {
        $profileInfo.Text = 'Custom JSON profile. Dry Run will validate it before installation.'
        return
    }
    $profile = $script:ProfilesByPath[$path]
    $profileInfo.Text = "$($profile.DisplayName)  |  $($profile.Upstream)  |  $($profile.ModelCount) models"
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
    Update-ProfileInfo
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

$configCombo.Add_SelectedIndexChanged({ Update-ProfileInfo })

$browseInstallButton.Add_Click({
    $dialog = [Windows.Forms.FolderBrowserDialog]::new()
    $dialog.Description = 'Select the Qoder CN installation folder'
    $dialog.SelectedPath = $installText.Text
    if ($dialog.ShowDialog($form) -eq 'OK') {
        $installText.Text = $dialog.SelectedPath
    }
    $dialog.Dispose()
})

$browseConfigButton.Add_Click({
    $dialog = [Windows.Forms.OpenFileDialog]::new()
    $dialog.Title = 'Select a provider JSON profile'
    $dialog.Filter = 'JSON configuration (*.json)|*.json|All files (*.*)|*.*'
    $dialog.InitialDirectory = $script:ConfigsDir
    if ($dialog.ShowDialog($form) -eq 'OK') {
        Refresh-Profiles $dialog.FileName
    }
    $dialog.Dispose()
})

$openConfigButton.Add_Click({
    if (Test-Path -LiteralPath $script:ConfigsDir -PathType Container) {
        Start-Process -FilePath 'explorer.exe' -ArgumentList @($script:ConfigsDir)
    }
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
        [Windows.Forms.MessageBox]::Show($form, 'Select a provider profile first.', 'Configuration required', 'OK', 'Warning') | Out-Null
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
        [Windows.Forms.MessageBox]::Show($form, 'Select a provider profile first.', 'Configuration required', 'OK', 'Warning') | Out-Null
        return
    }
    $answer = [Windows.Forms.MessageBox]::Show(
        $form,
        "Close Qoder CN before continuing.`r`n`r`nInstall or upgrade the v2.1 runtime patch now?",
        'Confirm installation',
        'YesNo',
        'Question'
    )
    if ($answer -ne 'Yes') {
        return
    }
    Set-Busy $true 'Waiting for administrator installation...'
    try {
        Add-Output '[INFO] Approve the Windows administrator prompt to continue.' -Clear
        $result = Invoke-PatcherElevated -Action Apply -TargetInstallDir $installText.Text.Trim() -ConfigPath $configPath
        Add-Output $result.Output
        if ($result.ExitCode -ne 0) {
            throw "Installer exited with code $($result.ExitCode)."
        }
        Add-Output '[OK] Installation command completed. Running final inspection...'
        $inspection = Invoke-PatcherLocal -Action Inspect -TargetInstallDir $installText.Text.Trim()
        Add-Output $inspection
        $statusLabel.Text = 'Installation completed'
        [Windows.Forms.MessageBox]::Show($form, 'Installation completed. You can launch Qoder CN and add the configured model.', 'Success', 'OK', 'Information') | Out-Null
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
        "Close Qoder CN before continuing.`r`n`r`nRestore the newest verified runtime backup?",
        'Confirm restore',
        'YesNo',
        'Warning'
    )
    if ($answer -ne 'Yes') {
        return
    }
    Set-Busy $true 'Waiting for administrator restore...'
    try {
        Add-Output '[INFO] Approve the Windows administrator prompt to continue.' -Clear
        $result = Invoke-PatcherElevated -Action Restore -TargetInstallDir $installText.Text.Trim()
        Add-Output $result.Output
        if ($result.ExitCode -ne 0) {
            throw "Restore exited with code $($result.ExitCode)."
        }
        Add-Output '[OK] Restore completed.'
        $statusLabel.Text = 'Restore completed'
    }
    catch {
        Add-Output ("[ERROR] " + ($_ | Out-String))
        $statusLabel.Text = 'Restore failed or was cancelled'
    }
    finally {
        Set-Busy $false $statusLabel.Text
    }
})

$launchButton.Add_Click({
    $executable = Get-QoderExecutable $installText.Text.Trim()
    if ($null -eq $executable) {
        [Windows.Forms.MessageBox]::Show($form, 'Qoder CN executable was not found in the selected folder.', 'Launch failed', 'OK', 'Error') | Out-Null
        return
    }
    Start-Process -FilePath $executable
    $statusLabel.Text = 'Qoder CN launched'
})

$form.Add_Shown({
    Refresh-Profiles $script:PreferredConfigPath
    Invoke-InspectUi -Clear
})

$null = $form.ShowDialog()
$form.Dispose()
