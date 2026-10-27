-- 99a  Rollback of 107 (Prep / Senior heads per department).
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
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [parent_object_id] = OBJECT_ID(N'dbo.Department') AND [name] = N'FK_Department_PrepHeadUser')
    ALTER TABLE [dbo].[Department] DROP CONSTRAINT [FK_Department_PrepHeadUser];
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [parent_object_id] = OBJECT_ID(N'dbo.Department') AND [name] = N'FK_Department_SeniorHeadUser')
    ALTER TABLE [dbo].[Department] DROP CONSTRAINT [FK_Department_SeniorHeadUser];
IF COL_LENGTH(N'dbo.Department', N'PrepHeadUserId') IS NOT NULL
    ALTER TABLE [dbo].[Department] DROP COLUMN [PrepHeadUserId];
IF COL_LENGTH(N'dbo.Department', N'SeniorHeadUserId') IS NOT NULL
    ALTER TABLE [dbo].[Department] DROP COLUMN [SeniorHeadUserId];
DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'107_DepartmentSectionHeads.sql';
COMMIT TRANSACTION;
PRINT N'99a done: 107 rolled back (heads per section removed).';
GO
SET NOEXEC OFF;
GO
