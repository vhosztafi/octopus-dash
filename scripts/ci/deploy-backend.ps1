param([string]$ArtifactDirectory = '')
$ErrorActionPreference = 'Stop'
$workerScript = Join-Path $PSScriptRoot 'deploy-worker.ps1'
$apiScript = Join-Path $PSScriptRoot 'deploy-api.ps1'
& $apiScript -ValidateOnly -ArtifactDirectory $ArtifactDirectory
& $workerScript -PreflightOnly -ArtifactDirectory $ArtifactDirectory
$wasRunning = & $workerScript -StopOnly -ArtifactDirectory $ArtifactDirectory
try {
    & $apiScript -ArtifactDirectory $ArtifactDirectory
    & $workerScript -ArtifactDirectory $ArtifactDirectory
} finally {
    if ($wasRunning) { & $workerScript -ResumeOnly -ArtifactDirectory $ArtifactDirectory }
}
