-- PREPARED, DO NOT RUN without sign-off. Rollback of 100_CatalogRequisitionFlow.sql (and 101 first, if applied).
-- Loses: catalog approval overrides, requisition batch links, line classifications, ApprovalFlowSource labels.
-- Keeps: every requisition and requisition line (only the added columns are dropped).
-- Order: 1) redeploy the build from BEFORE feature/catalog-requisition-flows (the new build maps these columns and
--           would fail on every requisition read/write without them); 2) take a COPY_ONLY WITH CHECKSUM backup;
--        3) run this script. Idempotent: each step only runs when its object still exists.
-- Verified 2026-10-12 with SET PARSEONLY against a scratch copy only; never executed.
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

-- Safety: 101 depends on nothing in 100, but roll it back first so both histories stay consistent.
IF OBJECT_ID(N'[dbo].[TR_AssetReceiving_RequireSubType]', N'TR') IS NOT NULL
    RAISERROR (N'101 is applied: run ops/101_Rollback_ReceivingSubTypeRequired_PREPARE_DO_NOT_RUN.sql first.', 16, 1);
GO

BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_PurchaseRequest_PurchaseRequestGroup')
    ALTER TABLE [dbo].[PurchaseRequest] DROP CONSTRAINT [FK_PurchaseRequest_PurchaseRequestGroup];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_PurchaseRequest_PurchaseRequestGroupId' AND [object_id] = OBJECT_ID(N'[dbo].[PurchaseRequest]'))
    DROP INDEX [IX_PurchaseRequest_PurchaseRequestGroupId] ON [dbo].[PurchaseRequest];
IF COL_LENGTH(N'dbo.PurchaseRequest', N'PurchaseRequestGroupId') IS NOT NULL
    ALTER TABLE [dbo].[PurchaseRequest] DROP COLUMN [PurchaseRequestGroupId];
IF COL_LENGTH(N'dbo.PurchaseRequest', N'ApprovalFlowSource') IS NOT NULL
    ALTER TABLE [dbo].[PurchaseRequest] DROP COLUMN [ApprovalFlowSource];

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_PurchaseRequestLine_AssetCategory')
    ALTER TABLE [dbo].[PurchaseRequestLine] DROP CONSTRAINT [FK_PurchaseRequestLine_AssetCategory];
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_PurchaseRequestLine_AssetType')
    ALTER TABLE [dbo].[PurchaseRequestLine] DROP CONSTRAINT [FK_PurchaseRequestLine_AssetType];
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_PurchaseRequestLine_AssetSubType')
    ALTER TABLE [dbo].[PurchaseRequestLine] DROP CONSTRAINT [FK_PurchaseRequestLine_AssetSubType];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_PurchaseRequestLine_AssetTypeId' AND [object_id] = OBJECT_ID(N'[dbo].[PurchaseRequestLine]'))
    DROP INDEX [IX_PurchaseRequestLine_AssetTypeId] ON [dbo].[PurchaseRequestLine];
IF COL_LENGTH(N'dbo.PurchaseRequestLine', N'AssetCategoryId') IS NOT NULL
    ALTER TABLE [dbo].[PurchaseRequestLine] DROP COLUMN [AssetCategoryId];
IF COL_LENGTH(N'dbo.PurchaseRequestLine', N'AssetTypeId') IS NOT NULL
    ALTER TABLE [dbo].[PurchaseRequestLine] DROP COLUMN [AssetTypeId];
IF COL_LENGTH(N'dbo.PurchaseRequestLine', N'AssetSubTypeId') IS NOT NULL
    ALTER TABLE [dbo].[PurchaseRequestLine] DROP COLUMN [AssetSubTypeId];

IF OBJECT_ID(N'[dbo].[PurchaseRequestGroup]', N'U') IS NOT NULL
    DROP TABLE [dbo].[PurchaseRequestGroup];
IF OBJECT_ID(N'[dbo].[CatalogRequisitionFlow]', N'U') IS NOT NULL
    DROP TABLE [dbo].[CatalogRequisitionFlow];

-- Policy rows written by the stage (c) Settings screen (inert for the old build, removed for cleanliness).
DELETE FROM [dbo].[SystemSetting]
WHERE [SettingKey] IN (N'Purchases.Requisition.ClassificationMode', N'Purchases.Requisition.MixedFlowPolicy');

DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'100_CatalogRequisitionFlow.sql';

COMMIT TRANSACTION;
GO

SELECT
    CASE WHEN OBJECT_ID(N'[dbo].[CatalogRequisitionFlow]', N'U') IS NULL THEN 1 ELSE 0 END AS FlowTableGone,
    CASE WHEN OBJECT_ID(N'[dbo].[PurchaseRequestGroup]', N'U') IS NULL THEN 1 ELSE 0 END AS GroupTableGone,
    CASE WHEN COL_LENGTH(N'dbo.PurchaseRequest', N'PurchaseRequestGroupId') IS NULL THEN 1 ELSE 0 END AS PrColumnsGone,
    CASE WHEN COL_LENGTH(N'dbo.PurchaseRequestLine', N'AssetSubTypeId') IS NULL THEN 1 ELSE 0 END AS LineColumnsGone,
    (SELECT COUNT(*) FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'100_CatalogRequisitionFlow.sql') AS HistoryRows;
GO
