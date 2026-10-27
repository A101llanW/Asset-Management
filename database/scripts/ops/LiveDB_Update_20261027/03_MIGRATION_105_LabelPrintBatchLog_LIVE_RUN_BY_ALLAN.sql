-- 03  Migration 105 (label print batch log tables).  LIVE_RUN_BY_ALLAN.  Idempotent; re-run = no-op.
-- Expected result: 'Commands completed successfully.'
-- Source: database/scripts/004_Migrations/105_LabelPrintBatchLog.sql (commit 488e78c), unchanged below the guard batch.
-- Guard (review S5): right database AND a COPY_ONLY full backup of THIS database (script 00) finished in the
-- last 2 hours (msdb.dbo.backupset, taken after the database was created/restored). Otherwise nothing runs.
-- Run in SSMS or sqlcmd only, NOT the web Sys/Database console: the guards rely on GO batches and SET NOEXEC.
SET NOEXEC OFF;
GO
DECLARE @guardMsg NVARCHAR(2000) = NULL;
IF DB_NAME() NOT LIKE N'AssetManagementModuleDb%'
    SET @guardMsg = N'NOT RUN: connect to the AssetManagementModuleDb database first (current database: ' + DB_NAME() + N').';
ELSE
BEGIN
    BEGIN TRY
        IF NOT EXISTS (SELECT 1 FROM msdb.dbo.backupset b
                       WHERE b.[database_name] = DB_NAME() AND b.[type] = 'D' AND b.[is_copy_only] = 1
                         AND b.[backup_finish_date] >= DATEADD(HOUR, -2, GETDATE())
                         AND b.[backup_start_date] >= (SELECT d.[create_date] FROM sys.databases d WHERE d.[name] = DB_NAME()))
            SET @guardMsg = N'NOT RUN: no COPY_ONLY full backup of ' + DB_NAME() + N' finished in the last 2 hours (msdb.dbo.backupset). Run 00 first.';
    END TRY
    BEGIN CATCH
        SET @guardMsg = N'NOT RUN: cannot read msdb.dbo.backupset to confirm the 00 backup: ' + ERROR_MESSAGE();
    END CATCH
END
IF @guardMsg IS NOT NULL
BEGIN
    RAISERROR(N'%s', 16, 1, @guardMsg);
    SET NOEXEC ON;
END
GO
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
GO
SET NOEXEC OFF;
GO
