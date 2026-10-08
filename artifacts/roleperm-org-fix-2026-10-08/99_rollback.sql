/* roleperm-org-fix-2026-10-08 / 99_rollback.sql
   Restores RolePermission.OrganizationId from dbo.Bak_RolePermOrg_20261008 by Id (undoes 02_retag.sql).
   COMMIT is a required sqlcmd variable (0 = dry run + ROLLBACK, 1 = COMMIT).
   Usage: sqlcmd -S .\SQLEXPRESS -E -d AssetManagementModuleDb -b -i 99_rollback.sql -v COMMIT=0 */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @commit INT = $(COMMIT);
IF @commit NOT IN (0, 1)
    THROW 50020, 'COMMIT must be 0 or 1.', 1;
IF OBJECT_ID(N'dbo.Bak_RolePermOrg_20261008', N'U') IS NULL
    THROW 50021, 'dbo.Bak_RolePermOrg_20261008 is missing; nothing to restore from.', 1;

BEGIN TRANSACTION;

DECLARE @missing INT = (SELECT COUNT(*) FROM dbo.Bak_RolePermOrg_20261008 b
                        WHERE NOT EXISTS (SELECT 1 FROM dbo.RolePermission rp WHERE rp.[Id] = b.[Id]
                                          AND rp.[RoleId] = b.[RoleId] AND rp.[PermissionId] = b.[PermissionId]));
SELECT COUNT(*) AS [backup_rows], @missing AS [rows_missing_or_changed_role_perm] FROM dbo.Bak_RolePermOrg_20261008;

UPDATE rp
SET rp.[OrganizationId] = b.[OrganizationId]
FROM dbo.RolePermission rp
INNER JOIN dbo.Bak_RolePermOrg_20261008 b ON b.[Id] = rp.[Id]
WHERE rp.[RoleId] = b.[RoleId]
  AND rp.[PermissionId] = b.[PermissionId]
  AND ISNULL(rp.[OrganizationId], -1) <> ISNULL(b.[OrganizationId], -1);

DECLARE @restored INT = @@ROWCOUNT;
DECLARE @notMatching INT = (SELECT COUNT(*) FROM dbo.Bak_RolePermOrg_20261008 b
                            INNER JOIN dbo.RolePermission rp ON rp.[Id] = b.[Id]
                            WHERE ISNULL(rp.[OrganizationId], -1) <> ISNULL(b.[OrganizationId], -1));
SELECT @restored AS [restored], @notMatching AS [still_differs_from_backup];

IF @restored > 31 OR @notMatching <> 0
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50022, 'Rollback post-check failed; rolled back.', 1;
END

IF @commit = 1
BEGIN
    COMMIT TRANSACTION;
    PRINT 'COMMITTED: restored ' + CAST(@restored AS VARCHAR(10)) + ' row(s) from backup.';
END
ELSE
BEGIN
    ROLLBACK TRANSACTION;
    PRINT 'DRY RUN (COMMIT=0): ' + CAST(@restored AS VARCHAR(10)) + ' row(s) would be restored; rolled back.';
END
