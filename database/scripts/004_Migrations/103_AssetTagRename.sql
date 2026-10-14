-- 103 (rel1013b): rename EXISTING asset tags to the rel1013a prefix format, in undoable batches.
-- What it does (additive only, no existing row is changed):
--   1. AssetTagRenameBatch - one row per applied batch (scope, actor, counts, Applied/Undone status).
--   2. AssetTagAlias - one row per renamed asset: the OLD tag (alias) and the NEW tag, per batch. Active aliases keep
--      QR scans, lookups and search working with the old tag. Unique per tenant among active aliases.
-- Must run BEFORE the rel1013b code (scan/search and the tag counter read AssetTagAlias). Needs 102 first.
-- Idempotent: every step is guarded; re-running is a no-op.
-- Rollback (only after redeploying rel1013a; renamed assets keep their NEW tags, old tags stop resolving):
--   DROP TABLE [dbo].[AssetTagAlias]; DROP TABLE [dbo].[AssetTagRenameBatch];
--   DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'103_AssetTagRename.sql';
--   (undo the batches first on the Settings page if the old tags must come back)
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.AssetTagRenameBatch', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AssetTagRenameBatch]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_AssetTagRenameBatch] PRIMARY KEY CLUSTERED,
        [OrganizationId] INT NOT NULL,
        [ScopeDepartmentId] INT NULL,
        [ScopeLabel] NVARCHAR(400) NOT NULL,
        [AssetCount] INT NOT NULL CONSTRAINT [DF_AssetTagRenameBatch_AssetCount] DEFAULT (0),
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT [DF_AssetTagRenameBatch_Status] DEFAULT (N'Applied'),
        [AppliedById] NVARCHAR(128) NULL,
        [AppliedAt] DATETIME2(3) NOT NULL CONSTRAINT [DF_AssetTagRenameBatch_AppliedAt] DEFAULT (SYSUTCDATETIME()),
        [UndoneById] NVARCHAR(128) NULL,
        [UndoneAt] DATETIME2(3) NULL
    );
    CREATE NONCLUSTERED INDEX [IX_AssetTagRenameBatch_Org] ON [dbo].[AssetTagRenameBatch] ([OrganizationId], [Id] DESC);
END

IF OBJECT_ID(N'dbo.AssetTagAlias', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AssetTagAlias]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_AssetTagAlias] PRIMARY KEY CLUSTERED,
        [OrganizationId] INT NOT NULL,
        [AssetId] INT NOT NULL CONSTRAINT [FK_AssetTagAlias_Asset] REFERENCES [dbo].[Asset]([Id]),
        [BatchId] INT NOT NULL CONSTRAINT [FK_AssetTagAlias_Batch] REFERENCES [dbo].[AssetTagRenameBatch]([Id]),
        [AliasTag] NVARCHAR(60) NOT NULL,
        [NewTag] NVARCHAR(60) NOT NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_AssetTagAlias_IsActive] DEFAULT (1),
        [CreatedAt] DATETIME2(3) NOT NULL CONSTRAINT [DF_AssetTagAlias_CreatedAt] DEFAULT (SYSUTCDATETIME())
    );
    CREATE UNIQUE NONCLUSTERED INDEX [UX_AssetTagAlias_Org_Alias_Active] ON [dbo].[AssetTagAlias] ([OrganizationId], [AliasTag]) WHERE [IsActive] = 1;
    CREATE NONCLUSTERED INDEX [IX_AssetTagAlias_Asset] ON [dbo].[AssetTagAlias] ([AssetId]) INCLUDE ([AliasTag], [IsActive]);
    CREATE NONCLUSTERED INDEX [IX_AssetTagAlias_Batch] ON [dbo].[AssetTagAlias] ([BatchId]);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'103_AssetTagRename.sql')
BEGIN
    INSERT INTO [dbo].[SchemaMigrationHistory] ([ScriptName], [Checksum], [AppliedAt], [AppliedBy])
    VALUES (N'103_AssetTagRename.sql', NULL, SYSUTCDATETIME(), SUSER_SNAME());
END

COMMIT TRANSACTION;