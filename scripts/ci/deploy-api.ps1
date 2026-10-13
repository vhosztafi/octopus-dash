param([switch]$ValidateOnly, [string]$ArtifactDirectory = '')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
if (-not $ArtifactDirectory) { $ArtifactDirectory = Join-Path $repo 'incoming' }
$siteHost = 'octopus-dash-api.vilmos.co.uk'
$root = [IO.Path]::GetFullPath('D:\Websites\octopus-dash-api.vilmos.co.uk\www').TrimEnd('\')
$privateRoot = [IO.Path]::Combine([IO.Path]::GetDirectoryName($root), '_energy-private')
$zip = Join-Path $ArtifactDirectory 'api.zip'
$manifest = Get-Content -LiteralPath (Join-Path $ArtifactDirectory 'api-manifest.json') -Raw | ConvertFrom-Json
if ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne $manifest.sha256) { throw 'API artifact checksum mismatch' }
Push-Location $repo
try {
    $revision = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $revision -ne $manifest.revision) { throw 'Artifact and deployment checkout revisions differ' }
} finally { Pop-Location }
if ($ValidateOnly) { Write-Host 'Artifact preflight passed; server untouched.'; return }

Import-Module WebAdministration
Add-Type -AssemblyName System.IO.Compression.FileSystem
$sites = @(Get-Website | Where-Object { @($_.Bindings.Collection | Where-Object { $_.protocol -eq 'https' -and $_.bindingInformation.EndsWith(':' + $siteHost, [StringComparison]::OrdinalIgnoreCase) }).Count -gt 0 })
if ($sites.Count -ne 1) { throw 'Expected exactly one IIS site with the specified HTTPS host binding' }
$site = $sites[0]
$physicalPath = [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($site.PhysicalPath)).TrimEnd('\')
if ($physicalPath -ne $root -or $root -ne 'D:\Websites\octopus-dash-api.vilmos.co.uk\www') { throw 'IIS physical path differs from the explicit deployment target' }
$pool = [string]$site.ApplicationPool
$poolItem = Get-Item -LiteralPath "IIS:\AppPools\$pool"
if ([int]$poolItem.processModel.identityType -ne 4) { throw 'Integrated SQL deployment requires the existing ApplicationPoolIdentity' }
if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw 'Expected existing IIS web directory' }
New-Item -ItemType Directory -Force -Path $privateRoot | Out-Null
# Secrets are never artifacts or web content. Restrict inheritance before generating them.
& icacls.exe $privateRoot '/inheritance:r' '/grant:r' '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' "IIS AppPool\${pool}:(OI)(CI)RX" | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Cannot secure persistent configuration directory' }
$lock = [IO.File]::Open((Join-Path $privateRoot 'deployment.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
$stage = Join-Path $privateRoot ('stage-' + [guid]::NewGuid().ToString('N'))
$backup = Join-Path $privateRoot ('backup-' + (Get-Date -Format 'yyyyMMddHHmmss') + '-' + [guid]::NewGuid().ToString('N'))
$configPath = Join-Path $privateRoot 'server-settings.json'
$offline = Join-Path $root 'app_offline.htm'
$touched = $false
$backedUp = $false
function Wait-Pool([string]$Desired) {
    $deadline = (Get-Date).AddSeconds(45)
    do {
        if ((Get-WebAppPoolState -Name $pool).Value -eq $Desired) { return }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    throw 'IIS pool failed to reach requested state'
}
function Copy-Tree([string]$Source, [string]$Destination) {
    # Both callers use fixed, verified site paths or unique children of the private directory.
    & robocopy.exe $Source $Destination /MIR /R:2 /W:2 /NFL /NDL /NP /NJH /NJS /XF app_offline.htm | Out-Null
    if ($LASTEXITCODE -ge 8) { throw 'Release copy failed' }
}
function Random-Hex {
    $bytes = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    return ([BitConverter]::ToString($bytes)).Replace('-', '').ToLowerInvariant()
}
function Check-Deployment {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    for ($attempt = 0; $attempt -lt 18; $attempt++) {
        try {
            $health = Invoke-RestMethod -Uri "https://$siteHost/health" -TimeoutSec 15
            if ($health.status -ne 'healthy') { throw 'Unhealthy' }
            $null = Invoke-RestMethod -Uri "https://$siteHost/api/status" -Headers @{ Authorization = 'Bearer ' + $settings.INTERNAL_API_TOKEN } -TimeoutSec 15
            $rejected = $false
            try { $null = Invoke-WebRequest -UseBasicParsing -Uri "https://$siteHost/api/status" -TimeoutSec 15 }
            catch { $rejected = $_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 401 }
            if (-not $rejected) { throw 'Unauthenticated request was not rejected' }
            return
        } catch {
            # Omit exception text, headers and response bodies: these can contain credentials.
            Write-Warning ("API verification attempt {0}/18 failed ({1})" -f ($attempt + 1), $_.Exception.GetType().Name)
        }
        Start-Sleep -Seconds 5
    }
    throw 'HTTPS database health or authentication verification failed'
}
try {
    if (-not (Test-Path -LiteralPath $configPath)) {
        $certPath = Join-Path $privateRoot 'data-protection.pfx'
        if (Test-Path -LiteralPath $certPath) { throw 'Existing certificate without settings; manual recovery required' }
        $certPassword = Random-Hex
        $cert = New-SelfSignedCertificate -Subject 'CN=EnergyDash Data Protection' -CertStoreLocation 'Cert:\LocalMachine\My' -KeyAlgorithm RSA -KeyLength 3072 -KeyExportPolicy Exportable -NotAfter (Get-Date).AddYears(10)
        try { Export-PfxCertificate -Cert $cert -FilePath $certPath -Password (ConvertTo-SecureString $certPassword -AsPlainText -Force) | Out-Null }
        finally { Remove-Item -LiteralPath ('Cert:\LocalMachine\My\' + $cert.Thumbprint) }
        @{
            ConnectionStrings = @{ Default = 'Server=.;Database=OctopusDash_Live;Integrated Security=true;Encrypt=true;TrustServerCertificate=true' }
            INTERNAL_API_TOKEN = (Random-Hex)
            DATA_PROTECTION_CERT = $certPath
            DATA_PROTECTION_CERT_PASSWORD = $certPassword
            Database = @{ MigrateOnStartup = $true }
            SampleMode = $false
            EnforceHttps = $true
            HttpsRedirection = @{ HttpsPort = 443 }
        } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $configPath -Encoding UTF8
        Write-Host 'Created private production configuration and certificate; values omitted.'
    }
    $settings = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    if ($settings.INTERNAL_API_TOKEN.Length -lt 32 -or -not (Test-Path -LiteralPath $settings.DATA_PROTECTION_CERT -PathType Leaf)) { throw 'Persistent production configuration is incomplete; refusing credential regeneration' }
    New-Item -ItemType Directory -Path $stage | Out-Null
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        foreach ($entry in $archive.Entries) {
            $entryTarget = [IO.Path]::GetFullPath((Join-Path $stage $entry.FullName))
            if (-not $entryTarget.StartsWith($stage + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe archive path' }
        }
    } finally { $archive.Dispose() }
    [IO.Compression.ZipFile]::ExtractToDirectory($zip, $stage)
    if ((Get-Content -LiteralPath (Join-Path $stage 'build-revision.txt') -Raw).Trim() -ne $revision) { throw 'Published revision marker mismatch' }
    [xml]$webConfig = Get-Content -LiteralPath (Join-Path $stage 'web.config') -Raw
    $aspNet = $webConfig.SelectSingleNode('//aspNetCore')
    if (-not $aspNet -or -not (Test-Path -LiteralPath (Join-Path $stage 'EnergyDash.Api.exe'))) { throw 'Invalid IIS publish output' }
    $environment = $webConfig.CreateElement('environmentVariables')
    foreach ($item in @{ ASPNETCORE_ENVIRONMENT = 'Production'; ENERGYDASH_CONFIG_FILE = $configPath }.GetEnumerator()) {
        $variable = $webConfig.CreateElement('environmentVariable')
        $variable.SetAttribute('name', $item.Key)
        $variable.SetAttribute('value', $item.Value)
        $null = $environment.AppendChild($variable)
    }
    $existingEnvironment = $aspNet.SelectSingleNode('environmentVariables')
    if ($existingEnvironment) { $null = $aspNet.RemoveChild($existingEnvironment) }
    $null = $aspNet.AppendChild($environment)
    $webConfig.Save((Join-Path $stage 'web.config'))
    Set-ItemProperty -LiteralPath "IIS:\AppPools\$pool" -Name managedRuntimeVersion -Value ''
    Set-ItemProperty -LiteralPath "IIS:\AppPools\$pool" -Name enable32BitAppOnWin64 -Value $false
    Set-Content -LiteralPath $offline -Value 'Deployment in progress' -Encoding ascii
    $touched = $true
    if ((Get-WebAppPoolState -Name $pool).Value -ne 'Stopped') { Stop-WebAppPool -Name $pool }
    Wait-Pool 'Stopped'
    New-Item -ItemType Directory -Path $backup | Out-Null
    Copy-Tree $root $backup
    $backedUp = $true
    Copy-Tree $stage $root
    Remove-Item -LiteralPath $offline -Force
    Start-WebAppPool -Name $pool
    Wait-Pool 'Started'
    Check-Deployment
    Write-Host "API deployment verified; revision $revision. Private backup retained at $backup"
} catch {
    if ($touched -and $backedUp) {
        Set-Content -LiteralPath $offline -Value 'Recovering previous deployment' -Encoding ascii
        if ((Get-WebAppPoolState -Name $pool).Value -ne 'Stopped') { Stop-WebAppPool -Name $pool }
        Wait-Pool 'Stopped'
        Copy-Tree $backup $root
        Write-Warning 'Restored previous application files. Database migrations are forward-only and require a separate database recovery decision.'
    }
    throw
} finally {
    if ($touched) {
        if (Test-Path -LiteralPath $offline) { Remove-Item -LiteralPath $offline -Force }
        if ((Get-WebAppPoolState -Name $pool).Value -eq 'Stopped') { Start-WebAppPool -Name $pool }
    }
    $resolvedStage = [IO.Path]::GetFullPath($stage)
    if (-not $resolvedStage.StartsWith($privateRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or (Split-Path $resolvedStage -Leaf) -notlike 'stage-*') { throw 'Unsafe staging cleanup path' }
    if (Test-Path -LiteralPath $resolvedStage) { Remove-Item -LiteralPath $resolvedStage -Recurse -Force }
    $lock.Dispose()
}
