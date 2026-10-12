-- PREPARED, DO NOT RUN without sign-off. Rollback of 101_ReceivingSubTypeRequired.sql.
-- Drops the receipt trigger (the app rule in ReceivingService stays until the build is rolled back) and the
-- policy rows 101 seeded (rows an admin saved later carry a different Description and are kept). Idempotent.
SET NOCOUNT ON;
GO
IF OBJECT_ID(N'[dbo].[TR_AssetReceiving_RequireSubType]', N'TR') IS NOT NULL
    DROP TRIGGER [dbo].[TR_AssetReceiving_RequireSubType];
GO
DELETE FROM [dbo].[SystemSetting]
WHERE [SettingKey] IN (N'Purchases.Requisition.ClassificationMode', N'Purchases.Requisition.MixedFlowPolicy')
  AND [Description] LIKE N'Seeded by 101%';
GO
DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'101_ReceivingSubTypeRequired.sql';
GO
SELECT CASE WHEN OBJECT_ID(N'[dbo].[TR_AssetReceiving_RequireSubType]', N'TR') IS NULL THEN 1 ELSE 0 END AS TriggerGone,
       (SELECT COUNT(*) FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'101_ReceivingSubTypeRequired.sql') AS HistoryRows;
GO
