-- Tracks applied migration scripts for controlled deploys.
IF OBJECT_ID(N'[dbo].[SchemaMigrationHistory]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SchemaMigrationHistory] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ScriptName] NVARCHAR(260) NOT NULL,
        [Checksum] NVARCHAR(64) NULL,
        [AppliedAt] DATETIME2 NOT NULL CONSTRAINT [DF_SchemaMigrationHistory_AppliedAt] DEFAULT (SYSUTCDATETIME()),
        [AppliedBy] NVARCHAR(128) NOT NULL CONSTRAINT [DF_SchemaMigrationHistory_AppliedBy] DEFAULT (SUSER_SNAME()),
        CONSTRAINT [UQ_SchemaMigrationHistory_ScriptName] UNIQUE ([ScriptName])
    );
END
GO
