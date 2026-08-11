# Restores Global.asax when IIS returns 404 StaticFile for tenant MVC URLs.
# Run in an elevated PowerShell session (Administrator).
param(
    [string]$SourceRoot = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) "src"),
    [string]$PublishRoot = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) ".build\publish"),
    [string]$SitePath = "C:\inetpub\AssetManagement",
    [string]$AppPoolName = "DefaultAppPool",
    [switch]$UsePublishFolder
)

$ErrorActionPreference = "Stop"

$sourceGlobal = if ($UsePublishFolder -and (Test-Path (Join-Path $PublishRoot "Global.asax"))) {
    Join-Path $PublishRoot "Global.asax"
} else {
    Join-Path $SourceRoot "AssetManagement.Web\Global.asax"
}

if (-not (Test-Path $sourceGlobal)) {
    throw "Global.asax not found at $sourceGlobal"
}

$destGlobal = Join-Path $SitePath "Global.asax"
Write-Host "Restoring $destGlobal from $sourceGlobal ..." -ForegroundColor Cyan

Import-Module WebAdministration -ErrorAction Stop
Stop-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

try {
    Copy-Item $sourceGlobal $destGlobal -Force
    Write-Host "Global.asax restored." -ForegroundColor Green
}
finally {
    Start-WebAppPool -Name $AppPoolName
}

Write-Host "Retry: http://192.168.30.122:8080/nanosoft/PurchaseRequests/Index" -ForegroundColor Green
