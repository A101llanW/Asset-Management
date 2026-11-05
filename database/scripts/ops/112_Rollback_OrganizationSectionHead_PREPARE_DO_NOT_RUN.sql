-- ROLLBACK for 112_OrganizationSectionHead.sql. DO NOT RUN on live unless undoing 112 after redeploying a build
-- that does not read OrganizationSectionHead. Confirm by setting @Confirm = N'YES'.
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Confirm NVARCHAR(10) = N'NO';
IF @Confirm <> N'YES'
BEGIN
    RAISERROR (N'112 rollback refused: set @Confirm = N''YES'' inside this script only when intentionally rolling back.', 16, 1);
    RETURN;
END

IF OBJECT_ID(N'dbo.OrganizationSectionHead', N'U') IS NULL
BEGIN
    DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'112_OrganizationSectionHead.sql';
    PRINT N'112 rollback: table already absent; history row cleared if present.';
    RETURN;
END

BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [parent_object_id] = OBJECT_ID(N'dbo.OrganizationSectionHead') AND [name] = N'FK_OrganizationSectionHead_HeadUser')
    ALTER TABLE [dbo].[OrganizationSectionHead] DROP CONSTRAINT [FK_OrganizationSectionHead_HeadUser];

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [parent_object_id] = OBJECT_ID(N'dbo.OrganizationSectionHead') AND [name] = N'FK_OrganizationSectionHead_Organization')
    ALTER TABLE [dbo].[OrganizationSectionHead] DROP CONSTRAINT [FK_OrganizationSectionHead_Organization];

DROP TABLE [dbo].[OrganizationSectionHead];

DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'112_OrganizationSectionHead.sql';

COMMIT TRANSACTION;
PRINT N'112 rollback done: OrganizationSectionHead dropped. Department Prep/Senior/KG head columns (107/111) are untouched.';
