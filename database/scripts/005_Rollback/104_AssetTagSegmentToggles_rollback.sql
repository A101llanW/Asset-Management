-- Rollback of 104 (rel1018b). Run ONLY after the rel1018a build is back on the site (rel1018b code reads these columns).
-- Removes the three Department switches and the three tenant default SystemSetting rows. Tags already generated stay as they are.
-- Review S11: ConfirmRollback guard. Does nothing unless @ConfirmRollback is changed to N'YES'. Run in SSMS/sqlcmd only.
SET NOEXEC OFF;
GO
DECLARE @ConfirmRollback NVARCHAR(3) = N'NO';
IF @ConfirmRollback <> N'YES'
BEGIN
    RAISERROR(N'104 rollback NOT run: @ConfirmRollback is not YES.', 16, 1);
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
GO
SET NOEXEC OFF;
GO
