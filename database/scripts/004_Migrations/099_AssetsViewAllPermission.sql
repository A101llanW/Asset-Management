-- 099 (role-access-scope): organization-wide asset scope becomes a permission (Assets.ViewAll) instead of the
-- hardcoded "Company Admin" role-name check in DepartmentScopeService / BaseController / MetricsService.
-- What it does (additive only, no existing row is changed or deleted):
--   1. Adds Permission Assets.ViewAll (Module Assets) when missing.
--   2. Grants it to every system "Company Admin" role (they already pass every tenant permission through the
--      AuthorizationService bypass, so their access is exactly unchanged; the explicit row documents it and
--      keeps the role matrix truthful). Custom roles get it only when an administrator ticks it.
--   3. Depreciation.View / Depreciation.Manage need no data change (they already exist; the role editor now shows them).
-- Idempotent: every insert is guarded; re-running is a no-op.
-- Rollback (custom roles that were given Assets.ViewAll afterwards lose it, i.e. fall back to department scope):
--   DELETE rp FROM [dbo].[RolePermission] rp JOIN [dbo].[Permission] p ON p.[Id] = rp.[PermissionId] WHERE p.[Code] = N'Assets.ViewAll';
--   DELETE FROM [dbo].[Permission] WHERE [Code] = N'Assets.ViewAll';
--   DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'099_AssetsViewAllPermission.sql';
--   (and redeploy the previous build, which still uses the role-name bypass)
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @now DATETIME = GETUTCDATE();
IF NOT EXISTS (SELECT 1 FROM [dbo].[Permission] WHERE [Code] = N'Assets.ViewAll')
BEGIN
    INSERT INTO [dbo].[Permission] ([Name],[Code],[Module],[Description],[CreatedAt],[IsActive])
    VALUES (N'View All Assets (organization-wide)', N'Assets.ViewAll', N'Assets',
            N'Sees assets, departments, lists and dashboard metrics across every department, not only the user''s own department', @now, 1);
END

DECLARE @perm INT = (SELECT TOP 1 [Id] FROM [dbo].[Permission] WHERE [Code] = N'Assets.ViewAll' ORDER BY [Id]);

INSERT INTO [dbo].[RolePermission] ([RoleId], [PermissionId], [OrganizationId])
SELECT r.[Id], @perm, r.[OrganizationId]
FROM [dbo].[Roles] r
WHERE r.[IsSystemRole] = 1
  AND r.[Name] = N'Company Admin'
  AND NOT EXISTS (SELECT 1 FROM [dbo].[RolePermission] rp WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = @perm);

IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'099_AssetsViewAllPermission.sql')
BEGIN
    INSERT INTO [dbo].[SchemaMigrationHistory] ([ScriptName], [Checksum], [AppliedAt], [AppliedBy])
    VALUES (N'099_AssetsViewAllPermission.sql', NULL, SYSUTCDATETIME(), SUSER_SNAME());
END

COMMIT TRANSACTION;

-- Verify
SELECT
    (SELECT COUNT(*) FROM [dbo].[Permission] WHERE [Code] = N'Assets.ViewAll') AS PermissionRows,
    (SELECT COUNT(*) FROM [dbo].[Roles] WHERE [IsSystemRole] = 1 AND [Name] = N'Company Admin') AS CompanyAdminRoles,
    (SELECT COUNT(*) FROM [dbo].[RolePermission] rp JOIN [dbo].[Permission] p ON p.[Id] = rp.[PermissionId]
        JOIN [dbo].[Roles] r ON r.[Id] = rp.[RoleId]
     WHERE p.[Code] = N'Assets.ViewAll' AND r.[IsSystemRole] = 1 AND r.[Name] = N'Company Admin') AS CompanyAdminGrants,
    (SELECT COUNT(*) FROM [dbo].[RolePermission] rp JOIN [dbo].[Permission] p ON p.[Id] = rp.[PermissionId]
        JOIN [dbo].[Roles] r ON r.[Id] = rp.[RoleId]
     WHERE p.[Code] = N'Assets.ViewAll' AND NOT (r.[IsSystemRole] = 1 AND r.[Name] = N'Company Admin')) AS OtherRoleGrants;
GO
