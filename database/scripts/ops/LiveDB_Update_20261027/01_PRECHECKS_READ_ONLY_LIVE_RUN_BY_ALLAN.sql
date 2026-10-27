-- 01  Pre-checks.  READ-ONLY (only SELECTs).  LIVE_RUN_BY_ALLAN.
-- Reports what is already on the database so the run is safe whether or not migration 104 (tag segment switches)
-- really reached live on 21 Oct.
-- Expected on live (if the 21 Oct console run happened):
--   103_AssetTagRename.sql            present
--   104_AssetTagSegmentToggles.sql    present (history row Id 107 on live), 3 columns present
--   105 / 106 / 107                   absent
--   RESULT = READY
-- Review S1: the 104 state is split in two:
--   READY_104_MISSING       the 3 columns are ABSENT: 02 adds them and the history row, marked
--                           Checksum = 'LiveDB_20261027:added-columns' (only this case may later use 99d).
--   READY_104_HISTORY_ONLY  the 3 columns are PRESENT but there is no history row: 02 only adds the history row
--                           (Checksum = 'LiveDB_20261027:history-only'); 99d will refuse, the columns stay.
-- RESULT = STOP_*: do not continue; see README "If something fails".
-- RESULT = ALREADY_APPLIED: 105-107 are already there; re-running 02-05 is harmless (no-op) but not needed; run 09.
-- Note: two different scripts carry the number 104. 104_widen_auditlog_entityid.sql (AuditLog.EntityId -> 450 chars)
-- writes NO history row; its state is reported from the column width only. Nothing here renumbers anything.
SET NOCOUNT ON;

SELECT DB_NAME() AS [database], @@SERVERNAME AS [server], SYSUTCDATETIME() AS checked_utc;

SELECT [Id], [ScriptName], [AppliedAt], [AppliedBy]
FROM [dbo].[SchemaMigrationHistory]
WHERE [ScriptName] LIKE N'10[3-9][_]%' OR [ScriptName] LIKE N'11[0-9][_]%'
ORDER BY [Id];

DECLARE @h103 BIT = CASE WHEN EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'103_AssetTagRename.sql') THEN 1 ELSE 0 END;
DECLARE @h104 BIT = CASE WHEN EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'104_AssetTagSegmentToggles.sql') THEN 1 ELSE 0 END;
DECLARE @h105 BIT = CASE WHEN EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'105_LabelPrintBatchLog.sql') THEN 1 ELSE 0 END;
DECLARE @h106 BIT = CASE WHEN EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'106_DepartmentSection.sql') THEN 1 ELSE 0 END;
DECLARE @h107 BIT = CASE WHEN EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'107_DepartmentSectionHeads.sql') THEN 1 ELSE 0 END;

DECLARE @c104 INT =
      CASE WHEN COL_LENGTH(N'dbo.Department', N'AssetTagUseDepartmentSegment')    IS NOT NULL THEN 1 ELSE 0 END
    + CASE WHEN COL_LENGTH(N'dbo.Department', N'AssetTagUseSubDepartmentSegment') IS NOT NULL THEN 1 ELSE 0 END
    + CASE WHEN COL_LENGTH(N'dbo.Department', N'AssetTagUseRoomSegment')          IS NOT NULL THEN 1 ELSE 0 END;
DECLARE @o105 INT = CASE WHEN OBJECT_ID(N'dbo.LabelPrintBatch', N'U') IS NOT NULL THEN 1 ELSE 0 END
                  + CASE WHEN OBJECT_ID(N'dbo.LabelPrintBatchItem', N'U') IS NOT NULL THEN 1 ELSE 0 END;
DECLARE @c106 INT = CASE WHEN COL_LENGTH(N'dbo.Department', N'Section') IS NOT NULL THEN 1 ELSE 0 END
                  + CASE WHEN COL_LENGTH(N'dbo.Department', N'SortOrder') IS NOT NULL THEN 1 ELSE 0 END;
DECLARE @c107 INT = CASE WHEN COL_LENGTH(N'dbo.Department', N'PrepHeadUserId') IS NOT NULL THEN 1 ELSE 0 END
                  + CASE WHEN COL_LENGTH(N'dbo.Department', N'SeniorHeadUserId') IS NOT NULL THEN 1 ELSE 0 END;
DECLARE @auditWidth INT = (SELECT [max_length] / 2 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'dbo.AuditLog') AND [name] = N'EntityId');

SELECT N'103 history row' AS [check], CASE @h103 WHEN 1 THEN N'present' ELSE N'ABSENT' END AS [state], N'must be present' AS expected
UNION ALL SELECT N'104_AssetTagSegmentToggles history row', CASE @h104 WHEN 1 THEN N'present' ELSE N'absent' END, N'present on live if 21 Oct run happened; absent is OK (02 adds it)'
UNION ALL SELECT N'104 tag-segment columns (of 3)', CONVERT(NVARCHAR(10), @c104), N'3 (or 0 if 104 never ran)'
UNION ALL SELECT N'104 history Checksum (marker)', ISNULL((SELECT TOP 1 ISNULL([Checksum], N'(NULL)') FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'104_AssetTagSegmentToggles.sql'), N'(no row)'), N'info: LiveDB_20261027:added-columns only if 02 added the columns'
UNION ALL SELECT N'104_widen_auditlog_entityid (no history row) AuditLog.EntityId chars', CONVERT(NVARCHAR(10), @auditWidth), N'info only: 450 = widened, 100 = not widened; not touched by this run'
UNION ALL SELECT N'105 history row / tables (of 2)', CASE @h105 WHEN 1 THEN N'present' ELSE N'absent' END + N' / ' + CONVERT(NVARCHAR(10), @o105), N'absent / 0'
UNION ALL SELECT N'106 history row / columns (of 2)', CASE @h106 WHEN 1 THEN N'present' ELSE N'absent' END + N' / ' + CONVERT(NVARCHAR(10), @c106), N'absent / 0'
UNION ALL SELECT N'107 history row / columns (of 2)', CASE @h107 WHEN 1 THEN N'present' ELSE N'absent' END + N' / ' + CONVERT(NVARCHAR(10), @c107), N'absent / 0';

-- Review S3: the 11 IDs the guarded 106 data fix looks at, each with its live name and whether it matches.
-- The match tolerates a straight (') or curly (U+2019) apostrophe, the same rule 04 uses ('Prep Counsellor's Room', 1587).
DECLARE @prep TABLE ([Id] INT PRIMARY KEY, [ExpectedName] NVARCHAR(120));
INSERT INTO @prep VALUES
    (1614, N'Prep'), (1582, N'Prep Lunch Area'), (1583, N'Prep Music Room'), (1584, N'Prep Art Room'),
    (1585, N'Prep Sports Office'), (1586, N'Prep Learning Support'), (1587, N'Prep Counsellor''s Room'),
    (1588, N'Prep Reception'), (1589, N'Prep Field'), (1590, N'Prep ICT Lab'), (1591, N'Prep Library');
SELECT p.[Id], p.[ExpectedName], d.[Name] AS live_name, d.[DepartmentKind] AS live_kind, d.[ParentDepartmentId] AS live_parent,
       CASE WHEN d.[Id] IS NULL THEN N'NO (id not found)'
            WHEN REPLACE(d.[Name], NCHAR(8217), N'''') = p.[ExpectedName] THEN N'YES'
            ELSE N'NO (name differs)' END AS [matches],
       CASE WHEN d.[Name] LIKE N'%' + NCHAR(8217) + N'%' THEN N'curly apostrophe (U+2019) - tolerated'
            WHEN d.[Name] LIKE N'%''%' THEN N'straight apostrophe' ELSE N'' END AS apostrophe_note
FROM @prep p LEFT JOIN [dbo].[Department] d ON d.[Id] = p.[Id]
ORDER BY CASE WHEN p.[Id] = 1614 THEN 0 ELSE 1 END, p.[Id];

SELECT COUNT(*) AS prep_rows_106_would_match
FROM @prep p INNER JOIN [dbo].[Department] d ON d.[Id] = p.[Id] AND REPLACE(d.[Name], NCHAR(8217), N'''') = p.[ExpectedName];
-- ^ write this number into @ExpectedPrep at the top of 09 (11 expected on live).

-- Review S3: every unit whose parent is 1614 (the Prep grade). Expected on live: NONE (checked read-only 27 Oct).
-- 106 does not mark children; if rows appear here they stay Senior until changed on Departments > Edit.
SELECT c.[Id], c.[Name], c.[DepartmentKind], c.[IsActive]
FROM [dbo].[Department] c WHERE c.[ParentDepartmentId] = 1614 ORDER BY c.[Id];
SELECT COUNT(*) AS children_of_1614 FROM [dbo].[Department] WHERE [ParentDepartmentId] = 1614;

SELECT CASE
    WHEN @h103 = 0 THEN N'STOP_103_MISSING'
    WHEN (@h104 = 1 AND @c104 <> 3) OR (@h104 = 0 AND @c104 NOT IN (0, 3)) THEN N'STOP_104_PARTIAL'
    WHEN @h105 = 1 AND @h106 = 1 AND @h107 = 1 AND @o105 = 2 AND @c106 = 2 AND @c107 = 2 THEN N'ALREADY_APPLIED'
    WHEN (@h105 = 0 AND @o105 > 0) OR (@h106 = 0 AND @c106 > 0) OR (@h107 = 0 AND @c107 > 0)
      OR (@h105 = 1 AND @o105 <> 2) OR (@h106 = 1 AND @c106 <> 2) OR (@h107 = 1 AND @c107 <> 2) THEN N'STOP_PARTIAL_105_107'
    WHEN @c104 = 0 THEN N'READY_104_MISSING'
    WHEN @h104 = 0 AND @c104 = 3 THEN N'READY_104_HISTORY_ONLY'
    ELSE N'READY' END AS [RESULT];
GO
