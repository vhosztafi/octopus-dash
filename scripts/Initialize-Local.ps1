param()
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $workspace '.env'
$secretDirectory = Join-Path $workspace 'secrets'
if (Test-Path -LiteralPath $envPath) { throw '.env already exists; refusing to overwrite local credentials.' }
if (Test-Path -LiteralPath $secretDirectory) { throw 'secrets directory already exists; inspect it before initializing.' }

function New-RandomSecret { return [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)).ToLowerInvariant() }
$dashboardPassword = New-RandomSecret
$sessionKey = New-RandomSecret
$internalKey = New-RandomSecret
$sqlPassword = 'Sql!' + (New-RandomSecret)
$certificatePassword = New-RandomSecret

# Feed the password through stdin so it does not appear in command-line/process listings.
$bcryptScript = "let text='';process.stdin.on('data',c=>text+=c);process.stdin.on('end',()=>process.stdout.write(require('./web/node_modules/bcryptjs').hashSync(text.trim(),12)));"
Push-Location $workspace
try { $dashboardHash = $dashboardPassword | node -e $bcryptScript; if ($LASTEXITCODE -ne 0) { throw 'Password hashing failed; install web dependencies first.' } }
finally { Pop-Location }

New-Item -ItemType Directory -Path $secretDirectory | Out-Null
$rsa = [Security.Cryptography.RSA]::Create(3072)
try {
    $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=EnergyDash Local Data Protection', $rsa, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddDays(-1), [DateTimeOffset]::UtcNow.AddYears(10))
    try { [IO.File]::WriteAllBytes((Join-Path $secretDirectory 'data-protection.pfx'), $certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $certificatePassword)) }
    finally { $certificate.Dispose() }
}
finally { $rsa.Dispose() }

$envLines = @(
    "APP_PASSWORD_HASH='$dashboardHash'",
    "SESSION_SECRET=$sessionKey",
    "INTERNAL_API_TOKEN=$internalKey",
    'API_BASE_URL=http://api:8080',
    "MSSQL_SA_PASSWORD=$sqlPassword",
    'DATA_PROTECTION_CERT=./secrets/data-protection.pfx',
    "DATA_PROTECTION_CERT_PASSWORD=$certificatePassword",
    'Octopus__ApiKey=', 'Octopus__AccountNumber=',
    'Tesla__ClientId=', 'Tesla__ClientSecret=', 'Tesla__RefreshToken=', 'Tesla__Region=eu',
    'Smtp__Host=', 'Smtp__Port=587', 'Smtp__From=', 'Smtp__User=', 'Smtp__Password=', 'Smtp__Tls=StartTls',
    'Ntfy__Topic=', 'Ntfy__BaseUrl=https://ntfy.sh', 'Ntfy__Token=', 'TZ=Europe/London'
)
[IO.File]::WriteAllLines($envPath, $envLines, [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $secretDirectory 'local-access.txt'), "Home Energy local login`nURL: http://localhost:3000`nPassword: $dashboardPassword`n`nKeep this file private. All bootstrap files are ignored by Git and Docker build contexts.`n", [Text.UTF8Encoding]::new($false))

# Private Windows configuration: owner and SYSTEM only. Docker Desktop runs for this owner.
$owner = [Security.Principal.WindowsIdentity]::GetCurrent().User
$system = [Security.Principal.SecurityIdentifier]::new('S-1-5-18')
foreach ($path in @($envPath, $secretDirectory)) {
    $acl = Get-Acl -LiteralPath $path
    $acl.SetAccessRuleProtection($true, $false)
    $inheritance = if ($path -eq $secretDirectory) { [Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit' } else { [Security.AccessControl.InheritanceFlags]::None }
    foreach ($identity in @($owner, $system)) { $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($identity, [Security.AccessControl.FileSystemRights]::FullControl, $inheritance, [Security.AccessControl.PropagationFlags]::None, [Security.AccessControl.AccessControlType]::Allow)) }
    Set-Acl -LiteralPath $path -AclObject $acl
}
Write-Output 'Initialized private .env and certificate. Login is in secrets/local-access.txt. No provider credentials were populated.'
