param(
    [Parameter(Mandatory)]
    [string]$BinaryPath,
    [string]$CertificateThumbprint = '',
    [switch]$CreateDevelopmentCertificate
)

$ErrorActionPreference = 'Stop'
$resolvedBinary = (Resolve-Path -LiteralPath $BinaryPath).Path
$cert = $null

if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    $normalizedThumbprint = $CertificateThumbprint.Replace(' ', '').ToUpperInvariant()
    $cert = Get-ChildItem -Path Cert:\CurrentUser\My -CodeSigningCert |
        Where-Object { $_.Thumbprint -eq $normalizedThumbprint } |
        Select-Object -First 1
    if ($null -eq $cert) { throw "Code-signing certificate was not found: $normalizedThumbprint" }
}
elseif ($CreateDevelopmentCertificate) {
    $cert = Get-ChildItem -Path Cert:\CurrentUser\My -CodeSigningCert |
        Where-Object { $_.Subject -eq 'CN=QoderCN-GatewayManager-Development' } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1
    if ($null -eq $cert) {
        $cert = New-SelfSignedCertificate -Type CodeSigningCert `
            -Subject 'CN=QoderCN-GatewayManager-Development' `
            -CertStoreLocation 'Cert:\CurrentUser\My' `
            -NotAfter (Get-Date).AddYears(2)
    }

    # Development signatures are trusted only by the current Windows user.
    $rootStore = [Security.Cryptography.X509Certificates.X509Store]::new('Root', 'CurrentUser')
    try {
        $rootStore.Open([Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $rootStore.Add($cert)
    }
    finally {
        $rootStore.Close()
    }
}
else {
    throw 'Signing requested without -CertificateThumbprint or -CreateDevelopmentCertificate.'
}

Write-Host "Signing $resolvedBinary with $($cert.Subject) [$($cert.Thumbprint)] ..." -ForegroundColor Cyan
try {
    $null = Set-AuthenticodeSignature -FilePath $resolvedBinary -Certificate $cert -HashAlgorithm SHA256 `
        -TimestampServer 'http://timestamp.digicert.com' -ErrorAction Stop
}
catch {
    if (-not $CreateDevelopmentCertificate) { throw }
    Write-Host '[WARN] Timestamping development signature failed; signing without a timestamp.' -ForegroundColor Yellow
    $null = Set-AuthenticodeSignature -FilePath $resolvedBinary -Certificate $cert -HashAlgorithm SHA256 -ErrorAction Stop
}

$signature = Get-AuthenticodeSignature -FilePath $resolvedBinary
if ($signature.Status -ne 'Valid') {
    throw "Authenticode verification failed: $($signature.Status) - $($signature.StatusMessage)"
}
Write-Host "[SUCCESS] Authenticode signature verified: $($signature.SignerCertificate.Subject)" -ForegroundColor Green
