$ErrorActionPreference = 'Stop'
dotnet publish (Join-Path $PSScriptRoot 'AllLive.WinUI\PlayWin.csproj') -c Release -p:Platform=x64 -o (Join-Path $PSScriptRoot 'artifacts\PlayWin-win-x64') --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'AllLive.WinUI\README.md') -Destination (Join-Path $PSScriptRoot 'artifacts\PlayWin-win-x64\README.md') -Force
