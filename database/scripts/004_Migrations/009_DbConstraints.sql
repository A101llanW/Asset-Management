-- Phase 5c: NOT NULL OrganizationId, FK to Organization, composite uniques per tenant

DECLARE @defaultOrgId INT = (SELECT TOP 1 [Id] FROM [Organization] ORDER BY [Id]);
IF @defaultOrgId IS NULL
BEGIN
    RAISERROR('Cannot apply tenant constraints: no Organization row exists.', 16, 1);
    RETURN;
END
GO

-- Users excluded from NOT NULL: platform admin accounts keep OrganizationId NULL.
DECLARE @tenantTables TABLE (Name NVARCHAR(128), RequireNotNull BIT NOT NULL DEFAULT(1));
INSERT INTO @tenantTables (Name, RequireNotNull) VALUES
(N'Users', 0),(N'Roles', 0),(N'RolePermission', 0),(N'Department', 1),(N'Supplier', 1),(N'AssetCategory', 1),(N'AssetType', 1),
(N'Asset', 1),(N'AssetRequest', 1),(N'AssetDocument', 1),(N'PurchaseRequest', 1),(N'PurchaseApprovalAction', 1),(N'PurchaseRecord', 1),
(N'AssetReceiving', 1),(N'AssetAssignment', 1),(N'AssetTransfer', 1),(N'TransferApprovalAction', 1),(N'AssetReturn', 1),
(N'AssetCustodyEvent', 1),(N'AssetMaintenanceRecord', 1),(N'AssetIncident', 1),(N'InsurancePolicy', 1),(N'InsuranceClaim', 1),
(N'DepreciationRecord', 1),(N'DisposalRecord', 1),(N'DisposalApprovalAction', 1),(N'Notification', 1),(N'AuditLog', 1),
(N'SystemSetting', 1),(N'WebhookSubscription', 1);

DECLARE @defaultOrgId INT = (SELECT TOP 1 [Id] FROM [Organization] ORDER BY [Id]);
DECLARE @tbl NVARCHAR(128);
DECLARE @requireNotNull BIT;
DECLARE @sql NVARCHAR(MAX);
DECLARE table_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT Name, RequireNotNull FROM @tenantTables;
OPEN table_cursor;
FETCH NEXT FROM table_cursor INTO @tbl, @requireNotNull;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'[' + @tbl + N']', N'U') IS NOT NULL AND COL_LENGTH(@tbl, N'OrganizationId') IS NOT NULL
    BEGIN
        IF @requireNotNull = 1
        BEGIN
            SET @sql = N'UPDATE [' + @tbl + N'] SET [OrganizationId] = @OrgId WHERE [OrganizationId] IS NULL;';
            EXEC sp_executesql @sql, N'@OrgId INT', @OrgId = @defaultOrgId;
        END

        IF NOT EXISTS (
            SELECT 1 FROM sys.foreign_keys
            WHERE name = N'FK_' + @tbl + N'_Organization' AND parent_object_id = OBJECT_ID(N'[' + @tbl + N']'))
        BEGIN
            SET @sql = N'ALTER TABLE [' + @tbl + N'] ADD CONSTRAINT FK_' + @tbl + N'_Organization FOREIGN KEY ([OrganizationId]) REFERENCES [Organization]([Id]);';
            EXEC sp_executesql @sql;
        END

        IF @requireNotNull = 1 AND EXISTS (
            SELECT 1 FROM sys.columns
            WHERE object_id = OBJECT_ID(N'[' + @tbl + N']')
              AND name = N'OrganizationId'
              AND is_nullable = 1)
        BEGIN
            SET @sql = N'';
            SELECT @sql = @sql + N'DROP INDEX [' + i.name + N'] ON [' + @tbl + N'];'
            FROM sys.indexes i
            WHERE i.object_id = OBJECT_ID(N'[' + @tbl + N']')
              AND i.type > 0
              AND i.is_primary_key = 0
              AND EXISTS (
                  SELECT 1
                  FROM sys.index_columns ic
                  INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                  WHERE ic.object_id = i.object_id
                    AND ic.index_id = i.index_id
                    AND c.name = N'OrganizationId');
            IF LEN(@sql) > 0
            BEGIN
                EXEC sp_executesql @sql;
            END

            SET @sql = N'ALTER TABLE [' + @tbl + N'] ALTER COLUMN [OrganizationId] INT NOT NULL;';
            EXEC sp_executesql @sql;
        END
    END
    FETCH NEXT FROM table_cursor INTO @tbl, @requireNotNull;
END
CLOSE table_cursor;
DEALLOCATE table_cursor;
GO

-- Users: platform accounts keep NULL OrganizationId; tenant users require org + composite email unique
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_OrganizationId_Email' AND object_id = OBJECT_ID(N'[Users]'))
BEGIN
    DROP INDEX IX_Users_OrganizationId_Email ON [Users];
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_OrganizationId_Email' AND object_id = OBJECT_ID(N'[Users]'))
BEGIN
    CREATE UNIQUE INDEX IX_Users_OrganizationId_Email ON [Users]([OrganizationId], [Email]) WHERE [OrganizationId] IS NOT NULL AND [Email] IS NOT NULL;
END
GO

-- Asset: per-tenant asset tag uniqueness (replace global IX_AssetTag if present)
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetTag' AND object_id = OBJECT_ID(N'[Asset]'))
BEGIN
    DROP INDEX IX_AssetTag ON [Asset];
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Asset_Org_AssetTag' AND object_id = OBJECT_ID(N'[Asset]'))
BEGIN
    CREATE UNIQUE INDEX IX_Asset_Org_AssetTag ON [Asset]([OrganizationId], [AssetTag]) WHERE [AssetTag] IS NOT NULL;
END
GO

-- Department codes unique within tenant (replace global IX_Department_Code if present)
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Department_Code' AND object_id = OBJECT_ID(N'[Department]'))
BEGIN
    DROP INDEX IX_Department_Code ON [Department];
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Department_Org_Code_Unique' AND object_id = OBJECT_ID(N'[Department]'))
BEGIN
    CREATE UNIQUE INDEX IX_Department_Org_Code_Unique ON [Department]([OrganizationId], [Code]) WHERE [Code] IS NOT NULL;
END
GO

-- Role names unique within tenant (platform roles remain NULL org; requires Roles.Name NVARCHAR(200), not MAX)
IF OBJECT_ID(N'[Roles]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Roles_Org_Name' AND object_id = OBJECT_ID(N'[Roles]'))
   AND NOT EXISTS (
       SELECT 1 FROM sys.columns c INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
       WHERE c.object_id = OBJECT_ID(N'[Roles]') AND c.name = N'Name' AND t.name IN (N'nvarchar', N'varchar') AND c.max_length = -1)
BEGIN
    CREATE UNIQUE INDEX IX_Roles_Org_Name ON [Roles]([OrganizationId], [Name]) WHERE [OrganizationId] IS NOT NULL AND [Name] IS NOT NULL;
END
GO

-- Recreate performance indexes dropped for OrganizationId NOT NULL migration
IF OBJECT_ID(N'[Asset]', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Asset_Org_IsActive_Dept_List' AND object_id = OBJECT_ID(N'[Asset]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Asset_Org_IsActive_Dept_List
        ON [Asset]([OrganizationId], [IsActive], [DepartmentId])
        INCLUDE ([AssetTag], [AssetName], [CurrentStatus], [CurrentBookValue], [CurrentCustodianId], [CategoryId]);
END
GO

IF OBJECT_ID(N'[Asset]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Asset_Org_AssetName' AND object_id = OBJECT_ID(N'[Asset]'))
   AND NOT EXISTS (
       SELECT 1 FROM sys.columns c INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
       WHERE c.object_id = OBJECT_ID(N'[Asset]') AND c.name = N'AssetName' AND t.name IN (N'nvarchar', N'varchar') AND c.max_length = -1)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Asset_Org_AssetName ON [Asset]([OrganizationId], [AssetName]);
END
GO

IF OBJECT_ID(N'[Notification]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notification_User_Status_CreatedAt' AND object_id = OBJECT_ID(N'[Notification]'))
   AND NOT EXISTS (
       SELECT 1 FROM sys.columns c INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
       WHERE c.object_id = OBJECT_ID(N'Notification') AND c.name = N'UserId' AND t.name IN (N'nvarchar', N'varchar') AND c.max_length = -1)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Notification_User_Status_CreatedAt
        ON [Notification]([UserId], [Status], [CreatedAt] DESC)
        INCLUDE ([Subject], [LinkUrl], [Type], [Message]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLog_Org_Timestamp' AND object_id = OBJECT_ID(N'[AuditLog]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_AuditLog_Org_Timestamp
        ON [AuditLog]([OrganizationId], [Timestamp] DESC)
        INCLUDE ([EntityType], [EntityId], [Action], [ActorUserId]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetTransfer_Org_ApprovalStatus' AND object_id = OBJECT_ID(N'[AssetTransfer]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_AssetTransfer_Org_ApprovalStatus
        ON [AssetTransfer]([OrganizationId], [ApprovalStatus])
        INCLUDE ([AssetId], [TransferDate], [CreatedAt]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DisposalRecord_Org_ApprovalStatus' AND object_id = OBJECT_ID(N'[DisposalRecord]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_DisposalRecord_Org_ApprovalStatus
        ON [DisposalRecord]([OrganizationId], [ApprovalStatus])
        INCLUDE ([AssetId], [DisposalRequestDate], [CreatedAt]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PurchaseRequest_Org_ApprovalStatus' AND object_id = OBJECT_ID(N'[PurchaseRequest]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_PurchaseRequest_Org_ApprovalStatus
        ON [PurchaseRequest]([OrganizationId], [ApprovalStatus])
        INCLUDE ([Id], [CreatedAt], [DepartmentId]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Department_Org_Code' AND object_id = OBJECT_ID(N'[Department]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Department_Org_Code ON [Department]([OrganizationId], [Code]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_Org_IsActive' AND object_id = OBJECT_ID(N'[Users]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Users_Org_IsActive
        ON [Users]([OrganizationId], [IsActive])
        INCLUDE ([FirstName], [LastName], [Email], [DepartmentId], [RoleId]);
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
