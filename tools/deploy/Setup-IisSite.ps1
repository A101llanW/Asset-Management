# One-time IIS site setup for local AssetManagement development.
# Run in an elevated PowerShell session (Administrator).
param(
    [string]$SiteName = 'AssetManagement',
    [string]$SitePath = 'C:\inetpub\AssetManagement',
    [string]$AppPoolName = 'AssetManagement',
    [int]$Port = 8080
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_IisCommon.ps1"

Import-IisAdministration

if (-not (Test-Path $SitePath)) {
    Write-Host "Creating site directory $SitePath ..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $SitePath -Force | Out-Null
}

if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
    Write-Host "Creating app pool '$AppPoolName' (.NET 4.0, integrated)..." -ForegroundColor Cyan
    New-WebAppPool -Name $AppPoolName | Out-Null
    Set-ItemProperty "IIS:\AppPools\$AppPoolName" managedRuntimeVersion 'v4.0'
    Set-ItemProperty "IIS:\AppPools\$AppPoolName" managedPipelineMode 'Integrated'
}

$existingSite = Get-Website -Name $SiteName -ErrorAction SilentlyContinue
if (-not $existingSite) {
    Write-Host "Creating IIS site '$SiteName' on port $Port ..." -ForegroundColor Cyan
    New-Website -Name $SiteName -PhysicalPath $SitePath -Port $Port -ApplicationPool $AppPoolName | Out-Null
}
else {
    Write-Host "Updating IIS site '$SiteName' ..." -ForegroundColor Cyan
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $SitePath
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $AppPoolName

    $binding = Get-WebBinding -Name $SiteName -Protocol 'http' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($binding) {
        $binding.bindingInformation = "*:${Port}:"
        $binding | Set-WebBinding
    }
}

Start-WebAppPool -Name $AppPoolName
Write-Host ""
Write-Host "IIS site ready:" -ForegroundColor Green
Write-Host "  Path:     $SitePath"
Write-Host "  App pool: $AppPoolName"
Write-Host "  URL:      $(Get-IisBaseUrl -Port $Port)/nanosoft/Account/Login"
