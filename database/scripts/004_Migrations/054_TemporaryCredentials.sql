-- One-time encrypted admin credential packages (mirrors Recruitment TemporaryCredentials).
IF OBJECT_ID(N'[dbo].[TemporaryCredentials]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[TemporaryCredentials] (
        [Id] INT IDENTITY(1, 1) NOT NULL CONSTRAINT [PK_TemporaryCredentials] PRIMARY KEY,
        [Token] NVARCHAR(100) NOT NULL,
        [EncryptedData] NVARCHAR(MAX) NOT NULL,
        [ExpiryDate] DATETIME NOT NULL,
        [IsUsed] BIT NOT NULL CONSTRAINT [DF_TemporaryCredentials_IsUsed] DEFAULT (0),
        [CreatedDate] DATETIME NOT NULL CONSTRAINT [DF_TemporaryCredentials_CreatedDate] DEFAULT (GETUTCDATE()),
        [CredentialType] NVARCHAR(50) NULL
    );

    CREATE UNIQUE INDEX [IX_TemporaryCredentials_Token] ON [dbo].[TemporaryCredentials] ([Token]);
END
GO
