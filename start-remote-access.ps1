param(
    [int]$Port = 51980,
    [switch]$SkipBuild,
    [switch]$SkipFirewall,
    [switch]$NoElevate
)

$ErrorActionPreference = 'Stop'

function Test-IsAdministrator {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not $NoElevate -and -not (Test-IsAdministrator)) {
    $argList = @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', "`"$PSCommandPath`"",
        '-Port', $Port,
        '-NoElevate'
    )
    if ($SkipBuild) { $argList += '-SkipBuild' }
    if ($SkipFirewall) { $argList += '-SkipFirewall' }
    Write-Host "Remote access needs Administrator (wildcard URL binding). Requesting elevation..."
    Start-Process powershell -Verb RunAs -ArgumentList $argList
    exit 0
}

$root = $PSScriptRoot
$webPath = Join-Path $root "src\AssetManagement.Web"
$iisExpressPath = Join-Path ${env:ProgramFiles} "IIS Express\iisexpress.exe"
$templatePath = Join-Path $env:USERPROFILE "Documents\IISExpress\config\applicationhost.config"
$configDir = Join-Path $root ".build\iis-remote"
$configPath = Join-Path $configDir "applicationhost.config"

if (-not (Test-Path $iisExpressPath)) {
    throw "IIS Express not found at $iisExpressPath"
}
if (-not (Test-Path $templatePath)) {
    throw "IIS Express template not found at $templatePath"
}

function Remove-PortReservations([int]$TargetPort) {
    @("http://+:$TargetPort/", "http://*:$TargetPort/") | ForEach-Object {
        netsh http delete urlacl url=$_ 2>$null | Out-Null
    }
}

function Stop-RemoteWebServer() {
    Get-Process iisexpress -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
}

if (-not $SkipBuild) {
    Write-Host "Restoring NuGet packages..."
    & (Join-Path $root "restore.ps1")

    Write-Host "Building AssetManagement.Web..."
    $msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe 2>$null | Select-Object -First 1
    if (-not $msbuild) { $msbuild = "msbuild" }

    & $msbuild (Join-Path $webPath "AssetManagement.Web.csproj") /t:Build /p:Configuration=Debug /p:ResolveNuGetPackages=false /v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild failed with exit code $LASTEXITCODE"
    }
}

New-Item -ItemType Directory -Force -Path $configDir | Out-Null
$physicalPath = (Resolve-Path $webPath).Path

Copy-Item $templatePath $configPath -Force
[xml]$config = Get-Content $configPath
$site = $config.configuration.'system.applicationHost'.sites.site | Select-Object -First 1
if (-not $site) {
    throw "Could not find a site in IIS Express template."
}

$site.name = 'AssetManagementRemote'
$site.application.virtualDirectory.physicalPath = $physicalPath
($site.bindings.binding | Select-Object -First 1).bindingInformation = "*:${Port}:*"
$config.Save($configPath)

# Clean broken reservations from earlier attempts (including old port 8080).
Stop-RemoteWebServer
Remove-PortReservations -TargetPort 8080
Remove-PortReservations -TargetPort $Port

if (-not $SkipFirewall) {
    $ruleName = "Asset Management Remote ($Port)"
    if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port -Profile Any | Out-Null
        Write-Host "Firewall rule added for TCP port $Port (all profiles)."
    }
}

Write-Host "Starting IIS Express on all interfaces, port $Port ..."
$iisProcess = Start-Process -FilePath $iisExpressPath -ArgumentList "/config:`"$configPath`" /site:AssetManagementRemote" -PassThru -WindowStyle Normal

$deadline = (Get-Date).AddSeconds(90)
$ready = $false
while ((Get-Date) -lt $deadline) {
    if ($iisProcess.HasExited) {
        throw "IIS Express exited early (code $($iisProcess.ExitCode)). Wildcard binding requires Administrator."
    }
    try {
        $response = Invoke-WebRequest -Uri "http://127.0.0.1:$Port/default/Account/Login" -UseBasicParsing -TimeoutSec 5
        if ($response.StatusCode -eq 200) {
            $ready = $true
            break
        }
    }
    catch {
        Start-Sleep -Seconds 2
    }
}

if (-not $ready) {
    Stop-RemoteWebServer
    throw "Timed out waiting for http://127.0.0.1:$Port/default/Account/Login"
}

$zeroTierIp = (Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.InterfaceAlias -like 'ZeroTier*' -and $_.IPAddress -notlike '169.254.*' } |
    Select-Object -First 1 -ExpandProperty IPAddress)

$zeroTierNetwork = (Get-NetAdapter -ErrorAction SilentlyContinue |
    Where-Object { $_.InterfaceDescription -like 'ZeroTier*' -and $_.Status -eq 'Up' } |
    Select-Object -First 1 -ExpandProperty Name)

Write-Host ""
Write-Host "App is running."
Write-Host "  Local:    http://localhost:$Port/default/Account/Login"
if ($zeroTierIp) {
    Write-Host "  ZeroTier: http://${zeroTierIp}:$Port/default/Account/Login"
    if ($zeroTierNetwork) {
        Write-Host "  Network:  $zeroTierNetwork"
    }
    Write-Host "  Note: ZeroTier IPs change per network. Do not use old IPs like 10.203.99.38."
}
else {
    Write-Host "  ZeroTier: (not connected — join your ZeroTier network on this PC)"
}
Write-Host ""
Write-Host "Login: assetmanager@asset.local / P@ssw0rd!"
Write-Host "Stop:  .\stop-remote-access.ps1 -Port $Port"
Write-Host ""
