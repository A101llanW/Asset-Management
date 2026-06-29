# Applies database/scripts/004_Migrations via the ASP.NET SqlDatabaseInitializer (AssetManagement.Runner migrate).
param(
    [string[]]$Targets = @(
        "(localdb)\MSSQLLocalDB|AssetManagementModuleDb_E2E",
        "localhost\SQLEXPRESS|AssetManagementModuleDb"
    )
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$runnerProject = Join-Path $root "src\AssetManagement.Runner\AssetManagement.Runner.csproj"
$scriptsPath = Join-Path $root "database\scripts"
$runnerConfig = Join-Path $root "src\AssetManagement.Runner\App.config"
$runnerConfigBackup = "$runnerConfig.run-backup"

if (-not (Test-Path $runnerConfigBackup)) {
    Copy-Item $runnerConfig $runnerConfigBackup -Force
}

Write-Host "Building AssetManagement.Runner..."
dotnet build $runnerProject -c Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$runnerExe = Join-Path $root "src\AssetManagement.Runner\bin\Release\net40\AssetManagement.Runner.exe"
if (-not (Test-Path $runnerExe)) {
    throw "Runner executable not found: $runnerExe"
}

function Set-RunnerConnectionString([string]$ServerInstance, [string]$Database) {
    [xml]$config = Get-Content $runnerConfigBackup
    $connection = $config.configuration.connectionStrings.add | Where-Object { $_.name -eq 'AssetManagementConnection' }
    $connection.connectionString = "Data Source=$ServerInstance;Initial Catalog=$Database;Integrated Security=True;MultipleActiveResultSets=True"
    $scriptsSetting = $config.configuration.appSettings.add | Where-Object { $_.key -eq 'DatabaseScriptsPath' }
    if ($scriptsSetting) {
        $scriptsSetting.SetAttribute('value', $scriptsPath)
    } else {
        $appSettings = $config.configuration.appSettings
        $node = $config.CreateElement('add')
        $node.SetAttribute('key', 'DatabaseScriptsPath')
        $node.SetAttribute('value', $scriptsPath)
        $appSettings.AppendChild($node) | Out-Null
    }
    $config.Save($runnerConfig)
    Copy-Item $runnerConfig (Join-Path (Split-Path $runnerExe) "AssetManagement.Runner.exe.config") -Force
}

foreach ($target in $Targets) {
    $parts = $target -split '\|', 2
    if ($parts.Length -ne 2) {
        throw "Invalid target '$target'. Use ServerInstance|Database."
    }

    $server = $parts[0]
    $database = $parts[1]
    Write-Host ""
    Write-Host "=== $server -> $database ===" -ForegroundColor Cyan
    Set-RunnerConnectionString $server $database
  & $runnerExe migrate --scripts $scriptsPath
    if ($LASTEXITCODE -ne 0) { throw "Migration failed for $database on $server." }
}

Copy-Item $runnerConfigBackup $runnerConfig -Force
Write-Host ""
Write-Host "All migrations applied via ASP.NET SqlDatabaseInitializer." -ForegroundColor Green
