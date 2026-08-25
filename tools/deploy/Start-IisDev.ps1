# Build, deploy, and verify AssetManagement on local IIS (not IIS Express).
param(
    [string]$SiteName = 'AssetManagement',
    [string]$SitePath = 'C:\inetpub\AssetManagement',
    [string]$AppPoolName = 'AssetManagement',
    [int]$Port = 8080,
    [switch]$SetupSite,
    [switch]$WaitForReady,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_IisCommon.ps1"

if (-not (Test-IisAvailable)) {
    throw 'IIS is not available on this machine. Enable IIS and ASP.NET 4.x, then rerun.'
}

Import-IisAdministration

$site = Get-Website -Name $SiteName -ErrorAction SilentlyContinue
if (-not $site -or $SetupSite) {
    Write-Host "Setting up IIS site '$SiteName' ..." -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot 'Setup-IisSite.ps1') -SiteName $SiteName -SitePath $SitePath -AppPoolName $AppPoolName -Port $Port
}

& (Join-Path $PSScriptRoot 'Publish-To-Iis.ps1') -SitePath $SitePath -AppPoolName $AppPoolName -SkipBuild:$SkipBuild

$baseUrl = Get-IisBaseUrl -Port $Port
$loginUrl = "$baseUrl/nanosoft/Account/Login"

if ($WaitForReady) {
    Write-Host "Waiting for $loginUrl ..." -ForegroundColor Cyan
    if (-not (Wait-ForWebApp -Url $loginUrl)) {
        throw "Timed out waiting for $loginUrl"
    }
}

Write-Host ""
Write-Host "AssetManagement is running on IIS:" -ForegroundColor Green
Write-Host "  $loginUrl"
Write-Host "  Demo login: nanosoft@asset.local / P@ssw0rd!"

if ($WaitForReady) {
    exit 0
}
