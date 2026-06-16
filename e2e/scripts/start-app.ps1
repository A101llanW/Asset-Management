param(
    [string]$Port = $(if ($env:E2E_PORT) { $env:E2E_PORT.Trim() } else { "51901" })
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$webPath = Join-Path $root "src\AssetManagement.Web"
$iisExpressPath = Join-Path ${env:ProgramFiles} "IIS Express\iisexpress.exe"

if (-not (Test-Path $iisExpressPath)) {
    throw "IIS Express not found at $iisExpressPath"
}

Write-Host "Preparing E2E database..."
& (Join-Path $PSScriptRoot "prepare-e2e-db.ps1")

Write-Host "Restoring NuGet packages..."
& (Join-Path $root "restore.ps1")

Write-Host "Building AssetManagement.Web..."
$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe 2>$null | Select-Object -First 1
if (-not $msbuild) {
    $msbuild = "msbuild"
}

& $msbuild (Join-Path $root "src\AssetManagement.Web\AssetManagement.Web.csproj") /t:Rebuild /p:Configuration=Debug /p:ResolveNuGetPackages=false /v:minimal
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild failed with exit code $LASTEXITCODE"
}

Write-Host "Starting IIS Express on port $Port..."
Get-Process iisexpress -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
$arguments = "/path:`"$webPath`" /port:$Port"
Start-Process -FilePath $iisExpressPath -ArgumentList $arguments -WindowStyle Hidden | Out-Null

$deadline = (Get-Date).AddSeconds(120)
$url = "http://localhost:$Port/Account/Login"
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
