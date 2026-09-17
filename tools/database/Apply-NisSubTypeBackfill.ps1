# Applies 073_NisClassroomSubTypeBackfill.sql and prints before/after counts.
param(
    [string]$ServerInstance = '.\SQLEXPRESS',
    [string]$Database = 'AssetManagementModuleDb'
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_Common.ps1"

Add-Type -AssemblyName System.Data

function Invoke-SqlNonQuery {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$Sql
    )
    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = $Sql
    [void]$cmd.ExecuteNonQuery()
}

function Invoke-SqlScalar {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$Sql
    )
    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = $Sql
    return $cmd.ExecuteScalar()
}

$connStr = "Data Source=$ServerInstance;Initial Catalog=$Database;Integrated Security=True;MultipleActiveResultSets=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()

$before = Invoke-SqlScalar $conn @"
SELECT COUNT(*)
FROM [Asset] a
JOIN [Organization] o ON o.[Id] = a.[OrganizationId]
WHERE o.[Slug] = N'nis'
  AND a.[IsActive] = 1
  AND a.[AssetSubTypeId] IS NULL
"@

Write-Host "NIS active assets missing sub-type (before): $before"

$repoRoot = Get-RepositoryRoot
$scripts = @(
    'database\scripts\004_Migrations\074_NisClassroomTaxonomyNormalize.sql'
)
if ($before -gt 0) {
    $scripts = @('database\scripts\004_Migrations\073_NisClassroomSubTypeBackfill.sql') + $scripts
}
foreach ($relativePath in $scripts) {
    $scriptPath = Join-Path $repoRoot $relativePath
    $sql = Get-Content $scriptPath -Raw
    $batches = Split-SqlBatches -Script $sql
    foreach ($batch in $batches) {
        if ($batch.Trim().Length -gt 0) {
            Invoke-SqlNonQuery -Connection $conn -Sql $batch
        }
    }
}

$after = Invoke-SqlScalar $conn @"
SELECT COUNT(*)
FROM [Asset] a
JOIN [Organization] o ON o.[Id] = a.[OrganizationId]
WHERE o.[Slug] = N'nis'
  AND a.[IsActive] = 1
  AND a.[AssetSubTypeId] IS NULL
"@

Write-Host "NIS active assets missing sub-type (after):  $after"

Write-Host ''
Write-Host 'Assignments by sub-type:'
$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
SELECT st.[Name], at.[Name] AS AssetTypeName, COUNT(*) AS AssetCount
FROM [Asset] a
JOIN [AssetSubType] st ON st.[Id] = a.[AssetSubTypeId]
JOIN [AssetType] at ON at.[Id] = st.[AssetTypeId]
JOIN [Organization] o ON o.[Id] = a.[OrganizationId]
WHERE o.[Slug] = N'nis'
  AND a.[IsActive] = 1
GROUP BY st.[Name], at.[Name]
ORDER BY AssetCount DESC
"@
$da = New-Object System.Data.SqlClient.SqlDataAdapter $cmd
$dt = New-Object System.Data.DataTable
[void]$da.Fill($dt)
$dt | Format-Table -AutoSize

$conn.Close()

if ($after -ne 0) {
    throw "Backfill incomplete: $after NIS assets still missing AssetSubTypeId."
}

Write-Host 'Backfill completed successfully.'
