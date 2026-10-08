/* roleperm-org-fix-2026-10-08 / 01_backup.sql
   Backs up the RolePermission rows that are tagged OrganizationId = 1 (Nanosoft) but point at
   a role owned by org 2, 9 or 10. Expected: 31 rows (org 2: 13, org 9: 9, org 10: 9).
   Platform Admin (role 1, Roles.OrganizationId NULL) rows are NOT touched.
   Safe to re-run: if the backup table already exists it is left as is and only re-verified.
   Usage: sqlcmd -S .\SQLEXPRESS -E -d AssetManagementModuleDb -b -i 01_backup.sql */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.Bak_RolePermOrg_20261008', N'U') IS NULL
BEGIN
    SELECT rp.[Id], rp.[RoleId], rp.[PermissionId], rp.[OrganizationId],
           r.[OrganizationId] AS [RoleOrganizationId],
           r.[Name]           AS [RoleName],
           p.[Code]           AS [PermissionCode],
           SYSUTCDATETIME()   AS [BackedUpAtUtc]
    INTO dbo.Bak_RolePermOrg_20261008
    FROM dbo.RolePermission rp
    INNER JOIN dbo.Roles r      ON r.[Id] = rp.[RoleId]
    INNER JOIN dbo.Permission p ON p.[Id] = rp.[PermissionId]
    WHERE rp.[OrganizationId] = 1
      AND r.[OrganizationId] IN (2, 9, 10);

    ALTER TABLE dbo.Bak_RolePermOrg_20261008 ADD CONSTRAINT PK_Bak_RolePermOrg_20261008 PRIMARY KEY ([Id]);
    PRINT 'Created dbo.Bak_RolePermOrg_20261008';
END
ELSE
    PRINT 'dbo.Bak_RolePermOrg_20261008 already exists; not recreated';

DECLARE @n INT = (SELECT COUNT(*) FROM dbo.Bak_RolePermOrg_20261008);
IF @n <> 31
    THROW 50001, 'Backup row count is not 31; stop and investigate before re-tagging.', 1;

SELECT [RoleOrganizationId], [RoleId], [RoleName], COUNT(*) AS [rows]
FROM dbo.Bak_RolePermOrg_20261008
GROUP BY [RoleOrganizationId], [RoleId], [RoleName]
ORDER BY [RoleOrganizationId], [RoleId];

SELECT COUNT(*) AS [backup_rows] FROM dbo.Bak_RolePermOrg_20261008;
