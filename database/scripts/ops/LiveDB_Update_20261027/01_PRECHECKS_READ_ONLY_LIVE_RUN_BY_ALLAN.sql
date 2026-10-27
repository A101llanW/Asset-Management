-- 01  Pre-checks.  READ-ONLY (only SELECTs).  LIVE_RUN_BY_ALLAN.
-- Reports what is already on the database so the run is safe whether or not migration 104 (tag segment switches)
-- really reached live on 21 Oct.
-- Expected on live (if the 21 Oct console run happened):
--   103_AssetTagRename.sql            present
--   104_AssetTagSegmentToggles.sql    present (history row Id 107 on live), 3 columns present
--   105 / 106 / 107                   absent
--   RESULT = READY
-- If 104 turns out to be ABSENT, RESULT = READY_104_MISSING: that is fine, 02 adds it (guarded).
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
UNION ALL SELECT N'104_widen_auditlog_entityid (no history row) AuditLog.EntityId chars', CONVERT(NVARCHAR(10), @auditWidth), N'info only: 450 = widened, 100 = not widened; not touched by this run'
UNION ALL SELECT N'105 history row / tables (of 2)', CASE @h105 WHEN 1 THEN N'present' ELSE N'absent' END + N' / ' + CONVERT(NVARCHAR(10), @o105), N'absent / 0'
UNION ALL SELECT N'106 history row / columns (of 2)', CASE @h106 WHEN 1 THEN N'present' ELSE N'absent' END + N' / ' + CONVERT(NVARCHAR(10), @c106), N'absent / 0'
UNION ALL SELECT N'107 history row / columns (of 2)', CASE @h107 WHEN 1 THEN N'present' ELSE N'absent' END + N' / ' + CONVERT(NVARCHAR(10), @c107), N'absent / 0';

-- Rows the guarded 106 data fix would mark as Prep (ID AND exact name must both match; 0-11).
SELECT COUNT(*) AS prep_rows_106_would_match
FROM [dbo].[Department] d
WHERE (d.[Id] = 1614 AND d.[Name] = N'Prep')
   OR (d.[Id] = 1582 AND d.[Name] = N'Prep Lunch Area')
   OR (d.[Id] = 1583 AND d.[Name] = N'Prep Music Room')
   OR (d.[Id] = 1584 AND d.[Name] = N'Prep Art Room')
   OR (d.[Id] = 1585 AND d.[Name] = N'Prep Sports Office')
   OR (d.[Id] = 1586 AND d.[Name] = N'Prep Learning Support')
   OR (d.[Id] = 1587 AND d.[Name] = N'Prep Counsellor''s Room')
   OR (d.[Id] = 1588 AND d.[Name] = N'Prep Reception')
   OR (d.[Id] = 1589 AND d.[Name] = N'Prep Field')
   OR (d.[Id] = 1590 AND d.[Name] = N'Prep ICT Lab')
   OR (d.[Id] = 1591 AND d.[Name] = N'Prep Library');

SELECT CASE
    WHEN @h103 = 0 THEN N'STOP_103_MISSING'
    WHEN (@h104 = 1 AND @c104 <> 3) OR (@h104 = 0 AND @c104 NOT IN (0, 3)) THEN N'STOP_104_PARTIAL'
    WHEN @h105 = 1 AND @h106 = 1 AND @h107 = 1 AND @o105 = 2 AND @c106 = 2 AND @c107 = 2 THEN N'ALREADY_APPLIED'
    WHEN (@h105 = 0 AND @o105 > 0) OR (@h106 = 0 AND @c106 > 0) OR (@h107 = 0 AND @c107 > 0)
      OR (@h105 = 1 AND @o105 <> 2) OR (@h106 = 1 AND @c106 <> 2) OR (@h107 = 1 AND @c107 <> 2) THEN N'STOP_PARTIAL_105_107'
    WHEN @h104 = 0 OR @c104 = 0 THEN N'READY_104_MISSING'
    ELSE N'READY' END AS [RESULT];
GO
