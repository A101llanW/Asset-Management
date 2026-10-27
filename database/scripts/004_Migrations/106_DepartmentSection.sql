-- 106 (Prep / Senior school): Section attribute on departments, grades, classes and rooms.
-- What it does (additive and idempotent; no existing row, tag or asset is changed except the guarded data fix in step 4):
--   1. Department.Section INT NOT NULL DEFAULT 0   (0 = Senior school, 1 = Prep). Every existing row becomes Senior school.
--   2. CK_Department_Section: only 0 or 1.
--   3. Department.SortOrder INT NULL: optional sort number for grades (NULL = taken from the G## code).
--   4. Data fix, guarded by ID AND exact name, never matches anything else:
--        Grade 'Prep' (ID 1614) and the ten Prep rooms (IDs 1582-1591) -> Section = Prep.
--      A row whose ID/name does not match is left alone (no-op); a row already Prep is left alone.
--   5. SchemaMigrationHistory row.
-- Tenant settings AssetTag.Prefix.Section.Prep / AssetTag.Prefix.Section.Senior are SystemSetting rows written by
-- Settings > Asset tag prefix; nothing is inserted here (missing = no section prefix = tags behave exactly as before).
-- Must run BEFORE the matching code is published (the Department entity map reads Section and SortOrder).
-- Idempotent: every step is guarded; re-running is a no-op.
-- Rollback (see database/scripts/ops/106_Rollback_DepartmentSection_PREPARE_DO_NOT_RUN.sql):
--   ALTER TABLE [dbo].[Department] DROP CONSTRAINT [CK_Department_Section];
--   ALTER TABLE [dbo].[Department] DROP CONSTRAINT [DF_Department_Section];
--   ALTER TABLE [dbo].[Department] DROP COLUMN [Section], [SortOrder];   (only after redeploying the previous build)
--   DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'106_DepartmentSection.sql';
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.Department', N'Section') IS NULL
BEGIN
    ALTER TABLE [dbo].[Department] ADD [Section] INT NOT NULL CONSTRAINT [DF_Department_Section] DEFAULT (0);
END

IF COL_LENGTH(N'dbo.Department', N'SortOrder') IS NULL
BEGIN
    ALTER TABLE [dbo].[Department] ADD [SortOrder] INT NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [parent_object_id] = OBJECT_ID(N'dbo.Department') AND [name] = N'CK_Department_Section')
BEGIN
    -- Dynamic SQL: the column was added in this same batch.
    EXEC (N'ALTER TABLE [dbo].[Department] WITH CHECK ADD CONSTRAINT [CK_Department_Section] CHECK ([Section] IN (0, 1));');
END

-- Step 4: guarded data fix. #SectionFix holds (ID, exact name); the UPDATE needs BOTH to match.
CREATE TABLE #SectionFix ([Id] INT NOT NULL PRIMARY KEY, [Name] NVARCHAR(120) NOT NULL);
INSERT INTO #SectionFix ([Id], [Name]) VALUES
    (1614, N'Prep'),
    (1582, N'Prep Lunch Area'),
    (1583, N'Prep Music Room'),
    (1584, N'Prep Art Room'),
    (1585, N'Prep Sports Office'),
    (1586, N'Prep Learning Support'),
    (1587, N'Prep Counsellor''s Room'),
    (1588, N'Prep Reception'),
    (1589, N'Prep Field'),
    (1590, N'Prep ICT Lab'),
    (1591, N'Prep Library');

DECLARE @fixed INT = 0;
EXEC sp_executesql
    N'UPDATE d SET d.[Section] = 1
      FROM [dbo].[Department] d
      INNER JOIN #SectionFix f ON f.[Id] = d.[Id] AND f.[Name] = d.[Name]
      WHERE d.[Section] <> 1;
      SET @fixedOut = @@ROWCOUNT;',
    N'@fixedOut INT OUTPUT',
    @fixedOut = @fixed OUTPUT;
PRINT N'106 data fix: ' + CONVERT(NVARCHAR(10), @fixed) + N' row(s) set to Section = Prep (0 is normal when the IDs/names do not match or were already set).';
DROP TABLE #SectionFix;

IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'106_DepartmentSection.sql')
BEGIN
    INSERT INTO [dbo].[SchemaMigrationHistory] ([ScriptName], [Checksum], [AppliedAt], [AppliedBy])
    VALUES (N'106_DepartmentSection.sql', NULL, SYSUTCDATETIME(), SUSER_SNAME());
END

COMMIT TRANSACTION;
