-- 108 (requisition line asset name + per-line tag): carries the item / asset name and the tagged asset per line
-- from requisition -> PO -> receiving.
-- What it does (additive and idempotent; every new column is NULLable; no existing row is changed):
--   1. PurchaseRequestLine.ItemName NVARCHAR(200) NULL      "Item / asset name" of the line (becomes the received asset's name).
--   2. PurchaseRequestLine.TargetAssetId INT NULL            the existing asset tagged on THIS line (split batches keep their tags),
--      FK_PurchaseRequestLine_TargetAsset -> Asset(Id) and IX_PurchaseRequestLine_TargetAssetId (plain nonclustered,
--      NOT filtered: DECISIONS-requisition A11, filtered indexes break QUOTED_IDENTIFIER OFF sessions, error 1934).
--      The tag only supplies the classification and pre-fills the name; receiving always creates a NEW asset.
--   3. PurchaseOrderLine.ItemName NVARCHAR(200) NULL        copied from the requisition line when the PO is raised.
--      PurchaseOrderLine already has AssetCategoryId / AssetTypeId / AssetSubTypeId (migration 098): no classification column added.
--   4. SchemaMigrationHistory row.
-- PROVISIONAL: NVARCHAR(200) matches Asset.AssetName. The application limit is AssetNameRules.MaxLength (150 for now,
--   150 vs 200 pending Allan); either value fits these columns, so the decision needs no schema change.
-- Rows created before 108 keep NULL ItemName / TargetAssetId and are NOT re-validated: the app falls back to the old
--   rules (header TargetAssetId, description / sub-type / brand-model name).
-- Must run BEFORE the matching code is published (the entity maps read the new columns).
-- Idempotent: every step is guarded; re-running is a no-op.
-- Rollback (see database/scripts/ops/108_Rollback_RequisitionLineAssetName_PREPARE_DO_NOT_RUN.sql):
--   DROP INDEX IX_PurchaseRequestLine_TargetAssetId; DROP CONSTRAINT FK_PurchaseRequestLine_TargetAsset;
--   DROP COLUMN PurchaseRequestLine.ItemName, PurchaseRequestLine.TargetAssetId, PurchaseOrderLine.ItemName
--   (only after redeploying the previous build);
--   DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'108_requisition_line_asset_name.sql';
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.PurchaseRequestLine', N'ItemName') IS NULL
BEGIN
    ALTER TABLE [dbo].[PurchaseRequestLine] ADD [ItemName] NVARCHAR(200) NULL;
END

IF COL_LENGTH(N'dbo.PurchaseRequestLine', N'TargetAssetId') IS NULL
BEGIN
    ALTER TABLE [dbo].[PurchaseRequestLine] ADD [TargetAssetId] INT NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [parent_object_id] = OBJECT_ID(N'dbo.PurchaseRequestLine') AND [name] = N'FK_PurchaseRequestLine_TargetAsset')
BEGIN
    -- Dynamic SQL: the column was added in this same batch.
    EXEC (N'ALTER TABLE [dbo].[PurchaseRequestLine] WITH CHECK ADD CONSTRAINT [FK_PurchaseRequestLine_TargetAsset] FOREIGN KEY ([TargetAssetId]) REFERENCES [dbo].[Asset] ([Id]);');
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'dbo.PurchaseRequestLine') AND [name] = N'IX_PurchaseRequestLine_TargetAssetId')
BEGIN
    EXEC (N'CREATE NONCLUSTERED INDEX [IX_PurchaseRequestLine_TargetAssetId] ON [dbo].[PurchaseRequestLine] ([TargetAssetId]);');
END

IF COL_LENGTH(N'dbo.PurchaseOrderLine', N'ItemName') IS NULL
BEGIN
    ALTER TABLE [dbo].[PurchaseOrderLine] ADD [ItemName] NVARCHAR(200) NULL;
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'108_requisition_line_asset_name.sql')
BEGIN
    INSERT INTO [dbo].[SchemaMigrationHistory] ([ScriptName], [Checksum], [AppliedAt], [AppliedBy])
    VALUES (N'108_requisition_line_asset_name.sql', NULL, SYSUTCDATETIME(), SUSER_SNAME());
END

COMMIT TRANSACTION;
