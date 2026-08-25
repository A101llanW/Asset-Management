# Restores Web.config from the backup created by Reset-E2eDatabase.ps1.
. "$PSScriptRoot\_Common.ps1"

$ErrorActionPreference = 'Stop'
$root = Get-RepositoryRoot
$webConfig = Get-WebConfigPath -RepositoryRoot $root
$webConfigBackup = "$webConfig.e2e-backup"

if (-not (Test-Path $webConfigBackup)) {
    Write-Host "No E2E Web.config backup found; nothing to restore."
    return
}

Copy-Item $webConfigBackup $webConfig -Force
Remove-Item $webConfigBackup -Force
Write-Host "Restored Web.config from E2E backup."
