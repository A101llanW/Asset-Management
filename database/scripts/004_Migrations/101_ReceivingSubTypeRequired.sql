-- 101 (feature/catalog-requisition-flows, stage c+d): sub-type mandatory for NEW receipts + requisition policy seed.
-- What it does:
--   1. Trigger TR_AssetReceiving_RequireSubType (AFTER INSERT on dbo.AssetReceiving): rejects a NEW receipt whose
--      asset has no AssetSubTypeId. Only the inserted rows are checked, so the legacy receipts/assets with a NULL
--      sub-type (12 assets on local data, 2026-10-12) are untouched and can still be edited. Why not a CHECK on
--      dbo.Asset: WITH NOCHECK still re-validates a legacy row on its next UPDATE (breaks editing legacy assets),
--      and it would also hit non-receiving inserts (asset copy/import). AFTER INSERT only does not interfere with the
--      ORM's UPDATE ... OUTPUT inserted.[RowVersion] (verified on a scratch table). App rule: ReceivingService.
--   2. Seeds Purchases.Requisition.ClassificationMode = Optional and Purchases.Requisition.MixedFlowPolicy =
--      AutoSplit for every organisation that has no row yet (= today's behaviour; existing rows are never changed).
-- Idempotent: trigger is (re)created with CREATE OR ALTER-equivalent logic, seeds use NOT EXISTS; re-running is a no-op.
-- Requires 100_CatalogRequisitionFlow.sql (not technically, but ship together).
-- Rollback (see ops/101_Rollback_ReceivingSubTypeRequired_PREPARE_DO_NOT_RUN.sql):
--   DROP TRIGGER [dbo].[TR_AssetReceiving_RequireSubType];
--   DELETE FROM [dbo].[SystemSetting] WHERE [SettingKey] IN (N'Purchases.Requisition.ClassificationMode', N'Purchases.Requisition.MixedFlowPolicy')
--       AND [Description] LIKE N'Seeded by 101%';   -- rows saved later by an admin keep their own description
--   DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'101_ReceivingSubTypeRequired.sql';
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'[dbo].[TR_AssetReceiving_RequireSubType]', N'TR') IS NOT NULL
    DROP TRIGGER [dbo].[TR_AssetReceiving_RequireSubType];
GO

CREATE TRIGGER [dbo].[TR_AssetReceiving_RequireSubType]
ON [dbo].[AssetReceiving]
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1
        FROM inserted i
        INNER JOIN [dbo].[Asset] a ON a.[Id] = i.[AssetId]
        WHERE a.[AssetSubTypeId] IS NULL)
    BEGIN
        RAISERROR (N'Every received asset needs a sub-type (catalog requisition flows, migration 101).', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END
END
GO

INSERT INTO [dbo].[SystemSetting] ([OrganizationId], [SettingKey], [SettingValue], [Description], [CreatedAt], [IsActive])
SELECT o.[Id], N'Purchases.Requisition.ClassificationMode', N'Optional',
       N'Seeded by 101: requisition line classification (Optional, RequiredPerLine, RequiredHeader).', GETUTCDATE(), 1
FROM [dbo].[Organization] o
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[SystemSetting] s WHERE s.[OrganizationId] = o.[Id] AND s.[SettingKey] = N'Purchases.Requisition.ClassificationMode');
GO

INSERT INTO [dbo].[SystemSetting] ([OrganizationId], [SettingKey], [SettingValue], [Description], [CreatedAt], [IsActive])
SELECT o.[Id], N'Purchases.Requisition.MixedFlowPolicy', N'AutoSplit',
       N'Seeded by 101: lines with different approval paths (AutoSplit, Block).', GETUTCDATE(), 1
FROM [dbo].[Organization] o
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[SystemSetting] s WHERE s.[OrganizationId] = o.[Id] AND s.[SettingKey] = N'Purchases.Requisition.MixedFlowPolicy');
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'101_ReceivingSubTypeRequired.sql')
BEGIN
    INSERT INTO [dbo].[SchemaMigrationHistory] ([ScriptName], [Checksum], [AppliedAt], [AppliedBy])
    VALUES (N'101_ReceivingSubTypeRequired.sql', NULL, SYSUTCDATETIME(), SUSER_SNAME());
END
GO

-- Verify
SELECT
    CASE WHEN OBJECT_ID(N'[dbo].[TR_AssetReceiving_RequireSubType]', N'TR') IS NULL THEN 0 ELSE 1 END AS HasTrigger,
    (SELECT COUNT(*) FROM [dbo].[Organization]) AS Organizations,
    (SELECT COUNT(*) FROM [dbo].[SystemSetting] WHERE [SettingKey] = N'Purchases.Requisition.ClassificationMode') AS ClassificationRows,
    (SELECT COUNT(*) FROM [dbo].[SystemSetting] WHERE [SettingKey] = N'Purchases.Requisition.MixedFlowPolicy') AS MixedPolicyRows,
    (SELECT COUNT(*) FROM [dbo].[Asset] WHERE [AssetSubTypeId] IS NULL) AS LegacyAssetsWithoutSubType,
    (SELECT COUNT(*) FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'101_ReceivingSubTypeRequired.sql') AS HistoryRows;
GO
