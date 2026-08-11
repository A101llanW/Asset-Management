# Applies database/scripts to the configured SQL Server instance (non-destructive: never DROP DATABASE).
param(
    [string]$ServerInstance = ".\SQLEXPRESS",
    [string]$Database = "AssetManagementModuleDb",
    [switch]$IncludeLargeDataset
)

. "$PSScriptRoot\_Common.ps1"

$ErrorActionPreference = 'Stop'
$root = Get-RepositoryRoot
$scriptsRoot = Join-Path $root "database\scripts"
$connectionString = "Data Source=$ServerInstance;Initial Catalog=$Database;Integrated Security=True;MultipleActiveResultSets=True"

Add-Type -AssemblyName System.Data

function Get-OrderedDatabaseScripts {
    param(
        [string]$ScriptsRoot,
        [string]$ExcludePath
    )

    $phaseFolders = @(
        @{ Order = 1; Path = Join-Path $ScriptsRoot "001_Schema" },
        @{ Order = 2; Path = Join-Path $ScriptsRoot "004_Migrations" },
        @{ Order = 3; Path = Join-Path $ScriptsRoot "002_Seed" },
        @{ Order = 4; Path = Join-Path $ScriptsRoot "003_Indexes" }
    )

    $files = New-Object System.Collections.Generic.List[object]
    foreach ($phase in $phaseFolders) {
        if (-not (Test-Path $phase.Path)) {
            continue
        }

        Get-ChildItem -Path $phase.Path -Filter *.sql |
            Where-Object { $_.FullName -ne $ExcludePath } |
            ForEach-Object {
                [void]$files.Add([PSCustomObject]@{
                    Order = $phase.Order
                    Name  = $_.Name
                    FullName = $_.FullName
                })
            }
    }

    return $files | Sort-Object Order, Name | ForEach-Object { $_.FullName }
}

Write-Host "Ensuring database [$Database] exists on [$ServerInstance]..."
$masterConnectionString = "Data Source=$ServerInstance;Initial Catalog=master;Integrated Security=True"
$masterConnection = New-Object System.Data.SqlClient.SqlConnection($masterConnectionString)
$masterConnection.Open()
try {
    $createDb = $masterConnection.CreateCommand()
    $createDb.CommandText = "IF DB_ID(@databaseName) IS NULL BEGIN EXEC('CREATE DATABASE [' + @databaseName + ']'); END"
    $null = $createDb.Parameters.AddWithValue("@databaseName", $Database)
    [void]$createDb.ExecuteNonQuery()
}
finally {
    $masterConnection.Close()
}

$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$connection.Open()
try {
    $largeDatasetScript = Join-Path $scriptsRoot "002_Seed\003_LargeDataset.sql"
    $scriptFiles = Get-OrderedDatabaseScripts -ScriptsRoot $scriptsRoot -ExcludePath $largeDatasetScript

    foreach ($scriptFile in $scriptFiles) {
        Write-Host "Applying $($scriptFile.Substring($root.Length + 1))..."
        Invoke-SqlScriptFile -Connection $connection -ScriptPath $scriptFile
    }

    if ($IncludeLargeDataset) {
        if (-not (Test-Path $largeDatasetScript)) {
            throw "Large dataset script not found: $largeDatasetScript"
        }

        Write-Host "Applying large dataset seed (dev-only, 100k assets)..."
        $largeDatasetContent = (Get-Content -Path $largeDatasetScript -Raw) -replace '@RunLargeDatasetSeed BIT = 0', '@RunLargeDatasetSeed BIT = 1'
        Invoke-SqlScriptFile -Connection $connection -ScriptPath $largeDatasetScript -CommandTimeout 3600 -ContentOverride $largeDatasetContent
    }
}
finally {
    $connection.Close()
}

Write-Host "Database initialization complete."
