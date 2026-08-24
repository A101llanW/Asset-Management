# Restore packages and build AssetManagement.Web with MSBuild.
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_IisCommon.ps1"

$root = Get-RepositoryRoot
$webProject = Join-Path $root 'src\AssetManagement.Web\AssetManagement.Web.csproj'
$msbuild = Resolve-MsBuildPath

Write-Host "Restoring NuGet packages..." -ForegroundColor Cyan
& (Join-Path $root 'restore.ps1')

Write-Host "Building AssetManagement.Web ($Configuration)..." -ForegroundColor Cyan
& $msbuild $webProject /t:Rebuild /p:Configuration=$Configuration /p:ResolveNuGetPackages=false /v:minimal
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild failed with exit code $LASTEXITCODE"
}

Write-Host "Build complete." -ForegroundColor Green
