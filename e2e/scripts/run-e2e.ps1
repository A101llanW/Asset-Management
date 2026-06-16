param(
    [switch]$Headed
)

$ErrorActionPreference = 'Stop'
$e2eRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent $e2eRoot

Push-Location $e2eRoot
try {
    Get-Process iisexpress -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

    if (-not (Test-Path "node_modules")) {
        Write-Host "Installing Playwright dependencies..."
        npm install
        npx playwright install chromium
    }

    if ($Headed) {
        npm run test:headed
    }
    else {
        npm test
    }
    exit $LASTEXITCODE
}
finally {
    & (Join-Path $PSScriptRoot "restore-e2e-webconfig.ps1")
    Pop-Location
}
