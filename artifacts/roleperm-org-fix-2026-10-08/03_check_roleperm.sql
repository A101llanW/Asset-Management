/* roleperm-org-fix-2026-10-08 / 03_check_roleperm.sql  -- READ-ONLY
   RolePermission integrity check: rows whose OrganizationId differs from their role's org,
   plus duplicate (OrganizationId, RoleId, PermissionId) triples. Expected after the fix: 0 / 0
   apart from the Platform Admin (role 1, Roles.OrganizationId NULL) rows, which are reported
   separately and intentionally left alone.
   The generic cross-org scan (artifacts/xorg-scan-2026-10-08/scan.sql, RolePermission.RoleId
   reference) reports the same rows as CROSS_ORG / NULL_OWNED.
   NOTE: copies of roles/permissions between orgs must go through the service mapping
   (OrganizationService.EnsureTenantRoles), never raw id copies.
   Usage: sqlcmd -S .\SQLEXPRESS -E -d AssetManagementModuleDb -W -i 03_check_roleperm.sql */
SET NOCOUNT ON;

SELECT
    SUM(CASE WHEN r.[OrganizationId] IS NOT NULL AND ISNULL(rp.[OrganizationId], -1) <> r.[OrganizationId] THEN 1 ELSE 0 END) AS [tenant_role_mismatches],
    SUM(CASE WHEN r.[OrganizationId] IS NULL THEN 1 ELSE 0 END) AS [platform_role_rows_left_alone],
    (SELECT COUNT(*) FROM (SELECT 1 AS x FROM dbo.RolePermission GROUP BY [OrganizationId],[RoleId],[PermissionId] HAVING COUNT(*) > 1) d) AS [duplicate_triples]
FROM dbo.RolePermission rp
INNER JOIN dbo.Roles r ON r.[Id] = rp.[RoleId];

SELECT rp.[Id], rp.[RoleId], r.[Name] AS [RoleName], rp.[PermissionId], rp.[OrganizationId] AS [rp_org], r.[OrganizationId] AS [role_org]
FROM dbo.RolePermission rp
INNER JOIN dbo.Roles r ON r.[Id] = rp.[RoleId]
WHERE r.[OrganizationId] IS NOT NULL
  AND ISNULL(rp.[OrganizationId], -1) <> r.[OrganizationId]
ORDER BY r.[OrganizationId], rp.[RoleId], rp.[Id];
