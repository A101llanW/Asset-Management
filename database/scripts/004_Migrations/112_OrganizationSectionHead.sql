-- 112 (Organisation section heads): one Head of KG / Prep / Senior school per company.
-- What it does (additive and idempotent; no existing department head, tag, asset or approval is changed):
--   1. Table OrganizationSectionHead (OrganizationId, Section 0/1/2, HeadUserId NULL until set).
--   2. Unique (OrganizationId, Section); FKs to Organization and Users (WITH NOCHECK, same pattern as 107).
--   3. Seed: one row per existing Organisation for KG (2), Prep (1) and Senior school (0), HeadUserId NULL.
--   4. Soft backfill: when every non-null Department head for that section column in an org is the SAME user,
--      copy that user onto OrganizationSectionHead.HeadUserId. Mixed or empty -> leave NULL (department
--      overrides from 107/111 keep working unchanged).
--   5. SchemaMigrationHistory row.
-- Depends on 106 (Section), 107 (Prep/Senior heads), 111 (KG head). Must run BEFORE the matching code is published
-- (the entity map reads OrganizationSectionHead). Idempotent: every step is guarded; re-running is a no-op.
-- Rollback: database/scripts/ops/112_Rollback_OrganizationSectionHead_PREPARE_DO_NOT_RUN.sql
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF COL_LENGTH(N'dbo.Department', N'Section') IS NULL
   OR COL_LENGTH(N'dbo.Department', N'PrepHeadUserId') IS NULL
   OR COL_LENGTH(N'dbo.Department', N'KgHeadUserId') IS NULL
BEGIN
    RAISERROR (N'112 NOT run: migrations 106, 107 and 111 must be applied first (Department.Section / PrepHeadUserId / KgHeadUserId missing).', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.OrganizationSectionHead', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrganizationSectionHead]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_OrganizationSectionHead] PRIMARY KEY CLUSTERED,
        [OrganizationId] INT NOT NULL,
        [Section] INT NOT NULL,
        [HeadUserId] NVARCHAR(128) NULL,
        CONSTRAINT [CK_OrganizationSectionHead_Section] CHECK ([Section] IN (0, 1, 2))
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'dbo.OrganizationSectionHead') AND [name] = N'UX_OrganizationSectionHead_Org_Section')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_OrganizationSectionHead_Org_Section]
        ON [dbo].[OrganizationSectionHead] ([OrganizationId], [Section]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [parent_object_id] = OBJECT_ID(N'dbo.OrganizationSectionHead') AND [name] = N'FK_OrganizationSectionHead_Organization')
BEGIN
    EXEC (N'ALTER TABLE [dbo].[OrganizationSectionHead] WITH NOCHECK ADD CONSTRAINT [FK_OrganizationSectionHead_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization]([Id]);');
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [parent_object_id] = OBJECT_ID(N'dbo.OrganizationSectionHead') AND [name] = N'FK_OrganizationSectionHead_HeadUser')
BEGIN
    EXEC (N'ALTER TABLE [dbo].[OrganizationSectionHead] WITH NOCHECK ADD CONSTRAINT [FK_OrganizationSectionHead_HeadUser] FOREIGN KEY ([HeadUserId]) REFERENCES [dbo].[Users]([Id]);');
END

-- Seed one row per org per section (NULL head).
INSERT INTO [dbo].[OrganizationSectionHead] ([OrganizationId], [Section], [HeadUserId])
SELECT o.[Id], s.[Section], NULL
FROM [dbo].[Organization] o
CROSS JOIN (SELECT 0 AS [Section] UNION ALL SELECT 1 UNION ALL SELECT 2) s
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[OrganizationSectionHead] h
    WHERE h.[OrganizationId] = o.[Id] AND h.[Section] = s.[Section]);

-- Soft backfill from department columns when the org has exactly one distinct non-null head for that section.
;WITH DeptHeads AS (
    SELECT d.[OrganizationId], 1 AS [Section], d.[PrepHeadUserId] AS [HeadUserId]
    FROM [dbo].[Department] d
    WHERE d.[OrganizationId] IS NOT NULL AND NULLIF(LTRIM(RTRIM(d.[PrepHeadUserId])), N'') IS NOT NULL
    UNION ALL
    SELECT d.[OrganizationId], 0 AS [Section], d.[SeniorHeadUserId]
    FROM [dbo].[Department] d
    WHERE d.[OrganizationId] IS NOT NULL AND NULLIF(LTRIM(RTRIM(d.[SeniorHeadUserId])), N'') IS NOT NULL
    UNION ALL
    SELECT d.[OrganizationId], 2 AS [Section], d.[KgHeadUserId]
    FROM [dbo].[Department] d
    WHERE d.[OrganizationId] IS NOT NULL AND NULLIF(LTRIM(RTRIM(d.[KgHeadUserId])), N'') IS NOT NULL
),
DistinctHeads AS (
    SELECT [OrganizationId], [Section], MIN([HeadUserId]) AS [HeadUserId], COUNT(DISTINCT [HeadUserId]) AS [DistinctCount]
    FROM DeptHeads
    GROUP BY [OrganizationId], [Section]
)
UPDATE h
SET h.[HeadUserId] = d.[HeadUserId]
FROM [dbo].[OrganizationSectionHead] h
INNER JOIN DistinctHeads d ON d.[OrganizationId] = h.[OrganizationId] AND d.[Section] = h.[Section]
WHERE h.[HeadUserId] IS NULL
  AND d.[DistinctCount] = 1;

IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'112_OrganizationSectionHead.sql')
BEGIN
    INSERT INTO [dbo].[SchemaMigrationHistory] ([ScriptName], [Checksum], [AppliedAt], [AppliedBy])
    VALUES (N'112_OrganizationSectionHead.sql', NULL, SYSUTCDATETIME(), SUSER_SNAME());
END

COMMIT TRANSACTION;
PRINT N'112 done: OrganizationSectionHead present (one row per org per section; HeadUserId NULL or soft-backfilled).';
