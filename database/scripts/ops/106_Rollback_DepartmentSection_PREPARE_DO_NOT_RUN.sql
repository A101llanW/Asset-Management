-- 106 ROLLBACK - PREPARED, DO NOT RUN. Not part of the migration set; the migration runner never picks this folder up.
-- Purpose: undo 106_DepartmentSection.sql. ONLY after the previous build (without Department.Section) is live again;
--   the current build reads Section/SortOrder and breaks the whole site if the columns disappear first.
-- Effect: Section and SortOrder values are LOST (Prep/Senior split, grade sort numbers). Take a COPY_ONLY backup first.
-- To run on purpose: sqlcmd -E -S <server> -i this_file.sql -v DbName="<database>" ConfirmRollback="YES"
:on error exit
-- DbName and ConfirmRollback are NOT :setvar'd on purpose (a :setvar would override -v). If either is missing sqlcmd stops
-- with "scripting variable not defined". SSMS (SQLCMD Mode): add  :setvar DbName "..."  and  :setvar ConfirmRollback "YES"  above.
IF N'$(ConfirmRollback)' <> N'YES'
BEGIN
    RAISERROR (N'106 rollback NOT run: ConfirmRollback is not YES. This script is DO NOT RUN unless you mean it.', 16, 1);
    SET NOEXEC ON;
END
GO
USE [$(DbName)];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
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
PRINT N'106 rollback done. Prep settings AssetTag.Prefix.Section.* in SystemSetting (if any) are harmless leftovers.';
GO
SET NOEXEC OFF;
