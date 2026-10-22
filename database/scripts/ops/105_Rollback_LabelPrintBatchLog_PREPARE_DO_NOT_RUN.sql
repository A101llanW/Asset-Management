-- PREPARED, DO NOT RUN without sign-off. Rollback of 105_LabelPrintBatchLog.sql. Run ONLY after redeploying a build without the print batch log.
-- Drops the batch log tables (batch history is lost). The per-asset print audit rows in AuditLog are kept.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.LabelPrintBatchItem', N'U') IS NOT NULL DROP TABLE [dbo].[LabelPrintBatchItem];
IF OBJECT_ID(N'dbo.LabelPrintBatch', N'U') IS NOT NULL DROP TABLE [dbo].[LabelPrintBatch];
DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'105_LabelPrintBatchLog.sql';
COMMIT TRANSACTION;
