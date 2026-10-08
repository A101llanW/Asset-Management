/* roleperm-org-fix-2026-10-08 / 02_retag.sql
   Re-tags the 31 backed-up RolePermission rows: OrganizationId := the role's OrganizationId.
   One transaction. COMMIT is a required sqlcmd variable:
     COMMIT=0  dry run: does the update, prints before/after, then ROLLBACK
     COMMIT=1  same, then COMMIT (only if every assertion passed)
   Usage: sqlcmd -S .\SQLEXPRESS -E -d AssetManagementModuleDb -b -i 02_retag.sql -v COMMIT=0
   Requires 01_backup.sql first (dbo.Bak_RolePermOrg_20261008 with 31 rows).
   Duplicate guard: a row whose target (OrganizationId, RoleId, PermissionId) already exists on
   another row is SKIPPED (left as is and listed). RolePermission has no IsActive column, so
   deactivation is not possible. The unique index IX_RolePermission_Role_Permission on
   (RoleId, PermissionId) means such a duplicate cannot exist today; the guard is belt-and-braces.
   Platform Admin (role 1, Roles.OrganizationId NULL) rows are never touched. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @commit INT = $(COMMIT);
IF @commit NOT IN (0, 1)
    THROW 50010, 'COMMIT must be 0 or 1.', 1;
IF OBJECT_ID(N'dbo.Bak_RolePermOrg_20261008', N'U') IS NULL
    THROW 50011, 'dbo.Bak_RolePermOrg_20261008 is missing; run 01_backup.sql first.', 1;

BEGIN TRANSACTION;

-- before
SELECT 'before' AS [phase], rp.[OrganizationId] AS [rp_org], r.[OrganizationId] AS [role_org], COUNT(*) AS [rows]
FROM dbo.RolePermission rp
INNER JOIN dbo.Roles r ON r.[Id] = rp.[RoleId]
WHERE ISNULL(rp.[OrganizationId], -1) <> ISNULL(r.[OrganizationId], -1)
GROUP BY rp.[OrganizationId], r.[OrganizationId]
ORDER BY 2, 3;

-- candidates: backed-up ids that are still mis-tagged exactly as backed up
DECLARE @cand TABLE ([Id] INT PRIMARY KEY, [RoleId] INT, [PermissionId] INT, [TargetOrg] INT, [IsDuplicate] BIT);
INSERT INTO @cand ([Id],[RoleId],[PermissionId],[TargetOrg],[IsDuplicate])
SELECT rp.[Id], rp.[RoleId], rp.[PermissionId], r.[OrganizationId],
       CASE WHEN EXISTS (SELECT 1 FROM dbo.RolePermission d
                         WHERE d.[Id] <> rp.[Id]
                           AND d.[OrganizationId] = r.[OrganizationId]
                           AND d.[RoleId] = rp.[RoleId]
                           AND d.[PermissionId] = rp.[PermissionId]) THEN 1 ELSE 0 END
FROM dbo.RolePermission rp WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.Bak_RolePermOrg_20261008 b ON b.[Id] = rp.[Id]
INNER JOIN dbo.Roles r ON r.[Id] = rp.[RoleId]
WHERE rp.[OrganizationId] = 1
  AND rp.[RoleId] = b.[RoleId]
  AND rp.[PermissionId] = b.[PermissionId]
  AND r.[OrganizationId] = b.[RoleOrganizationId]
  AND r.[OrganizationId] IN (2, 9, 10);

DECLARE @candidates INT = (SELECT COUNT(*) FROM @cand);
DECLARE @dups INT = (SELECT COUNT(*) FROM @cand WHERE [IsDuplicate] = 1);

SELECT 'candidates' AS [check], [TargetOrg] AS [target_org], COUNT(*) AS [rows], SUM(CAST([IsDuplicate] AS INT)) AS [would_duplicate]
FROM @cand GROUP BY [TargetOrg] ORDER BY [TargetOrg];

IF @dups > 0
    SELECT 'SKIPPED (would duplicate)' AS [note], c.* FROM @cand c WHERE c.[IsDuplicate] = 1;

IF @candidates <> 31
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50012, 'Expected exactly 31 candidate rows; rolled back.', 1;
END

UPDATE rp
SET rp.[OrganizationId] = c.[TargetOrg]
FROM dbo.RolePermission rp
INNER JOIN @cand c ON c.[Id] = rp.[Id]
WHERE c.[IsDuplicate] = 0;

DECLARE @updated INT = @@ROWCOUNT;
SELECT @candidates AS [candidates], @dups AS [skipped_duplicates], @updated AS [updated];

IF @updated + @dups <> 31
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50013, 'updated + skipped <> 31; rolled back.', 1;
END

-- after: remaining mismatches among the 31 ids (must be 0 unless skipped as duplicates)
DECLARE @left INT = (SELECT COUNT(*) FROM dbo.RolePermission rp
                     INNER JOIN dbo.Bak_RolePermOrg_20261008 b ON b.[Id] = rp.[Id]
                     INNER JOIN dbo.Roles r ON r.[Id] = rp.[RoleId]
                     WHERE ISNULL(rp.[OrganizationId], -1) <> ISNULL(r.[OrganizationId], -1));
-- after: any duplicate (OrganizationId, RoleId, PermissionId) anywhere (must be 0)
DECLARE @dupAfter INT = (SELECT COUNT(*) FROM (SELECT [OrganizationId],[RoleId],[PermissionId]
                         FROM dbo.RolePermission GROUP BY [OrganizationId],[RoleId],[PermissionId]
                         HAVING COUNT(*) > 1) x);
-- after: tenant-role mismatches anywhere (roles with an org; Platform Admin excluded) (must be 0)
DECLARE @tenantLeft INT = (SELECT COUNT(*) FROM dbo.RolePermission rp
                           INNER JOIN dbo.Roles r ON r.[Id] = rp.[RoleId]
                           WHERE r.[OrganizationId] IS NOT NULL
                             AND ISNULL(rp.[OrganizationId], -1) <> r.[OrganizationId]);

SELECT 'after' AS [phase], rp.[OrganizationId] AS [rp_org], r.[OrganizationId] AS [role_org], COUNT(*) AS [rows]
FROM dbo.RolePermission rp
INNER JOIN dbo.Roles r ON r.[Id] = rp.[RoleId]
WHERE ISNULL(rp.[OrganizationId], -1) <> ISNULL(r.[OrganizationId], -1)
GROUP BY rp.[OrganizationId], r.[OrganizationId]
ORDER BY 2, 3;

SELECT 'after' AS [phase], b.[RoleOrganizationId] AS [role_org], rp.[OrganizationId] AS [rp_org_now], COUNT(*) AS [rows]
FROM dbo.RolePermission rp
INNER JOIN dbo.Bak_RolePermOrg_20261008 b ON b.[Id] = rp.[Id]
GROUP BY b.[RoleOrganizationId], rp.[OrganizationId]
ORDER BY 2;

SELECT @left AS [mismatch_left_in_31], @dupAfter AS [duplicate_triples], @tenantLeft AS [tenant_role_mismatches];

IF @left <> @dups OR @dupAfter <> 0 OR @tenantLeft <> @dups
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50014, 'Post-check failed; rolled back.', 1;
END

IF @commit = 1
BEGIN
    COMMIT TRANSACTION;
    PRINT 'COMMITTED: ' + CAST(@updated AS VARCHAR(10)) + ' row(s) re-tagged.';
END
ELSE
BEGIN
    ROLLBACK TRANSACTION;
    PRINT 'DRY RUN (COMMIT=0): ' + CAST(@updated AS VARCHAR(10)) + ' row(s) would be re-tagged; rolled back.';
END
