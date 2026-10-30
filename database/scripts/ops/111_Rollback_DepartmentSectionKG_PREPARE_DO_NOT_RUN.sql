-- 111 ROLLBACK - PREPARED, DO NOT RUN. Not part of the migration set; the migration runner never picks this folder up.
-- Purpose: undo 111_DepartmentSectionKG.sql. ONLY after the previous build (without KG) is live again; the KG build reads
--   KgHeadUserId and breaks the whole site if the column disappears first.
-- Refuses (changes nothing) while any department is in Section KG (2) or has a KG head set: move those units to Prep or
--   Senior school and clear the KG heads on Departments > Edit first. Take a COPY_ONLY backup first.
-- To run on purpose: change @Confirm below to N'YES'. Single batch (no GO), safe for the /Sys/Database console.
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @Confirm NVARCHAR(10) = N'NO';

IF @Confirm IS NULL OR @Confirm <> N'YES'
BEGIN
    RAISERROR (N'111 rollback NOT run: @Confirm is not YES. This script is DO NOT RUN unless you mean it.', 16, 1);
    RETURN;
END

IF COL_LENGTH(N'dbo.Department', N'Section') IS NOT NULL
   AND EXISTS (SELECT 1 FROM [dbo].[Department] WHERE [Section] = 2)
BEGIN
    RAISERROR (N'111 rollback NOT run: some departments are still in Section KG (2). Move them to Prep or Senior school first.', 16, 1);
    RETURN;
END

IF COL_LENGTH(N'dbo.Department', N'KgHeadUserId') IS NOT NULL
BEGIN
    DECLARE @kgHeads INT;
    EXEC sp_executesql N'SELECT @n = COUNT(*) FROM [dbo].[Department] WHERE [KgHeadUserId] IS NOT NULL;', N'@n INT OUTPUT', @n = @kgHeads OUTPUT;
    IF @kgHeads > 0
    BEGIN
        RAISERROR (N'111 rollback NOT run: some departments still have a KG head. Clear them on Departments > Edit first.', 16, 1);
        RETURN;
    END
END

BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [parent_object_id] = OBJECT_ID(N'dbo.Department') AND [name] = N'FK_Department_KgHeadUser')
    ALTER TABLE [dbo].[Department] DROP CONSTRAINT [FK_Department_KgHeadUser];

IF COL_LENGTH(N'dbo.Department', N'KgHeadUserId') IS NOT NULL
    ALTER TABLE [dbo].[Department] DROP COLUMN [KgHeadUserId];

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE [parent_object_id] = OBJECT_ID(N'dbo.Department') AND [name] = N'CK_Department_Section')
    ALTER TABLE [dbo].[Department] DROP CONSTRAINT [CK_Department_Section];

EXEC (N'ALTER TABLE [dbo].[Department] WITH CHECK ADD CONSTRAINT [CK_Department_Section] CHECK ([Section] IN (0, 1));');

DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'111_DepartmentSectionKG.sql';

COMMIT TRANSACTION;

SELECT N'111_ROLLBACK_DONE' AS [RESULT];
