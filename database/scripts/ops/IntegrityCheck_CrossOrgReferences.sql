/*
================================================================================
 IntegrityCheck_CrossOrgReferences.sql
 Read-only integrity check: org-scoped rows that reference a row of ANOTHER
 organisation (company), plus dangling references (no such row at all).

 SAFE TO RE-RUN. This script never writes to the application database:
   * Only SELECTs against dbo tables; results go to session temp tables
     (#Checks, #Refs, #Findings in tempdb) that are dropped at the end.
   * No INSERT/UPDATE/DELETE/MERGE/DDL against dbo objects.
   * DEADLOCK_PRIORITY LOW and a lock timeout, so a busy app always wins.
   * Checks whose table/column does not exist on this database are reported
     as "skipped" instead of failing.

 IMPORTANT - how cross-org references get in:
   Org/template copies (new company from a template org, copying approval
   matrices, roles, settings, departments...) MUST go through the application
   service mapping (OrganizationService.EnsureTenantRoles -> role map,
   OrgReferenceGuard.MapCopiedApprovalSetting) which maps every id to the
   target company's equivalent and drops/reports unmapped ones. NEVER copy
   org data with raw SQL (INSERT ... SELECT from another org): the CSV id
   columns (*StageRoleIds / *StageUserIds) have no foreign key and no
   same-org check in the database, so raw ids silently cross companies.
   The 16 Sep 2026 incident (2,644 Test-WF / org 13 assets carrying org 11
   'Template 3' role ids 45,41 in Transfer/DisposalApprovalStageRoleIds) was
   most likely such a one-off script. It was remapped on 8 Oct 2026
   (backup dbo.Bak_XorgRoleRemap_20261008).

 What is checked (source row OrganizationId vs referenced row OrganizationId):
   * Every CSV id column: Asset/AssetTransfer/DisposalRecord/PurchaseRequest
     approval stage roles + persons, Department custom + requisition stage
     roles + persons, SystemSetting Approval.Process.*.StageRoleIds/UserIds.
   * Every org-scoped id column, with or without a foreign key (FKs only
     guarantee the row exists, not that it is in the same company):
     categories/types/sub-types/suppliers/departments/assets/users/roles,
     approval actions, child records, invitations, Users.RoleId and
     Users.DepartmentId, RolePermission.RoleId (RolePermission.OrganizationId
     vs Roles.OrganizationId), RoleTemplate.SourceRoleId, ...
   Referenced rows with OrganizationId NULL (platform-level users/roles) are
   not counted as cross-org. Source rows with OrganizationId NULL are ignored.
   AuditLog / security logs are history and are not checked.

 Output:
   1) One row per table/column: references checked, cross-org references
      (and distinct source rows), dangling references, sample ids.
   2) Breakdown of cross-org references by source org -> referenced org.
================================================================================
*/
SET NOCOUNT ON;
SET DEADLOCK_PRIORITY LOW;
SET LOCK_TIMEOUT 10000;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

IF OBJECT_ID('tempdb..#Checks') IS NOT NULL DROP TABLE #Checks;
IF OBJECT_ID('tempdb..#Refs') IS NOT NULL DROP TABLE #Refs;
IF OBJECT_ID('tempdb..#Findings') IS NOT NULL DROP TABLE #Findings;

CREATE TABLE #Checks
(
    CheckId      int IDENTITY(1,1) PRIMARY KEY,
    Category     nvarchar(40)  NOT NULL,   -- 'CSV ids', 'No FK', 'FK'
    SourceTable  sysname       NOT NULL,
    SourceColumn sysname       NOT NULL,
    TargetTable  sysname       NOT NULL,
    IsCsv        bit           NOT NULL DEFAULT 0,
    ExtraFilter  nvarchar(400) NULL         -- extra WHERE on the source row
);

CREATE TABLE #Refs
(
    CheckId int            NOT NULL,
    SrcId   nvarchar(450)  NOT NULL,
    SrcOrg  int            NOT NULL,
    RefId   nvarchar(450)  NOT NULL,
    RefOrg  int            NULL,
    Found   bit            NOT NULL
);

CREATE TABLE #Findings
(
    CheckId      int           NOT NULL,
    Status       nvarchar(60)  NOT NULL,
    RefsChecked  int           NOT NULL DEFAULT 0,
    CrossOrgRefs int           NOT NULL DEFAULT 0,
    CrossOrgRows int           NOT NULL DEFAULT 0,
    DanglingRefs int           NOT NULL DEFAULT 0
);

-- ---------------------------------------------------------------- CSV id columns (no FK, no same-org check in the DB)
INSERT #Checks (Category, SourceTable, SourceColumn, TargetTable, IsCsv, ExtraFilter) VALUES
 ('CSV ids', 'Asset',          'TransferApprovalStageRoleIds',     'Roles', 1, NULL),
 ('CSV ids', 'Asset',          'DisposalApprovalStageRoleIds',     'Roles', 1, NULL),
 ('CSV ids', 'Asset',          'TransferApprovalStageUserIds',     'Users', 1, NULL),
 ('CSV ids', 'Asset',          'DisposalApprovalStageUserIds',     'Users', 1, NULL),
 ('CSV ids', 'AssetTransfer',  'ApprovalStageRoleIds',             'Roles', 1, NULL),
 ('CSV ids', 'AssetTransfer',  'ApprovalStageUserIds',             'Users', 1, NULL),
 ('CSV ids', 'DisposalRecord', 'ApprovalStageRoleIds',             'Roles', 1, NULL),
 ('CSV ids', 'DisposalRecord', 'ApprovalStageUserIds',             'Users', 1, NULL),
 ('CSV ids', 'PurchaseRequest','ApprovalStageRoleIds',             'Roles', 1, NULL),
 ('CSV ids', 'PurchaseRequest','ApprovalStageUserIds',             'Users', 1, NULL),
 ('CSV ids', 'Department',     'CustomStageRoleIds',               'Roles', 1, NULL),
 ('CSV ids', 'Department',     'CustomStageUserIds',               'Users', 1, NULL),
 ('CSV ids', 'Department',     'RequisitionApprovalStageRoleIds',  'Roles', 1, NULL),
 ('CSV ids', 'Department',     'RequisitionApprovalStageUserIds',  'Users', 1, NULL),
 ('CSV ids', 'SystemSetting',  'SettingValue',                     'Roles', 1, N's.SettingKey LIKE N''Approval.Process.%.StageRoleIds'''),
 ('CSV ids', 'SystemSetting',  'SettingValue',                     'Users', 1, N's.SettingKey LIKE N''Approval.Process.%.StageUserIds''');

-- ---------------------------------------------------------------- single id columns WITHOUT a foreign key
INSERT #Checks (Category, SourceTable, SourceColumn, TargetTable) VALUES
 ('No FK', 'Users',                  'RoleId',                    'Roles'),
 ('No FK', 'Users',                  'DepartmentId',              'Department'),
 ('No FK', 'Department',             'PurchaseOrderRaiserRoleId', 'Roles'),
 ('No FK', 'Department',             'PurchaseOrderRaiserUserId', 'Users'),
 ('No FK', 'DepartmentApprovalStage','RoleId',                    'Roles'),
 ('No FK', 'DepartmentApprovalStage','UserId',                    'Users'),
 ('No FK', 'DepartmentApprovalStage','DepartmentId',              'Department'),
 ('No FK', 'TransferApprovalAction', 'RoleId',                    'Roles'),
 ('No FK', 'TransferApprovalAction', 'ApproverUserId',            'Users'),
 ('No FK', 'DisposalApprovalAction', 'RoleId',                    'Roles'),
 ('No FK', 'DisposalApprovalAction', 'ApproverUserId',            'Users'),
 ('No FK', 'PurchaseApprovalAction', 'RoleId',                    'Roles'),
 ('No FK', 'PurchaseApprovalAction', 'ApproverUserId',            'Users'),
 ('No FK', 'Asset',                  'CurrentCustodianId',        'Users'),
 ('No FK', 'AssetAssignment',        'ToUserId',                  'Users'),
 ('No FK', 'AssetAssignment',        'HandedOverById',            'Users'),
 ('No FK', 'AssetAssignment',        'ReceivedById',              'Users'),
 ('No FK', 'AssetCustodyEvent',      'FromUserId',                'Users'),
 ('No FK', 'AssetCustodyEvent',      'ToUserId',                  'Users'),
 ('No FK', 'AssetCustodyEvent',      'ApprovedById',              'Users'),
 ('No FK', 'AssetCustodyEvent',      'FromDepartmentId',          'Department'),
 ('No FK', 'AssetCustodyEvent',      'ToDepartmentId',            'Department'),
 ('No FK', 'AssetTransfer',          'FromUserId',                'Users'),
 ('No FK', 'AssetTransfer',          'ToUserId',                  'Users'),
 ('No FK', 'AssetTransfer',          'ApprovedById',              'Users'),
 ('No FK', 'AssetTransfer',          'RequestedById',             'Users'),
 ('No FK', 'DisposalRecord',         'DisposalApprovedById',      'Users'),
 ('No FK', 'DisposalRecord',         'RequestedById',             'Users'),
 ('No FK', 'PurchaseRequest',        'RequestedById',             'Users'),
 ('No FK', 'PurchaseRequest',        'ApprovedById',              'Users'),
 ('No FK', 'PurchaseRequest',        'OrderByUserId',             'Users'),
 ('No FK', 'AssetRequest',           'RequestedById',             'Users'),
 ('No FK', 'AssetRequest',           'ReviewedById',              'Users'),
 ('No FK', 'AssetRequest',           'FulfilledAssetId',          'Asset'),
 ('No FK', 'AssetDocument',          'UploadedById',              'Users'),
 ('No FK', 'AssetIncident',          'ReportedById',              'Users'),
 ('No FK', 'AssetMaintenanceRecord', 'ReportedById',              'Users'),
 ('No FK', 'AssetReceiving',         'ReceivedById',              'Users'),
 ('No FK', 'AssetReturn',            'ReturnedById',              'Users'),
 ('No FK', 'AssetReturn',            'ReceivedById',              'Users'),
 ('No FK', 'AssetDocumentRequirement','DocumentId',               'AssetDocument'),
 ('No FK', 'Notification',           'UserId',                    'Users'),
 ('No FK', 'UserInvitation',         'InvitedByUserId',           'Users'),
 ('No FK', 'UserInvitation',         'UsedByUserId',              'Users'),
 ('No FK', 'WebhookSubscription',    'CreatedByUserId',           'Users'),
 ('No FK', 'AssetMovement',          'AssetId',                   'Asset'),
 ('No FK', 'AssetMovement',          'FromDepartmentId',          'Department'),
 ('No FK', 'AssetMovement',          'ToDepartmentId',            'Department'),
 ('No FK', 'AssetMovement',          'FromCustodianUserId',       'Users'),
 ('No FK', 'AssetMovement',          'ToCustodianUserId',         'Users'),
 ('No FK', 'AssetMovement',          'PerformedByUserId',         'Users');

-- ---------------------------------------------------------------- org-scoped foreign keys (row exists, but same company?)
INSERT #Checks (Category, SourceTable, SourceColumn, TargetTable) VALUES
 ('FK', 'Asset',                   'CategoryId',            'AssetCategory'),
 ('FK', 'Asset',                   'AssetTypeId',           'AssetType'),
 ('FK', 'Asset',                   'AssetSubTypeId',        'AssetSubType'),
 ('FK', 'Asset',                   'SupplierId',            'Supplier'),
 ('FK', 'Asset',                   'DepartmentId',          'Department'),
 ('FK', 'AssetType',               'AssetCategoryId',       'AssetCategory'),
 ('FK', 'AssetSubType',            'AssetTypeId',           'AssetType'),
 ('FK', 'Department',              'ParentDepartmentId',    'Department'),
 ('FK', 'Department',              'ClassTeacherUserId',    'Users'),
 ('FK', 'Department',              'RoomCustodianUserId',   'Users'),
 ('FK', 'Department',              'PrepHeadUserId',        'Users'),
 ('FK', 'Department',              'SeniorHeadUserId',      'Users'),
 ('FK', 'RolePermission',          'RoleId',                'Roles'),
 ('FK', 'RoleTemplate',            'SourceRoleId',          'Roles'),
 ('FK', 'UserInvitation',          'RoleId',                'Roles'),
 ('FK', 'UserInvitation',          'DepartmentId',          'Department'),
 ('FK', 'AssetAssignment',         'AssetId',               'Asset'),
 ('FK', 'AssetAssignment',         'ToDepartmentId',        'Department'),
 ('FK', 'AssetCustodyEvent',       'AssetId',               'Asset'),
 ('FK', 'AssetDocument',           'AssetId',               'Asset'),
 ('FK', 'AssetDocument',           'RequirementId',         'AssetDocumentRequirement'),
 ('FK', 'AssetDocumentRequirement','AssetId',               'Asset'),
 ('FK', 'AssetIncident',           'AssetId',               'Asset'),
 ('FK', 'AssetMaintenanceRecord',  'AssetId',               'Asset'),
 ('FK', 'AssetReceiving',          'AssetId',               'Asset'),
 ('FK', 'AssetReceiving',          'PurchaseRecordId',      'PurchaseRecord'),
 ('FK', 'AssetReturn',             'AssetId',               'Asset'),
 ('FK', 'AssetTransfer',           'AssetId',               'Asset'),
 ('FK', 'AssetTransfer',           'FromDepartmentId',      'Department'),
 ('FK', 'AssetTransfer',           'ToDepartmentId',        'Department'),
 ('FK', 'DisposalRecord',          'AssetId',               'Asset'),
 ('FK', 'DepreciationRecord',      'AssetId',               'Asset'),
 ('FK', 'InsurancePolicy',         'AssetId',               'Asset'),
 ('FK', 'InsuranceClaim',          'AssetId',               'Asset'),
 ('FK', 'InsuranceClaim',          'IncidentId',            'AssetIncident'),
 ('FK', 'AssetRequest',            'DepartmentId',          'Department'),
 ('FK', 'AssetRequest',            'CategoryId',            'AssetCategory'),
 ('FK', 'AssetRequest',            'RequestedAssetId',      'Asset'),
 ('FK', 'PurchaseRequest',         'DepartmentId',          'Department'),
 ('FK', 'PurchaseRequest',         'TargetAssetId',         'Asset'),
 ('FK', 'PurchaseRequest',         'TargetDepartmentId',    'Department'),   -- migration 110 (skipped while the column is absent)
 ('FK', 'PurchaseRequestLine',     'PurchaseRequestId',     'PurchaseRequest'),
 ('FK', 'PurchaseRequestLine',     'TargetAssetId',         'Asset'),        -- migration 108 (skipped while the column is absent)
 ('FK', 'PurchaseRecord',          'PurchaseRequestId',     'PurchaseRequest'),
 ('FK', 'PurchaseRecord',          'SupplierId',            'Supplier'),
 ('FK', 'TransferApprovalAction',  'AssetTransferId',       'AssetTransfer'),
 ('FK', 'DisposalApprovalAction',  'DisposalRecordId',      'DisposalRecord'),
 ('FK', 'PurchaseApprovalAction',  'PurchaseRequestId',     'PurchaseRequest'),
 ('FK', 'SupplierCatalogItem',     'SupplierId',            'Supplier'),
 ('FK', 'SupplierCatalogItem',     'AssetCategoryId',       'AssetCategory'),
 ('FK', 'SupplierCatalogItem',     'AssetTypeId',           'AssetType'),
 ('FK', 'SupplierCatalogItem',     'TaggedAssetId',         'Asset'),
 ('FK', 'WebhookDelivery',         'WebhookSubscriptionId', 'WebhookSubscription'),
 ('FK', 'OrganizationLicenseHistory','OrganizationLicenseId','OrganizationLicense');

-- ---------------------------------------------------------------- run every check (SELECT only; results into #Refs)
DECLARE @CheckId int, @Category nvarchar(40), @SourceTable sysname, @SourceColumn sysname,
        @TargetTable sysname, @IsCsv bit, @ExtraFilter nvarchar(400), @Sql nvarchar(max),
        @RefExpr nvarchar(400), @From nvarchar(400), @Join nvarchar(400);

DECLARE check_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT CheckId, Category, SourceTable, SourceColumn, TargetTable, IsCsv, ExtraFilter FROM #Checks ORDER BY CheckId;
OPEN check_cursor;
FETCH NEXT FROM check_cursor INTO @CheckId, @Category, @SourceTable, @SourceColumn, @TargetTable, @IsCsv, @ExtraFilter;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'dbo.' + QUOTENAME(@SourceTable), N'U') IS NULL
       OR COL_LENGTH(N'dbo.' + QUOTENAME(@SourceTable), @SourceColumn) IS NULL
       OR COL_LENGTH(N'dbo.' + QUOTENAME(@SourceTable), N'OrganizationId') IS NULL
       OR OBJECT_ID(N'dbo.' + QUOTENAME(@TargetTable), N'U') IS NULL
       OR COL_LENGTH(N'dbo.' + QUOTENAME(@TargetTable), N'OrganizationId') IS NULL
    BEGIN
        INSERT #Findings (CheckId, Status) VALUES (@CheckId, N'skipped (table/column not on this DB)');
    END
    ELSE
    BEGIN
        IF @IsCsv = 1
        BEGIN
            SET @From = N'dbo.' + QUOTENAME(@SourceTable) + N' s CROSS APPLY STRING_SPLIT(s.' + QUOTENAME(@SourceColumn) + N', N'','') v';
            SET @RefExpr = N'LTRIM(RTRIM(v.value))';
        END
        ELSE
        BEGIN
            SET @From = N'dbo.' + QUOTENAME(@SourceTable) + N' s';
            SET @RefExpr = N'LTRIM(RTRIM(CAST(s.' + QUOTENAME(@SourceColumn) + N' AS nvarchar(450))))';
        END;

        -- Users.Id is a string; every other target has an int Id.
        SET @Join = CASE WHEN @TargetTable = N'Users'
                         THEN N't.Id = r.RefId'
                         ELSE N't.Id = TRY_CAST(r.RefId AS int)' END;

        SET @Sql = N'
;WITH r AS (
    SELECT CAST(s.Id AS nvarchar(450)) AS SrcId, s.OrganizationId AS SrcOrg, ' + @RefExpr + N' AS RefId
    FROM ' + @From + N'
    WHERE s.OrganizationId IS NOT NULL' + ISNULL(N' AND ' + @ExtraFilter, N'') + N'
)
INSERT #Refs (CheckId, SrcId, SrcOrg, RefId, RefOrg, Found)
SELECT @CheckId, r.SrcId, r.SrcOrg, r.RefId, t.OrganizationId, CASE WHEN t.Id IS NULL THEN 0 ELSE 1 END
FROM r
LEFT JOIN dbo.' + QUOTENAME(@TargetTable) + N' t ON ' + @Join + N'
WHERE r.RefId IS NOT NULL AND r.RefId <> N''''
  AND (t.Id IS NULL OR (t.OrganizationId IS NOT NULL AND t.OrganizationId <> r.SrcOrg));

INSERT #Findings (CheckId, Status, RefsChecked)
SELECT @CheckId, N''checked'', COUNT(*)
FROM ' + @From + N'
WHERE s.OrganizationId IS NOT NULL' + ISNULL(N' AND ' + @ExtraFilter, N'') + N'
  AND ' + @RefExpr + N' IS NOT NULL AND ' + @RefExpr + N' <> N'''';';

        BEGIN TRY
            EXEC sp_executesql @Sql, N'@CheckId int', @CheckId = @CheckId;
        END TRY
        BEGIN CATCH
            INSERT #Findings (CheckId, Status) VALUES (@CheckId, LEFT(N'error: ' + ERROR_MESSAGE(), 60));
        END CATCH;
    END;

    FETCH NEXT FROM check_cursor INTO @CheckId, @Category, @SourceTable, @SourceColumn, @TargetTable, @IsCsv, @ExtraFilter;
END;
CLOSE check_cursor;
DEALLOCATE check_cursor;

UPDATE f SET
    CrossOrgRefs = ISNULL(x.CrossOrgRefs, 0),
    CrossOrgRows = ISNULL(x.CrossOrgRows, 0),
    DanglingRefs = ISNULL(x.DanglingRefs, 0)
FROM #Findings f
OUTER APPLY (
    SELECT SUM(CASE WHEN r.Found = 1 THEN 1 ELSE 0 END) AS CrossOrgRefs,
           COUNT(DISTINCT CASE WHEN r.Found = 1 THEN r.SrcId END) AS CrossOrgRows,
           SUM(CASE WHEN r.Found = 0 THEN 1 ELSE 0 END) AS DanglingRefs
    FROM #Refs r WHERE r.CheckId = f.CheckId
) x;

-- ---------------------------------------------------------------- 1) per table/column summary
SELECT
    c.Category,
    c.SourceTable,
    c.SourceColumn + CASE WHEN c.ExtraFilter IS NULL THEN N''
                          WHEN c.ExtraFilter LIKE N'%StageRoleIds%' THEN N' (Approval.Process.*.StageRoleIds)'
                          ELSE N' (Approval.Process.*.StageUserIds)' END AS SourceColumn,
    c.TargetTable,
    f.Status,
    f.RefsChecked,
    f.CrossOrgRefs,
    f.CrossOrgRows,
    f.DanglingRefs,
    (SELECT STRING_AGG(z.Sample, N'; ') FROM (
        SELECT TOP (8) c.SourceTable + N'#' + r.SrcId + N'(org ' + CAST(r.SrcOrg AS nvarchar(10)) + N')->'
               + r.RefId + N'(org ' + CAST(r.RefOrg AS nvarchar(10)) + N')' AS Sample
        FROM #Refs r WHERE r.CheckId = c.CheckId AND r.Found = 1 ORDER BY r.SrcOrg, r.RefId, r.SrcId) z) AS SampleCrossOrg,
    (SELECT STRING_AGG(z.Sample, N'; ') FROM (
        SELECT TOP (8) c.SourceTable + N'#' + r.SrcId + N'(org ' + CAST(r.SrcOrg AS nvarchar(10)) + N')->' + r.RefId AS Sample
        FROM #Refs r WHERE r.CheckId = c.CheckId AND r.Found = 0 ORDER BY r.SrcOrg, r.RefId, r.SrcId) z) AS SampleDangling
FROM #Checks c
JOIN #Findings f ON f.CheckId = c.CheckId
ORDER BY CASE WHEN f.CrossOrgRefs > 0 THEN 0 WHEN f.DanglingRefs > 0 THEN 1 ELSE 2 END,
         f.CrossOrgRefs DESC, f.DanglingRefs DESC, c.CheckId;

-- ---------------------------------------------------------------- 2) cross-org breakdown by source org -> referenced org
SELECT
    c.SourceTable,
    c.SourceColumn,
    r.SrcOrg AS SourceOrgId,
    so.Name  AS SourceOrg,
    r.RefOrg AS ReferencedOrgId,
    ro.Name  AS ReferencedOrg,
    COUNT(*) AS CrossOrgRefs,
    COUNT(DISTINCT r.SrcId) AS SourceRows,
    (SELECT STRING_AGG(d.RefId, N',') FROM (
        SELECT DISTINCT TOP (10) r2.RefId FROM #Refs r2
        WHERE r2.CheckId = c.CheckId AND r2.SrcOrg = r.SrcOrg AND r2.RefOrg = r.RefOrg AND r2.Found = 1
        ORDER BY r2.RefId) d) AS ReferencedIds
FROM #Refs r
JOIN #Checks c ON c.CheckId = r.CheckId
LEFT JOIN dbo.Organization so ON so.Id = r.SrcOrg
LEFT JOIN dbo.Organization ro ON ro.Id = r.RefOrg
WHERE r.Found = 1
GROUP BY c.CheckId, c.SourceTable, c.SourceColumn, r.SrcOrg, so.Name, r.RefOrg, ro.Name
ORDER BY COUNT(*) DESC;

DROP TABLE #Refs;
DROP TABLE #Findings;
DROP TABLE #Checks;
