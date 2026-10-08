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

-- Role -> permission grants for the Phase 3 permissions.
-- RolePermission.OrganizationId is taken from the role (JOIN [Roles]) so a grant can never
-- be stamped with another org (see artifacts/roleperm-org-fix-2026-10-08). Idempotent:
-- a grant is skipped when (RoleId, PermissionId) already exists. The unique index
-- IX_RolePermission_Role_Permission is on (RoleId, PermissionId) and a role belongs to
-- exactly one org, so this also covers (OrganizationId, RoleId, PermissionId).
-- On a fresh install this script runs before 005 adds the OrganizationId columns, so the
-- org-aware insert is dynamic SQL and only used when both columns exist.
DECLARE @grants TABLE ([PermissionCode] NVARCHAR(100) NOT NULL, [RoleName] NVARCHAR(256) NOT NULL);
INSERT INTO @grants ([PermissionCode],[RoleName]) VALUES
(N'Api.Assets.Read', N'Super Admin'), (N'Api.Assets.Read', N'Asset Manager'), (N'Api.Assets.Read', N'Auditor'),
(N'Api.Assets.Write', N'Super Admin'), (N'Api.Assets.Write', N'Asset Manager'),
(N'Api.Webhooks.Manage', N'Super Admin'),
(N'Insurance.View', N'Super Admin'), (N'Insurance.View', N'Asset Manager'), (N'Insurance.View', N'Finance Officer'), (N'Insurance.View', N'Auditor'),
(N'Insurance.Manage', N'Super Admin'), (N'Insurance.Manage', N'Asset Manager'), (N'Insurance.Manage', N'Finance Officer'),
(N'AuditLogs.Export', N'Super Admin'), (N'AuditLogs.Export', N'Auditor');

DECLARE @pairs TABLE ([RoleId] INT NOT NULL, [PermissionId] INT NOT NULL, PRIMARY KEY ([RoleId],[PermissionId]));
INSERT INTO @pairs ([RoleId],[PermissionId])
SELECT DISTINCT r.[Id], p.[Id]
FROM @grants g
INNER JOIN [Permission] p ON p.[Code] = g.[PermissionCode]
INNER JOIN [Roles] r ON r.[Name] = g.[RoleName];

IF COL_LENGTH(N'[RolePermission]', N'OrganizationId') IS NOT NULL
   AND COL_LENGTH(N'[Roles]', N'OrganizationId') IS NOT NULL
BEGIN
    -- table variables are not visible inside sp_executesql, so pass the pairs as a temp table
    IF OBJECT_ID(N'tempdb..#Phase3Grants') IS NOT NULL DROP TABLE #Phase3Grants;
    SELECT [RoleId],[PermissionId] INTO #Phase3Grants FROM @pairs;

    EXEC sp_executesql N'
    INSERT INTO [RolePermission] ([RoleId],[PermissionId],[OrganizationId])
    SELECT g.[RoleId], g.[PermissionId], r.[OrganizationId]
    FROM #Phase3Grants g
    INNER JOIN [Roles] r ON r.[Id] = g.[RoleId]
    WHERE NOT EXISTS (SELECT 1 FROM [RolePermission] rp
                      WHERE rp.[RoleId] = g.[RoleId] AND rp.[PermissionId] = g.[PermissionId]);';

    DROP TABLE #Phase3Grants;
END
ELSE
BEGIN
    -- pre-multitenancy schema: no org columns yet; 005 assigns the org afterwards
    INSERT INTO [RolePermission] ([RoleId],[PermissionId])
    SELECT g.[RoleId], g.[PermissionId]
    FROM @pairs g
    WHERE NOT EXISTS (SELECT 1 FROM [RolePermission] rp
                      WHERE rp.[RoleId] = g.[RoleId] AND rp.[PermissionId] = g.[PermissionId]);
END
GO

IF NOT EXISTS (SELECT 1 FROM [SystemSetting] WHERE [SettingKey] = N'Finance.DefaultCurrency')
BEGIN
    DECLARE @now DATETIME = GETUTCDATE();
    INSERT INTO [SystemSetting] ([SettingKey],[SettingValue],[Description],[CreatedAt],[IsActive]) VALUES
    (N'Finance.DefaultCurrency', N'KES', N'Default finance currency code.', @now, 1);
END
GO
