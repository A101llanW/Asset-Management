-- Demo users (password for all: P@ssw0rd!)
IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Email] = N'assetmanager@asset.local')
BEGIN
    DECLARE @now DATETIME = GETUTCDATE();
    DECLARE @passwordHash NVARCHAR(MAX) = N'ALJwzw5r970vW+fpNg4Ivw5nutwiP9Omge0gCdgtDVM2h6NFmycZ2GwSH5fyBqDTaw==';
    DECLARE @securityStamp NVARCHAR(64) = N'00000000000000000000000000000001';
    DECLARE @orgId INT = (SELECT TOP 1 [Id] FROM [Organization] WHERE [Slug] IN (N'nanosoft', N'default') ORDER BY CASE WHEN [Slug] = N'nanosoft' THEN 0 ELSE 1 END, [Id]);
    IF @orgId IS NULL
        SET @orgId = (SELECT TOP 1 [Id] FROM [Organization] ORDER BY [Id]);

    DECLARE @deptIt INT;
    DECLARE @deptFin INT;
    DECLARE @deptHr INT;
    DECLARE @deptOps INT;
    DECLARE @deptAdmin INT;
    DECLARE @roleSuperAdmin INT;
    DECLARE @roleAssetManager INT;
    DECLARE @roleProcurement INT;
    DECLARE @roleFinance INT;
    DECLARE @roleStaff INT;
    DECLARE @roleAuditor INT;
    DECLARE @roleDeptHead INT;

    IF COL_LENGTH(N'[Department]', N'OrganizationId') IS NOT NULL AND @orgId IS NOT NULL
    BEGIN
        SET @deptIt = (SELECT TOP 1 [Id] FROM [Department] WHERE [Code] = N'IT' AND [OrganizationId] = @orgId ORDER BY [Id]);
        SET @deptFin = (SELECT TOP 1 [Id] FROM [Department] WHERE [Code] = N'FIN' AND [OrganizationId] = @orgId ORDER BY [Id]);
        SET @deptHr = (SELECT TOP 1 [Id] FROM [Department] WHERE [Code] = N'HR' AND [OrganizationId] = @orgId ORDER BY [Id]);
        SET @deptOps = (SELECT TOP 1 [Id] FROM [Department] WHERE [Code] = N'OPS' AND [OrganizationId] = @orgId ORDER BY [Id]);
        SET @deptAdmin = (SELECT TOP 1 [Id] FROM [Department] WHERE [Code] = N'ADMIN' AND [OrganizationId] = @orgId ORDER BY [Id]);
        SET @roleSuperAdmin = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Company Admin' AND [OrganizationId] = @orgId ORDER BY [Id]);
        SET @roleAssetManager = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Asset Manager' AND [OrganizationId] = @orgId ORDER BY [Id]);
        SET @roleProcurement = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Procurement Officer' AND [OrganizationId] = @orgId ORDER BY [Id]);
        SET @roleFinance = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Finance Officer' AND [OrganizationId] = @orgId ORDER BY [Id]);
        SET @roleStaff = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Staff' AND [OrganizationId] = @orgId ORDER BY [Id]);
        SET @roleAuditor = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Auditor' AND [OrganizationId] = @orgId ORDER BY [Id]);
        SET @roleDeptHead = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Department Head' AND [OrganizationId] = @orgId ORDER BY [Id]);
    END
    ELSE
    BEGIN
        SET @deptIt = (SELECT TOP 1 [Id] FROM [Department] WHERE [Code] = N'IT' ORDER BY [Id]);
        SET @deptFin = (SELECT TOP 1 [Id] FROM [Department] WHERE [Code] = N'FIN' ORDER BY [Id]);
        SET @deptHr = (SELECT TOP 1 [Id] FROM [Department] WHERE [Code] = N'HR' ORDER BY [Id]);
        SET @deptOps = (SELECT TOP 1 [Id] FROM [Department] WHERE [Code] = N'OPS' ORDER BY [Id]);
        SET @deptAdmin = (SELECT TOP 1 [Id] FROM [Department] WHERE [Code] = N'ADMIN' ORDER BY [Id]);
        SET @roleSuperAdmin = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Company Admin' ORDER BY [Id]);
        SET @roleAssetManager = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Asset Manager' ORDER BY [Id]);
        SET @roleProcurement = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Procurement Officer' ORDER BY [Id]);
        SET @roleFinance = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Finance Officer' ORDER BY [Id]);
        SET @roleStaff = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Staff' ORDER BY [Id]);
        SET @roleAuditor = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Auditor' ORDER BY [Id]);
        SET @roleDeptHead = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Department Head' ORDER BY [Id]);
    END

    INSERT INTO [Users]
        ([Id],[Email],[EmailConfirmed],[PasswordHash],[SecurityStamp],[PhoneNumberConfirmed],[TwoFactorEnabled],[LockoutEnabled],[AccessFailedCount],[UserName],
         [EmployeeNumber],[FirstName],[LastName],[Phone],[DepartmentId],[PositionTitle],[IsActive],[RoleId],[OrganizationId],[CreatedAt])
    VALUES
    (N'seed-user-001', N'nanosoft@asset.local', 1, @passwordHash, @securityStamp, 0, 0, 0, 0, N'nanosoft@asset.local', N'EMP-0001', N'System', N'Admin', N'+254700001001', @deptIt, N'Company Administrator', 1, @roleSuperAdmin, @orgId, @now),
    (N'seed-user-002', N'assetmanager@asset.local', 1, @passwordHash, @securityStamp, 0, 0, 0, 0, N'assetmanager@asset.local', N'EMP-0002', N'Peter', N'Asset', N'+254700001002', @deptIt, N'Asset Manager', 1, @roleAssetManager, @orgId, @now),
    (N'seed-user-003', N'procurement@asset.local', 1, @passwordHash, @securityStamp, 0, 0, 0, 0, N'procurement@asset.local', N'EMP-0003', N'Ruth', N'Procure', N'+254700001003', @deptOps, N'Procurement Officer', 1, @roleProcurement, @orgId, @now),
    (N'seed-user-004', N'finance@asset.local', 1, @passwordHash, @securityStamp, 0, 0, 0, 0, N'finance@asset.local', N'EMP-0004', N'James', N'Finance', N'+254700001004', @deptFin, N'Finance Officer', 1, @roleFinance, @orgId, @now),
    (N'seed-user-005', N'staff@asset.local', 1, @passwordHash, @securityStamp, 0, 0, 0, 0, N'staff@asset.local', N'EMP-0005', N'Lucy', N'Staff', N'+254700001005', @deptAdmin, N'Administrative Assistant', 1, @roleStaff, @orgId, @now),
    (N'seed-user-006', N'auditor@asset.local', 1, @passwordHash, @securityStamp, 0, 0, 0, 0, N'auditor@asset.local', N'EMP-0006', N'Ian', N'Audit', N'+254700001006', @deptFin, N'Auditor', 1, @roleAuditor, @orgId, @now),
    (N'seed-user-007', N'departmenthead@asset.local', 1, @passwordHash, @securityStamp, 0, 0, 0, 0, N'departmenthead@asset.local', N'EMP-0007', N'Grace', N'Head', N'+254700001007', @deptHr, N'HR Department Head', 1, @roleDeptHead, @orgId, @now),
    (N'seed-user-008', N'itstaff@asset.local', 1, @passwordHash, @securityStamp, 0, 0, 0, 0, N'itstaff@asset.local', N'EMP-0008', N'Samuel', N'Kamau', N'+254700001008', @deptIt, N'IT Support Specialist', 1, @roleStaff, @orgId, @now),
    (N'seed-user-009', N'opsstaff@asset.local', 1, @passwordHash, @securityStamp, 0, 0, 0, 0, N'opsstaff@asset.local', N'EMP-0009', N'Faith', N'Ochieng', N'+254700001009', @deptOps, N'Operations Coordinator', 1, @roleStaff, @orgId, @now),
    (N'seed-user-010', N'labtech@asset.local', 1, @passwordHash, @securityStamp, 0, 0, 0, 0, N'labtech@asset.local', N'EMP-0010', N'Daniel', N'Mwangi', N'+254700001010', @deptOps, N'Laboratory Technician', 1, @roleStaff, @orgId, @now);
END
GO

IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Email] = N'itstaff@asset.local')
BEGIN
    DECLARE @now DATETIME = GETUTCDATE();
    DECLARE @passwordHash NVARCHAR(MAX) = N'ALJwzw5r970vW+fpNg4Ivw5nutwiP9Omge0gCdgtDVM2h6NFmycZ2GwSH5fyBqDTaw==';
    DECLARE @securityStamp NVARCHAR(64) = N'00000000000000000000000000000001';
    DECLARE @orgId INT = (SELECT TOP 1 [Id] FROM [Organization] WHERE [Slug] IN (N'nanosoft', N'default') ORDER BY CASE WHEN [Slug] = N'nanosoft' THEN 0 ELSE 1 END, [Id]);
    IF @orgId IS NULL
        SET @orgId = (SELECT TOP 1 [Id] FROM [Organization] ORDER BY [Id]);

    DECLARE @deptIt INT;
    DECLARE @deptOps INT;
    DECLARE @roleStaff INT;
    IF COL_LENGTH(N'[Department]', N'OrganizationId') IS NOT NULL AND @orgId IS NOT NULL
    BEGIN
        SET @deptIt = (SELECT TOP 1 [Id] FROM [Department] WHERE [Code] = N'IT' AND [OrganizationId] = @orgId ORDER BY [Id]);
        SET @deptOps = (SELECT TOP 1 [Id] FROM [Department] WHERE [Code] = N'OPS' AND [OrganizationId] = @orgId ORDER BY [Id]);
        SET @roleStaff = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Staff' AND [OrganizationId] = @orgId ORDER BY [Id]);
    END
    ELSE
    BEGIN
        SET @deptIt = (SELECT TOP 1 [Id] FROM [Department] WHERE [Code] = N'IT' ORDER BY [Id]);
        SET @deptOps = (SELECT TOP 1 [Id] FROM [Department] WHERE [Code] = N'OPS' ORDER BY [Id]);
        SET @roleStaff = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Staff' ORDER BY [Id]);
    END

    INSERT INTO [Users]
        ([Id],[Email],[EmailConfirmed],[PasswordHash],[SecurityStamp],[PhoneNumberConfirmed],[TwoFactorEnabled],[LockoutEnabled],[AccessFailedCount],[UserName],
         [EmployeeNumber],[FirstName],[LastName],[Phone],[DepartmentId],[PositionTitle],[IsActive],[RoleId],[OrganizationId],[CreatedAt])
    VALUES
    (N'seed-user-008', N'itstaff@asset.local', 1, @passwordHash, @securityStamp, 0, 0, 0, 0, N'itstaff@asset.local', N'EMP-0008', N'Samuel', N'Kamau', N'+254700001008', @deptIt, N'IT Support Specialist', 1, @roleStaff, @orgId, @now),
    (N'seed-user-009', N'opsstaff@asset.local', 1, @passwordHash, @securityStamp, 0, 0, 0, 0, N'opsstaff@asset.local', N'EMP-0009', N'Faith', N'Ochieng', N'+254700001009', @deptOps, N'Operations Coordinator', 1, @roleStaff, @orgId, @now),
    (N'seed-user-010', N'labtech@asset.local', 1, @passwordHash, @securityStamp, 0, 0, 0, 0, N'labtech@asset.local', N'EMP-0010', N'Daniel', N'Mwangi', N'+254700001010', @deptOps, N'Laboratory Technician', 1, @roleStaff, @orgId, @now);
END
GO

-- Diverse demo assets: database/scripts/004_Migrations/017_DiverseDemoAssets.sql
-- Second demo organization (tenant B): database/scripts/004_Migrations/013_SecondDemoOrg.sql
