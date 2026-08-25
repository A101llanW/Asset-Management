# One-command local dev: restore packages, ensure IIS site, build, deploy, and verify.
# Works from any directory when invoked with a full path, e.g.:
#   & "C:\Users\You\source\Asset-Management\Start-Dev.ps1"
param(
    [int]$Port = 8080,
    [switch]$SetupSite,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
Set-Location $Root

Write-Host "Repository: $Root" -ForegroundColor Cyan
Write-Host "Restoring NuGet packages..." -ForegroundColor Cyan
& (Join-Path $Root 'restore.ps1')

$startScript = Join-Path $Root 'tools\deploy\Start-IisDev.ps1'
if (-not (Test-Path $startScript)) {
    throw "Missing IIS start script: $startScript"
}

Write-Host "Starting AssetManagement on IIS (port $Port)..." -ForegroundColor Cyan
& $startScript -Port $Port -SetupSite:$SetupSite -SkipBuild:$SkipBuild -WaitForReady

Write-Host ""
Write-Host "Done. Open http://localhost:$Port/nanosoft/Account/Login" -ForegroundColor Green
