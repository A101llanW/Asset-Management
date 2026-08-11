-- Procurement officers need org-wide user visibility for requisition coordination.
DECLARE @permUsersView INT = (SELECT TOP 1 [Id] FROM [Permission] WHERE [Code] = N'Users.View' ORDER BY [Id]);

IF @permUsersView IS NOT NULL
BEGIN
    INSERT INTO [RolePermission] ([RoleId], [PermissionId], [OrganizationId])
    SELECT r.[Id], @permUsersView, r.[OrganizationId]
    FROM [Roles] r
    WHERE r.[Name] = N'Procurement Officer'
      AND NOT EXISTS (
          SELECT 1 FROM [RolePermission] rp
          WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = @permUsersView
            AND ((rp.[OrganizationId] IS NULL AND r.[OrganizationId] IS NULL) OR rp.[OrganizationId] = r.[OrganizationId]));
END
GO
