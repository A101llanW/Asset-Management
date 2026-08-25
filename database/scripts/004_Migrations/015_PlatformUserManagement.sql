-- Platform user management permissions
DECLARE @permNow DATETIME = GETUTCDATE();
IF NOT EXISTS (SELECT 1 FROM [Permission] WHERE [Code] = N'Platform.Users.View')
BEGIN
    INSERT INTO [Permission] ([Name],[Code],[Module],[Description],[CreatedAt],[IsActive]) VALUES
    (N'View Platform Users', N'Platform.Users.View', N'Platform', N'View system and company users across all organizations', @permNow, 1),
    (N'Manage Platform Users', N'Platform.Users.Manage', N'Platform', N'Create and manage system and company users', @permNow, 1);
END
GO

DECLARE @platformRoleId INT = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Platform Admin' ORDER BY [Id]);
IF @platformRoleId IS NOT NULL
BEGIN
    INSERT INTO [RolePermission] ([RoleId],[PermissionId],[OrganizationId])
    SELECT @platformRoleId, p.[Id], NULL
    FROM [Permission] p
    WHERE p.[Code] IN (N'Platform.Users.View', N'Platform.Users.Manage')
      AND NOT EXISTS (
          SELECT 1 FROM [RolePermission] rp
          WHERE rp.[RoleId] = @platformRoleId AND rp.[PermissionId] = p.[Id]
      );
END
GO
