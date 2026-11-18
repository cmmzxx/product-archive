param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw "Run this script on Windows. macOS is used for source editing only."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    & dotnet restore TopSolidMingdao.sln
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & dotnet build TopSolidMingdao.sln --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & dotnet test tests/TopSolidMingdao.Tests/TopSolidMingdao.Tests.csproj --configuration $Configuration --no-build --no-restore --logger "trx;LogFileName=tests.trx" --results-directory TestResults
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
