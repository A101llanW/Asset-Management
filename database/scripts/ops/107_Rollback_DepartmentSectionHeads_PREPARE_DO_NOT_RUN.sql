-- 107 ROLLBACK - PREPARED, DO NOT RUN. Not part of the migration set; the migration runner never picks this folder up.
-- Purpose: undo 107_DepartmentSectionHeads.sql. ONLY after the previous build (without section heads) is live again;
--   the current build reads PrepHeadUserId/SeniorHeadUserId and breaks the whole site if the columns disappear first.
-- Effect: every Prep / Senior head set on Departments > Edit is LOST; the Department Head stage goes back to the role
--   holders. Take a COPY_ONLY backup first.
-- To run on purpose: sqlcmd -E -S <server> -i this_file.sql -v DbName="<database>" ConfirmRollback="YES"
:on error exit
-- DbName and ConfirmRollback are NOT :setvar'd on purpose (a :setvar would override -v). If either is missing sqlcmd stops
-- with "scripting variable not defined". SSMS (SQLCMD Mode): add  :setvar DbName "..."  and  :setvar ConfirmRollback "YES"  above.
IF N'$(ConfirmRollback)' <> N'YES'
BEGIN
    RAISERROR (N'107 rollback NOT run: ConfirmRollback is not YES. This script is DO NOT RUN unless you mean it.', 16, 1);
    SET NOEXEC ON;
END
GO
USE [$(DbName)];
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
PRINT N'107 rollback done.';
GO
SET NOEXEC OFF;
