-- 99d  Rollback of 104_AssetTagSegmentToggles.  SPECIAL - review S1.
-- It only works when 02 ADDED the columns in this run, i.e. the 104 history row carries
-- Checksum = 'LiveDB_20261027:added-columns'. Otherwise it refuses (THROW) and changes nothing: if 104 was on live
-- before (21 Oct, or 01 said READY / READY_104_HISTORY_ONLY), the columns stay. The in-repo reference rollback for that
-- older case is database/scripts/005_Rollback/104_AssetTagSegmentToggles_rollback.sql (it has its own confirm guard).
-- It never deletes the AssetTag.Segment.* SystemSetting rows (they are harmless without the columns and may predate
-- this run). Never touches 104_widen_auditlog_entityid.
-- DO_NOT_RUN unless Allan has decided to roll back AND the previous build (without this feature) is already back on
-- the site. The new code reads these columns; dropping them under the new build breaks the site.
-- To run on purpose: change @Confirm below from N'NO' to N'YES'. With N'NO' the script does nothing.
-- Run rollbacks newest first: 99a (107) -> 99b (106) -> 99c (105) -> 99d (104, special).
-- Run in SSMS or sqlcmd only. Take a COPY_ONLY backup first (00) if the database has changed since the last one.
SET NOEXEC OFF;
GO
DECLARE @Confirm NVARCHAR(3) = N'NO';
IF @Confirm <> N'YES'
BEGIN
    RAISERROR(N'Rollback NOT run: @Confirm is not YES. This script is DO_NOT_RUN.', 16, 1);
    SET NOEXEC ON;
END
GO
IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory]
               WHERE [ScriptName] = N'104_AssetTagSegmentToggles.sql' AND [Checksum] = N'LiveDB_20261027:added-columns')
BEGIN
    RAISERROR(N'99d NOT RUN: 104 was not added by 02 in this run (no LiveDB_20261027:added-columns marker). The 104 columns stay.', 16, 1);
    SET NOEXEC ON;
END
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory]
               WHERE [ScriptName] = N'104_AssetTagSegmentToggles.sql' AND [Checksum] = N'LiveDB_20261027:added-columns')
    THROW 50041, N'99d NOT RUN: marker missing (104 was not added by 02 in this run).', 1;
IF COL_LENGTH(N'dbo.Department', N'AssetTagUseDepartmentSegment') IS NOT NULL
    ALTER TABLE [dbo].[Department] DROP COLUMN [AssetTagUseDepartmentSegment];
IF COL_LENGTH(N'dbo.Department', N'AssetTagUseSubDepartmentSegment') IS NOT NULL
    ALTER TABLE [dbo].[Department] DROP COLUMN [AssetTagUseSubDepartmentSegment];
IF COL_LENGTH(N'dbo.Department', N'AssetTagUseRoomSegment') IS NOT NULL
    ALTER TABLE [dbo].[Department] DROP COLUMN [AssetTagUseRoomSegment];
DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'104_AssetTagSegmentToggles.sql' AND [Checksum] = N'LiveDB_20261027:added-columns';
COMMIT TRANSACTION;
PRINT N'99d done: 104 columns added by 02 in this run were removed (SystemSetting rows kept).';
GO
SET NOEXEC OFF;
GO
