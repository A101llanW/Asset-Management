-- 104 (rel1018b): per-level "use this segment" switches for derived asset tag prefixes.
-- What it does (additive only, no existing row or tag is changed; NULL = inherit, so behaviour is identical to rel1018a until someone sets a switch):
--   Department.AssetTagUseDepartmentSegment    BIT NULL - use the top-department code in derived tags for this node and below.
--   Department.AssetTagUseSubDepartmentSegment BIT NULL - use the sub-department code.
--   Department.AssetTagUseRoomSegment          BIT NULL - use the room code.
-- Tenant defaults are SystemSetting rows written by Settings > Asset tag prefix (no rows inserted here; missing = rel1018a shape):
--   AssetTag.Segment.Department (default True), AssetTag.Segment.SubDepartment (default False), AssetTag.Segment.Room (default True).
-- Must run BEFORE the rel1018b code is published (the Department entity map reads the new columns). Needs 102.
-- Idempotent: every step is guarded; re-running is a no-op.
-- Rollback: database/scripts/005_Rollback/104_AssetTagSegmentToggles_rollback.sql (only after redeploying rel1018a).
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.Department', N'AssetTagUseDepartmentSegment') IS NULL
    ALTER TABLE [dbo].[Department] ADD [AssetTagUseDepartmentSegment] BIT NULL;

IF COL_LENGTH(N'dbo.Department', N'AssetTagUseSubDepartmentSegment') IS NULL
    ALTER TABLE [dbo].[Department] ADD [AssetTagUseSubDepartmentSegment] BIT NULL;

IF COL_LENGTH(N'dbo.Department', N'AssetTagUseRoomSegment') IS NULL
    ALTER TABLE [dbo].[Department] ADD [AssetTagUseRoomSegment] BIT NULL;

IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'104_AssetTagSegmentToggles.sql')
BEGIN
    INSERT INTO [dbo].[SchemaMigrationHistory] ([ScriptName], [Checksum], [AppliedAt], [AppliedBy])
    VALUES (N'104_AssetTagSegmentToggles.sql', NULL, SYSUTCDATETIME(), SUSER_SNAME());
END

COMMIT TRANSACTION;
