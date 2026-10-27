-- 99b  Rollback of 106 (Department.Section Prep/Senior + SortOrder). Run 99a first.
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
IF COL_LENGTH(N'dbo.Department', N'PrepHeadUserId') IS NOT NULL OR COL_LENGTH(N'dbo.Department', N'SeniorHeadUserId') IS NOT NULL
    THROW 50001, N'99b: 107 columns still present - run 99a first.', 1;
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE [parent_object_id] = OBJECT_ID(N'dbo.Department') AND [name] = N'CK_Department_Section')
    ALTER TABLE [dbo].[Department] DROP CONSTRAINT [CK_Department_Section];
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE [parent_object_id] = OBJECT_ID(N'dbo.Department') AND [name] = N'DF_Department_Section')
    ALTER TABLE [dbo].[Department] DROP CONSTRAINT [DF_Department_Section];
IF COL_LENGTH(N'dbo.Department', N'Section') IS NOT NULL
    ALTER TABLE [dbo].[Department] DROP COLUMN [Section];
IF COL_LENGTH(N'dbo.Department', N'SortOrder') IS NOT NULL
    ALTER TABLE [dbo].[Department] DROP COLUMN [SortOrder];
DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'106_DepartmentSection.sql';
COMMIT TRANSACTION;
PRINT N'99b done: 106 rolled back. Any AssetTag.Prefix.Section.* SystemSetting rows are harmless leftovers.';
GO
SET NOEXEC OFF;
GO
