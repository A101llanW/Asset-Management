-- MFA, legal acceptance, and security event tracking (HireHub recruitment pattern)

IF COL_LENGTH(N'dbo.Users', N'MfaMethod') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD MfaMethod NVARCHAR(50) NULL;
END
GO

IF COL_LENGTH(N'dbo.Users', N'TwoFactorCode') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD TwoFactorCode NVARCHAR(10) NULL;
END
GO

IF COL_LENGTH(N'dbo.Users', N'TwoFactorExpiryUtc') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD TwoFactorExpiryUtc DATETIME NULL;
END
GO

IF COL_LENGTH(N'dbo.Users', N'PrivacyAcceptedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD PrivacyAcceptedAt DATETIME NULL;
END
GO

IF COL_LENGTH(N'dbo.Users', N'TermsAcceptedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD TermsAcceptedAt DATETIME NULL;
END
GO

IF COL_LENGTH(N'dbo.Users', N'PrivacyVersion') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD PrivacyVersion NVARCHAR(20) NULL;
END
GO

IF COL_LENGTH(N'dbo.Users', N'TermsVersion') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD TermsVersion NVARCHAR(20) NULL;
END
GO

IF OBJECT_ID(N'[SecurityEvents]', N'U') IS NULL
BEGIN
    CREATE TABLE [SecurityEvents] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [EventType] NVARCHAR(64) NOT NULL,
        [Email] NVARCHAR(256) NULL,
        [IpAddress] NVARCHAR(64) NULL,
        [OrganizationId] INT NULL,
        [CreatedAtUtc] DATETIME NOT NULL CONSTRAINT DF_SecurityEvents_CreatedAtUtc DEFAULT(GETUTCDATE())
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_SecurityEvents_EventType_Ip_Created'
      AND object_id = OBJECT_ID(N'dbo.SecurityEvents')
)
BEGIN
    CREATE INDEX IX_SecurityEvents_EventType_Ip_Created
        ON [SecurityEvents] ([EventType], [IpAddress], [CreatedAtUtc]);
END
GO
