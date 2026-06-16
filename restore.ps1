# Restores packages.config (Web) and PackageReference (SDK) projects into .nuget\packages
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
& "$root\.nuget\nuget.exe" restore "$root\AssetManagementModule.sln" -ConfigFile "$root\NuGet.Config" -NonInteractive
