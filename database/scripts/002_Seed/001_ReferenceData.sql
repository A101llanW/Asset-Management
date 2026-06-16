DECLARE @now DATETIME = GETUTCDATE();
DECLARE @passwordHash NVARCHAR(MAX) = N'ALJwzw5r970vW+fpNg4Ivw5nutwiP9Omge0gCdgtDVM2h6NFmycZ2GwSH5fyBqDTaw==';
DECLARE @securityStamp NVARCHAR(64) = N'00000000000000000000000000000001';

-- Permissions (upsert by code; migrations may have inserted some permissions first)
INSERT INTO [Permission] ([Name],[Code],[Module],[Description],[CreatedAt],[IsActive])
SELECT v.[Name], v.[Code], v.[Module], v.[Description], @now, 1
FROM (VALUES
    (N'View Users', N'Users.View', N'Users', N'Can view users'),
    (N'Create Users', N'Users.Create', N'Users', N'Can create users'),
    (N'Edit Users', N'Users.Edit', N'Users', N'Can edit users'),
    (N'Delete Users', N'Users.Delete', N'Users', N'Can delete users'),
    (N'View Roles', N'Roles.View', N'Roles', N'Can view roles'),
    (N'Create Roles', N'Roles.Create', N'Roles', N'Can create roles'),
    (N'Edit Roles', N'Roles.Edit', N'Roles', N'Can edit roles'),
    (N'Delete Roles', N'Roles.Delete', N'Roles', N'Can delete roles'),
    (N'Assign Permissions', N'Permissions.Assign', N'Roles', N'Can assign permissions'),
    (N'View Departments', N'Departments.View', N'Departments', N'Can view departments'),
    (N'Create Departments', N'Departments.Create', N'Departments', N'Can create departments'),
    (N'Edit Departments', N'Departments.Edit', N'Departments', N'Can edit departments'),
    (N'Delete Departments', N'Departments.Delete', N'Departments', N'Can delete departments'),
    (N'View Suppliers', N'Suppliers.View', N'Suppliers', N'Can view suppliers'),
    (N'Create Suppliers', N'Suppliers.Create', N'Suppliers', N'Can create suppliers'),
    (N'Edit Suppliers', N'Suppliers.Edit', N'Suppliers', N'Can edit suppliers'),
    (N'Delete Suppliers', N'Suppliers.Delete', N'Suppliers', N'Can delete suppliers'),
    (N'View Assets', N'Assets.View', N'Assets', N'Can view assets'),
    (N'Create Assets', N'Assets.Create', N'Assets', N'Can create assets'),
    (N'Edit Assets', N'Assets.Edit', N'Assets', N'Can edit assets'),
    (N'Delete Assets', N'Assets.Delete', N'Assets', N'Can delete assets'),
    (N'Assign Assets', N'Assets.Assign', N'Assets', N'Can assign assets'),
    (N'Transfer Assets', N'Assets.Transfer', N'Assets', N'Can transfer assets'),
    (N'Return Assets', N'Assets.Return', N'Assets', N'Can return assets'),
    (N'Receive Assets', N'Assets.Receive', N'Assets', N'Can receive assets'),
    (N'Dispose Assets', N'Assets.Dispose', N'Assets', N'Can dispose assets'),
    (N'Approve Disposal', N'Assets.ApproveDisposal', N'Assets', N'Can approve disposal'),
    (N'Request Assets', N'Assets.Request', N'Assets', N'Can submit asset requests'),
    (N'Approve Asset Requests', N'Assets.Request.Approve', N'Assets', N'Can approve and fulfill asset requests'),
    (N'View Purchases', N'Purchases.View', N'Purchases', N'Can view purchases'),
    (N'Create Purchases', N'Purchases.Create', N'Purchases', N'Can create purchases'),
    (N'Edit Purchases', N'Purchases.Edit', N'Purchases', N'Can edit purchases'),
    (N'Approve Purchases', N'Purchases.Approve', N'Purchases', N'Can approve purchases'),
    (N'View Incidents', N'Incidents.View', N'Incidents', N'Can view incidents'),
    (N'Create Incidents', N'Incidents.Create', N'Incidents', N'Can create incidents'),
    (N'Edit Incidents', N'Incidents.Edit', N'Incidents', N'Can edit incidents'),
    (N'View Claims', N'Claims.View', N'Claims', N'Can view claims'),
    (N'Create Claims', N'Claims.Create', N'Claims', N'Can create claims'),
    (N'Edit Claims', N'Claims.Edit', N'Claims', N'Can edit claims'),
    (N'View Financials', N'Financials.View', N'Financials', N'Can view financial data'),
    (N'Edit Financials', N'Financials.Edit', N'Financials', N'Can edit financial data'),
    (N'View Depreciation', N'Depreciation.View', N'Depreciation', N'Can view depreciation'),
    (N'Manage Depreciation', N'Depreciation.Manage', N'Depreciation', N'Can run/manage depreciation'),
    (N'View Documents', N'Documents.View', N'Documents', N'Can view asset documents'),
    (N'Download Documents', N'Documents.Download', N'Documents', N'Can download asset documents'),
    (N'Upload Documents', N'Documents.Upload', N'Documents', N'Can upload documents'),
    (N'Delete Documents', N'Documents.Delete', N'Documents', N'Can delete documents'),
    (N'View Reports', N'Reports.View', N'Reports', N'Can view reports'),
    (N'Export Reports', N'Reports.Export', N'Reports', N'Can export reports'),
    (N'View Audit Logs', N'AuditLogs.View', N'Audit', N'Can view audit logs'),
    (N'Manage Settings', N'Settings.Manage', N'Settings', N'Can manage settings')
) AS v([Name],[Code],[Module],[Description])
WHERE NOT EXISTS (SELECT 1 FROM [Permission] p WHERE p.[Code] = v.[Code]);
GO

-- Roles and role permissions (upsert; Platform Admin may already exist from migrations)
DECLARE @seedOrgId INT = (SELECT TOP 1 [Id] FROM [Organization] WHERE [Slug] = N'default' ORDER BY [Id]);
IF @seedOrgId IS NULL
    SET @seedOrgId = (SELECT TOP 1 [Id] FROM [Organization] ORDER BY [Id]);

IF @seedOrgId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [Roles] WHERE [Name] = N'Asset Manager' AND [OrganizationId] = @seedOrgId)
BEGIN
    DECLARE @now DATETIME = GETUTCDATE();
    DECLARE @hasRoleOrgId BIT = CASE WHEN COL_LENGTH(N'[Roles]', N'OrganizationId') IS NOT NULL THEN 1 ELSE 0 END;

    IF @hasRoleOrgId = 1
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [Name] = N'Company Admin' AND [OrganizationId] = @seedOrgId)
            INSERT INTO [Roles] ([Name],[Description],[IsSystemRole],[OrganizationId],[CreatedAt],[IsActive])
            VALUES (N'Company Admin', N'Company Admin seeded demo role', 1, @seedOrgId, @now, 1);
        IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [Name] = N'Asset Manager' AND [OrganizationId] = @seedOrgId)
            INSERT INTO [Roles] ([Name],[Description],[IsSystemRole],[OrganizationId],[CreatedAt],[IsActive])
            VALUES (N'Asset Manager', N'Asset Manager seeded demo role', 0, @seedOrgId, @now, 1);
        IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [Name] = N'Procurement Officer' AND [OrganizationId] = @seedOrgId)
            INSERT INTO [Roles] ([Name],[Description],[IsSystemRole],[OrganizationId],[CreatedAt],[IsActive])
            VALUES (N'Procurement Officer', N'Procurement Officer seeded demo role', 0, @seedOrgId, @now, 1);
        IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [Name] = N'Finance Officer' AND [OrganizationId] = @seedOrgId)
            INSERT INTO [Roles] ([Name],[Description],[IsSystemRole],[OrganizationId],[CreatedAt],[IsActive])
            VALUES (N'Finance Officer', N'Finance Officer seeded demo role', 0, @seedOrgId, @now, 1);
        IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [Name] = N'Department Head' AND [OrganizationId] = @seedOrgId)
            INSERT INTO [Roles] ([Name],[Description],[IsSystemRole],[OrganizationId],[CreatedAt],[IsActive])
            VALUES (N'Department Head', N'Department Head seeded demo role', 0, @seedOrgId, @now, 1);
        IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [Name] = N'Staff' AND [OrganizationId] = @seedOrgId)
            INSERT INTO [Roles] ([Name],[Description],[IsSystemRole],[OrganizationId],[CreatedAt],[IsActive])
            VALUES (N'Staff', N'Staff seeded demo role', 0, @seedOrgId, @now, 1);
        IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [Name] = N'Auditor' AND [OrganizationId] = @seedOrgId)
            INSERT INTO [Roles] ([Name],[Description],[IsSystemRole],[OrganizationId],[CreatedAt],[IsActive])
            VALUES (N'Auditor', N'Auditor seeded demo role', 0, @seedOrgId, @now, 1);
    END
    ELSE
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [Name] = N'Company Admin')
            INSERT INTO [Roles] ([Name],[Description],[IsSystemRole],[CreatedAt],[IsActive])
            VALUES (N'Company Admin', N'Company Admin seeded demo role', 1, @now, 1);
        IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [Name] = N'Asset Manager')
            INSERT INTO [Roles] ([Name],[Description],[IsSystemRole],[CreatedAt],[IsActive])
            VALUES (N'Asset Manager', N'Asset Manager seeded demo role', 0, @now, 1);
        IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [Name] = N'Procurement Officer')
            INSERT INTO [Roles] ([Name],[Description],[IsSystemRole],[CreatedAt],[IsActive])
            VALUES (N'Procurement Officer', N'Procurement Officer seeded demo role', 0, @now, 1);
        IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [Name] = N'Finance Officer')
            INSERT INTO [Roles] ([Name],[Description],[IsSystemRole],[CreatedAt],[IsActive])
            VALUES (N'Finance Officer', N'Finance Officer seeded demo role', 0, @now, 1);
        IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [Name] = N'Department Head')
            INSERT INTO [Roles] ([Name],[Description],[IsSystemRole],[CreatedAt],[IsActive])
            VALUES (N'Department Head', N'Department Head seeded demo role', 0, @now, 1);
        IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [Name] = N'Staff')
            INSERT INTO [Roles] ([Name],[Description],[IsSystemRole],[CreatedAt],[IsActive])
            VALUES (N'Staff', N'Staff seeded demo role', 0, @now, 1);
        IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [Name] = N'Auditor')
            INSERT INTO [Roles] ([Name],[Description],[IsSystemRole],[CreatedAt],[IsActive])
            VALUES (N'Auditor', N'Auditor seeded demo role', 0, @now, 1);
    END

    DECLARE @companyAdminRoleId INT = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Company Admin' AND (@hasRoleOrgId = 0 OR [OrganizationId] = @seedOrgId) ORDER BY [Id]);
    IF @companyAdminRoleId IS NOT NULL
    BEGIN
        INSERT INTO [RolePermission] ([RoleId],[PermissionId],[OrganizationId])
        SELECT @companyAdminRoleId, p.[Id], CASE WHEN @hasRoleOrgId = 1 THEN @seedOrgId ELSE NULL END
        FROM [Permission] p
        WHERE NOT EXISTS (
            SELECT 1 FROM [RolePermission] rp
            WHERE rp.[RoleId] = @companyAdminRoleId AND rp.[PermissionId] = p.[Id]);
    END

    INSERT INTO [RolePermission] ([RoleId],[PermissionId],[OrganizationId])
    SELECT r.[Id], p.[Id], CASE WHEN @hasRoleOrgId = 1 THEN r.[OrganizationId] ELSE NULL END
    FROM [Roles] r
    CROSS JOIN [Permission] p
    WHERE (@hasRoleOrgId = 0 OR r.[OrganizationId] = @seedOrgId)
      AND r.[Name] = N'Asset Manager'
      AND p.[Code] IN (N'Reports.View',N'Assets.View',N'Assets.Create',N'Assets.Edit',N'Assets.Assign',N'Assets.Transfer',N'Assets.Return',N'Assets.Receive',N'Assets.Dispose',N'Assets.ApproveDisposal',N'Assets.Request',N'Assets.Request.Approve',N'Purchases.View',N'Purchases.Approve',N'Departments.View',N'Suppliers.View',N'Incidents.View',N'Incidents.Create',N'Claims.View',N'Claims.Create',N'Documents.View',N'Documents.Download',N'Documents.Upload')
      AND NOT EXISTS (SELECT 1 FROM [RolePermission] rp WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = p.[Id]);

    INSERT INTO [RolePermission] ([RoleId],[PermissionId],[OrganizationId])
    SELECT r.[Id], p.[Id], CASE WHEN @hasRoleOrgId = 1 THEN r.[OrganizationId] ELSE NULL END
    FROM [Roles] r CROSS JOIN [Permission] p
    WHERE (@hasRoleOrgId = 0 OR r.[OrganizationId] = @seedOrgId)
      AND r.[Name] = N'Procurement Officer'
      AND p.[Code] IN (N'Reports.View',N'Assets.View',N'Purchases.View',N'Purchases.Create',N'Purchases.Edit',N'Purchases.Approve',N'Suppliers.View',N'Suppliers.Create',N'Suppliers.Edit')
      AND NOT EXISTS (SELECT 1 FROM [RolePermission] rp WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = p.[Id]);

    INSERT INTO [RolePermission] ([RoleId],[PermissionId],[OrganizationId])
    SELECT r.[Id], p.[Id], CASE WHEN @hasRoleOrgId = 1 THEN r.[OrganizationId] ELSE NULL END
    FROM [Roles] r CROSS JOIN [Permission] p
    WHERE (@hasRoleOrgId = 0 OR r.[OrganizationId] = @seedOrgId)
      AND r.[Name] = N'Finance Officer'
      AND p.[Code] IN (N'Reports.View',N'Assets.View',N'Purchases.View',N'Purchases.Approve',N'Financials.View',N'Financials.Edit',N'Depreciation.View',N'Depreciation.Manage',N'Claims.View',N'Claims.Edit')
      AND NOT EXISTS (SELECT 1 FROM [RolePermission] rp WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = p.[Id]);

    INSERT INTO [RolePermission] ([RoleId],[PermissionId],[OrganizationId])
    SELECT r.[Id], p.[Id], CASE WHEN @hasRoleOrgId = 1 THEN r.[OrganizationId] ELSE NULL END
    FROM [Roles] r CROSS JOIN [Permission] p
    WHERE (@hasRoleOrgId = 0 OR r.[OrganizationId] = @seedOrgId)
      AND r.[Name] = N'Department Head'
      AND p.[Code] IN (N'Reports.View',N'Departments.View',N'Departments.Edit',N'Assets.View',N'Assets.Assign',N'Assets.Return',N'Assets.Request',N'Assets.Request.Approve',N'Purchases.View',N'Purchases.Approve',N'Incidents.View',N'Incidents.Create',N'Claims.View',N'Assets.Transfer')
      AND NOT EXISTS (SELECT 1 FROM [RolePermission] rp WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = p.[Id]);

    INSERT INTO [RolePermission] ([RoleId],[PermissionId],[OrganizationId])
    SELECT r.[Id], p.[Id], CASE WHEN @hasRoleOrgId = 1 THEN r.[OrganizationId] ELSE NULL END
    FROM [Roles] r CROSS JOIN [Permission] p
    WHERE (@hasRoleOrgId = 0 OR r.[OrganizationId] = @seedOrgId)
      AND r.[Name] = N'Staff'
      AND p.[Code] IN (N'Assets.View',N'Assets.Return',N'Assets.Request',N'Purchases.View',N'Purchases.Create',N'Incidents.Create',N'Incidents.View',N'Documents.View',N'Documents.Upload')
      AND NOT EXISTS (SELECT 1 FROM [RolePermission] rp WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = p.[Id]);

    INSERT INTO [RolePermission] ([RoleId],[PermissionId],[OrganizationId])
    SELECT r.[Id], p.[Id], CASE WHEN @hasRoleOrgId = 1 THEN r.[OrganizationId] ELSE NULL END
    FROM [Roles] r CROSS JOIN [Permission] p
    WHERE (@hasRoleOrgId = 0 OR r.[OrganizationId] = @seedOrgId)
      AND r.[Name] = N'Auditor'
      AND p.[Code] IN (N'Reports.View',N'AuditLogs.View',N'Assets.View',N'Incidents.View',N'Claims.View',N'Depreciation.View',N'Financials.View')
      AND NOT EXISTS (SELECT 1 FROM [RolePermission] rp WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = p.[Id]);
END
GO

-- Departments (upsert per code/org for idempotent re-init)
DECLARE @nowDept DATETIME = GETUTCDATE();
DECLARE @seedOrgId INT = NULL;
DECLARE @hasDeptOrgId BIT = CASE WHEN COL_LENGTH(N'[Department]', N'OrganizationId') IS NOT NULL THEN 1 ELSE 0 END;
IF @hasDeptOrgId = 1
    SET @seedOrgId = (SELECT TOP 1 [Id] FROM [Organization] ORDER BY [Id]);

IF @hasDeptOrgId = 0
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [Department] WHERE [Code] = N'IT')
        INSERT INTO [Department] ([Name],[Code],[Description],[CreatedAt],[IsActive]) VALUES (N'Information Technology', N'IT', N'IT department', @nowDept, 1);
    IF NOT EXISTS (SELECT 1 FROM [Department] WHERE [Code] = N'FIN')
        INSERT INTO [Department] ([Name],[Code],[Description],[CreatedAt],[IsActive]) VALUES (N'Finance', N'FIN', N'Finance department', @nowDept, 1);
    IF NOT EXISTS (SELECT 1 FROM [Department] WHERE [Code] = N'HR')
        INSERT INTO [Department] ([Name],[Code],[Description],[CreatedAt],[IsActive]) VALUES (N'Human Resources', N'HR', N'HR department', @nowDept, 1);
    IF NOT EXISTS (SELECT 1 FROM [Department] WHERE [Code] = N'OPS')
        INSERT INTO [Department] ([Name],[Code],[Description],[CreatedAt],[IsActive]) VALUES (N'Operations', N'OPS', N'Operations department', @nowDept, 1);
    IF NOT EXISTS (SELECT 1 FROM [Department] WHERE [Code] = N'ADMIN')
        INSERT INTO [Department] ([Name],[Code],[Description],[CreatedAt],[IsActive]) VALUES (N'Administration', N'ADMIN', N'Administration department', @nowDept, 1);
END
ELSE IF @seedOrgId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [Department] WHERE [Code] = N'IT' AND [OrganizationId] = @seedOrgId)
        INSERT INTO [Department] ([Name],[Code],[Description],[OrganizationId],[CreatedAt],[IsActive]) VALUES (N'Information Technology', N'IT', N'IT department', @seedOrgId, @nowDept, 1);
    IF NOT EXISTS (SELECT 1 FROM [Department] WHERE [Code] = N'FIN' AND [OrganizationId] = @seedOrgId)
        INSERT INTO [Department] ([Name],[Code],[Description],[OrganizationId],[CreatedAt],[IsActive]) VALUES (N'Finance', N'FIN', N'Finance department', @seedOrgId, @nowDept, 1);
    IF NOT EXISTS (SELECT 1 FROM [Department] WHERE [Code] = N'HR' AND [OrganizationId] = @seedOrgId)
        INSERT INTO [Department] ([Name],[Code],[Description],[OrganizationId],[CreatedAt],[IsActive]) VALUES (N'Human Resources', N'HR', N'HR department', @seedOrgId, @nowDept, 1);
    IF NOT EXISTS (SELECT 1 FROM [Department] WHERE [Code] = N'OPS' AND [OrganizationId] = @seedOrgId)
        INSERT INTO [Department] ([Name],[Code],[Description],[OrganizationId],[CreatedAt],[IsActive]) VALUES (N'Operations', N'OPS', N'Operations department', @seedOrgId, @nowDept, 1);
    IF NOT EXISTS (SELECT 1 FROM [Department] WHERE [Code] = N'ADMIN' AND [OrganizationId] = @seedOrgId)
        INSERT INTO [Department] ([Name],[Code],[Description],[OrganizationId],[CreatedAt],[IsActive]) VALUES (N'Administration', N'ADMIN', N'Administration department', @seedOrgId, @nowDept, 1);
END
GO

-- Suppliers (upsert per name/org for idempotent re-init)
DECLARE @nowSup DATETIME = GETUTCDATE();
DECLARE @seedOrgId INT = NULL;
DECLARE @hasSupplierOrgId BIT = CASE WHEN COL_LENGTH(N'[Supplier]', N'OrganizationId') IS NOT NULL THEN 1 ELSE 0 END;
IF @hasSupplierOrgId = 1
    SET @seedOrgId = (SELECT TOP 1 [Id] FROM [Organization] ORDER BY [Id]);

IF @hasSupplierOrgId = 0
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [Supplier] WHERE [SupplierName] = N'Tech Source Ltd')
        INSERT INTO [Supplier] ([SupplierName],[ContactPerson],[Email],[Phone],[Address],[RegistrationNumber],[Notes],[CreatedAt],[IsActive]) VALUES (N'Tech Source Ltd', N'Mary Wanjiku', N'sales@techsource.example', N'+254700000001', N'Nairobi', N'TSL-001', N'Primary IT supplier', @nowSup, 1);
    IF NOT EXISTS (SELECT 1 FROM [Supplier] WHERE [SupplierName] = N'Office Works Hub')
        INSERT INTO [Supplier] ([SupplierName],[ContactPerson],[Email],[Phone],[Address],[RegistrationNumber],[Notes],[CreatedAt],[IsActive]) VALUES (N'Office Works Hub', N'David Mwangi', N'contact@officeworks.example', N'+254700000002', N'Mombasa', N'OWH-003', N'Furniture and office equipment', @nowSup, 1);
    IF NOT EXISTS (SELECT 1 FROM [Supplier] WHERE [SupplierName] = N'MedEquip Africa')
        INSERT INTO [Supplier] ([SupplierName],[ContactPerson],[Email],[Phone],[Address],[RegistrationNumber],[Notes],[CreatedAt],[IsActive]) VALUES (N'MedEquip Africa', N'Anne Njeri', N'support@medequip.example', N'+254700000003', N'Kisumu', N'MEA-018', N'Medical and lab equipment', @nowSup, 1);
END
ELSE IF @seedOrgId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [Supplier] WHERE [SupplierName] = N'Tech Source Ltd' AND [OrganizationId] = @seedOrgId)
        INSERT INTO [Supplier] ([SupplierName],[ContactPerson],[Email],[Phone],[Address],[RegistrationNumber],[Notes],[OrganizationId],[CreatedAt],[IsActive]) VALUES (N'Tech Source Ltd', N'Mary Wanjiku', N'sales@techsource.example', N'+254700000001', N'Nairobi', N'TSL-001', N'Primary IT supplier', @seedOrgId, @nowSup, 1);
    IF NOT EXISTS (SELECT 1 FROM [Supplier] WHERE [SupplierName] = N'Office Works Hub' AND [OrganizationId] = @seedOrgId)
        INSERT INTO [Supplier] ([SupplierName],[ContactPerson],[Email],[Phone],[Address],[RegistrationNumber],[Notes],[OrganizationId],[CreatedAt],[IsActive]) VALUES (N'Office Works Hub', N'David Mwangi', N'contact@officeworks.example', N'+254700000002', N'Mombasa', N'OWH-003', N'Furniture and office equipment', @seedOrgId, @nowSup, 1);
    IF NOT EXISTS (SELECT 1 FROM [Supplier] WHERE [SupplierName] = N'MedEquip Africa' AND [OrganizationId] = @seedOrgId)
        INSERT INTO [Supplier] ([SupplierName],[ContactPerson],[Email],[Phone],[Address],[RegistrationNumber],[Notes],[OrganizationId],[CreatedAt],[IsActive]) VALUES (N'MedEquip Africa', N'Anne Njeri', N'support@medequip.example', N'+254700000003', N'Kisumu', N'MEA-018', N'Medical and lab equipment', @seedOrgId, @nowSup, 1);
END
GO

-- Asset taxonomy: upsert categories per org, then seed types only when category ids resolve.
DECLARE @nowTax DATETIME = GETUTCDATE();
DECLARE @seedOrgId INT = NULL;
DECLARE @hasCategoryOrgId BIT = CASE WHEN COL_LENGTH(N'[AssetCategory]', N'OrganizationId') IS NOT NULL THEN 1 ELSE 0 END;
DECLARE @hasTypeOrgId BIT = CASE WHEN COL_LENGTH(N'[AssetType]', N'OrganizationId') IS NOT NULL THEN 1 ELSE 0 END;
IF @hasCategoryOrgId = 1
    SET @seedOrgId = (SELECT TOP 1 [Id] FROM [Organization] ORDER BY [Id]);

IF @hasCategoryOrgId = 0
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [AssetCategory] WHERE [Name] = N'IT Equipment')
        INSERT INTO [AssetCategory] ([Name],[Description],[CreatedAt],[IsActive]) VALUES (N'IT Equipment', N'Computing and peripheral assets', @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetCategory] WHERE [Name] = N'Office Equipment')
        INSERT INTO [AssetCategory] ([Name],[Description],[CreatedAt],[IsActive]) VALUES (N'Office Equipment', N'Printers, projectors, and general office devices', @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetCategory] WHERE [Name] = N'Furniture')
        INSERT INTO [AssetCategory] ([Name],[Description],[CreatedAt],[IsActive]) VALUES (N'Furniture', N'Office furniture assets', @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetCategory] WHERE [Name] = N'Networking')
        INSERT INTO [AssetCategory] ([Name],[Description],[CreatedAt],[IsActive]) VALUES (N'Networking', N'Network and communication assets', @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetCategory] WHERE [Name] = N'Medical/Lab Equipment')
        INSERT INTO [AssetCategory] ([Name],[Description],[CreatedAt],[IsActive]) VALUES (N'Medical/Lab Equipment', N'Healthcare and laboratory assets', @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetCategory] WHERE [Name] = N'Vehicles')
        INSERT INTO [AssetCategory] ([Name],[Description],[CreatedAt],[IsActive]) VALUES (N'Vehicles', N'Fleet and transport assets', @nowTax, 1);
END
ELSE IF @seedOrgId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [AssetCategory] WHERE [Name] = N'IT Equipment' AND [OrganizationId] = @seedOrgId)
        INSERT INTO [AssetCategory] ([Name],[Description],[OrganizationId],[CreatedAt],[IsActive]) VALUES (N'IT Equipment', N'Computing and peripheral assets', @seedOrgId, @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetCategory] WHERE [Name] = N'Office Equipment' AND [OrganizationId] = @seedOrgId)
        INSERT INTO [AssetCategory] ([Name],[Description],[OrganizationId],[CreatedAt],[IsActive]) VALUES (N'Office Equipment', N'Printers, projectors, and general office devices', @seedOrgId, @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetCategory] WHERE [Name] = N'Furniture' AND [OrganizationId] = @seedOrgId)
        INSERT INTO [AssetCategory] ([Name],[Description],[OrganizationId],[CreatedAt],[IsActive]) VALUES (N'Furniture', N'Office furniture assets', @seedOrgId, @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetCategory] WHERE [Name] = N'Networking' AND [OrganizationId] = @seedOrgId)
        INSERT INTO [AssetCategory] ([Name],[Description],[OrganizationId],[CreatedAt],[IsActive]) VALUES (N'Networking', N'Network and communication assets', @seedOrgId, @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetCategory] WHERE [Name] = N'Medical/Lab Equipment' AND [OrganizationId] = @seedOrgId)
        INSERT INTO [AssetCategory] ([Name],[Description],[OrganizationId],[CreatedAt],[IsActive]) VALUES (N'Medical/Lab Equipment', N'Healthcare and laboratory assets', @seedOrgId, @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetCategory] WHERE [Name] = N'Vehicles' AND [OrganizationId] = @seedOrgId)
        INSERT INTO [AssetCategory] ([Name],[Description],[OrganizationId],[CreatedAt],[IsActive]) VALUES (N'Vehicles', N'Fleet and transport assets', @seedOrgId, @nowTax, 1);
END

DECLARE @itCategoryId INT;
DECLARE @officeCategoryId INT;
DECLARE @furnitureCategoryId INT;
DECLARE @networkCategoryId INT;
DECLARE @medicalCategoryId INT;
DECLARE @vehicleCategoryId INT;

IF @hasCategoryOrgId = 0
BEGIN
    SET @itCategoryId = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [Name] = N'IT Equipment' ORDER BY [Id]);
    SET @officeCategoryId = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [Name] = N'Office Equipment' ORDER BY [Id]);
    SET @furnitureCategoryId = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [Name] = N'Furniture' ORDER BY [Id]);
    SET @networkCategoryId = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [Name] = N'Networking' ORDER BY [Id]);
    SET @medicalCategoryId = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [Name] = N'Medical/Lab Equipment' ORDER BY [Id]);
    SET @vehicleCategoryId = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [Name] = N'Vehicles' ORDER BY [Id]);
END
ELSE
BEGIN
    SET @itCategoryId = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [Name] = N'IT Equipment' AND [OrganizationId] = @seedOrgId ORDER BY [Id]);
    SET @officeCategoryId = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [Name] = N'Office Equipment' AND [OrganizationId] = @seedOrgId ORDER BY [Id]);
    SET @furnitureCategoryId = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [Name] = N'Furniture' AND [OrganizationId] = @seedOrgId ORDER BY [Id]);
    SET @networkCategoryId = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [Name] = N'Networking' AND [OrganizationId] = @seedOrgId ORDER BY [Id]);
    SET @medicalCategoryId = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [Name] = N'Medical/Lab Equipment' AND [OrganizationId] = @seedOrgId ORDER BY [Id]);
    SET @vehicleCategoryId = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [Name] = N'Vehicles' AND [OrganizationId] = @seedOrgId ORDER BY [Id]);
END

IF @hasTypeOrgId = 0
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Laptop') AND @itCategoryId IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[CreatedAt],[IsActive]) VALUES (@itCategoryId, N'Laptop', N'Portable computer', @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Desktop') AND @itCategoryId IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[CreatedAt],[IsActive]) VALUES (@itCategoryId, N'Desktop', N'Desktop computer', @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Router') AND @networkCategoryId IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[CreatedAt],[IsActive]) VALUES (@networkCategoryId, N'Router', N'Router and gateway', @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Office Chair') AND @furnitureCategoryId IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[CreatedAt],[IsActive]) VALUES (@furnitureCategoryId, N'Office Chair', N'Ergonomic chair', @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Lab Microscope') AND @medicalCategoryId IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[CreatedAt],[IsActive]) VALUES (@medicalCategoryId, N'Lab Microscope', N'Microscope device', @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Lab Centrifuge') AND @medicalCategoryId IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[CreatedAt],[IsActive]) VALUES (@medicalCategoryId, N'Lab Centrifuge', N'Benchtop laboratory centrifuge', @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Printer') AND @officeCategoryId IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[CreatedAt],[IsActive]) VALUES (@officeCategoryId, N'Printer', N'Office printer or MFP', @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Projector') AND @officeCategoryId IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[CreatedAt],[IsActive]) VALUES (@officeCategoryId, N'Projector', N'Conference room projector', @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Office Desk') AND @furnitureCategoryId IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[CreatedAt],[IsActive]) VALUES (@furnitureCategoryId, N'Office Desk', N'Office desk or workstation', @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Vehicle') AND @vehicleCategoryId IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[CreatedAt],[IsActive]) VALUES (@vehicleCategoryId, N'Vehicle', N'Company fleet vehicle', @nowTax, 1);
END
ELSE IF @seedOrgId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Laptop' AND [OrganizationId] = @seedOrgId) AND @itCategoryId IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[OrganizationId],[CreatedAt],[IsActive]) VALUES (@itCategoryId, N'Laptop', N'Portable computer', @seedOrgId, @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Desktop' AND [OrganizationId] = @seedOrgId) AND @itCategoryId IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[OrganizationId],[CreatedAt],[IsActive]) VALUES (@itCategoryId, N'Desktop', N'Desktop computer', @seedOrgId, @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Router' AND [OrganizationId] = @seedOrgId) AND @networkCategoryId IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[OrganizationId],[CreatedAt],[IsActive]) VALUES (@networkCategoryId, N'Router', N'Router and gateway', @seedOrgId, @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Office Chair' AND [OrganizationId] = @seedOrgId) AND @furnitureCategoryId IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[OrganizationId],[CreatedAt],[IsActive]) VALUES (@furnitureCategoryId, N'Office Chair', N'Ergonomic chair', @seedOrgId, @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Lab Microscope' AND [OrganizationId] = @seedOrgId) AND @medicalCategoryId IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[OrganizationId],[CreatedAt],[IsActive]) VALUES (@medicalCategoryId, N'Lab Microscope', N'Microscope device', @seedOrgId, @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Lab Centrifuge' AND [OrganizationId] = @seedOrgId) AND @medicalCategoryId IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[OrganizationId],[CreatedAt],[IsActive]) VALUES (@medicalCategoryId, N'Lab Centrifuge', N'Benchtop laboratory centrifuge', @seedOrgId, @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Printer' AND [OrganizationId] = @seedOrgId) AND @officeCategoryId IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[OrganizationId],[CreatedAt],[IsActive]) VALUES (@officeCategoryId, N'Printer', N'Office printer or MFP', @seedOrgId, @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Projector' AND [OrganizationId] = @seedOrgId) AND @officeCategoryId IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[OrganizationId],[CreatedAt],[IsActive]) VALUES (@officeCategoryId, N'Projector', N'Conference room projector', @seedOrgId, @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Office Desk' AND [OrganizationId] = @seedOrgId) AND @furnitureCategoryId IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[OrganizationId],[CreatedAt],[IsActive]) VALUES (@furnitureCategoryId, N'Office Desk', N'Office desk or workstation', @seedOrgId, @nowTax, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Vehicle' AND [OrganizationId] = @seedOrgId) AND @vehicleCategoryId IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[OrganizationId],[CreatedAt],[IsActive]) VALUES (@vehicleCategoryId, N'Vehicle', N'Company fleet vehicle', @seedOrgId, @nowTax, 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM [SystemSetting] WHERE [SettingKey] = N'Finance.DefaultCurrency')
BEGIN
    DECLARE @now DATETIME = GETUTCDATE();
    DECLARE @seedOrgId INT = NULL;
    IF COL_LENGTH(N'[SystemSetting]', N'OrganizationId') IS NOT NULL
        SET @seedOrgId = (SELECT TOP 1 [Id] FROM [Organization] ORDER BY [Id]);

    IF COL_LENGTH(N'[SystemSetting]', N'OrganizationId') IS NULL
    BEGIN
        INSERT INTO [SystemSetting] ([SettingKey],[SettingValue],[Description],[CreatedAt],[IsActive]) VALUES
        (N'Finance.DefaultCurrency', N'KES', N'Default finance currency code.', @now, 1);
    END
    ELSE IF @seedOrgId IS NOT NULL
    BEGIN
        INSERT INTO [SystemSetting] ([SettingKey],[SettingValue],[Description],[OrganizationId],[CreatedAt],[IsActive]) VALUES
        (N'Finance.DefaultCurrency', N'KES', N'Default finance currency code.', @seedOrgId, @now, 1);
    END
END
GO

-- Default approval workflow settings (Requires approval off unless enabled in Settings).
IF NOT EXISTS (SELECT 1 FROM [SystemSetting] WHERE [SettingKey] = N'Approval.RequireDisposalApproval')
BEGIN
    DECLARE @now DATETIME = GETUTCDATE();
    DECLARE @seedOrgId INT = NULL;
    IF COL_LENGTH(N'[SystemSetting]', N'OrganizationId') IS NOT NULL
        SET @seedOrgId = (SELECT TOP 1 [Id] FROM [Organization] ORDER BY [Id]);
    DECLARE @companyAdminRoleId INT = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Company Admin' ORDER BY [Id]);
    DECLARE @assetManagerRoleId INT = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Asset Manager' ORDER BY [Id]);

    IF COL_LENGTH(N'[SystemSetting]', N'OrganizationId') IS NULL
    BEGIN
        INSERT INTO [SystemSetting] ([SettingKey],[SettingValue],[Description],[CreatedAt],[IsActive]) VALUES
        (N'Approval.RequireDisposalApproval', N'false', N'Require approval before asset disposal.', @now, 1),
        (N'Approval.Process.Disposal.StageRoleIds', CAST(@companyAdminRoleId AS NVARCHAR(20)), N'Stage 1 approver role ids for disposal.', @now, 1),
        (N'Approval.RequireTransferApproval', N'false', N'Require approval before asset transfer.', @now, 1),
        (N'Approval.Process.Transfer.StageRoleIds', CAST(@assetManagerRoleId AS NVARCHAR(20)), N'Stage 1 approver role ids for transfer.', @now, 1),
        (N'Approval.RequirePurchaseApproval', N'false', N'Require approval before purchase request fulfillment.', @now, 1),
        (N'Approval.Process.Purchase.StageRoleIds', CAST(@companyAdminRoleId AS NVARCHAR(20)), N'Stage 1 approver role ids for purchase.', @now, 1);
    END
    ELSE IF @seedOrgId IS NOT NULL
    BEGIN
        INSERT INTO [SystemSetting] ([SettingKey],[SettingValue],[Description],[OrganizationId],[CreatedAt],[IsActive]) VALUES
        (N'Approval.RequireDisposalApproval', N'false', N'Require approval before asset disposal.', @seedOrgId, @now, 1),
        (N'Approval.Process.Disposal.StageRoleIds', CAST(@companyAdminRoleId AS NVARCHAR(20)), N'Stage 1 approver role ids for disposal.', @seedOrgId, @now, 1),
        (N'Approval.RequireTransferApproval', N'false', N'Require approval before asset transfer.', @seedOrgId, @now, 1),
        (N'Approval.Process.Transfer.StageRoleIds', CAST(@assetManagerRoleId AS NVARCHAR(20)), N'Stage 1 approver role ids for transfer.', @seedOrgId, @now, 1),
        (N'Approval.RequirePurchaseApproval', N'false', N'Require approval before purchase request fulfillment.', @seedOrgId, @now, 1),
        (N'Approval.Process.Purchase.StageRoleIds', CAST(@companyAdminRoleId AS NVARCHAR(20)), N'Stage 1 approver role ids for purchase.', @seedOrgId, @now, 1);
    END
END
GO
