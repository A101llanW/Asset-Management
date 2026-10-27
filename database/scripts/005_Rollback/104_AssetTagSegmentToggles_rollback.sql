-- Rollback of 104 (rel1018b). Run ONLY after the rel1018a build is back on the site (rel1018b code reads these columns).
-- Removes the three Department switches and the three tenant default SystemSetting rows. Tags already generated stay as they are.
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
