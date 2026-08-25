# Build AssetManagement.Web and mirror the site to the IIS physical path.
param(
    [string]$SitePath = 'C:\inetpub\AssetManagement',
    [string]$AppPoolName = 'AssetManagement',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_IisCommon.ps1"

$root = Get-RepositoryRoot
$webSource = Join-Path $root 'src\AssetManagement.Web'

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'Build-WebForIis.ps1') -Configuration $Configuration
}

Import-IisAdministration

Write-Host "Deploying to $SitePath ..." -ForegroundColor Cyan
Stop-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

try {
    Invoke-RobocopyMirror -Source $webSource -Destination $SitePath
}
finally {
    Start-WebAppPool -Name $AppPoolName
}

Write-Host "Deploy complete." -ForegroundColor Green
