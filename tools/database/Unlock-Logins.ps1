# Clears login lockouts and repairs demo/platform admin accounts (runs migration SQL 030/031).
param(
    [string]$ServerInstance,
    [string]$Database
)

. "$PSScriptRoot\_Common.ps1"

$ErrorActionPreference = 'Stop'
$root = Get-RepositoryRoot
$target = Resolve-SqlTargetFromWebConfig -ServerInstance $ServerInstance -Database $Database -RepositoryRoot $root
$ServerInstance = $target.ServerInstance
$Database = $target.Database

Write-Host "Unlocking logins on [$Database] @ [$ServerInstance]..."

$scriptPath = Join-Path $root "database\scripts\004_Migrations\030_UnlockLoginAttempts.sql"
$renameScriptPath = Join-Path $root "database\scripts\004_Migrations\031_RenamePrimaryTenantNanosoft.sql"
if (-not (Test-Path $scriptPath)) {
    throw "Missing script: $scriptPath"
}

Add-Type -AssemblyName System.Data
$connectionString = "Data Source=$ServerInstance;Initial Catalog=$Database;Integrated Security=True;MultipleActiveResultSets=True"
$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$connection.Open()

foreach ($batch in (Split-SqlBatches (Get-Content $scriptPath -Raw))) {
    if ([string]::IsNullOrWhiteSpace($batch)) { continue }
    $command = $connection.CreateCommand()
    $command.CommandText = $batch
    $command.CommandTimeout = 120
    [void]$command.ExecuteNonQuery()
}

if (Test-Path $renameScriptPath) {
    foreach ($batch in (Split-SqlBatches (Get-Content $renameScriptPath -Raw))) {
        if ([string]::IsNullOrWhiteSpace($batch)) { continue }
        $command = $connection.CreateCommand()
        $command.CommandText = $batch
        $command.CommandTimeout = 120
        [void]$command.ExecuteNonQuery()
    }
}

$connection.Close()
Write-Host "Done."
Write-Host "Platform login: /Account/Login -> superadmin@asset.local / P@ssw0rd!"
Write-Host "Company admin:  /{slug}/Account/Login -> {slug}@asset.local / P@ssw0rd!"
