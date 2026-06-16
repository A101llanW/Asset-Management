-- Second demo organization for tenant isolation testing (Phase 5c).
-- Runs after multitenancy backfill so OrganizationId is available on tenant tables.

IF NOT EXISTS (SELECT 1 FROM [Organization] WHERE [Slug] = N'demo-b')
BEGIN
    DECLARE @now DATETIME = GETUTCDATE();
    INSERT INTO [Organization] ([Name],[Code],[Slug],[Status],[Email],[CurrencyCode],[CreatedAt],[IsActive])
    VALUES (N'Demo Organization B', N'DEMOB', N'demo-b', N'Active', N'admin@demo-b.asset.local', N'KES', @now, 1);
END
GO

DECLARE @orgBId INT = (SELECT TOP 1 [Id] FROM [Organization] WHERE [Slug] = N'demo-b' ORDER BY [Id]);
IF @orgBId IS NULL
BEGIN
    RETURN;
END
GO

DECLARE @orgBId INT = (SELECT TOP 1 [Id] FROM [Organization] WHERE [Slug] = N'demo-b' ORDER BY [Id]);
DECLARE @now DATETIME = GETUTCDATE();
DECLARE @passwordHash NVARCHAR(MAX) = N'ALJwzw5r970vW+fpNg4Ivw5nutwiP9Omge0gCdgtDVM2h6NFmycZ2GwSH5fyBqDTaw==';
DECLARE @securityStamp NVARCHAR(64) = N'00000000000000000000000000000004';

-- Departments for org B
IF NOT EXISTS (SELECT 1 FROM [Department] WHERE [OrganizationId] = @orgBId)
BEGIN
    INSERT INTO [Department] ([Name],[Code],[Description],[OrganizationId],[CreatedAt],[IsActive]) VALUES
    (N'Information Technology', N'IT', N'IT department (org B)', @orgBId, @now, 1),
    (N'Finance', N'FIN', N'Finance department (org B)', @orgBId, @now, 1),
    (N'Operations', N'OPS', N'Operations department (org B)', @orgBId, @now, 1);
END
GO

DECLARE @orgBId INT = (SELECT TOP 1 [Id] FROM [Organization] WHERE [Slug] = N'demo-b' ORDER BY [Id]);
DECLARE @defaultOrgId INT = (SELECT TOP 1 [Id] FROM [Organization] WHERE [Slug] = N'default' ORDER BY [Id]);
IF @orgBId IS NULL OR @defaultOrgId IS NULL
BEGIN
    RETURN;
END
GO

-- Clone tenant roles from default org into org B
DECLARE @orgBId INT = (SELECT TOP 1 [Id] FROM [Organization] WHERE [Slug] = N'demo-b' ORDER BY [Id]);
DECLARE @defaultOrgId INT = (SELECT TOP 1 [Id] FROM [Organization] WHERE [Slug] = N'default' ORDER BY [Id]);
DECLARE @now DATETIME = GETUTCDATE();

IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [OrganizationId] = @orgBId AND [Name] = N'Company Admin')
BEGIN
    DECLARE @roleMap TABLE (TemplateRoleId INT NOT NULL, NewRoleId INT NOT NULL);
    DECLARE @templateRoleId INT;
    DECLARE @newRoleId INT;
    DECLARE @roleName NVARCHAR(120);
    DECLARE @roleDescription NVARCHAR(500);
    DECLARE @isSystemRole BIT;

    DECLARE role_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT [Id], [Name], [Description], [IsSystemRole]
        FROM [Roles]
        WHERE [OrganizationId] = @defaultOrgId
          AND [Name] <> N'Platform Admin';

    OPEN role_cursor;
    FETCH NEXT FROM role_cursor INTO @templateRoleId, @roleName, @roleDescription, @isSystemRole;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        INSERT INTO [Roles] ([Name],[Description],[IsSystemRole],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (@roleName, @roleDescription, @isSystemRole, @orgBId, @now, 1);
        SET @newRoleId = SCOPE_IDENTITY();
        INSERT INTO @roleMap (TemplateRoleId, NewRoleId) VALUES (@templateRoleId, @newRoleId);

        INSERT INTO [RolePermission] ([RoleId],[PermissionId],[OrganizationId])
        SELECT @newRoleId, rp.[PermissionId], @orgBId
        FROM [RolePermission] rp
        WHERE rp.[RoleId] = @templateRoleId AND rp.[OrganizationId] = @defaultOrgId;

        FETCH NEXT FROM role_cursor INTO @templateRoleId, @roleName, @roleDescription, @isSystemRole;
    END
    CLOSE role_cursor;
    DEALLOCATE role_cursor;
END
GO

-- Org B demo users (password: P@ssw0rd!)
DECLARE @orgBId INT = (SELECT TOP 1 [Id] FROM [Organization] WHERE [Slug] = N'demo-b' ORDER BY [Id]);
IF @orgBId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [Users] WHERE [Email] = N'demo@asset.local')
BEGIN
    DECLARE @now DATETIME = GETUTCDATE();
    DECLARE @passwordHash NVARCHAR(MAX) = N'ALJwzw5r970vW+fpNg4Ivw5nutwiP9Omge0gCdgtDVM2h6NFmycZ2GwSH5fyBqDTaw==';
    DECLARE @securityStamp NVARCHAR(64) = N'00000000000000000000000000000004';
    DECLARE @deptIt INT = (SELECT TOP 1 [Id] FROM [Department] WHERE [OrganizationId] = @orgBId AND [Code] = N'IT');
    DECLARE @roleCompanyAdmin INT = (SELECT TOP 1 [Id] FROM [Roles] WHERE [OrganizationId] = @orgBId AND [Name] = N'Company Admin');
    DECLARE @roleStaff INT = (SELECT TOP 1 [Id] FROM [Roles] WHERE [OrganizationId] = @orgBId AND [Name] = N'Staff');

    INSERT INTO [Users]
        ([Id],[Email],[EmailConfirmed],[PasswordHash],[SecurityStamp],[PhoneNumberConfirmed],[TwoFactorEnabled],[LockoutEnabled],[AccessFailedCount],[UserName],
         [EmployeeNumber],[FirstName],[LastName],[Phone],[DepartmentId],[PositionTitle],[IsActive],[RoleId],[OrganizationId],[CreatedAt])
    VALUES
    (N'seed-user-b-admin', N'demo@asset.local', 1, @passwordHash, @securityStamp, 0, 0, 0, 0, N'demo@asset.local',
     N'EMP-B-001', N'Beta', N'Admin', N'+254700002001', @deptIt, N'Company Administrator', 1, @roleCompanyAdmin, @orgBId, @now),
    (N'seed-user-b-staff', N'staff@demo-b.asset.local', 1, @passwordHash, @securityStamp, 0, 0, 0, 0, N'staff@demo-b.asset.local',
     N'EMP-B-002', N'Sam', N'Staff', N'+254700002002', @deptIt, N'Staff', 1, @roleStaff, @orgBId, @now);
END
GO

-- Sample assets for org B (isolation smoke data)
DECLARE @orgBId INT = (SELECT TOP 1 [Id] FROM [Organization] WHERE [Slug] = N'demo-b' ORDER BY [Id]);
IF @orgBId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [Asset] WHERE [OrganizationId] = @orgBId)
BEGIN
    DECLARE @now DATETIME = GETUTCDATE();
    DECLARE @deptIt INT = (SELECT TOP 1 [Id] FROM [Department] WHERE [OrganizationId] = @orgBId AND [Code] = N'IT');
    DECLARE @categoryId INT = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [OrganizationId] = (SELECT TOP 1 [Id] FROM [Organization] WHERE [Slug] = N'default') ORDER BY [Id]);
    DECLARE @typeId INT = (SELECT TOP 1 [Id] FROM [AssetType] WHERE [OrganizationId] = (SELECT TOP 1 [Id] FROM [Organization] WHERE [Slug] = N'default') ORDER BY [Id]);
    DECLARE @supplierId INT = (SELECT TOP 1 [Id] FROM [Supplier] WHERE [OrganizationId] = (SELECT TOP 1 [Id] FROM [Organization] WHERE [Slug] = N'default') ORDER BY [Id]);

    IF @categoryId IS NULL
    BEGIN
        INSERT INTO [AssetCategory] ([Name],[Description],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (N'IT Equipment', N'Org B category', @orgBId, @now, 1);
        SET @categoryId = SCOPE_IDENTITY();
    END

    IF @typeId IS NULL
    BEGIN
        INSERT INTO [AssetType] ([Name],[Description],[AssetCategoryId],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (N'Laptop', N'Org B laptop type', @categoryId, @orgBId, @now, 1);
        SET @typeId = SCOPE_IDENTITY();
    END

    IF @supplierId IS NULL
    BEGIN
        INSERT INTO [Supplier] ([SupplierName],[ContactPerson],[Email],[Phone],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (N'Beta Supply Co', N'Contact', N'supply@demo-b.local', N'+254700000000', @orgBId, @now, 1);
        SET @supplierId = SCOPE_IDENTITY();
    END

    DECLARE @i INT = 1;
    WHILE @i <= 5
    BEGIN
        INSERT INTO [Asset]
            ([AssetName],[AssetTag],[CategoryId],[AssetTypeId],[Brand],[Model],[SerialNumber],[Condition],[CurrentStatus],[Description],
             [PurchaseDate],[AcquisitionCost],[TaxAmount],[Currency],[SupplierId],[DepartmentId],[ConditionOnReceipt],[UsefulLifeMonths],[SalvageValue],
             [DepreciationMethod],[DepreciationStartDate],[CurrentBookValue],[AccumulatedDepreciation],
             [IsLeased],[IsInsured],[OrganizationId],[CreatedAt],[IsActive])
        VALUES
        (
            N'Org B Laptop ' + CAST(@i AS NVARCHAR(10)),
            N'BETA-2026-' + RIGHT(N'00' + CAST(@i AS NVARCHAR(3)), 3),
            @categoryId, @typeId, N'Dell', N'Latitude', N'B-SN-' + RIGHT(N'0000' + CAST(@i AS NVARCHAR(4)), 4),
            1, 6, N'Org B isolation sample asset',
            DATEADD(MONTH, -@i, @now), 120000, 19200, N'KES', @supplierId, @deptIt, N'New', 48, 8000,
            0, DATEADD(MONTH, -@i, @now), 110000, 10000,
            0, 0, @orgBId, @now, 1
        );
        SET @i = @i + 1;
    END
END
GO

-- Default license for org B (if license tables exist)
IF OBJECT_ID(N'[OrganizationLicense]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM [OrganizationLicense] WHERE [OrganizationId] = (SELECT TOP 1 [Id] FROM [Organization] WHERE [Slug] = N'demo-b' ORDER BY [Id]))
BEGIN
    DECLARE @orgBId INT = (SELECT TOP 1 [Id] FROM [Organization] WHERE [Slug] = N'demo-b' ORDER BY [Id]);
    DECLARE @now DATETIME = GETUTCDATE();
    INSERT INTO [OrganizationLicense]
        ([OrganizationId],[PlanCode],[PlanName],[Status],[StartDate],[ExpiryDate],[CreatedAt],[IsActive])
    VALUES
        (@orgBId, N'Standard', N'Standard Plan', N'Active', @now, DATEADD(MONTH, 12, @now), @now, 1);
END
GO
