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
