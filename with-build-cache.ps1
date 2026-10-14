param([Parameter(Mandatory)][scriptblock]$Action)
$ErrorActionPreference = 'Stop'
$cacheRoot = Join-Path $PSScriptRoot 'artifacts\cache'
$paths = @{
    TEMP = Join-Path $cacheRoot 'temp'
    TMP = Join-Path $cacheRoot 'temp'
    NUGET_PACKAGES = Join-Path $cacheRoot 'nuget-packages'
    NUGET_HTTP_CACHE_PATH = Join-Path $cacheRoot 'nuget-http'
}
$previous = @{}
try {
    foreach ($name in $paths.Keys) {
        New-Item -ItemType Directory -Path $paths[$name] -Force | Out-Null
        $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $paths[$name], 'Process')
    }
    & $Action
}
finally {
    foreach ($name in $previous.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process')
    }
}