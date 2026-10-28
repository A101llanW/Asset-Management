-- 01  Pre-checks.  READ-ONLY (only SELECTs).  LIVE_RUN_BY_ALLAN.
-- Reports what is already on the database before migration 110.
-- Expected on live: 108 present (110 needs the 108 requisition-line columns), 110 absent, 0 of the 10 columns present.
-- RESULT = READY            run 02.
-- RESULT = ALREADY_APPLIED  110 is already there; 02 would be a harmless no-op; run 09 only.
-- RESULT = STOP_*           do not continue; see README "If something fails".
SET NOCOUNT ON;

SELECT DB_NAME() AS [database], @@SERVERNAME AS [server], SYSUTCDATETIME() AS checked_utc;

SELECT [Id], [ScriptName], [AppliedAt], [AppliedBy]
FROM [dbo].[SchemaMigrationHistory]
WHERE [ScriptName] LIKE N'10[7-9][_]%' OR [ScriptName] LIKE N'11[0-9][_]%'
ORDER BY [Id];

DECLARE @h108 BIT = CASE WHEN EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'108_requisition_line_asset_name.sql') THEN 1 ELSE 0 END;
DECLARE @h110 BIT = CASE WHEN EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'110_requisition_target_room_or_person.sql') THEN 1 ELSE 0 END;
DECLARE @c110 INT =
      CASE WHEN COL_LENGTH(N'dbo.PurchaseRequest', N'RequisitionTargetKind') IS NOT NULL THEN 1 ELSE 0 END
    + CASE WHEN COL_LENGTH(N'dbo.PurchaseRequest', N'TargetUserId')          IS NOT NULL THEN 1 ELSE 0 END
    + CASE WHEN COL_LENGTH(N'dbo.PurchaseRequest', N'TargetPlacement')       IS NOT NULL THEN 1 ELSE 0 END
    + CASE WHEN COL_LENGTH(N'dbo.PurchaseRequest', N'TargetDepartmentId')    IS NOT NULL THEN 1 ELSE 0 END
    + CASE WHEN COL_LENGTH(N'dbo.AssetCategory', N'CreatedById') IS NOT NULL THEN 1 ELSE 0 END
    + CASE WHEN COL_LENGTH(N'dbo.AssetCategory', N'CreatedVia') IS NOT NULL THEN 1 ELSE 0 END
    + CASE WHEN COL_LENGTH(N'dbo.AssetType', N'CreatedById') IS NOT NULL THEN 1 ELSE 0 END
    + CASE WHEN COL_LENGTH(N'dbo.AssetType', N'CreatedVia') IS NOT NULL THEN 1 ELSE 0 END
    + CASE WHEN COL_LENGTH(N'dbo.AssetSubType', N'CreatedById') IS NOT NULL THEN 1 ELSE 0 END
    + CASE WHEN COL_LENGTH(N'dbo.AssetSubType', N'CreatedVia') IS NOT NULL THEN 1 ELSE 0 END;
DECLARE @c108 INT = CASE WHEN COL_LENGTH(N'dbo.PurchaseRequestLine', N'ItemName') IS NOT NULL THEN 1 ELSE 0 END
                  + CASE WHEN COL_LENGTH(N'dbo.PurchaseRequestLine', N'TargetAssetId') IS NOT NULL THEN 1 ELSE 0 END;

DECLARE @backupOk BIT = 0;
BEGIN TRY
    IF EXISTS (SELECT 1 FROM msdb.dbo.backupset b
               WHERE b.[database_name] = DB_NAME() AND b.[type] = 'D' AND b.[is_copy_only] = 1
                 AND b.[backup_finish_date] >= DATEADD(HOUR, -2, GETDATE()))
        SET @backupOk = 1;
END TRY
BEGIN CATCH
    SET @backupOk = 0;
END CATCH

SELECT N'108 history row / line columns (of 2)' AS [check], CASE @h108 WHEN 1 THEN N'present' ELSE N'ABSENT' END + N' / ' + CONVERT(NVARCHAR(10), @c108) AS [state], N'present / 2' AS expected
UNION ALL SELECT N'110 history row / new columns (of 10)', CASE @h110 WHEN 1 THEN N'present' ELSE N'absent' END + N' / ' + CONVERT(NVARCHAR(10), @c110), N'absent / 0'
UNION ALL SELECT N'COPY_ONLY full backup in the last 2 hours (00)', CASE @backupOk WHEN 1 THEN N'yes' ELSE N'NO' END, N'yes (run 00 first)'
UNION ALL SELECT N'PurchaseRequest rows (info: none are changed by 110)', CONVERT(NVARCHAR(20), (SELECT COUNT_BIG(*) FROM [dbo].[PurchaseRequest])), N'info only';

SELECT CASE
    WHEN @h108 = 0 OR @c108 < 2 THEN N'STOP_108_MISSING'
    WHEN @h110 = 1 AND @c110 = 10 THEN N'ALREADY_APPLIED'
    WHEN @h110 = 1 OR (@c110 > 0 AND @c110 < 10) THEN N'STOP_110_PARTIAL'
    WHEN @c110 = 10 THEN N'STOP_110_COLUMNS_WITHOUT_HISTORY'
    WHEN @backupOk = 0 THEN N'STOP_NO_BACKUP'
    ELSE N'READY' END AS [RESULT];
