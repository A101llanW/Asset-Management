-- Incremental updates for databases created before AssetRequest support.

IF OBJECT_ID(N'[AssetRequest]', N'U') IS NULL
BEGIN
    CREATE TABLE [AssetRequest] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [RequestedById] NVARCHAR(128) NOT NULL,
        [DepartmentId] INT NULL,
        [CategoryId] INT NULL,
        [RequestedAssetTag] NVARCHAR(60) NULL,
        [Justification] NVARCHAR(MAX) NULL,
        [Status] INT NOT NULL,
        [FulfilledAssetId] INT NULL,
        [ReviewedById] NVARCHAR(128) NULL,
        [ReviewedAt] DATETIME NULL,
        [ReviewNotes] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_AssetRequest_IsActive DEFAULT(1),
        CONSTRAINT FK_AssetRequest_Department FOREIGN KEY ([DepartmentId]) REFERENCES [Department]([Id]),
        CONSTRAINT FK_AssetRequest_Category FOREIGN KEY ([CategoryId]) REFERENCES [AssetCategory]([Id]),
        CONSTRAINT FK_AssetRequest_FulfilledAsset FOREIGN KEY ([FulfilledAssetId]) REFERENCES [Asset]([Id])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM [Permission] WHERE [Code] = N'Assets.Request')
BEGIN
    INSERT INTO [Permission] ([Name],[Code],[Module],[Description],[CreatedAt],[IsActive])
    VALUES (N'Request Assets', N'Assets.Request', N'Assets', N'Can submit asset requests', GETUTCDATE(), 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM [Permission] WHERE [Code] = N'Assets.Request.Approve')
BEGIN
    INSERT INTO [Permission] ([Name],[Code],[Module],[Description],[CreatedAt],[IsActive])
    VALUES (N'Approve Asset Requests', N'Assets.Request.Approve', N'Assets', N'Can approve and fulfill asset requests', GETUTCDATE(), 1);
END
GO

DECLARE @requestPermissionId INT = (SELECT [Id] FROM [Permission] WHERE [Code] = N'Assets.Request');

IF @requestPermissionId IS NOT NULL
BEGIN
    INSERT INTO [RolePermission] ([RoleId],[PermissionId])
    SELECT r.[Id], @requestPermissionId
    FROM [Roles] r
    WHERE r.[Name] IN (N'Super Admin', N'Asset Manager', N'Department Head')
      AND NOT EXISTS (
          SELECT 1 FROM [RolePermission] rp
          WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = @requestPermissionId);
END
GO

DECLARE @approvePermissionId INT = (SELECT [Id] FROM [Permission] WHERE [Code] = N'Assets.Request.Approve');

IF @approvePermissionId IS NOT NULL
BEGIN
    INSERT INTO [RolePermission] ([RoleId],[PermissionId])
    SELECT r.[Id], @approvePermissionId
    FROM [Roles] r
    WHERE r.[Name] IN (N'Super Admin', N'Asset Manager', N'Department Head')
      AND NOT EXISTS (
          SELECT 1 FROM [RolePermission] rp
          WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = @approvePermissionId);
END
GO
