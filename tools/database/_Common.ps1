# Shared helpers for database operational scripts (tools/database).
$ErrorActionPreference = 'Stop'

function Get-RepositoryRoot {
    $dir = $PSScriptRoot
    while ($dir) {
        $scriptsSchema = Join-Path $dir 'database\scripts\001_Schema'
        if (Test-Path $scriptsSchema) {
            return $dir
        }

        $parent = Split-Path $dir -Parent
        if ($parent -eq $dir) {
            break
        }

        $dir = $parent
    }

    throw 'Repository root not found (expected database\scripts\001_Schema).'
}

function Get-WebConfigPath {
    param([string]$RepositoryRoot = (Get-RepositoryRoot))
    return Join-Path $RepositoryRoot 'src\AssetManagement.Web\Web.config'
}

function Read-ConnectionStringFromWebConfig {
    param([string]$RepositoryRoot = (Get-RepositoryRoot))

    $webConfig = Get-WebConfigPath -RepositoryRoot $RepositoryRoot
    if (-not (Test-Path $webConfig)) {
        return $null
    }

    [xml]$config = Get-Content $webConfig
    $cs = $config.configuration.connectionStrings.add |
        Where-Object { $_.name -eq 'AssetManagementConnection' } |
        Select-Object -First 1

    if ($cs -and $cs.connectionString) {
        return $cs.connectionString
    }

    return $null
}

function Resolve-SqlTargetFromWebConfig {
    param(
        [string]$ServerInstance,
        [string]$Database,
        [string]$RepositoryRoot = (Get-RepositoryRoot)
    )

    if ($ServerInstance -and $Database) {
        return @{ ServerInstance = $ServerInstance; Database = $Database }
    }

    $connectionString = Read-ConnectionStringFromWebConfig -RepositoryRoot $RepositoryRoot
    if ($connectionString) {
        if (-not $ServerInstance -and $connectionString -match 'Data Source=([^;]+)') {
            $ServerInstance = $Matches[1]
        }
        if (-not $Database -and $connectionString -match 'Initial Catalog=([^;]+)') {
            $Database = $Matches[1]
        }
    }

    if (-not $ServerInstance) { $ServerInstance = '.\SQLEXPRESS' }
    if (-not $Database) { $Database = 'AssetManagementModuleDb' }

    return @{ ServerInstance = $ServerInstance; Database = $Database }
}

function Assert-E2eDatabaseName {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Database
    )

    if ($Database -notmatch '_E2E$') {
        throw @"
Refusing destructive E2E reset on database '$Database'.
Only databases whose names end with '_E2E' are allowed (e.g. AssetManagementModuleDb_E2E).
Use Initialize-Database.ps1 for non-destructive setup on other databases.
"@
    }
}

function Assert-DestructiveResetAllowed {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Database,
        [switch]$ConfirmDestructive
    )

    Assert-E2eDatabaseName -Database $Database

    if ($ConfirmDestructive) {
        return
    }

    if ($env:ALLOW_E2E_DB_RESET -eq 'true') {
        return
    }

    throw @"
Destructive E2E database reset blocked for '$Database'.
Pass -ConfirmDestructive or set ALLOW_E2E_DB_RESET=true to proceed.
This script DROPs the database and recreates it from seed scripts.
"@
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

function Invoke-SqlBatch {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$Batch,
        [int]$CommandTimeout = 120
    )

    if ([string]::IsNullOrWhiteSpace($Batch)) { return }

    $command = $Connection.CreateCommand()
    $command.CommandText = $Batch
    $command.CommandTimeout = $CommandTimeout
    [void]$command.ExecuteNonQuery()
}

function Invoke-SqlScriptFile {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$ScriptPath,
        [int]$CommandTimeout = 120,
        [string]$ContentOverride = $null
    )

    $script = if ($ContentOverride) { $ContentOverride } else { Get-Content -Path $ScriptPath -Raw }
    foreach ($batch in (Split-SqlBatches -Script $script)) {
        Invoke-SqlBatch -Connection $Connection -Batch $batch -CommandTimeout $CommandTimeout
    }
}
