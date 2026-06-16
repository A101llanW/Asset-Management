$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$webConfig = Join-Path $root "src\AssetManagement.Web\Web.config"
$webConfigBackup = "$webConfig.e2e-backup"

if (-not (Test-Path $webConfigBackup)) {
    return
}

Copy-Item $webConfigBackup $webConfig -Force
Remove-Item $webConfigBackup -Force
Write-Host "Restored Web.config from E2E backup."
