-- 105 (rel1022a, feat/print-log-bulk): label print batch log.
-- 104 is claimed twice (104_AssetTagSegmentToggles.sql on feat/tag-level-toggle and the untracked
-- 104_widen_auditlog_entityid.sql in the main checkout), so this is 105.
-- What it does (additive only; no existing table, column, index or row is changed):
--   1. LabelPrintBatch - one row per group/bulk send to label printing: who, when, source (BulkPrint, AssetRegister,
--      Reprint), the group/filter used, counts, status, reprint link, and a soft "Cleared" flag (who/when).
--   2. LabelPrintBatchItem - the asset list of each batch (asset id, tag at send time, print count, last printed).
-- Per-asset print history is NOT stored here: every print/reprint is an AuditLog row
-- (Action Assets.LabelPrint / Assets.LabelReprint, EntityType Asset), read through the existing IX_AuditLog_Org_Entity.
-- Clearing a batch only sets IsCleared; batch items and the per-asset audit trail are kept.
-- Idempotent: every step is guarded; re-running is a no-op.
-- Rollback (only after redeploying a build without the print batch log):
--   DROP TABLE [dbo].[LabelPrintBatchItem]; DROP TABLE [dbo].[LabelPrintBatch];
--   DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'105_LabelPrintBatchLog.sql';
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.LabelPrintBatch', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[LabelPrintBatch]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_LabelPrintBatch] PRIMARY KEY CLUSTERED,
        [OrganizationId] INT NOT NULL CONSTRAINT [FK_LabelPrintBatch_Organization] REFERENCES [dbo].[Organization]([Id]),
        [Source] NVARCHAR(40) NOT NULL CONSTRAINT [DF_LabelPrintBatch_Source] DEFAULT (N'BulkPrint'),
        [GroupLabel] NVARCHAR(400) NULL,
        [FilterDescription] NVARCHAR(2000) NULL,
        [AssetCount] INT NOT NULL CONSTRAINT [DF_LabelPrintBatch_AssetCount] DEFAULT (0),
        [PrintedCount] INT NOT NULL CONSTRAINT [DF_LabelPrintBatch_PrintedCount] DEFAULT (0),
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT [DF_LabelPrintBatch_Status] DEFAULT (N'Queued'),
        [IsReprint] BIT NOT NULL CONSTRAINT [DF_LabelPrintBatch_IsReprint] DEFAULT (0),
        [ReprintOfBatchId] INT NULL CONSTRAINT [FK_LabelPrintBatch_ReprintOf] REFERENCES [dbo].[LabelPrintBatch]([Id]),
        [TemplateId] NVARCHAR(100) NULL,
        [CodeType] NVARCHAR(20) NULL,
        [CreatedById] NVARCHAR(256) NULL,
        [CreatedAt] DATETIME2(3) NOT NULL CONSTRAINT [DF_LabelPrintBatch_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [LastPrintedAt] DATETIME2(3) NULL,
        [CompletedAt] DATETIME2(3) NULL,
        [IsCleared] BIT NOT NULL CONSTRAINT [DF_LabelPrintBatch_IsCleared] DEFAULT (0),
        [ClearedById] NVARCHAR(256) NULL,
        [ClearedAt] DATETIME2(3) NULL
    );
    CREATE NONCLUSTERED INDEX [IX_LabelPrintBatch_Org] ON [dbo].[LabelPrintBatch] ([OrganizationId], [IsCleared], [Id] DESC);
END

IF OBJECT_ID(N'dbo.LabelPrintBatchItem', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[LabelPrintBatchItem]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_LabelPrintBatchItem] PRIMARY KEY CLUSTERED,
        [OrganizationId] INT NOT NULL,
        [BatchId] INT NOT NULL CONSTRAINT [FK_LabelPrintBatchItem_Batch] REFERENCES [dbo].[LabelPrintBatch]([Id]),
        [AssetId] INT NOT NULL CONSTRAINT [FK_LabelPrintBatchItem_Asset] REFERENCES [dbo].[Asset]([Id]),
        [AssetTag] NVARCHAR(60) NULL,
        [SortOrder] INT NOT NULL CONSTRAINT [DF_LabelPrintBatchItem_SortOrder] DEFAULT (0),
        [PrintCount] INT NOT NULL CONSTRAINT [DF_LabelPrintBatchItem_PrintCount] DEFAULT (0),
        [LastPrintedAt] DATETIME2(3) NULL
    );
    CREATE UNIQUE NONCLUSTERED INDEX [UX_LabelPrintBatchItem_Batch_Asset] ON [dbo].[LabelPrintBatchItem] ([BatchId], [AssetId]);
    CREATE NONCLUSTERED INDEX [IX_LabelPrintBatchItem_Asset] ON [dbo].[LabelPrintBatchItem] ([AssetId]);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'105_LabelPrintBatchLog.sql')
BEGIN
    INSERT INTO [dbo].[SchemaMigrationHistory] ([ScriptName], [Checksum], [AppliedAt], [AppliedBy])
    VALUES (N'105_LabelPrintBatchLog.sql', NULL, SYSUTCDATETIME(), SUSER_SNAME());
END

COMMIT TRANSACTION;
