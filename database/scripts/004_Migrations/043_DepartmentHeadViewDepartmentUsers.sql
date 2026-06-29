-- Department heads can view users (employees) in their own department only
DECLARE @now DATETIME = GETUTCDATE();

IF NOT EXISTS (SELECT 1 FROM [Permission] WHERE [Code] = N'Users.ViewDepartment')
BEGIN
    INSERT INTO [Permission] ([Name],[Code],[Module],[Description],[CreatedAt],[IsActive])
    VALUES (
        N'View Department Users',
        N'Users.ViewDepartment',
        N'Users',
        N'Can view users registered in the signed-in user''s department',
        @now,
        1);
END

DECLARE @permViewDepartmentUsers INT = (SELECT TOP 1 [Id] FROM [Permission] WHERE [Code] = N'Users.ViewDepartment' ORDER BY [Id]);

IF @permViewDepartmentUsers IS NOT NULL
BEGIN
    INSERT INTO [RolePermission] ([RoleId], [PermissionId], [OrganizationId])
    SELECT r.[Id], @permViewDepartmentUsers, r.[OrganizationId]
    FROM [Roles] r
    WHERE r.[Name] = N'Department Head'
      AND NOT EXISTS (
          SELECT 1 FROM [RolePermission] rp
          WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = @permViewDepartmentUsers
            AND ((rp.[OrganizationId] IS NULL AND r.[OrganizationId] IS NULL) OR rp.[OrganizationId] = r.[OrganizationId]));
END
GO
