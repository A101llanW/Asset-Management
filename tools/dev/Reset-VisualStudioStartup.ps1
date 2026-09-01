# Clears Visual Studio user options so AssetManagement.Web becomes the F5 startup project.
# The checked-in solution lists the web app first; a local .suo can still remember Domain.
#
# Run from the Asset-Management clone (the folder that contains AssetManagementModule.sln):
#   cd C:\Users\<you>\source\repos\Asset-Management
#   .\tools\dev\Reset-VisualStudioStartup.ps1
# Invoking this file (or Reset-VisualStudioStartup.cmd) by full path also works from any directory.
$ErrorActionPreference = 'Stop'

function Resolve-AssetManagementRoot {
    $dir = $PSScriptRoot
    while ($dir) {
        if (Test-Path (Join-Path $dir 'AssetManagementModule.sln')) {
            return $dir
        }

        $parent = Split-Path $dir -Parent
        if ($parent -eq $dir) {
            break
        }

        $dir = $parent
    }

    return $null
}

function Write-RepoRootHelp {
    $user = if ($env:USERNAME) { $env:USERNAME } else { $env:USER }
    Write-Host "Current location: $(Get-Location)"
    Write-Host "A home folder such as C:\Users\$user is not the repository."
    Write-Host ""
    Write-Host "cd into the folder that contains AssetManagementModule.sln, then retry:"
    Write-Host "  cd C:\Users\$user\source\repos\Asset-Management"
    Write-Host "  git pull origin cursor/set-web-startup-project-9fdb"
    Write-Host "  .\tools\dev\Reset-VisualStudioStartup.ps1"
    Write-Host ""
    Write-Host "Other common clone paths:"
    Write-Host "  C:\Users\$user\Asset-Management"
    Write-Host "  C:\Users\$user\Documents\GitHub\Asset-Management"
    Write-Host "  C:\Users\$user\source\Asset-Management"
    Write-Host ""
    Write-Host "If you do not know the path:"
    Write-Host '  Get-ChildItem -Path $HOME -Filter AssetManagementModule.sln -Recurse -ErrorAction SilentlyContinue | Select-Object -ExpandProperty DirectoryName'
}

$root = Resolve-AssetManagementRoot
if (-not $root) {
    Write-Host "This script must run from an Asset-Management git clone." -ForegroundColor Red
    Write-RepoRootHelp
    exit 1
}

$cwd = (Get-Location).ProviderPath
if ([IO.Path]::GetFullPath($cwd).TrimEnd('\') -ne [IO.Path]::GetFullPath($root).TrimEnd('\')) {
    Write-Host "Working directory is $cwd" -ForegroundColor Yellow
    Write-Host "Using repository root $root (resolved from this script)."
    Write-Host "Next time, cd into that folder first so relative paths and git pull work."
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
