$ErrorActionPreference = 'Stop'
$cs = 'Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=AssetManagementModuleDb;Integrated Security=True'
& "$PSScriptRoot\initialize-database.ps1" | Out-Null
$c = New-Object System.Data.SqlClient.SqlConnection($cs)
$c.Open()
$cmd = $c.CreateCommand()
$cmd.CommandText = @"
SELECT c.Name AS Category, COUNT(*) AS Cnt
FROM Asset a
INNER JOIN AssetCategory c ON c.Id = a.CategoryId
WHERE a.OrganizationId = (SELECT TOP 1 Id FROM Organization WHERE Slug = N'default' ORDER BY Id)
  AND (a.AssetTag LIKE N'IT-%' OR a.AssetTag LIKE N'FIN-%' OR a.AssetTag LIKE N'HR-%'
       OR a.AssetTag LIKE N'OPS-%' OR a.AssetTag LIKE N'ADMIN-%')
GROUP BY c.Name
ORDER BY c.Name;
"@
$r = $cmd.ExecuteReader()
while ($r.Read()) { Write-Host "$($r['Category']): $($r['Cnt'])" }
$c.Close()
