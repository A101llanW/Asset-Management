# Deploy built AssetManagement.Web to local IIS (C:\inetpub\AssetManagement).
# Prefer Publish-To-Iis.ps1 for full site sync. This script remains for publish-folder deploys.
# Run in an elevated PowerShell session (Administrator) when creating the site for the first time.
param(
    [string]$SourceRoot = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) "src"),
    [string]$PublishRoot = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) ".build\publish"),
    [string]$SitePath = "C:\inetpub\AssetManagement",
    [string]$AppPoolName = "AssetManagement",
    [switch]$UsePublishFolder
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\_IisCommon.ps1"

if (-not $UsePublishFolder) {
    & (Join-Path $PSScriptRoot "Publish-To-Iis.ps1") -SitePath $SitePath -AppPoolName $AppPoolName
    exit 0
}

if (-not (Test-Path $SitePath)) {
    throw "IIS site path not found: $SitePath"
}

if (-not (Test-Path $PublishRoot)) {
    throw "Publish folder not found: $PublishRoot"
}

Import-IisAdministration
Stop-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

try {
    Write-Host "Copying publish output from $PublishRoot ..." -ForegroundColor Cyan
    Invoke-RobocopyMirror -Source $PublishRoot -Destination $SitePath
}
finally {
    Start-WebAppPool -Name $AppPoolName
}

Write-Host ""
Write-Host "Deploy complete. Open:" -ForegroundColor Green
Write-Host "  $(Get-IisBaseUrl)/nanosoft/Account/Login"
