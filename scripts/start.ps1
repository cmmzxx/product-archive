param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw "Run this script on Windows."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src/TopSolidMingdao.Desktop/TopSolidMingdao.Desktop.csproj"
$previousToken = $env:TOPSOLID_MINGDAO_TOKEN
$secret = $null
try {
    if ([string]::IsNullOrWhiteSpace($env:TOPSOLID_MINGDAO_TOKEN)) {
        $secret = Read-Host "Mingdao Bearer token (without the Bearer prefix)" -AsSecureString
        # Only pass the token through this process environment; never save it in a file or command argument.
        $env:TOPSOLID_MINGDAO_TOKEN = [Net.NetworkCredential]::new("", $secret).Password
    }
    Push-Location $repoRoot
    try {
        & dotnet run --project $projectPath --configuration $Configuration --no-build --no-restore
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    finally {
        Pop-Location
    }
}
finally {
    $env:TOPSOLID_MINGDAO_TOKEN = $previousToken
    if ($null -ne $secret) { $secret.Dispose() }
}
