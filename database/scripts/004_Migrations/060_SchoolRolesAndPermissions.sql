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

    -- RolePermission.OrganizationId comes from the role (JOIN [Roles]) so these grants can
    -- never be stamped with another org (see artifacts/roleperm-org-fix-2026-10-08).
    -- Idempotent: skipped when (RoleId, PermissionId) already exists; the unique index is on
    -- (RoleId, PermissionId) and a role has exactly one org, so this covers
    -- (OrganizationId, RoleId, PermissionId) too. NULL role/permission ids drop out of the join.
    INSERT INTO [RolePermission] ([RoleId], [PermissionId], [OrganizationId])
    SELECT r.[Id], g.[PermissionId], r.[OrganizationId]
    FROM (VALUES
        (@facilitiesRoleId, @permCreatePurchases),
        (@facilitiesRoleId, @permCreateAnyDept),
        (@facilitiesRoleId, @permViewPurchases),
        (@facilitiesRoleId, @permViewAssets),
        (@facilitiesRoleId, @permAssignAssets),
        (@facilitiesRoleId, @permTransferAssets),
        (@procurementManagerRoleId, @permViewPurchases),
        (@procurementManagerRoleId, @permApprovePurchases),
        (@procurementManagerRoleId, @permEditPurchases),
        (@procurementManagerRoleId, @permReceiveAssets),
        (@procurementManagerRoleId, @permViewSuppliers),
        (@procurementManagerRoleId, @permCreateSuppliers),
        (@procurementManagerRoleId, @permEditSuppliers)
    ) AS g([RoleId], [PermissionId])
    INNER JOIN [Roles] r ON r.[Id] = g.[RoleId]
    WHERE g.[PermissionId] IS NOT NULL
      AND r.[OrganizationId] = @orgId
      AND NOT EXISTS (SELECT 1 FROM [RolePermission] rp
                      WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = g.[PermissionId]);

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
