# Clears login lockouts and repairs demo/platform admin accounts.
param(
    [string]$ServerInstance = ".\SQLEXPRESS",
    [string]$Database = "AssetManagementModuleDb"
)

$ErrorActionPreference = 'Stop'
$root = if ($PSScriptRoot) { $PSScriptRoot } else { Get-Location }
$webConfig = Join-Path $root "src\AssetManagement.Web\Web.config"

if (Test-Path $webConfig) {
    [xml]$config = Get-Content $webConfig
    $cs = $config.configuration.connectionStrings.add | Where-Object { $_.name -eq 'AssetManagementConnection' } | Select-Object -First 1
    if ($cs -and $cs.connectionString) {
        if ($cs.connectionString -match 'Data Source=([^;]+)') {
            $ServerInstance = $Matches[1]
        }
        if ($cs.connectionString -match 'Initial Catalog=([^;]+)') {
            $Database = $Matches[1]
        }
    }
}

Write-Host "Unlocking logins on [$Database] @ [$ServerInstance]..."

$scriptPath = Join-Path $root "database\scripts\004_Migrations\030_UnlockLoginAttempts.sql"
$renameScriptPath = Join-Path $root "database\scripts\004_Migrations\031_RenamePrimaryTenantNanosoft.sql"
if (-not (Test-Path $scriptPath)) {
    throw "Missing script: $scriptPath"
}

function Split-SqlBatches {
    param([string]$Script)
    $batches = New-Object System.Collections.Generic.List[string]
    $batch = New-Object System.Text.StringBuilder
    foreach ($line in ($Script -split "`r`n|`n|`r")) {
        if ($line.Trim().Equals('GO', [System.StringComparison]::OrdinalIgnoreCase)) {
            if ($batch.Length -gt 0) {
                [void]$batches.Add($batch.ToString())
                $batch.Clear() | Out-Null
            }
            continue
        }
        [void]$batch.AppendLine($line)
    }
    if ($batch.Length -gt 0) {
        [void]$batches.Add($batch.ToString())
    }
    return $batches
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
