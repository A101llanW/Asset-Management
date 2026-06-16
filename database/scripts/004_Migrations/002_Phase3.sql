IF OBJECT_ID(N'[WebhookSubscription]', N'U') IS NULL
BEGIN
    CREATE TABLE [WebhookSubscription] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [EventType] NVARCHAR(100) NOT NULL,
        [TargetUrl] NVARCHAR(500) NOT NULL,
        [Secret] NVARCHAR(200) NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_WebhookSubscription_IsActive DEFAULT(1),
        [CreatedByUserId] NVARCHAR(128) NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL
    );
END
GO

IF OBJECT_ID(N'[PasswordResetToken]', N'U') IS NULL
BEGIN
    CREATE TABLE [PasswordResetToken] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [UserId] NVARCHAR(128) NOT NULL,
        [TokenHash] NVARCHAR(128) NOT NULL,
        [ExpiresAtUtc] DATETIME NOT NULL,
        [CreatedAtUtc] DATETIME NOT NULL,
        [UsedAtUtc] DATETIME NULL,
        CONSTRAINT FK_PasswordResetToken_User FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM [Permission] WHERE [Code] = N'Api.Assets.Read')
BEGIN
    DECLARE @now DATETIME = GETUTCDATE();

    INSERT INTO [Permission] ([Name],[Code],[Module],[Description],[CreatedAt],[IsActive]) VALUES
    (N'API Read Assets', N'Api.Assets.Read', N'Api', N'Read assets via versioned API', @now, 1),
    (N'API Write Assets', N'Api.Assets.Write', N'Api', N'Write assets via versioned API', @now, 1),
    (N'API Manage Webhooks', N'Api.Webhooks.Manage', N'Api', N'Manage outbound webhook subscriptions', @now, 1),
    (N'View Insurance Policies', N'Insurance.View', N'Insurance', N'View insurance policies', @now, 1),
    (N'Manage Insurance Policies', N'Insurance.Manage', N'Insurance', N'Create and edit insurance policies', @now, 1),
    (N'Export Audit Logs', N'AuditLogs.Export', N'Audit', N'Export audit logs to CSV', @now, 1);
END
GO

DECLARE @apiReadId INT = (SELECT [Id] FROM [Permission] WHERE [Code] = N'Api.Assets.Read');

IF @apiReadId IS NOT NULL
BEGIN
    INSERT INTO [RolePermission] ([RoleId],[PermissionId])
    SELECT r.[Id], @apiReadId FROM [Roles] r
    WHERE r.[Name] IN (N'Super Admin', N'Asset Manager', N'Auditor')
      AND NOT EXISTS (SELECT 1 FROM [RolePermission] rp WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = @apiReadId);
END
GO

DECLARE @apiWriteId INT = (SELECT [Id] FROM [Permission] WHERE [Code] = N'Api.Assets.Write');

IF @apiWriteId IS NOT NULL
BEGIN
    INSERT INTO [RolePermission] ([RoleId],[PermissionId])
    SELECT r.[Id], @apiWriteId FROM [Roles] r
    WHERE r.[Name] IN (N'Super Admin', N'Asset Manager')
      AND NOT EXISTS (SELECT 1 FROM [RolePermission] rp WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = @apiWriteId);
END
GO

DECLARE @apiWebhookId INT = (SELECT [Id] FROM [Permission] WHERE [Code] = N'Api.Webhooks.Manage');

IF @apiWebhookId IS NOT NULL
BEGIN
    INSERT INTO [RolePermission] ([RoleId],[PermissionId])
    SELECT r.[Id], @apiWebhookId FROM [Roles] r
    WHERE r.[Name] = N'Super Admin'
      AND NOT EXISTS (SELECT 1 FROM [RolePermission] rp WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = @apiWebhookId);
END
GO

DECLARE @insViewId INT = (SELECT [Id] FROM [Permission] WHERE [Code] = N'Insurance.View');

IF @insViewId IS NOT NULL
BEGIN
    INSERT INTO [RolePermission] ([RoleId],[PermissionId])
    SELECT r.[Id], @insViewId FROM [Roles] r
    WHERE r.[Name] IN (N'Super Admin', N'Asset Manager', N'Finance Officer', N'Auditor')
      AND NOT EXISTS (SELECT 1 FROM [RolePermission] rp WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = @insViewId);
END
GO

DECLARE @insManageId INT = (SELECT [Id] FROM [Permission] WHERE [Code] = N'Insurance.Manage');

IF @insManageId IS NOT NULL
BEGIN
    INSERT INTO [RolePermission] ([RoleId],[PermissionId])
    SELECT r.[Id], @insManageId FROM [Roles] r
    WHERE r.[Name] IN (N'Super Admin', N'Asset Manager', N'Finance Officer')
      AND NOT EXISTS (SELECT 1 FROM [RolePermission] rp WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = @insManageId);
END
GO

DECLARE @auditExportId INT = (SELECT [Id] FROM [Permission] WHERE [Code] = N'AuditLogs.Export');

IF @auditExportId IS NOT NULL
BEGIN
    INSERT INTO [RolePermission] ([RoleId],[PermissionId])
    SELECT r.[Id], @auditExportId FROM [Roles] r
    WHERE r.[Name] IN (N'Super Admin', N'Auditor')
      AND NOT EXISTS (SELECT 1 FROM [RolePermission] rp WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = @auditExportId);
END
GO

IF NOT EXISTS (SELECT 1 FROM [SystemSetting] WHERE [SettingKey] = N'Finance.DefaultCurrency')
BEGIN
    DECLARE @now DATETIME = GETUTCDATE();
    INSERT INTO [SystemSetting] ([SettingKey],[SettingValue],[Description],[CreatedAt],[IsActive]) VALUES
    (N'Finance.DefaultCurrency', N'KES', N'Default finance currency code.', @now, 1);
END
GO
