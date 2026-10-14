$ErrorActionPreference = 'Stop'
$key = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'cloudflare-commission-public.json') -Raw | ConvertFrom-Json
$settings = Get-Content -LiteralPath 'D:\Websites\octopus-dash-api.vilmos.co.uk\_energy-private\server-settings.json' -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($settings.INTERNAL_API_TOKEN) -or $settings.INTERNAL_API_TOKEN.Length -lt 32) { throw 'Production API token is missing' }
$parameters = New-Object Security.Cryptography.RSAParameters
$parameters.Modulus = [Convert]::FromBase64String($key.modulus)
$parameters.Exponent = [Convert]::FromBase64String($key.exponent)
$rsa = New-Object Security.Cryptography.RSACng
try {
    $rsa.ImportParameters($parameters)
    $cipher = $rsa.Encrypt([Text.Encoding]::UTF8.GetBytes($settings.INTERNAL_API_TOKEN), [Security.Cryptography.RSAEncryptionPadding]::OaepSHA256)
    # One-time ciphertext can be decrypted only by the owner's private key; never output the token.
    Write-Host ('CF_ENVELOPE:' + [Convert]::ToBase64String($cipher))
} finally { $rsa.Dispose() }
