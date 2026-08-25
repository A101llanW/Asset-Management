param(
    [string]$Port = $(if ($env:E2E_PORT) { $env:E2E_PORT.Trim() } else { "8080" }),
    [switch]$ResetDatabase,
    [switch]$UseIisExpress
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

if ($ResetDatabase) {
    Write-Host "Resetting E2E database (destructive)..."
    & (Join-Path $root "tools\database\Reset-E2eDatabase.ps1") -ConfirmDestructive
    if ($LASTEXITCODE -ne 0) { throw "E2E database reset failed." }
}
else {
    Write-Host "Skipping E2E database reset (pass -ResetDatabase to drop/recreate AssetManagementModuleDb_E2E)."
    Write-Host "See tools/database/README.md for database operational scripts."
}

if ($UseIisExpress) {
    $webPath = Join-Path $root "src\AssetManagement.Web"
    $iisExpressPath = Join-Path ${env:ProgramFiles} "IIS Express\iisexpress.exe"
    $configPath = Join-Path $root ".build\iis-remote\applicationhost.config"
    $ensureConfigScript = Join-Path $root ".build\ensure-iis-config.ps1"

    if (-not (Test-Path $iisExpressPath)) {
        throw "IIS Express not found at $iisExpressPath"
    }
    if (-not (Test-Path $configPath)) {
        throw "IIS Express config not found at $configPath"
    }

    Write-Host "Restoring NuGet packages..."
    & (Join-Path $root "restore.ps1")

    Write-Host "Building AssetManagement.Web..."
    $msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe 2>$null | Select-Object -First 1
    if (-not $msbuild) { $msbuild = "msbuild" }
    & $msbuild (Join-Path $root "src\AssetManagement.Web\AssetManagement.Web.csproj") /t:Rebuild /p:Configuration=Debug /p:ResolveNuGetPackages=false /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE" }

    Write-Host "Starting IIS Express on port $Port..."
    Get-Process iisexpress -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
    & $ensureConfigScript -WebPath $webPath -Port ([int]$Port) -ConfigPath $configPath | Out-Null
    Get-ChildItem (Join-Path $env:TEMP 'iisexpress') -Filter 'applicationhost*.config' -ErrorAction SilentlyContinue |
        Remove-Item -Force -ErrorAction SilentlyContinue
    $arguments = "/config:`"$configPath`" /site:AssetManagementRemote"
    Start-Process -FilePath $iisExpressPath -ArgumentList $arguments -WindowStyle Hidden | Out-Null
    $url = "http://localhost:$Port/Account/Login"
}
else {
    Write-Host "Starting AssetManagement on local IIS (port $Port)..."
    & (Join-Path $root "tools\deploy\Start-IisDev.ps1") -Port ([int]$Port) -WaitForReady
    if ($LASTEXITCODE -ne 0) { throw "IIS start failed with exit code $LASTEXITCODE" }
    $url = "http://localhost:$Port/nanosoft/Account/Login"
}

$deadline = (Get-Date).AddSeconds(120)
while ((Get-Date) -lt $deadline) {
    try {
        $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 5
        if ($response.StatusCode -eq 200) {
            Write-Host "App ready at $url"
            exit 0
        }
    }
    catch {
        Start-Sleep -Seconds 2
    }
}

throw "Timed out waiting for $url"
