-- Multitenancy: OrganizationId columns, ImpersonationRequest, platform admin seed
-- Extend Organization with slug and status
IF COL_LENGTH(N'[Organization]', N'Slug') IS NULL
BEGIN
    ALTER TABLE [Organization] ADD [Slug] NVARCHAR(50) NULL;
END
GO

IF COL_LENGTH(N'[Organization]', N'Status') IS NULL
BEGIN
    ALTER TABLE [Organization] ADD [Status] NVARCHAR(20) NOT NULL CONSTRAINT DF_Organization_Status DEFAULT(N'Active');
END
GO

-- Tenant tables: add OrganizationId
DECLARE @tables TABLE (Name NVARCHAR(128));
INSERT INTO @tables (Name) VALUES
(N'Users'),(N'Roles'),(N'RolePermission'),(N'Department'),(N'Supplier'),(N'AssetCategory'),(N'AssetType'),
(N'Asset'),(N'AssetRequest'),(N'AssetDocument'),(N'PurchaseRequest'),(N'PurchaseApprovalAction'),(N'PurchaseRecord'),
(N'AssetReceiving'),(N'AssetAssignment'),(N'AssetTransfer'),(N'TransferApprovalAction'),(N'AssetReturn'),
(N'AssetCustodyEvent'),(N'AssetMaintenanceRecord'),(N'AssetIncident'),(N'InsurancePolicy'),(N'InsuranceClaim'),
(N'DepreciationRecord'),(N'DisposalRecord'),(N'DisposalApprovalAction'),(N'Notification'),(N'AuditLog'),
(N'SystemSetting'),(N'WebhookSubscription');

DECLARE @tbl NVARCHAR(128);
DECLARE @sql NVARCHAR(MAX);
DECLARE table_cursor CURSOR FOR SELECT Name FROM @tables;
OPEN table_cursor;
FETCH NEXT FROM table_cursor INTO @tbl;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF COL_LENGTH(@tbl, N'OrganizationId') IS NULL
    BEGIN
        SET @sql = N'ALTER TABLE [' + @tbl + N'] ADD [OrganizationId] INT NULL;';
        EXEC sp_executesql @sql;
    END
    FETCH NEXT FROM table_cursor INTO @tbl;
END
CLOSE table_cursor;
DEALLOCATE table_cursor;
GO

-- ImpersonationRequest table
IF OBJECT_ID(N'[ImpersonationRequest]', N'U') IS NULL
BEGIN
    CREATE TABLE [ImpersonationRequest] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [OrganizationId] INT NOT NULL,
        [RequestedBy] NVARCHAR(256) NOT NULL,
        [RequestedFrom] NVARCHAR(256) NOT NULL,
        [RequestDate] DATETIME NOT NULL,
        [Status] INT NOT NULL,
        [Reason] NVARCHAR(MAX) NULL,
        [DecisionDate] DATETIME NULL,
        [AdminNotes] NVARCHAR(MAX) NULL,
        [ExpiryDate] DATETIME NULL,
        [CreatedAt] DATETIME NOT NULL CONSTRAINT DF_ImpersonationRequest_CreatedAt DEFAULT(GETUTCDATE()),
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_ImpersonationRequest_IsActive DEFAULT(1),
        CONSTRAINT FK_ImpersonationRequest_Organization FOREIGN KEY ([OrganizationId]) REFERENCES [Organization]([Id])
    );
END
GO

-- AuditLog: optional impersonation link
IF COL_LENGTH(N'[AuditLog]', N'ImpersonationRequestId') IS NULL
BEGIN
    ALTER TABLE [AuditLog] ADD [ImpersonationRequestId] INT NULL;
END
GO

-- Default organization
IF NOT EXISTS (SELECT 1 FROM [Organization])
BEGIN
    INSERT INTO [Organization] ([Name],[Code],[Slug],[Status],[Email],[CurrencyCode],[CreatedAt],[IsActive])
    VALUES (N'Nanosoft', N'NANOSOFT', N'nanosoft', N'Active', N'admin@nanosoft.asset.local', N'KES', GETUTCDATE(), 1);
END
GO

DECLARE @defaultOrgId INT = (SELECT TOP 1 [Id] FROM [Organization] ORDER BY [Id]);
IF @defaultOrgId IS NOT NULL
BEGIN
    UPDATE [Organization] SET [Slug] = N'nanosoft', [Status] = N'Active' WHERE [Id] = @defaultOrgId AND ([Slug] IS NULL OR [Slug] = N'' OR [Slug] = N'default');

    UPDATE [Users] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [Roles] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL AND [Name] <> N'Platform Admin';
    UPDATE [RolePermission] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [Department] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [Supplier] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [AssetCategory] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [AssetType] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [Asset] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [AssetRequest] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [AssetDocument] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [PurchaseRequest] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [PurchaseApprovalAction] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [PurchaseRecord] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [AssetReceiving] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [AssetAssignment] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [AssetTransfer] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [TransferApprovalAction] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [AssetReturn] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [AssetCustodyEvent] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [AssetMaintenanceRecord] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [AssetIncident] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [InsurancePolicy] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [InsuranceClaim] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [DepreciationRecord] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [DisposalRecord] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [DisposalApprovalAction] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [Notification] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [AuditLog] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    UPDATE [SystemSetting] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
    IF OBJECT_ID(N'[WebhookSubscription]', N'U') IS NOT NULL
        UPDATE [WebhookSubscription] SET [OrganizationId] = @defaultOrgId WHERE [OrganizationId] IS NULL;
END
GO

-- Rename Super Admin role to Company Admin
UPDATE [Roles] SET [Name] = N'Company Admin', [Description] = N'Tenant-wide company administrator' WHERE [Name] = N'Super Admin';
GO

-- Platform permissions
IF NOT EXISTS (SELECT 1 FROM [Permission] WHERE [Code] = N'Platform.Organizations.View')
BEGIN
    DECLARE @now2 DATETIME = GETUTCDATE();
    INSERT INTO [Permission] ([Name],[Code],[Module],[Description],[CreatedAt],[IsActive]) VALUES
    (N'View Organizations', N'Platform.Organizations.View', N'Platform', N'View organization list', @now2, 1),
    (N'Manage Organizations', N'Platform.Organizations.Manage', N'Platform', N'Create and manage organizations', @now2, 1),
    (N'Impersonate Support', N'Platform.Support.Impersonate', N'Platform', N'Request elevation into tenant context', @now2, 1);
END
GO

-- Platform Admin role
IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [Name] = N'Platform Admin' AND [OrganizationId] IS NULL)
BEGIN
    DECLARE @now3 DATETIME = GETUTCDATE();
    INSERT INTO [Roles] ([Name],[Description],[IsSystemRole],[OrganizationId],[CreatedAt],[IsActive])
    VALUES (N'Platform Admin', N'Global platform administrator', 1, NULL, @now3, 1);

    DECLARE @platformRoleId INT = SCOPE_IDENTITY();
    INSERT INTO [RolePermission] ([RoleId],[PermissionId],[OrganizationId])
    SELECT @platformRoleId, [Id], NULL FROM [Permission] WHERE [Code] LIKE N'Platform.%';
END
GO

-- Platform admin user (OrganizationId NULL)
IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Email] = N'superadmin@asset.local' AND [OrganizationId] IS NULL)
BEGIN
    DECLARE @now4 DATETIME = GETUTCDATE();
    DECLARE @passwordHash NVARCHAR(MAX) = N'ALJwzw5r970vW+fpNg4Ivw5nutwiP9Omge0gCdgtDVM2h6NFmycZ2GwSH5fyBqDTaw==';
    DECLARE @securityStamp NVARCHAR(64) = N'00000000000000000000000000000002';
    DECLARE @platformRoleId2 INT = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Platform Admin' ORDER BY [Id]);

    INSERT INTO [Users]
        ([Id],[Email],[EmailConfirmed],[PasswordHash],[SecurityStamp],[PhoneNumberConfirmed],[TwoFactorEnabled],[LockoutEnabled],[AccessFailedCount],[UserName],
         [EmployeeNumber],[FirstName],[LastName],[IsActive],[RoleId],[OrganizationId],[CreatedAt])
    VALUES
        (N'seed-user-platform', N'superadmin@asset.local', 1, @passwordHash, @securityStamp, 0, 0, 0, 0, N'superadmin@asset.local',
         N'EMP-PLATFORM', N'Platform', N'Admin', 1, @platformRoleId2, NULL, @now4);
END
GO

-- Default-tenant company admin is seeded in 002_Seed/002_DemoData.sql as nanosoft@asset.local.

-- Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_OrganizationId_Email' AND object_id = OBJECT_ID(N'[Users]'))
BEGIN
    CREATE UNIQUE INDEX IX_Users_OrganizationId_Email ON [Users]([OrganizationId], [Email]) WHERE [Email] IS NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Asset_OrganizationId' AND object_id = OBJECT_ID(N'[Asset]'))
BEGIN
    CREATE INDEX IX_Asset_OrganizationId ON [Asset]([OrganizationId]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ImpersonationRequest_OrganizationId_Status' AND object_id = OBJECT_ID(N'[ImpersonationRequest]'))
BEGIN
    CREATE INDEX IX_ImpersonationRequest_OrganizationId_Status ON [ImpersonationRequest]([OrganizationId], [Status]);
END
GO
