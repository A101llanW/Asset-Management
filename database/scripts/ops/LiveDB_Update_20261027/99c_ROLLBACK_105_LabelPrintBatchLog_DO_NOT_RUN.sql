-- 99c  Rollback of 105 (label print batch log). Drops the print-log tables and their rows (per-asset print history in AuditLog stays).
-- DO_NOT_RUN unless Allan has decided to roll back AND the previous build (without this feature) is already back on
-- the site. The new code reads these columns/tables; dropping them under the new build breaks the site.
-- To run on purpose: change @Confirm below from N'NO' to N'YES'. With N'NO' the script does nothing.
-- Run rollbacks newest first: 99a (107) -> 99b (106) -> 99c (105) -> 99d (104, special, see its header).
-- Take a COPY_ONLY backup first (00) if the database has changed since the last one.
DECLARE @Confirm NVARCHAR(3) = N'NO';
IF @Confirm <> N'YES'
BEGIN
    RAISERROR(N'Rollback NOT run: @Confirm is not YES. This script is DO_NOT_RUN.', 16, 1);
    SET NOEXEC ON;
END
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.LabelPrintBatchItem', N'U') IS NOT NULL DROP TABLE [dbo].[LabelPrintBatchItem];
IF OBJECT_ID(N'dbo.LabelPrintBatch', N'U') IS NOT NULL DROP TABLE [dbo].[LabelPrintBatch];
DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'105_LabelPrintBatchLog.sql';
COMMIT TRANSACTION;
PRINT N'99c done: 105 rolled back.';
GO
SET NOEXEC OFF;
GO
