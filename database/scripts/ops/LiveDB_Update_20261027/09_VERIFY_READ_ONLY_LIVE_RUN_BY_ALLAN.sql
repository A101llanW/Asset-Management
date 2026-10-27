-- 09  Verification after 02-05.  READ-ONLY (only SELECTs).  LIVE_RUN_BY_ALLAN.
-- Expected: every row in the first result says PASS, and the last line says RESULT = ALL_PASS.
-- Safe to run as often as you like.
SET NOCOUNT ON;

DECLARE @r TABLE ([check] NVARCHAR(200), [state] NVARCHAR(200), [pass] BIT);

INSERT INTO @r SELECT N'104 history row', CASE WHEN EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'104_AssetTagSegmentToggles.sql') THEN N'present' ELSE N'absent' END,
    CASE WHEN EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'104_AssetTagSegmentToggles.sql') THEN 1 ELSE 0 END;
INSERT INTO @r SELECT N'104 columns AssetTagUse{Department,SubDepartment,Room}Segment (BIT NULL)',
    CONVERT(NVARCHAR(10), COUNT(*)) + N' of 3', CASE WHEN COUNT(*) = 3 THEN 1 ELSE 0 END
    FROM sys.columns WHERE [object_id] = OBJECT_ID(N'dbo.Department') AND [is_nullable] = 1 AND TYPE_NAME([system_type_id]) = N'bit'
      AND [name] IN (N'AssetTagUseDepartmentSegment', N'AssetTagUseSubDepartmentSegment', N'AssetTagUseRoomSegment');
INSERT INTO @r SELECT N'105 history row', CASE WHEN EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'105_LabelPrintBatchLog.sql') THEN N'present' ELSE N'absent' END,
    CASE WHEN EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'105_LabelPrintBatchLog.sql') THEN 1 ELSE 0 END;
INSERT INTO @r SELECT N'105 tables LabelPrintBatch + LabelPrintBatchItem',
    CONVERT(NVARCHAR(10), (CASE WHEN OBJECT_ID(N'dbo.LabelPrintBatch', N'U') IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN OBJECT_ID(N'dbo.LabelPrintBatchItem', N'U') IS NOT NULL THEN 1 ELSE 0 END)) + N' of 2',
    CASE WHEN OBJECT_ID(N'dbo.LabelPrintBatch', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.LabelPrintBatchItem', N'U') IS NOT NULL THEN 1 ELSE 0 END;
INSERT INTO @r SELECT N'106 history row', CASE WHEN EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'106_DepartmentSection.sql') THEN N'present' ELSE N'absent' END,
    CASE WHEN EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'106_DepartmentSection.sql') THEN 1 ELSE 0 END;
INSERT INTO @r SELECT N'106 Department.Section (INT NOT NULL) + SortOrder + CK_Department_Section',
    CONVERT(NVARCHAR(10),
        (CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'dbo.Department') AND [name] = N'Section' AND [is_nullable] = 0) THEN 1 ELSE 0 END)
      + (CASE WHEN COL_LENGTH(N'dbo.Department', N'SortOrder') IS NOT NULL THEN 1 ELSE 0 END)
      + (CASE WHEN EXISTS (SELECT 1 FROM sys.check_constraints WHERE [parent_object_id] = OBJECT_ID(N'dbo.Department') AND [name] = N'CK_Department_Section') THEN 1 ELSE 0 END)) + N' of 3',
    CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'dbo.Department') AND [name] = N'Section' AND [is_nullable] = 0)
          AND COL_LENGTH(N'dbo.Department', N'SortOrder') IS NOT NULL
          AND EXISTS (SELECT 1 FROM sys.check_constraints WHERE [parent_object_id] = OBJECT_ID(N'dbo.Department') AND [name] = N'CK_Department_Section') THEN 1 ELSE 0 END;
INSERT INTO @r SELECT N'107 history row', CASE WHEN EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'107_DepartmentSectionHeads.sql') THEN N'present' ELSE N'absent' END,
    CASE WHEN EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'107_DepartmentSectionHeads.sql') THEN 1 ELSE 0 END;
INSERT INTO @r SELECT N'107 PrepHeadUserId + SeniorHeadUserId + 2 FKs',
    CONVERT(NVARCHAR(10),
        (CASE WHEN COL_LENGTH(N'dbo.Department', N'PrepHeadUserId') IS NOT NULL THEN 1 ELSE 0 END)
      + (CASE WHEN COL_LENGTH(N'dbo.Department', N'SeniorHeadUserId') IS NOT NULL THEN 1 ELSE 0 END)
      + (SELECT COUNT(*) FROM sys.foreign_keys WHERE [parent_object_id] = OBJECT_ID(N'dbo.Department') AND [name] IN (N'FK_Department_PrepHeadUser', N'FK_Department_SeniorHeadUser'))) + N' of 4',
    CASE WHEN COL_LENGTH(N'dbo.Department', N'PrepHeadUserId') IS NOT NULL AND COL_LENGTH(N'dbo.Department', N'SeniorHeadUserId') IS NOT NULL
          AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE [parent_object_id] = OBJECT_ID(N'dbo.Department') AND [name] IN (N'FK_Department_PrepHeadUser', N'FK_Department_SeniorHeadUser')) = 2 THEN 1 ELSE 0 END;
INSERT INTO @r SELECT N'each history name appears once (no duplicates from re-runs)',
    CONVERT(NVARCHAR(10), COUNT(*)) + N' duplicate name(s)', CASE WHEN COUNT(*) = 0 THEN 1 ELSE 0 END
    FROM (SELECT [ScriptName] FROM [dbo].[SchemaMigrationHistory]
          WHERE [ScriptName] IN (N'104_AssetTagSegmentToggles.sql', N'105_LabelPrintBatchLog.sql', N'106_DepartmentSection.sql', N'107_DepartmentSectionHeads.sql')
          GROUP BY [ScriptName] HAVING COUNT(*) > 1) d;

SELECT [check], [state], CASE [pass] WHEN 1 THEN N'PASS' ELSE N'FAIL' END AS result FROM @r;

-- Information (not pass/fail): new columns must not have changed any existing tag or asset.
IF COL_LENGTH(N'dbo.Department', N'Section') IS NOT NULL AND COL_LENGTH(N'dbo.Department', N'PrepHeadUserId') IS NOT NULL
    EXEC (N'SELECT SUM(CASE WHEN [Section] = 1 THEN 1 ELSE 0 END) AS prep_units,
                  SUM(CASE WHEN [Section] = 0 THEN 1 ELSE 0 END) AS senior_units,
                  SUM(CASE WHEN [PrepHeadUserId] IS NOT NULL OR [SeniorHeadUserId] IS NOT NULL THEN 1 ELSE 0 END) AS units_with_section_heads_set
           FROM [dbo].[Department];');

SELECT [Id], [ScriptName], [AppliedAt], [AppliedBy] FROM [dbo].[SchemaMigrationHistory]
WHERE [ScriptName] LIKE N'10[4-9][_]%' ORDER BY [Id];

SELECT CASE WHEN EXISTS (SELECT 1 FROM @r WHERE [pass] = 0) THEN N'SOME_FAIL' ELSE N'ALL_PASS' END AS [RESULT];
GO
