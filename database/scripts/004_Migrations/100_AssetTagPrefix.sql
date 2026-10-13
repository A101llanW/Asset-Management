-- 100 (rel1013a): configurable asset tag prefix per tenant.
-- What it does (additive only, no existing row or tag is changed):
--   1. Department.AssetTagPrefix NVARCHAR(20) NULL - optional override on a department, sub-department or room (NULL = inherit).
--   2. AssetTagSequence (OrganizationId, Prefix) -> LastValue - concurrency-safe counter for PREFIX-NNN tags.
--   3. Makes sure the unique index IX_Asset_Org_AssetTag (OrganizationId, AssetTag) WHERE AssetTag IS NOT NULL exists
--      (it already does on current databases; it is only created if missing AND there are no duplicate tags).
-- Tenant settings (AssetTag.Prefix.Enabled, AssetTag.Prefix.TenantDefault) are SystemSetting rows written by the
-- Settings > Asset tag prefix page; no rows are inserted here (missing = enabled, derived DEPT-ROOM prefix).
-- Must run BEFORE the rel1013a code is published (the Department entity map reads AssetTagPrefix).
-- Idempotent: every step is guarded; re-running is a no-op.
-- Rollback:
--   DROP TABLE [dbo].[AssetTagSequence];
--   ALTER TABLE [dbo].[Department] DROP COLUMN [AssetTagPrefix];   (only after redeploying the previous build)
--   DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'100_AssetTagPrefix.sql';
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.Department', N'AssetTagPrefix') IS NULL
BEGIN
    ALTER TABLE [dbo].[Department] ADD [AssetTagPrefix] NVARCHAR(20) NULL;
END

IF OBJECT_ID(N'dbo.AssetTagSequence', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AssetTagSequence]
    (
        [OrganizationId] INT NOT NULL,
        [Prefix] NVARCHAR(20) NOT NULL,
        [LastValue] INT NOT NULL CONSTRAINT [DF_AssetTagSequence_LastValue] DEFAULT (0),
        [UpdatedAt] DATETIME2(3) NOT NULL CONSTRAINT [DF_AssetTagSequence_UpdatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_AssetTagSequence] PRIMARY KEY CLUSTERED ([OrganizationId], [Prefix])
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'dbo.Asset') AND [name] = N'IX_Asset_Org_AssetTag')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [dbo].[Asset] WHERE [AssetTag] IS NOT NULL GROUP BY [OrganizationId], [AssetTag] HAVING COUNT(*) > 1)
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX [IX_Asset_Org_AssetTag] ON [dbo].[Asset] ([OrganizationId], [AssetTag]) WHERE [AssetTag] IS NOT NULL;
    END
    ELSE
    BEGIN
        PRINT N'WARNING: duplicate (OrganizationId, AssetTag) rows exist; IX_Asset_Org_AssetTag NOT created. The tag counter still avoids repeats; fix the duplicates and re-run.';
    END
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'100_AssetTagPrefix.sql')
BEGIN
    INSERT INTO [dbo].[SchemaMigrationHistory] ([ScriptName], [Checksum], [AppliedAt], [AppliedBy])
    VALUES (N'100_AssetTagPrefix.sql', NULL, SYSUTCDATETIME(), SUSER_SNAME());
END

COMMIT TRANSACTION;