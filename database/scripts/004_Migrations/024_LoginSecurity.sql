-- Login attempts, security logs permission, platform SMTP setting keys (HireHub pattern)

IF OBJECT_ID(N'[LoginAttempts]', N'U') IS NULL
BEGIN
    CREATE TABLE [LoginAttempts] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Username] NVARCHAR(256) NOT NULL,
        [IpAddress] NVARCHAR(64) NOT NULL,
        [AttemptedAtUtc] DATETIME NOT NULL CONSTRAINT DF_LoginAttempts_AttemptedAtUtc DEFAULT(GETUTCDATE()),
        [Success] BIT NOT NULL,
        [FailureReason] NVARCHAR(256) NULL,
        [OrganizationId] INT NULL
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_LoginAttempts_Username_Org_Attempted'
      AND object_id = OBJECT_ID(N'dbo.LoginAttempts')
)
BEGIN
    CREATE INDEX IX_LoginAttempts_Username_Org_Attempted
        ON [LoginAttempts] ([Username], [OrganizationId], [AttemptedAtUtc]);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_LoginAttempts_Ip_Attempted'
      AND object_id = OBJECT_ID(N'dbo.LoginAttempts')
)
BEGIN
    CREATE INDEX IX_LoginAttempts_Ip_Attempted
        ON [LoginAttempts] ([IpAddress], [AttemptedAtUtc]);
END
GO

IF NOT EXISTS (SELECT 1 FROM [Permission] WHERE [Code] = N'SecurityLogs.View')
BEGIN
    DECLARE @now DATETIME = GETUTCDATE();
    INSERT INTO [Permission] ([Name],[Code],[Module],[Description],[CreatedAt],[IsActive]) VALUES
    (N'View Security Logs', N'SecurityLogs.View', N'Security', N'Can view login attempts and security events', @now, 1);
END
GO

DECLARE @securityLogsPermissionId INT = (SELECT [Id] FROM [Permission] WHERE [Code] = N'SecurityLogs.View');
DECLARE @companyAdminRoleId INT = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Company Admin' AND [OrganizationId] IS NOT NULL ORDER BY [Id]);
DECLARE @platformRoleId INT = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Platform Admin' AND [OrganizationId] IS NULL ORDER BY [Id]);

IF @securityLogsPermissionId IS NOT NULL AND @companyAdminRoleId IS NOT NULL
BEGIN
    INSERT INTO [RolePermission] ([RoleId],[PermissionId],[OrganizationId])
    SELECT @companyAdminRoleId, @securityLogsPermissionId, r.[OrganizationId]
    FROM [Roles] r
    WHERE r.[Id] = @companyAdminRoleId
      AND NOT EXISTS (
          SELECT 1 FROM [RolePermission] rp
          WHERE rp.[RoleId] = @companyAdminRoleId
            AND rp.[PermissionId] = @securityLogsPermissionId
            AND rp.[OrganizationId] = r.[OrganizationId]);
END

IF @securityLogsPermissionId IS NOT NULL AND @platformRoleId IS NOT NULL
BEGIN
    INSERT INTO [RolePermission] ([RoleId],[PermissionId],[OrganizationId])
    SELECT @platformRoleId, @securityLogsPermissionId, NULL
    WHERE NOT EXISTS (
        SELECT 1 FROM [RolePermission] rp
        WHERE rp.[RoleId] = @platformRoleId AND rp.[PermissionId] = @securityLogsPermissionId);
END
GO
