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
--   4. C3 override trail (who / when / classification before the change), on BOTH tables, all NULL = never changed:
--      ClassificationOverriddenById NVARCHAR(128) NULL (user id, like AssetReceiving.ReceivedById),
--      ClassificationOverriddenAt DATETIME NULL (UTC, like 098's CreatedAt),
--      OriginalAssetCategoryId / OriginalAssetTypeId / OriginalAssetSubTypeId INT NULL (snapshot ids, no FK on purpose:
--      they record history and must not block deleting or purging a type).
--      PurchaseOrderLine: set when the buyer edits the prefilled classification on the PO ("original" = the prefill).
--      PurchaseOrderLineReceipt: set when it is edited at receiving ("original" = the PO line's; the asset gets the new one).
--   5. SchemaMigrationHistory row.
-- NVARCHAR(200) matches Asset.AssetName and the application limit AssetNameRules.MaxLength = 200 (final, Allan 2026-10-27).
-- Rows created before 108 keep NULL ItemName / TargetAssetId and are NOT re-validated: the app falls back to the old
--   rules (header TargetAssetId, description / sub-type / brand-model name).
-- Must run BEFORE the matching code is published (the entity maps read the new columns).
-- Idempotent: every step is guarded; re-running is a no-op.
-- Rollback (see database/scripts/ops/108_Rollback_RequisitionLineAssetName_PREPARE_DO_NOT_RUN.sql):
--   DROP INDEX IX_PurchaseRequestLine_TargetAssetId; DROP CONSTRAINT FK_PurchaseRequestLine_TargetAsset;
--   DROP COLUMN PurchaseRequestLine.ItemName, PurchaseRequestLine.TargetAssetId, PurchaseOrderLine.ItemName,
--   and the five C3 override columns on PurchaseOrderLine and on PurchaseOrderLineReceipt
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

-- C3 classification override trail (Allan 2026-10-27 21:20 EAT): who / when / the classification before the change.
IF COL_LENGTH(N'dbo.PurchaseOrderLine', N'ClassificationOverriddenById') IS NULL
BEGIN
    ALTER TABLE [dbo].[PurchaseOrderLine] ADD [ClassificationOverriddenById] NVARCHAR(128) NULL;
END

IF COL_LENGTH(N'dbo.PurchaseOrderLine', N'ClassificationOverriddenAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[PurchaseOrderLine] ADD [ClassificationOverriddenAt] DATETIME NULL;
END

IF COL_LENGTH(N'dbo.PurchaseOrderLine', N'OriginalAssetCategoryId') IS NULL
BEGIN
    ALTER TABLE [dbo].[PurchaseOrderLine] ADD [OriginalAssetCategoryId] INT NULL;
END

IF COL_LENGTH(N'dbo.PurchaseOrderLine', N'OriginalAssetTypeId') IS NULL
BEGIN
    ALTER TABLE [dbo].[PurchaseOrderLine] ADD [OriginalAssetTypeId] INT NULL;
END

IF COL_LENGTH(N'dbo.PurchaseOrderLine', N'OriginalAssetSubTypeId') IS NULL
BEGIN
    ALTER TABLE [dbo].[PurchaseOrderLine] ADD [OriginalAssetSubTypeId] INT NULL;
END

IF COL_LENGTH(N'dbo.PurchaseOrderLineReceipt', N'ClassificationOverriddenById') IS NULL
BEGIN
    ALTER TABLE [dbo].[PurchaseOrderLineReceipt] ADD [ClassificationOverriddenById] NVARCHAR(128) NULL;
END

IF COL_LENGTH(N'dbo.PurchaseOrderLineReceipt', N'ClassificationOverriddenAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[PurchaseOrderLineReceipt] ADD [ClassificationOverriddenAt] DATETIME NULL;
END

IF COL_LENGTH(N'dbo.PurchaseOrderLineReceipt', N'OriginalAssetCategoryId') IS NULL
BEGIN
    ALTER TABLE [dbo].[PurchaseOrderLineReceipt] ADD [OriginalAssetCategoryId] INT NULL;
END

IF COL_LENGTH(N'dbo.PurchaseOrderLineReceipt', N'OriginalAssetTypeId') IS NULL
BEGIN
    ALTER TABLE [dbo].[PurchaseOrderLineReceipt] ADD [OriginalAssetTypeId] INT NULL;
END

IF COL_LENGTH(N'dbo.PurchaseOrderLineReceipt', N'OriginalAssetSubTypeId') IS NULL
BEGIN
    ALTER TABLE [dbo].[PurchaseOrderLineReceipt] ADD [OriginalAssetSubTypeId] INT NULL;
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'108_requisition_line_asset_name.sql')
BEGIN
    INSERT INTO [dbo].[SchemaMigrationHistory] ([ScriptName], [Checksum], [AppliedAt], [AppliedBy])
    VALUES (N'108_requisition_line_asset_name.sql', NULL, SYSUTCDATETIME(), SUSER_SNAME());
END

COMMIT TRANSACTION;
