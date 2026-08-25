-- Phase 5c: covering indexes aligned to hot-path query patterns

-- Asset list filter/sort/page
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Asset_Org_IsActive_Dept_List' AND object_id = OBJECT_ID(N'[Asset]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Asset_Org_IsActive_Dept_List
        ON [Asset]([OrganizationId], [IsActive], [DepartmentId])
        INCLUDE ([AssetTag], [AssetName], [CurrentStatus], [CurrentBookValue], [CurrentCustodianId], [CategoryId]);
END
GO

-- Asset prefix name search within tenant (requires AssetName NVARCHAR(200), not MAX)
IF OBJECT_ID(N'[Asset]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Asset_Org_AssetName' AND object_id = OBJECT_ID(N'[Asset]'))
   AND NOT EXISTS (
       SELECT 1
       FROM sys.columns c
       INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
       WHERE c.object_id = OBJECT_ID(N'[Asset]')
         AND c.name = N'AssetName'
         AND t.name IN (N'nvarchar', N'varchar')
         AND c.max_length = -1
   )
BEGIN
    CREATE NONCLUSTERED INDEX IX_Asset_Org_AssetName
        ON [Asset]([OrganizationId], [AssetName]);
END
GO

-- Notification inbox (requires UserId NVARCHAR(128), not MAX)
IF OBJECT_ID(N'[Notification]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notification_User_Status_CreatedAt' AND object_id = OBJECT_ID(N'[Notification]'))
   AND NOT EXISTS (
       SELECT 1
       FROM sys.columns c
       INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
       WHERE c.object_id = OBJECT_ID(N'Notification')
         AND c.name = N'UserId'
         AND t.name IN (N'nvarchar', N'varchar')
         AND c.max_length = -1
   )
BEGIN
    CREATE NONCLUSTERED INDEX IX_Notification_User_Status_CreatedAt
        ON [Notification]([UserId], [Status], [CreatedAt] DESC)
        INCLUDE ([Subject], [LinkUrl], [Type], [Message]);
END
GO

-- Audit log tenant timeline
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLog_Org_Timestamp' AND object_id = OBJECT_ID(N'[AuditLog]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_AuditLog_Org_Timestamp
        ON [AuditLog]([OrganizationId], [Timestamp] DESC)
        INCLUDE ([EntityType], [EntityId], [Action], [ActorUserId]);
END
GO

-- Pending approval inboxes
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

-- Assignment history by asset
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetAssignment_Asset_AssignedDate' AND object_id = OBJECT_ID(N'[AssetAssignment]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_AssetAssignment_Asset_AssignedDate
        ON [AssetAssignment]([AssetId], [AssignedDate] DESC);
END
GO

-- Organization tenant routing
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Organization_Slug' AND object_id = OBJECT_ID(N'[Organization]'))
BEGIN
    CREATE UNIQUE INDEX IX_Organization_Slug ON [Organization]([Slug]) WHERE [Slug] IS NOT NULL;
END
GO

-- Department scoped lookups within tenant
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Department_Org_Code' AND object_id = OBJECT_ID(N'[Department]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Department_Org_Code ON [Department]([OrganizationId], [Code]);
END
GO

-- Users dropdown / org user lists
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_Org_IsActive' AND object_id = OBJECT_ID(N'[Users]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Users_Org_IsActive
        ON [Users]([OrganizationId], [IsActive])
        INCLUDE ([FirstName], [LastName], [Email], [DepartmentId], [RoleId]);
END
GO
