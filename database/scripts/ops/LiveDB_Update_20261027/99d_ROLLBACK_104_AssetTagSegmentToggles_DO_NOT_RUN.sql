-- 99d  Rollback of 104_AssetTagSegmentToggles. SPECIAL: only if 01 reported 104 ABSENT before this run (i.e. 02 added it now). If 104 was already on live from 21 Oct, do NOT run this - the 21 Oct rollback (/workspace/live-update/tagseg/99_rollback.sql) is the reference. Never touches 104_widen_auditlog_entityid.
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
IF COL_LENGTH(N'dbo.Department', N'AssetTagUseDepartmentSegment') IS NOT NULL
    ALTER TABLE [dbo].[Department] DROP COLUMN [AssetTagUseDepartmentSegment];
IF COL_LENGTH(N'dbo.Department', N'AssetTagUseSubDepartmentSegment') IS NOT NULL
    ALTER TABLE [dbo].[Department] DROP COLUMN [AssetTagUseSubDepartmentSegment];
IF COL_LENGTH(N'dbo.Department', N'AssetTagUseRoomSegment') IS NOT NULL
    ALTER TABLE [dbo].[Department] DROP COLUMN [AssetTagUseRoomSegment];
DELETE FROM [dbo].[SystemSetting] WHERE [SettingKey] IN (N'AssetTag.Segment.Department', N'AssetTag.Segment.SubDepartment', N'AssetTag.Segment.Room');
DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'104_AssetTagSegmentToggles.sql';
COMMIT TRANSACTION;
PRINT N'99d done: 104 tag segment switches rolled back.';
GO
SET NOEXEC OFF;
GO
