$ErrorActionPreference = "Stop"
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw "Run this script on Windows. macOS is used for source editing only."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src/TopSolidMingdao.Desktop/TopSolidMingdao.Desktop.csproj"
$outputPath = Join-Path $repoRoot "artifacts/win-x64"
Push-Location $repoRoot
try {
    & dotnet publish $projectPath --configuration Release --runtime win-x64 --self-contained true --output $outputPath -p:PublishSingleFile=false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
Write-Host "Published to $outputPath"
