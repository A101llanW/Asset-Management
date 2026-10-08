-- 094: OrganizationSmtpSettings (additive only: one new table; no existing data changed).
-- Per-company outgoing email (SMTP) settings, one row per organization (unique OrganizationId).
-- PasswordEncrypted holds EncryptionHelper v2 ciphertext ("v2:..."), never plain text; the UI never shows it.
-- CreatedAt / UpdatedAt are UTC (screens show EAT). Soft delete stays IsActive.
-- An org whose row is missing, disabled (IsEnabled = 0), inactive or incomplete (no host / port / From address,
-- or a username without a password) sends through the platform default SMTP (smtp.secrets.config / SystemSetting).
-- The app tolerates this table being absent (every email uses the platform default and Settings > Email says
-- "not available yet"), so the code may be deployed before or after this script.
-- Rollback (company SMTP settings are lost; every organization falls back to the platform default):
--   DROP TABLE [dbo].[OrganizationSmtpSettings];
--   DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'094_OrganizationSmtpSettings.sql';

IF OBJECT_ID(N'[dbo].[OrganizationSmtpSettings]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrganizationSmtpSettings] (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_OrganizationSmtpSettings] PRIMARY KEY CLUSTERED,
        [OrganizationId] INT NOT NULL,
        [Host] NVARCHAR(255) NULL,
        [Port] INT NOT NULL CONSTRAINT [DF_OrganizationSmtpSettings_Port] DEFAULT (587),
        [UseSsl] BIT NOT NULL CONSTRAINT [DF_OrganizationSmtpSettings_UseSsl] DEFAULT (1),
        [Username] NVARCHAR(256) NULL,
        [PasswordEncrypted] NVARCHAR(1024) NULL,
        [FromAddress] NVARCHAR(256) NULL,
        [FromDisplayName] NVARCHAR(200) NULL,
        [IsEnabled] BIT NOT NULL CONSTRAINT [DF_OrganizationSmtpSettings_IsEnabled] DEFAULT (0),
        [IsActive] BIT NOT NULL CONSTRAINT [DF_OrganizationSmtpSettings_IsActive] DEFAULT (1),
        [CreatedAt] DATETIME2(3) NOT NULL CONSTRAINT [DF_OrganizationSmtpSettings_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] NVARCHAR(128) NULL,
        [UpdatedAt] DATETIME2(3) NULL,
        [UpdatedByUserId] NVARCHAR(128) NULL,
        CONSTRAINT [UQ_OrganizationSmtpSettings_Organization] UNIQUE ([OrganizationId]),
        CONSTRAINT [FK_OrganizationSmtpSettings_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization]([Id]),
        CONSTRAINT [CK_OrganizationSmtpSettings_Port] CHECK ([Port] BETWEEN 1 AND 65535)
    );
END
GO
