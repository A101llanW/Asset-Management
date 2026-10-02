# Copies all assets from one organization to another as duplicates (source org unchanged).
param(
    [Parameter(Mandatory = $true)]
    [string]$SourceSlug,

    [Parameter(Mandatory = $true)]
    [string]$TargetSlug,

    [string]$ServerInstance = '.\SQLEXPRESS',
    [string]$Database = 'AssetManagementModuleDb',

    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_Common.ps1"

Add-Type -AssemblyName System.Data

function Invoke-SqlScalar {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$Sql
    )
    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = $Sql
    return $cmd.ExecuteScalar()
}

function Invoke-SqlNonQuery {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$Sql
    )
    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = $Sql
    $cmd.CommandTimeout = 600
    return $cmd.ExecuteNonQuery()
}

$target = Resolve-SqlTargetFromWebConfig -ServerInstance $ServerInstance -Database $Database
$connStr = "Data Source=$($target.ServerInstance);Initial Catalog=$($target.Database);Integrated Security=True;MultipleActiveResultSets=True;Connect Timeout=30"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()

$sourceOrgId = [int](Invoke-SqlScalar $conn "SELECT TOP 1 [Id] FROM [Organization] WHERE [Slug] = N'$SourceSlug'")
$targetOrgId = [int](Invoke-SqlScalar $conn "SELECT TOP 1 [Id] FROM [Organization] WHERE [Slug] = N'$TargetSlug'")

if (-not $sourceOrgId) { throw "Source organization not found: $SourceSlug" }
if (-not $targetOrgId) { throw "Target organization not found: $TargetSlug" }
if ($sourceOrgId -eq $targetOrgId) { throw 'Source and target organizations must differ.' }

$sourceCount = [int](Invoke-SqlScalar $conn "SELECT COUNT(*) FROM [Asset] WHERE [OrganizationId] = $sourceOrgId AND [IsActive] = 1")
$targetBefore = [int](Invoke-SqlScalar $conn "SELECT COUNT(*) FROM [Asset] WHERE [OrganizationId] = $targetOrgId AND [IsActive] = 1")

Write-Host "Source org $SourceSlug (Id=$sourceOrgId): $sourceCount active assets"
Write-Host "Target org $TargetSlug (Id=$targetOrgId): $targetBefore active assets (before copy)"

if ($sourceCount -eq 0) {
    Write-Host 'Nothing to copy.'
    $conn.Close()
    return
}

if ($WhatIf) {
    Write-Host 'WhatIf: no changes made.'
    $conn.Close()
    return
}

$copySql = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @sourceOrgId INT = $sourceOrgId;
DECLARE @targetOrgId INT = $targetOrgId;
DECLARE @now DATETIME = GETUTCDATE();
DECLARE @suffix NVARCHAR(20) = N'-COPY';

BEGIN TRANSACTION;

-- Category map: match by Name within target org; create if missing.
DECLARE @categoryMap TABLE (SourceId INT NOT NULL PRIMARY KEY, TargetId INT NOT NULL);
INSERT INTO @categoryMap (SourceId, TargetId)
SELECT sc.[Id],
       COALESCE(
           (SELECT TOP 1 tc.[Id]
            FROM [AssetCategory] tc
            WHERE tc.[OrganizationId] = @targetOrgId
              AND tc.[Name] = sc.[Name]
            ORDER BY tc.[Id]),
           0)
FROM [AssetCategory] sc
WHERE sc.[OrganizationId] = @sourceOrgId
  AND sc.[Id] IN (SELECT DISTINCT [CategoryId] FROM [Asset] WHERE [OrganizationId] = @sourceOrgId AND [IsActive] = 1);

INSERT INTO [AssetCategory] ([Name],[Description],[OrganizationId],[CreatedAt],[IsActive])
SELECT sc.[Name], sc.[Description], @targetOrgId, @now, sc.[IsActive]
FROM [AssetCategory] sc
INNER JOIN @categoryMap cm ON cm.SourceId = sc.[Id]
WHERE cm.TargetId = 0;

UPDATE cm
SET cm.TargetId = tc.[Id]
FROM @categoryMap cm
INNER JOIN [AssetCategory] sc ON sc.[Id] = cm.SourceId
INNER JOIN [AssetCategory] tc ON tc.[OrganizationId] = @targetOrgId AND tc.[Name] = sc.[Name]
WHERE cm.TargetId = 0;

-- Asset type map: match by Name + mapped category.
DECLARE @typeMap TABLE (SourceId INT NOT NULL PRIMARY KEY, TargetId INT NOT NULL);
INSERT INTO @typeMap (SourceId, TargetId)
SELECT st.[Id],
       COALESCE(
           (SELECT TOP 1 tt.[Id]
            FROM [AssetType] tt
            INNER JOIN @categoryMap cm ON cm.SourceId = st.[AssetCategoryId]
            WHERE tt.[OrganizationId] = @targetOrgId
              AND tt.[Name] = st.[Name]
              AND tt.[AssetCategoryId] = cm.TargetId
            ORDER BY tt.[Id]),
           0)
FROM [AssetType] st
WHERE st.[OrganizationId] = @sourceOrgId
  AND st.[Id] IN (SELECT DISTINCT [AssetTypeId] FROM [Asset] WHERE [OrganizationId] = @sourceOrgId AND [IsActive] = 1);

INSERT INTO [AssetType]
    ([Name],[Description],[AssetCategoryId],[OrganizationId],[CreatedAt],[IsActive],
     [UsefulLifeMonths],[DepreciationLifeMonths],[DepreciationRatePercent])
SELECT st.[Name], st.[Description], cm.TargetId, @targetOrgId, @now, st.[IsActive],
       st.[UsefulLifeMonths], st.[DepreciationLifeMonths], st.[DepreciationRatePercent]
FROM [AssetType] st
INNER JOIN @typeMap tm ON tm.SourceId = st.[Id]
INNER JOIN @categoryMap cm ON cm.SourceId = st.[AssetCategoryId]
WHERE tm.TargetId = 0;

UPDATE tm
SET tm.TargetId = tt.[Id]
FROM @typeMap tm
INNER JOIN [AssetType] st ON st.[Id] = tm.SourceId
INNER JOIN @categoryMap cm ON cm.SourceId = st.[AssetCategoryId]
INNER JOIN [AssetType] tt ON tt.[OrganizationId] = @targetOrgId
    AND tt.[Name] = st.[Name]
    AND tt.[AssetCategoryId] = cm.TargetId
WHERE tm.TargetId = 0;

-- Sub-type map: match by Name + mapped type.
DECLARE @subTypeMap TABLE (SourceId INT NOT NULL PRIMARY KEY, TargetId INT NULL);
INSERT INTO @subTypeMap (SourceId, TargetId)
SELECT ss.[Id],
       (SELECT TOP 1 ts.[Id]
        FROM [AssetSubType] ts
        INNER JOIN @typeMap tm ON tm.SourceId = ss.[AssetTypeId]
        WHERE ts.[OrganizationId] = @targetOrgId
          AND ts.[Name] = ss.[Name]
          AND ts.[AssetTypeId] = tm.TargetId
        ORDER BY ts.[Id])
FROM [AssetSubType] ss
WHERE ss.[OrganizationId] = @sourceOrgId
  AND ss.[Id] IN (
      SELECT DISTINCT [AssetSubTypeId]
      FROM [Asset]
      WHERE [OrganizationId] = @sourceOrgId AND [IsActive] = 1 AND [AssetSubTypeId] IS NOT NULL
  );

INSERT INTO [AssetSubType]
    ([Name],[Brand],[Model],[Specifications],[Sku],[AssetTypeId],[OrganizationId],[CreatedAt],[IsActive],[DefaultAcquisitionCost])
SELECT ss.[Name], ss.[Brand], ss.[Model], ss.[Specifications], ss.[Sku], tm.TargetId, @targetOrgId, @now, ss.[IsActive], ss.[DefaultAcquisitionCost]
FROM [AssetSubType] ss
INNER JOIN @subTypeMap sm ON sm.SourceId = ss.[Id]
INNER JOIN @typeMap tm ON tm.SourceId = ss.[AssetTypeId]
WHERE sm.TargetId IS NULL;

UPDATE sm
SET sm.TargetId = ts.[Id]
FROM @subTypeMap sm
INNER JOIN [AssetSubType] ss ON ss.[Id] = sm.SourceId
INNER JOIN @typeMap tm ON tm.SourceId = ss.[AssetTypeId]
INNER JOIN [AssetSubType] ts ON ts.[OrganizationId] = @targetOrgId
    AND ts.[Name] = ss.[Name]
    AND ts.[AssetTypeId] = tm.TargetId
WHERE sm.TargetId IS NULL;

-- Department map: match by Code, then Name.
DECLARE @deptMap TABLE (SourceId INT NOT NULL PRIMARY KEY, TargetId INT NULL);
INSERT INTO @deptMap (SourceId, TargetId)
SELECT sd.[Id],
       COALESCE(
           (SELECT TOP 1 td.[Id]
            FROM [Department] td
            WHERE td.[OrganizationId] = @targetOrgId
              AND (
                  (sd.[Code] IS NOT NULL AND td.[Code] = sd.[Code])
                  OR (sd.[Code] IS NULL AND td.[Name] = sd.[Name])
              )
            ORDER BY td.[Id]),
           NULL)
FROM [Department] sd
WHERE sd.[OrganizationId] = @sourceOrgId
  AND sd.[Id] IN (
      SELECT DISTINCT [DepartmentId]
      FROM [Asset]
      WHERE [OrganizationId] = @sourceOrgId AND [IsActive] = 1 AND [DepartmentId] IS NOT NULL
  );

INSERT INTO [Department] ([Name],[Code],[Description],[OrganizationId],[CreatedAt],[IsActive],[ParentDepartmentId],[DepartmentKind])
SELECT sd.[Name], sd.[Code], sd.[Description], @targetOrgId, @now, sd.[IsActive], NULL, sd.[DepartmentKind]
FROM [Department] sd
INNER JOIN @deptMap dm ON dm.SourceId = sd.[Id]
WHERE dm.TargetId IS NULL;

UPDATE dm
SET dm.TargetId = td.[Id]
FROM @deptMap dm
INNER JOIN [Department] sd ON sd.[Id] = dm.SourceId
INNER JOIN [Department] td ON td.[OrganizationId] = @targetOrgId
    AND (
        (sd.[Code] IS NOT NULL AND td.[Code] = sd.[Code])
        OR (sd.[Code] IS NULL AND td.[Name] = sd.[Name])
    )
WHERE dm.TargetId IS NULL;

-- Supplier map: match by Name.
DECLARE @supplierMap TABLE (SourceId INT NOT NULL PRIMARY KEY, TargetId INT NULL);
INSERT INTO @supplierMap (SourceId, TargetId)
SELECT ss.[Id],
       (SELECT TOP 1 ts.[Id]
        FROM [Supplier] ts
        WHERE ts.[OrganizationId] = @targetOrgId AND ts.[SupplierName] = ss.[SupplierName]
        ORDER BY ts.[Id])
FROM [Supplier] ss
WHERE ss.[OrganizationId] = @sourceOrgId
  AND ss.[Id] IN (
      SELECT DISTINCT [SupplierId]
      FROM [Asset]
      WHERE [OrganizationId] = @sourceOrgId AND [IsActive] = 1 AND [SupplierId] IS NOT NULL
  );

INSERT INTO [Supplier]
    ([SupplierName],[ContactPerson],[Email],[Phone],[Address],[OrganizationId],[CreatedAt],[IsActive])
SELECT ss.[SupplierName], ss.[ContactPerson], ss.[Email], ss.[Phone], ss.[Address], @targetOrgId, @now, ss.[IsActive]
FROM [Supplier] ss
INNER JOIN @supplierMap sm ON sm.SourceId = ss.[Id]
WHERE sm.TargetId IS NULL;

UPDATE sm
SET sm.TargetId = ts.[Id]
FROM @supplierMap sm
INNER JOIN [Supplier] ss ON ss.[Id] = sm.SourceId
INNER JOIN [Supplier] ts ON ts.[OrganizationId] = @targetOrgId AND ts.[SupplierName] = ss.[SupplierName]
WHERE sm.TargetId IS NULL;

-- Copy assets (skip if same AssetTag already exists in target org).
INSERT INTO [Asset]
    ([AssetName],[AssetTag],[CategoryId],[AssetTypeId],[AssetSubTypeId],[Brand],[Model],[SerialNumber],[BarcodeOrQRCode],
     [Specifications],[Condition],[CurrentStatus],[Description],[PurchaseDate],[AcquisitionCost],[TaxAmount],[Currency],
     [SupplierId],[DepartmentId],[CurrentCustodianId],[ConditionOnReceipt],[UsefulLifeMonths],[SalvageValue],
     [DepreciationMethod],[DepreciationLifeMonths],[DepreciationRatePercent],[DepreciationStartDate],
     [CurrentBookValue],[AccumulatedDepreciation],[ImpairmentNotes],[WarrantyStartDate],[WarrantyEndDate],
     [PolicyReference],[InsuredValue],[ImagePath],[IsLeased],[IsInsured],
     [RequireTransferApproval],[TransferApprovalStageRoleIds],[TransferApprovalStageUserIds],
     [RequireDisposalApproval],[DisposalApprovalStageRoleIds],[DisposalApprovalStageUserIds],
     [OrganizationId],[CreatedAt],[IsActive])
SELECT
    a.[AssetName],
    a.[AssetTag],
    cm.TargetId,
    tm.TargetId,
    CASE WHEN a.[AssetSubTypeId] IS NULL THEN NULL ELSE sm.TargetId END,
    a.[Brand],
    a.[Model],
    CASE
        WHEN a.[SerialNumber] IS NULL OR LTRIM(RTRIM(a.[SerialNumber])) = N'' THEN NULL
        ELSE LEFT(a.[SerialNumber] + @suffix, 120)
    END,
    CASE
        WHEN a.[BarcodeOrQRCode] IS NULL OR LTRIM(RTRIM(a.[BarcodeOrQRCode])) = N'' THEN NULL
        ELSE LEFT(a.[BarcodeOrQRCode] + @suffix, 120)
    END,
    a.[Specifications],
    a.[Condition],
    a.[CurrentStatus],
    a.[Description],
    a.[PurchaseDate],
    a.[AcquisitionCost],
    a.[TaxAmount],
    a.[Currency],
    CASE WHEN a.[SupplierId] IS NULL THEN NULL ELSE sup.TargetId END,
    CASE WHEN a.[DepartmentId] IS NULL THEN NULL ELSE dm.TargetId END,
    NULL,
    a.[ConditionOnReceipt],
    a.[UsefulLifeMonths],
    a.[SalvageValue],
    a.[DepreciationMethod],
    a.[DepreciationLifeMonths],
    a.[DepreciationRatePercent],
    a.[DepreciationStartDate],
    a.[CurrentBookValue],
    a.[AccumulatedDepreciation],
    a.[ImpairmentNotes],
    a.[WarrantyStartDate],
    a.[WarrantyEndDate],
    a.[PolicyReference],
    a.[InsuredValue],
    a.[ImagePath],
    a.[IsLeased],
    a.[IsInsured],
    a.[RequireTransferApproval],
    a.[TransferApprovalStageRoleIds],
    a.[TransferApprovalStageUserIds],
    a.[RequireDisposalApproval],
    a.[DisposalApprovalStageRoleIds],
    a.[DisposalApprovalStageUserIds],
    @targetOrgId,
    @now,
    a.[IsActive]
FROM [Asset] a
INNER JOIN @categoryMap cm ON cm.SourceId = a.[CategoryId]
INNER JOIN @typeMap tm ON tm.SourceId = a.[AssetTypeId]
LEFT JOIN @subTypeMap sm ON sm.SourceId = a.[AssetSubTypeId]
LEFT JOIN @deptMap dm ON dm.SourceId = a.[DepartmentId]
LEFT JOIN @supplierMap sup ON sup.SourceId = a.[SupplierId]
WHERE a.[OrganizationId] = @sourceOrgId
  AND a.[IsActive] = 1
  AND NOT EXISTS (
      SELECT 1
      FROM [Asset] existing
      WHERE existing.[OrganizationId] = @targetOrgId
        AND existing.[AssetTag] = a.[AssetTag]
  );

DECLARE @inserted INT = @@ROWCOUNT;

COMMIT TRANSACTION;

SELECT @inserted AS AssetsCopied;
"@

$copied = Invoke-SqlScalar $conn $copySql
$targetAfter = [int](Invoke-SqlScalar $conn "SELECT COUNT(*) FROM [Asset] WHERE [OrganizationId] = $targetOrgId AND [IsActive] = 1")
$sourceAfter = [int](Invoke-SqlScalar $conn "SELECT COUNT(*) FROM [Asset] WHERE [OrganizationId] = $sourceOrgId AND [IsActive] = 1")

Write-Host ''
Write-Host "Copied $copied assets into $TargetSlug."
Write-Host "Source org unchanged: $sourceAfter active assets"
Write-Host "Target org after copy: $targetAfter active assets"

$conn.Close()
