-- 02  Migration 104_AssetTagSegmentToggles (tag segment switches).  LIVE_RUN_BY_ALLAN.
-- Safe whether or not the 21 Oct console run reached live: every column add is guarded by COL_LENGTH and the history
-- row by IF NOT EXISTS. If 104 is already there (expected: history row Id 107 on live) this is a no-op.
-- History name stays '104_AssetTagSegmentToggles.sql' (distinct from the other 104, 104_widen_auditlog_entityid.sql,
-- which writes no history row and is NOT part of this run).
-- Expected result: 'Commands completed successfully.'  If it fails: the transaction rolls back by itself; STOP and see README.
-- Source: database/scripts/004_Migrations/104_AssetTagSegmentToggles.sql (commit 488e78c), plus the review S1 marker:
--   columns ABSENT (01 = READY_104_MISSING): adds them + history row with Checksum = 'LiveDB_20261027:added-columns'.
--   columns PRESENT, no history row (01 = READY_104_HISTORY_ONLY): adds only the history row ('LiveDB_20261027:history-only').
--   columns and history row present (01 = READY): no-op.   1-2 columns, or a history row without columns: refuses.
-- Only the 'added-columns' marker allows 99d to drop the columns later.
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
-- 104 (rel1018b): per-level "use this segment" switches for derived asset tag prefixes.
-- What it does (additive only, no existing row or tag is changed; NULL = inherit, so behaviour is identical to rel1018a until someone sets a switch):
--   Department.AssetTagUseDepartmentSegment    BIT NULL - use the top-department code in derived tags for this node and below.
--   Department.AssetTagUseSubDepartmentSegment BIT NULL - use the sub-department code.
--   Department.AssetTagUseRoomSegment          BIT NULL - use the room code.
-- Tenant defaults are SystemSetting rows written by Settings > Asset tag prefix (no rows inserted here; missing = rel1018a shape):
--   AssetTag.Segment.Department (default True), AssetTag.Segment.SubDepartment (default False), AssetTag.Segment.Room (default True).
-- Must run BEFORE the rel1018b code is published (the Department entity map reads the new columns). Needs 102.
-- Idempotent: every step is guarded; re-running is a no-op.
-- Rollback: database/scripts/005_Rollback/104_AssetTagSegmentToggles_rollback.sql (only after redeploying rel1018a).
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @cols INT =
      CASE WHEN COL_LENGTH(N'dbo.Department', N'AssetTagUseDepartmentSegment')    IS NOT NULL THEN 1 ELSE 0 END
    + CASE WHEN COL_LENGTH(N'dbo.Department', N'AssetTagUseSubDepartmentSegment') IS NOT NULL THEN 1 ELSE 0 END
    + CASE WHEN COL_LENGTH(N'dbo.Department', N'AssetTagUseRoomSegment')          IS NOT NULL THEN 1 ELSE 0 END;
DECLARE @hist BIT = CASE WHEN EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'104_AssetTagSegmentToggles.sql') THEN 1 ELSE 0 END;

IF @cols NOT IN (0, 3)
    THROW 50021, N'02 NOT RUN: only some of the 104 columns exist (STOP_104_PARTIAL). Send the 01 output for review.', 1;
IF @cols = 0 AND @hist = 1
    THROW 50022, N'02 NOT RUN: 104 history row exists but the columns do not (STOP_104_PARTIAL). Send the 01 output for review.', 1;

IF @cols = 0
BEGIN
    ALTER TABLE [dbo].[Department] ADD [AssetTagUseDepartmentSegment] BIT NULL;
    ALTER TABLE [dbo].[Department] ADD [AssetTagUseSubDepartmentSegment] BIT NULL;
    ALTER TABLE [dbo].[Department] ADD [AssetTagUseRoomSegment] BIT NULL;
    INSERT INTO [dbo].[SchemaMigrationHistory] ([ScriptName], [Checksum], [AppliedAt], [AppliedBy])
    VALUES (N'104_AssetTagSegmentToggles.sql', N'LiveDB_20261027:added-columns', SYSUTCDATETIME(), SUSER_SNAME());
    PRINT N'02: 104 columns added now (marker LiveDB_20261027:added-columns).';
END
ELSE IF @hist = 0
BEGIN
    INSERT INTO [dbo].[SchemaMigrationHistory] ([ScriptName], [Checksum], [AppliedAt], [AppliedBy])
    VALUES (N'104_AssetTagSegmentToggles.sql', N'LiveDB_20261027:history-only', SYSUTCDATETIME(), SUSER_SNAME());
    PRINT N'02: 104 columns were already there; history row added (marker LiveDB_20261027:history-only). 99d will refuse.';
END
ELSE
    PRINT N'02: 104 already applied (columns + history row); nothing changed.';

COMMIT TRANSACTION;
GO
SET NOEXEC OFF;
GO
