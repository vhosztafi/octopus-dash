param([switch]$ValidateOnly, [switch]$PreflightOnly, [switch]$StopOnly, [switch]$ResumeOnly, [string]$ArtifactDirectory = '')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
if (-not $ArtifactDirectory) { $ArtifactDirectory = Join-Path $repo 'incoming' }
$serviceName = 'OctopusDashWorker'
$account = 'NT SERVICE\OctopusDashWorker'
$root = 'D:\Websites\octopus-dash-api.vilmos.co.uk\worker'
$exe = [IO.Path]::Combine($root, 'EnergyDash.Worker.exe')
$privateRoot = 'D:\Websites\octopus-dash-api.vilmos.co.uk\_energy-private'
$stateRoot = [IO.Path]::Combine($privateRoot, 'worker-state')
$configPath = [IO.Path]::Combine($privateRoot, 'server-settings.json')
$heartbeat = [IO.Path]::Combine($stateRoot, 'heartbeat.txt')
$zip = Join-Path $ArtifactDirectory 'worker.zip'
$manifest = Get-Content -LiteralPath (Join-Path $ArtifactDirectory 'worker-manifest.json') -Raw | ConvertFrom-Json
if ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne $manifest.sha256) { throw 'Worker artifact checksum mismatch' }
Push-Location $repo
try { $revision = (& git rev-parse HEAD).Trim(); if ($LASTEXITCODE -ne 0 -or $revision -ne $manifest.revision) { throw 'Worker artifact and checkout revisions differ' } }
finally { Pop-Location }
if ($ValidateOnly) { Write-Host 'Worker artifact preflight passed; server untouched.'; return }

function Owned-Service {
    $item = Get-CimInstance Win32_Service -Filter "Name='OctopusDashWorker'"
    if ($item -and ($item.PathName.Trim().Trim('"') -ne $exe -or $item.StartName -ne $account)) { throw 'Existing worker service is not the expected executable/account; refusing changes' }
    return $item
}
function Stop-OwnedService {
    $item = Owned-Service
    if ($item -and $item.State -ne 'Stopped') {
        Stop-Service -Name $serviceName
        (Get-Service -Name $serviceName).WaitForStatus('Stopped', [TimeSpan]::FromSeconds(90))
    }
}
if ($StopOnly) { $item = Owned-Service; $wasRunning = [bool]($item -and $item.State -eq 'Running'); Stop-OwnedService; return $wasRunning }
if ($ResumeOnly) { if (Owned-Service) { if ((Get-Service $serviceName).Status -eq 'Stopped') { Start-Service $serviceName } }; return }
$null = Owned-Service
if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) { throw 'Commission the IIS API private configuration before the worker' }
$settings = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
if (-not (Test-Path -LiteralPath $settings.DATA_PROTECTION_CERT -PathType Leaf)) { throw 'Shared Data Protection certificate is missing' }
$connectionString = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($settings.ConnectionStrings.Default)
if ($connectionString.DataSource -ne '.' -or $connectionString.InitialCatalog -ne 'OctopusDash_Live' -or -not $connectionString.IntegratedSecurity) { throw 'Worker requires the explicitly configured local integrated-security database' }
$sql = New-Object System.Data.SqlClient.SqlConnection($connectionString.ConnectionString)
function Sql-Scalar([string]$Text) { $command = $sql.CreateCommand(); try { $command.CommandText = $Text; return $command.ExecuteScalar() } finally { $command.Dispose() } }
function Sql-Execute([string]$Text) { $command = $sql.CreateCommand(); try { $command.CommandText = $Text; $null = $command.ExecuteNonQuery() } finally { $command.Dispose() } }
try {
    $sql.Open()
    $hasLogin = [bool](Sql-Scalar "SELECT CASE WHEN SUSER_ID(N'NT SERVICE\OctopusDashWorker') IS NULL THEN 0 ELSE 1 END")
    $isAdmin = [int](Sql-Scalar "SELECT IS_SRVROLEMEMBER(N'sysadmin')") -eq 1
    $canAlterLogin = $isAdmin -or [int](Sql-Scalar "SELECT HAS_PERMS_BY_NAME(NULL,NULL,N'ALTER ANY LOGIN')") -eq 1
    $canManageDb = $isAdmin -or [int](Sql-Scalar "SELECT IS_MEMBER(N'db_owner')") -eq 1
    $hasRoles = [int](Sql-Scalar "SELECT COUNT(*) FROM sys.database_role_members rm JOIN sys.database_principals r ON r.principal_id=rm.role_principal_id JOIN sys.database_principals u ON u.principal_id=rm.member_principal_id WHERE u.name=N'NT SERVICE\OctopusDashWorker' AND r.name IN (N'db_datareader',N'db_datawriter')") -eq 2
    Write-Host ("Worker SQL login exists: {0}; required roles present: {1}; provisioning permitted: {2}" -f $hasLogin, $hasRoles, ($canManageDb -and ($hasLogin -or $canAlterLogin)))
    if ((-not $hasLogin -and -not $canAlterLogin) -or (-not $hasRoles -and -not $canManageDb)) { throw 'Deploy account cannot provision the dedicated worker SQL identity; no worker service/files changed' }
    if ($PreflightOnly) { Write-Host 'Worker server preflight passed.'; return }

    New-Item -ItemType Directory -Force -Path $root, $stateRoot | Out-Null
    if (-not (Owned-Service)) {
        & sc.exe create $serviceName 'binPath=' $exe 'start=' 'auto' 'obj=' $account | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Cannot create the dedicated virtual-account worker service' }
    }
    $null = Owned-Service
    # The service receives only this application's data roles, never server admin or database owner.
    if (-not $hasLogin) { Sql-Execute "CREATE LOGIN [NT SERVICE\OctopusDashWorker] FROM WINDOWS" }
    Sql-Execute @"
IF USER_ID(N'NT SERVICE\OctopusDashWorker') IS NULL CREATE USER [NT SERVICE\OctopusDashWorker] FOR LOGIN [NT SERVICE\OctopusDashWorker];
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name=N'NT SERVICE\OctopusDashWorker' AND sid=SUSER_SID(N'NT SERVICE\OctopusDashWorker')) THROW 50000, 'Unexpected worker database identity', 1;
IF IS_ROLEMEMBER(N'db_datareader',N'NT SERVICE\OctopusDashWorker') <> 1 ALTER ROLE [db_datareader] ADD MEMBER [NT SERVICE\OctopusDashWorker];
IF IS_ROLEMEMBER(N'db_datawriter',N'NT SERVICE\OctopusDashWorker') <> 1 ALTER ROLE [db_datawriter] ADD MEMBER [NT SERVICE\OctopusDashWorker];
"@
} catch {
    if ($_.Exception -is [System.Data.SqlClient.SqlException]) { throw ("Worker SQL provisioning failed (SQL error {0}); values omitted" -f $_.Exception.Number) }
    throw
} finally { $sql.Dispose() }

& icacls.exe $root '/inheritance:r' '/grant:r' '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' "${account}:(OI)(CI)RX" | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Cannot secure worker executable directory' }
& icacls.exe $privateRoot '/grant:r' "${account}:(OI)(CI)RX" | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Cannot grant worker read access to shared private configuration' }
& icacls.exe $stateRoot '/inheritance:r' '/grant:r' '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' "${account}:(OI)(CI)M" | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Cannot secure worker health directory' }
$registryPath = 'HKLM:\SYSTEM\CurrentControlSet\Services\OctopusDashWorker'
New-ItemProperty -LiteralPath $registryPath -Name Environment -PropertyType MultiString -Value @('DOTNET_ENVIRONMENT=Production', ('ENERGYDASH_CONFIG_FILE=' + $configPath), ('Worker__HeartbeatPath=' + $heartbeat)) -Force | Out-Null
& sc.exe config $serviceName 'start=' 'delayed-auto' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Cannot configure worker automatic startup' }
& sc.exe failure $serviceName 'reset=' '86400' 'actions=' 'restart/60000/restart/60000/restart/60000' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Cannot configure worker recovery' }
if (-not [Diagnostics.EventLog]::SourceExists('EnergyDash.Worker')) { [Diagnostics.EventLog]::CreateEventSource('EnergyDash.Worker', 'Application') }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$stage = Join-Path $privateRoot ('worker-stage-' + [guid]::NewGuid().ToString('N'))
$backup = Join-Path $privateRoot ('worker-backup-' + [guid]::NewGuid().ToString('N'))
$backedUp = $false
function Copy-Worker([string]$Source, [string]$Destination) {
    foreach ($copyPath in @($Source, $Destination)) {
        $absolute = [IO.Path]::GetFullPath($copyPath).TrimEnd('\')
        if ($absolute -ne $root -and -not ($absolute.StartsWith($privateRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and (Split-Path $absolute -Leaf) -match '^worker-(stage|backup)-[a-f0-9]{32}$')) { throw 'Unsafe worker copy path' }
    }
    & robocopy.exe $Source $Destination /MIR /R:2 /W:2 /NFL /NDL /NP /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) { throw 'Worker release copy failed' }
}
try {
    New-Item -ItemType Directory -Path $stage | Out-Null
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        foreach ($entry in $archive.Entries) {
            $target = [IO.Path]::GetFullPath((Join-Path $stage $entry.FullName))
            if (-not $target.StartsWith($stage + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe worker archive path' }
        }
    } finally { $archive.Dispose() }
    [IO.Compression.ZipFile]::ExtractToDirectory($zip, $stage)
    if (-not (Test-Path -LiteralPath (Join-Path $stage 'EnergyDash.Worker.exe')) -or (Get-Content -LiteralPath (Join-Path $stage 'build-revision.txt') -Raw).Trim() -ne $revision) { throw 'Worker artifact executable/revision check failed' }
    Stop-OwnedService
    New-Item -ItemType Directory -Path $backup | Out-Null
    Copy-Worker $root $backup; $backedUp = $true
    Copy-Worker $stage $root
    if (Test-Path -LiteralPath $heartbeat) { Remove-Item -LiteralPath $heartbeat -Force }
    $startedUtc = [DateTime]::UtcNow
    Start-Service $serviceName
    $deadline = (Get-Date).AddSeconds(120)
    $healthy = $false
    do {
        $running = (Get-Service $serviceName).Status -eq 'Running'
        if ($running -and (Test-Path -LiteralPath $heartbeat)) {
            $at = [DateTime]::Parse((Get-Content -LiteralPath $heartbeat -Raw).Trim()).ToUniversalTime()
            if ($at -ge $startedUtc.AddSeconds(-1) -and ([DateTime]::UtcNow - $at).TotalSeconds -lt 45) { $healthy = $true; break }
        }
        Start-Sleep -Seconds 3
    } while ((Get-Date) -lt $deadline)
    if (-not $healthy) { throw 'Worker did not produce a fresh SQL/encrypted-key-ring health marker' }
    Start-Sleep -Seconds 5
    if ((Get-Service $serviceName).Status -ne 'Running') { throw 'Worker stopped after startup verification' }
    Write-Host "Worker service verified; revision $revision; SQL and shared encrypted credentials readable. Private backup retained."
} catch {
    Stop-OwnedService
    if ($backedUp -and (Test-Path -LiteralPath (Join-Path $backup 'EnergyDash.Worker.exe'))) { Copy-Worker $backup $root; Start-Service $serviceName; Write-Warning 'Previous worker files restored; SQL identity/configuration preserved.' }
    throw
} finally {
    $resolvedStage = [IO.Path]::GetFullPath($stage)
    if (-not $resolvedStage.StartsWith($privateRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or (Split-Path $resolvedStage -Leaf) -notmatch '^worker-stage-[a-f0-9]{32}$') { throw 'Unsafe worker staging cleanup path' }
    if (Test-Path -LiteralPath $resolvedStage) { Remove-Item -LiteralPath $resolvedStage -Recurse -Force }
}
