# Applies 075_TaxonomyPluralSynonymNormalize.sql and prints before/after duplicate counts.
param(
    [string]$ServerInstance = '.\SQLEXPRESS',
    [string]$Database = 'AssetManagementModuleDb'
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_Common.ps1"

Add-Type -AssemblyName System.Data

function Invoke-SqlScalar {
    param([System.Data.SqlClient.SqlConnection]$Connection, [string]$Sql)
    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = $Sql
    return $cmd.ExecuteScalar()
}

$connStr = "Data Source=$ServerInstance;Initial Catalog=$Database;Integrated Security=True;MultipleActiveResultSets=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()

$beforeTypes = Invoke-SqlScalar $conn @"
SELECT COUNT(*)
FROM AssetType t1
JOIN AssetType t2 ON t1.OrganizationId=t2.OrganizationId AND t1.AssetCategoryId=t2.AssetCategoryId AND t1.Id<t2.Id AND t1.IsActive=1 AND t2.IsActive=1
WHERE LOWER(t1.Name)+'s'=LOWER(t2.Name) OR LOWER(t2.Name)+'s'=LOWER(t1.Name)
"@

$beforeItDupes = Invoke-SqlScalar $conn @"
SELECT COUNT(*)
FROM Department d
WHERE d.IsActive=1 AND d.Code IN ('IT-101','ICT','ICT-ALL')
"@

Write-Host "Plural type pairs (before): $beforeTypes"
Write-Host "Legacy IT/ICT duplicate departments (before): $beforeItDupes"

$repoRoot = Get-RepositoryRoot
Invoke-SqlScriptFile -Connection $conn -ScriptPath (Join-Path $repoRoot 'database\scripts\004_Migrations\075_TaxonomyPluralSynonymNormalize.sql') -CommandTimeout 600

$afterTypes = Invoke-SqlScalar $conn @"
SELECT COUNT(*)
FROM AssetType t1
JOIN AssetType t2 ON t1.OrganizationId=t2.OrganizationId AND t1.AssetCategoryId=t2.AssetCategoryId AND t1.Id<t2.Id AND t1.IsActive=1 AND t2.IsActive=1
WHERE LOWER(t1.Name)+'s'=LOWER(t2.Name) OR LOWER(t2.Name)+'s'=LOWER(t1.Name)
"@

$afterItDupes = Invoke-SqlScalar $conn @"
SELECT COUNT(*)
FROM Department d
WHERE d.IsActive=1 AND d.Code IN ('IT-101','ICT','ICT-ALL')
"@

Write-Host "Plural type pairs (after):  $afterTypes"
Write-Host "Legacy IT/ICT duplicate departments (after):  $afterItDupes"

$conn.Close()
