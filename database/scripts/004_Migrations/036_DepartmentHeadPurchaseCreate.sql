-- Grant Purchases.Create to Department Head; remove from Staff for existing orgs
DECLARE @permCreatePurchases INT = (SELECT TOP 1 [Id] FROM [Permission] WHERE [Code] = N'Purchases.Create' ORDER BY [Id]);

IF @permCreatePurchases IS NOT NULL
BEGIN
    INSERT INTO [RolePermission] ([RoleId], [PermissionId], [OrganizationId])
    SELECT r.[Id], @permCreatePurchases, r.[OrganizationId]
    FROM [Roles] r
    WHERE r.[Name] = N'Department Head'
      AND NOT EXISTS (
          SELECT 1 FROM [RolePermission] rp
          WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = @permCreatePurchases
            AND ((rp.[OrganizationId] IS NULL AND r.[OrganizationId] IS NULL) OR rp.[OrganizationId] = r.[OrganizationId]));

    DELETE rp
    FROM [RolePermission] rp
    INNER JOIN [Roles] r ON r.[Id] = rp.[RoleId]
    WHERE r.[Name] = N'Staff'
      AND rp.[PermissionId] = @permCreatePurchases
      AND ((rp.[OrganizationId] IS NULL AND r.[OrganizationId] IS NULL) OR rp.[OrganizationId] = r.[OrganizationId]);
END
GO
