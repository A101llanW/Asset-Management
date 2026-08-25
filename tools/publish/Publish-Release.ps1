param(
    [int]$SmokeTestPort = 51902,
    [switch]$SkipSmokeHost
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$webProject = Join-Path $root 'src\AssetManagement.Web'
$publishPath = Join-Path $root '.build\publish'
$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' 2>$null | Select-Object -First 1
if (-not $msbuild) {
    throw 'MSBuild not found.'
}

Write-Host 'Publishing Release to .build\publish ...'
& $msbuild $webProject /t:WebPublish /p:Configuration=Release /p:PublishProfile=FolderProfile /p:ResolveNuGetPackages=false /v:minimal
if ($LASTEXITCODE -ne 0) {
    throw "Publish failed with exit code $LASTEXITCODE"
}

$secrets = @(
    (Join-Path $webProject 'smtp.secrets.config'),
    (Join-Path $webProject 'connectionStrings.config')
)

foreach ($secret in $secrets) {
    if (-not (Test-Path $secret)) {
        throw "Missing required secret file: $secret"
    }

    Copy-Item $secret (Join-Path $publishPath (Split-Path $secret -Leaf)) -Force
    Write-Host "Copied $(Split-Path $secret -Leaf) -> publish folder"
}

$publishedConfig = Join-Path $publishPath 'Web.config'
if (-not (Test-Path $publishedConfig)) {
    throw "Published Web.config not found at $publishedConfig"
}

# Keep smoke-test ExternalBaseUrl aligned with the ephemeral host port.
$smtpPublishPath = Join-Path $publishPath 'smtp.secrets.config'
if (Test-Path $smtpPublishPath) {
    [xml]$smtpXml = Get-Content $smtpPublishPath
    $external = $smtpXml.appSettings.add | Where-Object { $_.key -eq 'ExternalBaseUrl' } | Select-Object -First 1
    if ($external) {
        $external.value = "http://localhost:$SmokeTestPort"
        $smtpXml.Save($smtpPublishPath)
    }
}

[xml]$webConfig = Get-Content $publishedConfig
$mfa = ($webConfig.configuration.appSettings.add | Where-Object { $_.key -eq 'MfaAllowAnyCode' }).value
$debug = $webConfig.configuration.'system.web'.compilation.debug
$connSource = $webConfig.configuration.connectionStrings.configSource
$smtpFile = $webConfig.configuration.appSettings.file

Write-Host ''
Write-Host 'Publish verification:'
Write-Host "  Path:              $publishPath"
Write-Host "  MfaAllowAnyCode:   $mfa"
Write-Host "  compilation debug: $debug"
Write-Host "  connectionStrings: $connSource"
Write-Host "  appSettings file:  $smtpFile"

if ($mfa -ne 'false') {
    throw 'Published Web.config does not have MfaAllowAnyCode=false'
}

if ($connSource -ne 'connectionStrings.config') {
    throw 'Published Web.config does not reference connectionStrings.config'
}

if ([string]::IsNullOrWhiteSpace($smtpFile)) {
    throw 'Published Web.config is missing appSettings file="smtp.secrets.config"'
}

if (-not (Test-Path (Join-Path $publishPath 'smtp.secrets.config'))) {
    throw 'smtp.secrets.config missing from publish folder'
}

if ($SkipSmokeHost) {
    Write-Host ''
    Write-Host 'Publish complete (smoke host skipped).'
    exit 0
}

Write-Host ''
Write-Host "Starting IIS Express smoke host on port $SmokeTestPort ..."
$iisExpress = Join-Path ${env:ProgramFiles} 'IIS Express\iisexpress.exe'
$configPath = Join-Path $root '.build\iis-remote\applicationhost.config'
$ensureConfig = Join-Path $root '.build\ensure-iis-config.ps1'

if (-not (Test-Path $iisExpress)) {
    throw "IIS Express not found at $iisExpress"
}

Get-Process iisexpress -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

& $ensureConfig -WebPath $publishPath -Port $SmokeTestPort -ConfigPath $configPath | Out-Null
$arguments = "/config:`"$configPath`" /site:AssetManagementRemote"
Start-Process -FilePath $iisExpress -ArgumentList $arguments -WindowStyle Hidden | Out-Null

$loginUrl = "http://localhost:$SmokeTestPort/Account/Login"
$deadline = (Get-Date).AddSeconds(120)
while ((Get-Date) -lt $deadline) {
    try {
        $response = Invoke-WebRequest -Uri $loginUrl -UseBasicParsing -TimeoutSec 5
        if ($response.StatusCode -eq 200) {
            Write-Host "Smoke host ready: $loginUrl"
            break
        }
    }
    catch {
        Start-Sleep -Seconds 2
    }
}

if ((Get-Date) -ge $deadline) {
    throw "Timed out waiting for smoke host at $loginUrl"
}

Write-Host ''
Write-Host 'Publish complete. Next: open the smoke URL and test login/MFA/forgot password.'
