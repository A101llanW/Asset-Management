-- Backfill OrganizationId on RolePermission from parent Role; add document view/download permissions
DECLARE @defaultOrgId INT = (SELECT TOP 1 [Id] FROM [Organization] ORDER BY [Id]);

IF @defaultOrgId IS NOT NULL
BEGIN
    UPDATE rp
    SET rp.[OrganizationId] = r.[OrganizationId]
    FROM [RolePermission] rp
    INNER JOIN [Roles] r ON r.[Id] = rp.[RoleId]
    WHERE rp.[OrganizationId] IS NULL AND r.[OrganizationId] IS NOT NULL;

    UPDATE [RolePermission]
    SET [OrganizationId] = @defaultOrgId
    WHERE [OrganizationId] IS NULL;
END
GO

DECLARE @now DATETIME = GETUTCDATE();

IF NOT EXISTS (SELECT 1 FROM [Permission] WHERE [Code] = N'Documents.View')
BEGIN
    INSERT INTO [Permission] ([Name],[Code],[Module],[Description],[CreatedAt],[IsActive])
    VALUES (N'View Documents', N'Documents.View', N'Documents', N'Can view asset documents', @now, 1);
END
GO

DECLARE @now DATETIME = GETUTCDATE();

IF NOT EXISTS (SELECT 1 FROM [Permission] WHERE [Code] = N'Documents.Download')
BEGIN
    INSERT INTO [Permission] ([Name],[Code],[Module],[Description],[CreatedAt],[IsActive])
    VALUES (N'Download Documents', N'Documents.Download', N'Documents', N'Can download asset documents', @now, 1);
END
GO

DECLARE @companyAdminRoleId INT = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Company Admin' ORDER BY [Id]);
IF @companyAdminRoleId IS NOT NULL
BEGIN
    INSERT INTO [RolePermission] ([RoleId],[PermissionId],[OrganizationId])
    SELECT @companyAdminRoleId, p.[Id], r.[OrganizationId]
    FROM [Permission] p
    CROSS JOIN [Roles] r
    WHERE r.[Id] = @companyAdminRoleId
      AND p.[Code] IN (N'Documents.View', N'Documents.Download')
      AND NOT EXISTS (
          SELECT 1 FROM [RolePermission] rp
          WHERE rp.[RoleId] = @companyAdminRoleId AND rp.[PermissionId] = p.[Id]);
END
GO

INSERT INTO [RolePermission] ([RoleId],[PermissionId],[OrganizationId])
SELECT r.[Id], p.[Id], r.[OrganizationId]
FROM [Roles] r
CROSS JOIN [Permission] p
WHERE r.[Name] = N'Asset Manager'
  AND p.[Code] IN (N'Documents.View', N'Documents.Download')
  AND NOT EXISTS (
      SELECT 1 FROM [RolePermission] rp
      WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = p.[Id]);
GO

INSERT INTO [RolePermission] ([RoleId],[PermissionId],[OrganizationId])
SELECT r.[Id], p.[Id], r.[OrganizationId]
FROM [Roles] r
CROSS JOIN [Permission] p
WHERE r.[Name] = N'Staff'
  AND p.[Code] = N'Documents.View'
  AND NOT EXISTS (
      SELECT 1 FROM [RolePermission] rp
      WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = p.[Id]);
GO
