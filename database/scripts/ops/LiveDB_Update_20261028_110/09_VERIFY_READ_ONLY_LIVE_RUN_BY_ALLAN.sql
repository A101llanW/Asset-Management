-- 09  Verify after 02.  READ-ONLY (only SELECTs).  LIVE_RUN_BY_ALLAN.
-- Expected: 10 rows (the new columns, all is_nullable = 1), the 110 history row, and RESULT = OK.
SET NOCOUNT ON;
SELECT OBJECT_NAME(c.[object_id]) AS [table], c.[name] AS [column], t.[name] AS [type], c.[max_length], c.[is_nullable]
FROM sys.columns c JOIN sys.types t ON t.[user_type_id] = c.[user_type_id]
WHERE (c.[object_id] = OBJECT_ID(N'dbo.PurchaseRequest')
       AND c.[name] IN (N'RequisitionTargetKind', N'TargetUserId', N'TargetPlacement', N'TargetDepartmentId'))
   OR (c.[object_id] IN (OBJECT_ID(N'dbo.AssetCategory'), OBJECT_ID(N'dbo.AssetType'), OBJECT_ID(N'dbo.AssetSubType'))
       AND c.[name] IN (N'CreatedById', N'CreatedVia'))
ORDER BY 1, c.[column_id];

SELECT [Id], [ScriptName], [AppliedAt], [AppliedBy]
FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'110_requisition_target_room_or_person.sql';

SELECT CASE WHEN (SELECT COUNT(*) FROM sys.columns WHERE [object_id] = OBJECT_ID(N'dbo.PurchaseRequest') AND [is_nullable] = 1
                    AND [name] IN (N'RequisitionTargetKind', N'TargetUserId', N'TargetPlacement', N'TargetDepartmentId')) = 4
             AND (SELECT COUNT(*) FROM sys.columns WHERE [is_nullable] = 1 AND [name] IN (N'CreatedById', N'CreatedVia')
                    AND [object_id] IN (OBJECT_ID(N'dbo.AssetCategory'), OBJECT_ID(N'dbo.AssetType'), OBJECT_ID(N'dbo.AssetSubType'))) = 6
             AND EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'110_requisition_target_room_or_person.sql')
            THEN N'OK' ELSE N'NOT_OK' END AS [RESULT];
