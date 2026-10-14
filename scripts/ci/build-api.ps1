param([ValidateSet('Release', 'Debug')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
function Run([string]$Exe, [string[]]$Arguments) {
    & $Exe @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Exe failed ($LASTEXITCODE)" }
}
Push-Location $repo
try {
    $out = [IO.Path]::GetFullPath((Join-Path $repo 'output/teamcity-api'))
    if (-not $out.StartsWith($repo + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or (Split-Path $out -Leaf) -ne 'teamcity-api') { throw 'Unsafe output path' }
    if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }
    New-Item -ItemType Directory -Path $out | Out-Null
    Run 'dotnet' @('test', 'api/EnergyDash.sln', '-c', $Configuration, '--logger', 'trx', '--results-directory', "$out/test-results")
    Run 'dotnet' @('publish', 'api/src/EnergyDash.Api/EnergyDash.Api.csproj', '-c', $Configuration, '-r', 'win-x64', '--self-contained', 'true', '-p:RuntimeFrameworkVersion=10.0.13', '-o', "$out/publish")
    foreach ($name in @('EnergyDash.Api.exe', 'EnergyDash.Api.dll', 'web.config')) {
        if (-not (Test-Path -LiteralPath "$out/publish/$name" -PathType Leaf)) { throw "Missing published $name" }
    }
    if (Get-ChildItem -LiteralPath "$out/publish" -Recurse -File | Where-Object { $_.Extension -in @('.pfx', '.p12') -or $_.Name -like '.env*' -or $_.Name -eq 'appsettings.Production.json' }) { throw 'Private configuration found in publish output' }
    $revision = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot determine revision' }
    Set-Content -LiteralPath "$out/publish/build-revision.txt" -Value $revision -Encoding ascii
    Compress-Archive -Path "$out/publish/*" -DestinationPath "$out/api.zip"
    @{ revision = $revision; sha256 = (Get-FileHash -LiteralPath "$out/api.zip" -Algorithm SHA256).Hash } | ConvertTo-Json | Set-Content -LiteralPath "$out/api-manifest.json" -Encoding ascii
    Run 'dotnet' @('publish', 'api/src/EnergyDash.Worker/EnergyDash.Worker.csproj', '-c', $Configuration, '-r', 'win-x64', '--self-contained', 'true', '-p:RuntimeFrameworkVersion=10.0.13', '-o', "$out/worker-publish")
    if (-not (Test-Path -LiteralPath "$out/worker-publish/EnergyDash.Worker.exe" -PathType Leaf)) { throw 'Missing published Windows worker' }
    if (Get-ChildItem -LiteralPath "$out/worker-publish" -Recurse -File | Where-Object { $_.Extension -in @('.pfx', '.p12') -or $_.Name -like '.env*' -or $_.Name -in @('server-settings.json', 'appsettings.Production.json') }) { throw 'Private configuration found in worker output' }
    Set-Content -LiteralPath "$out/worker-publish/build-revision.txt" -Value $revision -Encoding ascii
    Compress-Archive -Path "$out/worker-publish/*" -DestinationPath "$out/worker.zip"
    @{ revision = $revision; sha256 = (Get-FileHash -LiteralPath "$out/worker.zip" -Algorithm SHA256).Hash } | ConvertTo-Json | Set-Content -LiteralPath "$out/worker-manifest.json" -Encoding ascii
} finally { Pop-Location }
