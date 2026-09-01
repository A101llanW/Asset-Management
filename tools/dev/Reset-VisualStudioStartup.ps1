# Clears Visual Studio user options so AssetManagement.Web becomes the F5 startup project.
# Run from the repo, or invoke this file by full path from any directory.
# The checked-in solution lists the web app first; a local .suo can still remember Domain.
$ErrorActionPreference = 'Stop'

$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$sln = Join-Path $root 'AssetManagementModule.sln'
if (-not (Test-Path $sln)) {
    Write-Host "This script must run from the Asset-Management repo (folder that contains AssetManagementModule.sln)." -ForegroundColor Red
    Write-Host "You are not in that folder. Example:" -ForegroundColor Yellow
    Write-Host '  cd C:\Users\allan\source\repos\Asset-Management'
    Write-Host '  git checkout cursor/set-web-startup-project-9fdb'
    Write-Host '  git pull'
    Write-Host '  .\Reset-VisualStudioStartup.cmd'
    exit 1
}

Write-Host "Repository: $root" -ForegroundColor Cyan
$vsDir = Join-Path $root '.vs'

if (Test-Path $vsDir) {
    Get-ChildItem -Path $vsDir -Recurse -Force -Include *.suo, *.user | Remove-Item -Force -ErrorAction SilentlyContinue
    Write-Host "Removed Visual Studio user startup cache under .vs"
}
else {
    Write-Host "No .vs folder found; nothing to reset."
}

Write-Host ""
Write-Host "Reopen AssetManagementModule.sln. Startup project is AssetManagement.Web."
Write-Host "F5 starts IIS Express. Demo login: nanosoft@asset.local / P@ssw0rd! at /nanosoft/Account/Login"
