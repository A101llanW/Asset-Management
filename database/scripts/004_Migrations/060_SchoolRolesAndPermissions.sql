-- School roles: Facilities Manager and Procurement Manager; requisition-any-department permission.

IF NOT EXISTS (SELECT 1 FROM [Permission] WHERE [Code] = N'Purchases.CreateForAnyDepartment')
BEGIN
    INSERT INTO [Permission] ([Name], [Code], [Module], [Description], [CreatedAt], [IsActive])
    VALUES (N'Create Purchases For Any Department', N'Purchases.CreateForAnyDepartment', N'Purchases', N'Can submit requisitions for any leaf department', GETUTCDATE(), 1);
END
GO

DECLARE @now DATETIME = GETUTCDATE();
DECLARE @permCreateAnyDept INT = (SELECT TOP 1 [Id] FROM [Permission] WHERE [Code] = N'Purchases.CreateForAnyDepartment' ORDER BY [Id]);
DECLARE @permCreatePurchases INT = (SELECT TOP 1 [Id] FROM [Permission] WHERE [Code] = N'Purchases.Create' ORDER BY [Id]);
DECLARE @permViewPurchases INT = (SELECT TOP 1 [Id] FROM [Permission] WHERE [Code] = N'Purchases.View' ORDER BY [Id]);
DECLARE @permApprovePurchases INT = (SELECT TOP 1 [Id] FROM [Permission] WHERE [Code] = N'Purchases.Approve' ORDER BY [Id]);
DECLARE @permEditPurchases INT = (SELECT TOP 1 [Id] FROM [Permission] WHERE [Code] = N'Purchases.Edit' ORDER BY [Id]);
DECLARE @permViewAssets INT = (SELECT TOP 1 [Id] FROM [Permission] WHERE [Code] = N'Assets.View' ORDER BY [Id]);
DECLARE @permAssignAssets INT = (SELECT TOP 1 [Id] FROM [Permission] WHERE [Code] = N'Assets.Assign' ORDER BY [Id]);
DECLARE @permTransferAssets INT = (SELECT TOP 1 [Id] FROM [Permission] WHERE [Code] = N'Assets.Transfer' ORDER BY [Id]);
DECLARE @permReceiveAssets INT = (SELECT TOP 1 [Id] FROM [Permission] WHERE [Code] = N'Assets.Receive' ORDER BY [Id]);
DECLARE @permViewSuppliers INT = (SELECT TOP 1 [Id] FROM [Permission] WHERE [Code] = N'Suppliers.View' ORDER BY [Id]);
DECLARE @permCreateSuppliers INT = (SELECT TOP 1 [Id] FROM [Permission] WHERE [Code] = N'Suppliers.Create' ORDER BY [Id]);
DECLARE @permEditSuppliers INT = (SELECT TOP 1 [Id] FROM [Permission] WHERE [Code] = N'Suppliers.Edit' ORDER BY [Id]);

DECLARE @orgId INT;
DECLARE org_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT [Id] FROM [Organization] WHERE [IsActive] = 1;

OPEN org_cursor;
FETCH NEXT FROM org_cursor INTO @orgId;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [OrganizationId] = @orgId AND [Name] = N'Facilities Manager')
    BEGIN
        INSERT INTO [Roles] ([Name], [Description], [IsSystemRole], [OrganizationId], [CreatedAt], [IsActive])
        VALUES (N'Facilities Manager', N'Submits requisitions for class/admin departments; assigns and transfers assets after procurement.', 0, @orgId, @now, 1);
    END

    IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [OrganizationId] = @orgId AND [Name] = N'Procurement Manager')
    BEGIN
        INSERT INTO [Roles] ([Name], [Description], [IsSystemRole], [OrganizationId], [CreatedAt], [IsActive])
        VALUES (N'Procurement Manager', N'Approves requisitions, records POs, selects suppliers, and receives goods.', 0, @orgId, @now, 1);
    END

    DECLARE @facilitiesRoleId INT = (SELECT TOP 1 [Id] FROM [Roles] WHERE [OrganizationId] = @orgId AND [Name] = N'Facilities Manager' ORDER BY [Id]);
    DECLARE @procurementManagerRoleId INT = (SELECT TOP 1 [Id] FROM [Roles] WHERE [OrganizationId] = @orgId AND [Name] = N'Procurement Manager' ORDER BY [Id]);

    IF @facilitiesRoleId IS NOT NULL AND @permCreatePurchases IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM [RolePermission] WHERE [RoleId] = @facilitiesRoleId AND [PermissionId] = @permCreatePurchases)
        INSERT INTO [RolePermission] ([RoleId], [PermissionId]) VALUES (@facilitiesRoleId, @permCreatePurchases);

    IF @facilitiesRoleId IS NOT NULL AND @permCreateAnyDept IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM [RolePermission] WHERE [RoleId] = @facilitiesRoleId AND [PermissionId] = @permCreateAnyDept)
        INSERT INTO [RolePermission] ([RoleId], [PermissionId]) VALUES (@facilitiesRoleId, @permCreateAnyDept);

    IF @facilitiesRoleId IS NOT NULL AND @permViewPurchases IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM [RolePermission] WHERE [RoleId] = @facilitiesRoleId AND [PermissionId] = @permViewPurchases)
        INSERT INTO [RolePermission] ([RoleId], [PermissionId]) VALUES (@facilitiesRoleId, @permViewPurchases);

    IF @facilitiesRoleId IS NOT NULL AND @permViewAssets IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM [RolePermission] WHERE [RoleId] = @facilitiesRoleId AND [PermissionId] = @permViewAssets)
        INSERT INTO [RolePermission] ([RoleId], [PermissionId]) VALUES (@facilitiesRoleId, @permViewAssets);

    IF @facilitiesRoleId IS NOT NULL AND @permAssignAssets IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM [RolePermission] WHERE [RoleId] = @facilitiesRoleId AND [PermissionId] = @permAssignAssets)
        INSERT INTO [RolePermission] ([RoleId], [PermissionId]) VALUES (@facilitiesRoleId, @permAssignAssets);

    IF @facilitiesRoleId IS NOT NULL AND @permTransferAssets IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM [RolePermission] WHERE [RoleId] = @facilitiesRoleId AND [PermissionId] = @permTransferAssets)
        INSERT INTO [RolePermission] ([RoleId], [PermissionId]) VALUES (@facilitiesRoleId, @permTransferAssets);

    IF @procurementManagerRoleId IS NOT NULL AND @permViewPurchases IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM [RolePermission] WHERE [RoleId] = @procurementManagerRoleId AND [PermissionId] = @permViewPurchases)
        INSERT INTO [RolePermission] ([RoleId], [PermissionId]) VALUES (@procurementManagerRoleId, @permViewPurchases);

    IF @procurementManagerRoleId IS NOT NULL AND @permApprovePurchases IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM [RolePermission] WHERE [RoleId] = @procurementManagerRoleId AND [PermissionId] = @permApprovePurchases)
        INSERT INTO [RolePermission] ([RoleId], [PermissionId]) VALUES (@procurementManagerRoleId, @permApprovePurchases);

    IF @procurementManagerRoleId IS NOT NULL AND @permEditPurchases IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM [RolePermission] WHERE [RoleId] = @procurementManagerRoleId AND [PermissionId] = @permEditPurchases)
        INSERT INTO [RolePermission] ([RoleId], [PermissionId]) VALUES (@procurementManagerRoleId, @permEditPurchases);

    IF @procurementManagerRoleId IS NOT NULL AND @permReceiveAssets IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM [RolePermission] WHERE [RoleId] = @procurementManagerRoleId AND [PermissionId] = @permReceiveAssets)
        INSERT INTO [RolePermission] ([RoleId], [PermissionId]) VALUES (@procurementManagerRoleId, @permReceiveAssets);

    IF @procurementManagerRoleId IS NOT NULL AND @permViewSuppliers IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM [RolePermission] WHERE [RoleId] = @procurementManagerRoleId AND [PermissionId] = @permViewSuppliers)
        INSERT INTO [RolePermission] ([RoleId], [PermissionId]) VALUES (@procurementManagerRoleId, @permViewSuppliers);

    IF @procurementManagerRoleId IS NOT NULL AND @permCreateSuppliers IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM [RolePermission] WHERE [RoleId] = @procurementManagerRoleId AND [PermissionId] = @permCreateSuppliers)
        INSERT INTO [RolePermission] ([RoleId], [PermissionId]) VALUES (@procurementManagerRoleId, @permCreateSuppliers);

    IF @procurementManagerRoleId IS NOT NULL AND @permEditSuppliers IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM [RolePermission] WHERE [RoleId] = @procurementManagerRoleId AND [PermissionId] = @permEditSuppliers)
        INSERT INTO [RolePermission] ([RoleId], [PermissionId]) VALUES (@procurementManagerRoleId, @permEditSuppliers);

    IF @procurementManagerRoleId IS NOT NULL
    BEGIN
        UPDATE [SystemSetting]
        SET [SettingValue] = CAST(@procurementManagerRoleId AS NVARCHAR(20)),
            [UpdatedAt] = CASE WHEN COL_LENGTH(N'[SystemSetting]', N'UpdatedAt') IS NOT NULL THEN @now ELSE [UpdatedAt] END
        WHERE [OrganizationId] = @orgId
          AND [SettingKey] = N'Approval.Process.Purchase.StageRoleIds';
    END

    FETCH NEXT FROM org_cursor INTO @orgId;
END
CLOSE org_cursor;
DEALLOCATE org_cursor;
GO
