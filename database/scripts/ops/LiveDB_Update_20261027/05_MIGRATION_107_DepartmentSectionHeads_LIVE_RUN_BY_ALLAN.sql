-- 05  Migration 107 (separate Prep / Senior head per department).  LIVE_RUN_BY_ALLAN.  Needs 106 first.
-- Idempotent; re-run = no-op. Expected message: '107 done: ...'
-- Source: database/scripts/004_Migrations/107_DepartmentSectionHeads.sql (commit 488e78c), unchanged below the guard batch.
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
IF COL_LENGTH(N'dbo.Department', N'Section') IS NULL
BEGIN
    RAISERROR(N'05 NOT RUN: 106 (Department.Section) is missing - run 04 first.', 16, 1);
    SET NOEXEC ON;
END
GO
-- 107 (Department heads per section): a department can have a different head for Prep and for Senior school.
-- What it does (additive and idempotent; no existing row, tag, asset or approval is changed):
--   1. Department.PrepHeadUserId   NVARCHAR(128) NULL  (any active user, whatever their role; NULL = not set)
--   2. Department.SeniorHeadUserId NVARCHAR(128) NULL
--   3. FK_Department_PrepHeadUser / FK_Department_SeniorHeadUser -> Users(Id), added WITH NOCHECK (same pattern as 082).
--   4. SchemaMigrationHistory row.
-- Every existing department gets NULL in both columns, so approvals keep routing exactly as today (Department Head role
-- holders) until someone sets a head on Departments > Edit.
-- Depends on 106 (Department.Section). Must run BEFORE the matching code is published (the Department entity map reads
-- both columns).
-- Idempotent: every step is guarded; re-running is a no-op.
-- Rollback (see database/scripts/ops/107_Rollback_DepartmentSectionHeads_PREPARE_DO_NOT_RUN.sql), only after redeploying
-- the previous build:
--   ALTER TABLE [dbo].[Department] DROP CONSTRAINT [FK_Department_PrepHeadUser], [FK_Department_SeniorHeadUser];
--   ALTER TABLE [dbo].[Department] DROP COLUMN [PrepHeadUserId], [SeniorHeadUserId];
--   DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'107_DepartmentSectionHeads.sql';
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.Department', N'PrepHeadUserId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Department] ADD [PrepHeadUserId] NVARCHAR(128) NULL;
END

IF COL_LENGTH(N'dbo.Department', N'SeniorHeadUserId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Department] ADD [SeniorHeadUserId] NVARCHAR(128) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [parent_object_id] = OBJECT_ID(N'dbo.Department') AND [name] = N'FK_Department_PrepHeadUser')
BEGIN
    -- Dynamic SQL: the column was added in this same batch.
    EXEC (N'ALTER TABLE [dbo].[Department] WITH NOCHECK ADD CONSTRAINT [FK_Department_PrepHeadUser] FOREIGN KEY ([PrepHeadUserId]) REFERENCES [dbo].[Users]([Id]);');
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [parent_object_id] = OBJECT_ID(N'dbo.Department') AND [name] = N'FK_Department_SeniorHeadUser')
BEGIN
    EXEC (N'ALTER TABLE [dbo].[Department] WITH NOCHECK ADD CONSTRAINT [FK_Department_SeniorHeadUser] FOREIGN KEY ([SeniorHeadUserId]) REFERENCES [dbo].[Users]([Id]);');
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'107_DepartmentSectionHeads.sql')
BEGIN
    INSERT INTO [dbo].[SchemaMigrationHistory] ([ScriptName], [Checksum], [AppliedAt], [AppliedBy])
    VALUES (N'107_DepartmentSectionHeads.sql', NULL, SYSUTCDATETIME(), SUSER_SNAME());
END

COMMIT TRANSACTION;
PRINT N'107 done: Department.PrepHeadUserId / SeniorHeadUserId present (all NULL until set on Departments > Edit).';
GO
SET NOEXEC OFF;
GO
