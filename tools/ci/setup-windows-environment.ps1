# Windows cloud/local bootstrap: restore packages and build the web app for IIS.
$ErrorActionPreference = 'Stop'

$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $root

if ($PSVersionTable.PSVersion.Major -lt 5) {
    throw 'PowerShell 5 or later is required.'
}

Write-Host 'Restoring NuGet packages...'
& (Join-Path $root 'restore.ps1')

$deployCommon = Join-Path $root 'tools\deploy\_IisCommon.ps1'
if (Test-Path $deployCommon) {
    . $deployCommon
    try {
        $msbuild = Resolve-MsBuildPath
        Write-Host "Building AssetManagement.Web with $msbuild ..."
        & (Join-Path $root 'tools\deploy\Build-WebForIis.ps1') -Configuration Debug
    }
    catch {
        Write-Warning $_.Exception.Message
        Write-Warning 'MSBuild build skipped during install. Start-IisDev.ps1 will build on first run.'
    }
}
else {
    Write-Warning 'IIS deploy helpers not found; install limited to NuGet restore.'
}

Write-Host 'Windows environment bootstrap complete.'
