# Clears Visual Studio user options so AssetManagement.Web becomes the F5 startup project.
# The checked-in solution lists the web app first; a local .suo can still remember Domain.
$ErrorActionPreference = 'Stop'

$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
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
