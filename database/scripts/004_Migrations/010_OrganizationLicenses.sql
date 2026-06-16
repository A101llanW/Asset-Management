-- Organization license management (Phase 8)
IF OBJECT_ID(N'[OrganizationLicense]', N'U') IS NULL
BEGIN
    CREATE TABLE [OrganizationLicense] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [OrganizationId] INT NOT NULL,
        [PlanCode] NVARCHAR(50) NOT NULL,
        [PlanName] NVARCHAR(100) NOT NULL,
        [Status] NVARCHAR(20) NOT NULL,
        [StartDate] DATETIME NOT NULL,
        [ExpiryDate] DATETIME NOT NULL,
        [MaxUsers] INT NULL,
        [PausedAt] DATETIME NULL,
        [PausedBy] NVARCHAR(256) NULL,
        [PauseReason] NVARCHAR(500) NULL,
        [Notes] NVARCHAR(1000) NULL,
        [CreatedAt] DATETIME NOT NULL CONSTRAINT DF_OrganizationLicense_CreatedAt DEFAULT(GETUTCDATE()),
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_OrganizationLicense_IsActive DEFAULT(1),
        CONSTRAINT FK_OrganizationLicense_Organization FOREIGN KEY ([OrganizationId]) REFERENCES [Organization]([Id]),
        CONSTRAINT UQ_OrganizationLicense_Organization UNIQUE ([OrganizationId])
    );
END
GO

IF COL_LENGTH(N'[OrganizationLicense]', N'UpdatedAt') IS NULL
BEGIN
    ALTER TABLE [OrganizationLicense] ADD [UpdatedAt] DATETIME NULL;
END
GO

IF OBJECT_ID(N'[OrganizationLicenseHistory]', N'U') IS NULL
BEGIN
    CREATE TABLE [OrganizationLicenseHistory] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [OrganizationLicenseId] INT NOT NULL,
        [OrganizationId] INT NOT NULL,
        [Action] NVARCHAR(50) NOT NULL,
        [PreviousExpiryDate] DATETIME NULL,
        [NewExpiryDate] DATETIME NULL,
        [PreviousStatus] NVARCHAR(20) NULL,
        [NewStatus] NVARCHAR(20) NULL,
        [PerformedBy] NVARCHAR(256) NOT NULL,
        [Reason] NVARCHAR(500) NULL,
        [CreatedAt] DATETIME NOT NULL CONSTRAINT DF_OrganizationLicenseHistory_CreatedAt DEFAULT(GETUTCDATE()),
        CONSTRAINT FK_OrganizationLicenseHistory_License FOREIGN KEY ([OrganizationLicenseId]) REFERENCES [OrganizationLicense]([Id]),
        CONSTRAINT FK_OrganizationLicenseHistory_Organization FOREIGN KEY ([OrganizationId]) REFERENCES [Organization]([Id])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OrganizationLicense_OrganizationId' AND object_id = OBJECT_ID(N'[OrganizationLicense]'))
BEGIN
    CREATE INDEX IX_OrganizationLicense_OrganizationId ON [OrganizationLicense]([OrganizationId]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OrganizationLicense_Status_ExpiryDate' AND object_id = OBJECT_ID(N'[OrganizationLicense]'))
BEGIN
    CREATE INDEX IX_OrganizationLicense_Status_ExpiryDate ON [OrganizationLicense]([Status], [ExpiryDate]);
END
GO

-- Backfill default Active 12-month license for existing organizations
DECLARE @now DATETIME = GETUTCDATE();
INSERT INTO [OrganizationLicense]
    ([OrganizationId],[PlanCode],[PlanName],[Status],[StartDate],[ExpiryDate],[MaxUsers],[CreatedAt],[IsActive])
SELECT o.[Id], N'Standard', N'Standard', N'Active', @now, DATEADD(MONTH, 12, @now), NULL, @now, 1
FROM [Organization] o
WHERE o.[IsActive] = 1
  AND NOT EXISTS (SELECT 1 FROM [OrganizationLicense] ol WHERE ol.[OrganizationId] = o.[Id]);
GO

DECLARE @now DATETIME = GETUTCDATE();

INSERT INTO [OrganizationLicenseHistory]
    ([OrganizationLicenseId],[OrganizationId],[Action],[NewExpiryDate],[NewStatus],[PerformedBy],[Reason],[CreatedAt])
SELECT ol.[Id], ol.[OrganizationId], N'Created', ol.[ExpiryDate], ol.[Status], N'system', N'Migration backfill', @now
FROM [OrganizationLicense] ol
WHERE NOT EXISTS (
    SELECT 1 FROM [OrganizationLicenseHistory] h
    WHERE h.[OrganizationLicenseId] = ol.[Id] AND h.[Action] = N'Created'
);
GO

-- Platform license permissions
DECLARE @permNow DATETIME = GETUTCDATE();
IF NOT EXISTS (SELECT 1 FROM [Permission] WHERE [Code] = N'Platform.Licenses.View')
BEGIN
    INSERT INTO [Permission] ([Name],[Code],[Module],[Description],[CreatedAt],[IsActive]) VALUES
    (N'View Licenses', N'Platform.Licenses.View', N'Platform', N'View organization license list and details', @permNow, 1),
    (N'Manage Licenses', N'Platform.Licenses.Manage', N'Platform', N'Renew, pause, resume, and update organization licenses', @permNow, 1);
END
GO

DECLARE @platformRoleId INT = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Platform Admin' ORDER BY [Id]);
IF @platformRoleId IS NOT NULL
BEGIN
    INSERT INTO [RolePermission] ([RoleId],[PermissionId],[OrganizationId])
    SELECT @platformRoleId, p.[Id], NULL
    FROM [Permission] p
    WHERE p.[Code] IN (N'Platform.Licenses.View', N'Platform.Licenses.Manage')
      AND NOT EXISTS (
          SELECT 1 FROM [RolePermission] rp
          WHERE rp.[RoleId] = @platformRoleId AND rp.[PermissionId] = p.[Id]
      );
END
GO
