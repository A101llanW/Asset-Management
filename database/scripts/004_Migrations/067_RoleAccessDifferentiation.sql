-- Differentiate procurement vs department-head access: dept heads submit requisitions;
-- procurement officers approve and manage POs/suppliers. Staff should not see procurement lists.
DECLARE @permPurchasesView INT = (SELECT TOP 1 [Id] FROM [Permission] WHERE [Code] = N'Purchases.View' ORDER BY [Id]);
DECLARE @permPurchasesApprove INT = (SELECT TOP 1 [Id] FROM [Permission] WHERE [Code] = N'Purchases.Approve' ORDER BY [Id]);

IF @permPurchasesApprove IS NOT NULL
BEGIN
    DELETE rp
    FROM [RolePermission] rp
    INNER JOIN [Roles] r ON r.[Id] = rp.[RoleId]
    WHERE r.[Name] = N'Department Head'
      AND rp.[PermissionId] = @permPurchasesApprove
      AND ((rp.[OrganizationId] IS NULL AND r.[OrganizationId] IS NULL) OR rp.[OrganizationId] = r.[OrganizationId]);
END

IF @permPurchasesView IS NOT NULL
BEGIN
    DELETE rp
    FROM [RolePermission] rp
    INNER JOIN [Roles] r ON r.[Id] = rp.[RoleId]
    WHERE r.[Name] = N'Department Head'
      AND rp.[PermissionId] = @permPurchasesView
      AND ((rp.[OrganizationId] IS NULL AND r.[OrganizationId] IS NULL) OR rp.[OrganizationId] = r.[OrganizationId]);

    DELETE rp
    FROM [RolePermission] rp
    INNER JOIN [Roles] r ON r.[Id] = rp.[RoleId]
    WHERE r.[Name] = N'Staff'
      AND rp.[PermissionId] = @permPurchasesView
      AND ((rp.[OrganizationId] IS NULL AND r.[OrganizationId] IS NULL) OR rp.[OrganizationId] = r.[OrganizationId]);
END
GO
