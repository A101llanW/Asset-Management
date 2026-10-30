-- 111 (KG section, pt560u 2026-10-30): a third school section, KG (Kindergarten), next to Prep and Senior school.
-- What it does (additive and idempotent; no existing row, tag, asset or approval is changed):
--   1. CK_Department_Section: re-created as [Section] IN (0, 1, 2)   (0 = Senior school, 1 = Prep, 2 = KG).
--      Every existing row is 0 or 1, so the WITH CHECK re-create always succeeds. DF_Department_Section stays 0 (Senior).
--   2. Department.KgHeadUserId NVARCHAR(128) NULL (head for the KG section, like 107's Prep/Senior heads; NULL = not set).
--   3. FK_Department_KgHeadUser -> Users(Id), added WITH NOCHECK (same pattern as 107).
--   4. SchemaMigrationHistory row.
-- Depends on 106 (Department.Section) and 107 (section heads). Must run BEFORE the matching code is published
-- (the Department entity map reads KgHeadUserId).
-- Idempotent: every step is guarded; re-running is a no-op.
-- Rollback: database/scripts/ops/111_Rollback_DepartmentSectionKG_PREPARE_DO_NOT_RUN.sql (refuses while any unit is KG
-- or has a KG head), only after redeploying the previous build.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF COL_LENGTH(N'dbo.Department', N'Section') IS NULL OR COL_LENGTH(N'dbo.Department', N'PrepHeadUserId') IS NULL
BEGIN
    RAISERROR (N'111 NOT run: migrations 106 and 107 must be applied first (Department.Section / PrepHeadUserId missing).', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE [parent_object_id] = OBJECT_ID(N'dbo.Department') AND [name] = N'CK_Department_Section'
           AND REPLACE([definition], N' ', N'') NOT LIKE N'%[[]Section]=(2)%')
BEGIN
    ALTER TABLE [dbo].[Department] DROP CONSTRAINT [CK_Department_Section];
END

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [parent_object_id] = OBJECT_ID(N'dbo.Department') AND [name] = N'CK_Department_Section')
BEGIN
    EXEC (N'ALTER TABLE [dbo].[Department] WITH CHECK ADD CONSTRAINT [CK_Department_Section] CHECK ([Section] IN (0, 1, 2));');
END

IF COL_LENGTH(N'dbo.Department', N'KgHeadUserId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Department] ADD [KgHeadUserId] NVARCHAR(128) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [parent_object_id] = OBJECT_ID(N'dbo.Department') AND [name] = N'FK_Department_KgHeadUser')
BEGIN
    -- Dynamic SQL: the column was added in this same batch.
    EXEC (N'ALTER TABLE [dbo].[Department] WITH NOCHECK ADD CONSTRAINT [FK_Department_KgHeadUser] FOREIGN KEY ([KgHeadUserId]) REFERENCES [dbo].[Users]([Id]);');
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'111_DepartmentSectionKG.sql')
BEGIN
    INSERT INTO [dbo].[SchemaMigrationHistory] ([ScriptName], [Checksum], [AppliedAt], [AppliedBy])
    VALUES (N'111_DepartmentSectionKG.sql', NULL, SYSUTCDATETIME(), SUSER_SNAME());
END

COMMIT TRANSACTION;
PRINT N'111 done: Section KG (2) allowed; Department.KgHeadUserId present (NULL until set on Departments > Edit).';
