-- 100 (feature/catalog-requisition-flows, stage a+b): catalog-level requisition approval overrides, per-line
-- classification on requisition lines, and linked requisition groups for auto-split submits. Additive only.
-- What it does:
--   1. CatalogRequisitionFlow: optional approval override per catalog node (Scope 1=Category, 2=Type, 3=Sub-type;
--      ScopedEntityId = that row's Id). FlowMode mirrors Department.RequisitionFlowMode (0 Inherit = no override,
--      1 Custom, 2 AutoApprove); stage role/user CSVs and PO raiser as on Department. Unique per
--      (OrganizationId, Scope, ScopedEntityId). No rows are created: until an admin configures one, every
--      requisition resolves exactly as before (department chain, then org matrix).
--   2. PurchaseRequestGroup: one row per auto-split submit (BatchLabel BATCH-000042).
--   3. PurchaseRequest.PurchaseRequestGroupId (NULL = stand-alone, every existing row) + ApprovalFlowSource
--      (label of the winning approval source at submit; NULL for existing rows).
--   4. PurchaseRequestLine.AssetCategoryId / AssetTypeId / AssetSubTypeId (NULL for every existing line).
--   Org settings Purchases.Requisition.ClassificationMode / MixedFlowPolicy are NOT seeded: missing = Optional /
--   AutoSplit in code, which is today's behaviour.
-- Idempotent: every object/column is created only when missing; re-running is a no-op.
-- Deploy order: apply BEFORE the build that maps these columns (the ORM writes every mapped column).
-- Rollback (loses catalog overrides, group links and line classifications; requisitions/lines themselves stay):
--   ALTER TABLE [dbo].[PurchaseRequest] DROP CONSTRAINT [FK_PurchaseRequest_PurchaseRequestGroup];
--   DROP INDEX [IX_PurchaseRequest_PurchaseRequestGroupId] ON [dbo].[PurchaseRequest];
--   ALTER TABLE [dbo].[PurchaseRequest] DROP COLUMN [PurchaseRequestGroupId], [ApprovalFlowSource];
--   ALTER TABLE [dbo].[PurchaseRequestLine] DROP CONSTRAINT [FK_PurchaseRequestLine_AssetCategory], [FK_PurchaseRequestLine_AssetType], [FK_PurchaseRequestLine_AssetSubType];
--   DROP INDEX [IX_PurchaseRequestLine_AssetTypeId] ON [dbo].[PurchaseRequestLine];
--   ALTER TABLE [dbo].[PurchaseRequestLine] DROP COLUMN [AssetCategoryId], [AssetTypeId], [AssetSubTypeId];
--   DROP TABLE [dbo].[PurchaseRequestGroup];
--   DROP TABLE [dbo].[CatalogRequisitionFlow];
--   DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'100_CatalogRequisitionFlow.sql';
--   (and redeploy the previous build first: it does not map these columns)
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'[dbo].[CatalogRequisitionFlow]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CatalogRequisitionFlow] (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_CatalogRequisitionFlow] PRIMARY KEY,
        [OrganizationId] INT NULL,
        [Scope] INT NOT NULL,
        [ScopedEntityId] INT NOT NULL,
        [FlowMode] INT NOT NULL CONSTRAINT [DF_CatalogRequisitionFlow_FlowMode] DEFAULT (0),
        [CustomStageRoleIds] NVARCHAR(500) NULL,
        [CustomStageUserIds] NVARCHAR(2000) NULL,
        [PurchaseOrderRaiserRoleId] INT NULL,
        [PurchaseOrderRaiserUserId] NVARCHAR(128) NULL,
        [CreatedAt] DATETIME NOT NULL CONSTRAINT [DF_CatalogRequisitionFlow_CreatedAt] DEFAULT (GETUTCDATE()),
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_CatalogRequisitionFlow_IsActive] DEFAULT (1),
        CONSTRAINT [CK_CatalogRequisitionFlow_Scope] CHECK ([Scope] IN (1, 2, 3)),
        CONSTRAINT [CK_CatalogRequisitionFlow_FlowMode] CHECK ([FlowMode] IN (0, 1, 2))
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'UX_CatalogRequisitionFlow_Org_Scope_Entity' AND [object_id] = OBJECT_ID(N'[dbo].[CatalogRequisitionFlow]'))
    CREATE UNIQUE NONCLUSTERED INDEX [UX_CatalogRequisitionFlow_Org_Scope_Entity]
        ON [dbo].[CatalogRequisitionFlow] ([OrganizationId], [Scope], [ScopedEntityId]);
GO

IF OBJECT_ID(N'[dbo].[PurchaseRequestGroup]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PurchaseRequestGroup] (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_PurchaseRequestGroup] PRIMARY KEY,
        [OrganizationId] INT NULL,
        [BatchLabel] NVARCHAR(40) NULL,
        [CreatedByUserId] NVARCHAR(128) NULL,
        [CreatedAt] DATETIME NOT NULL CONSTRAINT [DF_PurchaseRequestGroup_CreatedAt] DEFAULT (GETUTCDATE()),
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_PurchaseRequestGroup_IsActive] DEFAULT (1)
    );
END
GO

IF COL_LENGTH(N'dbo.PurchaseRequest', N'PurchaseRequestGroupId') IS NULL
    ALTER TABLE [dbo].[PurchaseRequest] ADD [PurchaseRequestGroupId] INT NULL;
GO

IF COL_LENGTH(N'dbo.PurchaseRequest', N'ApprovalFlowSource') IS NULL
    ALTER TABLE [dbo].[PurchaseRequest] ADD [ApprovalFlowSource] NVARCHAR(200) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_PurchaseRequest_PurchaseRequestGroup')
    ALTER TABLE [dbo].[PurchaseRequest] ADD CONSTRAINT [FK_PurchaseRequest_PurchaseRequestGroup]
        FOREIGN KEY ([PurchaseRequestGroupId]) REFERENCES [dbo].[PurchaseRequestGroup]([Id]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_PurchaseRequest_PurchaseRequestGroupId' AND [object_id] = OBJECT_ID(N'[dbo].[PurchaseRequest]'))
    CREATE NONCLUSTERED INDEX [IX_PurchaseRequest_PurchaseRequestGroupId]
        ON [dbo].[PurchaseRequest] ([PurchaseRequestGroupId]);
GO

IF COL_LENGTH(N'dbo.PurchaseRequestLine', N'AssetCategoryId') IS NULL
    ALTER TABLE [dbo].[PurchaseRequestLine] ADD [AssetCategoryId] INT NULL;
GO

IF COL_LENGTH(N'dbo.PurchaseRequestLine', N'AssetTypeId') IS NULL
    ALTER TABLE [dbo].[PurchaseRequestLine] ADD [AssetTypeId] INT NULL;
GO

IF COL_LENGTH(N'dbo.PurchaseRequestLine', N'AssetSubTypeId') IS NULL
    ALTER TABLE [dbo].[PurchaseRequestLine] ADD [AssetSubTypeId] INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_PurchaseRequestLine_AssetCategory')
    ALTER TABLE [dbo].[PurchaseRequestLine] ADD CONSTRAINT [FK_PurchaseRequestLine_AssetCategory]
        FOREIGN KEY ([AssetCategoryId]) REFERENCES [dbo].[AssetCategory]([Id]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_PurchaseRequestLine_AssetType')
    ALTER TABLE [dbo].[PurchaseRequestLine] ADD CONSTRAINT [FK_PurchaseRequestLine_AssetType]
        FOREIGN KEY ([AssetTypeId]) REFERENCES [dbo].[AssetType]([Id]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_PurchaseRequestLine_AssetSubType')
    ALTER TABLE [dbo].[PurchaseRequestLine] ADD CONSTRAINT [FK_PurchaseRequestLine_AssetSubType]
        FOREIGN KEY ([AssetSubTypeId]) REFERENCES [dbo].[AssetSubType]([Id]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_PurchaseRequestLine_AssetTypeId' AND [object_id] = OBJECT_ID(N'[dbo].[PurchaseRequestLine]'))
    CREATE NONCLUSTERED INDEX [IX_PurchaseRequestLine_AssetTypeId]
        ON [dbo].[PurchaseRequestLine] ([AssetTypeId]) INCLUDE ([AssetSubTypeId], [AssetCategoryId]);
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'100_CatalogRequisitionFlow.sql')
BEGIN
    INSERT INTO [dbo].[SchemaMigrationHistory] ([ScriptName], [Checksum], [AppliedAt], [AppliedBy])
    VALUES (N'100_CatalogRequisitionFlow.sql', NULL, SYSUTCDATETIME(), SUSER_SNAME());
END
GO

-- Verify
SELECT
    CASE WHEN OBJECT_ID(N'[dbo].[CatalogRequisitionFlow]', N'U') IS NULL THEN 0 ELSE 1 END AS HasCatalogRequisitionFlow,
    CASE WHEN OBJECT_ID(N'[dbo].[PurchaseRequestGroup]', N'U') IS NULL THEN 0 ELSE 1 END AS HasPurchaseRequestGroup,
    CASE WHEN COL_LENGTH(N'dbo.PurchaseRequest', N'PurchaseRequestGroupId') IS NULL THEN 0 ELSE 1 END AS HasPrGroupId,
    CASE WHEN COL_LENGTH(N'dbo.PurchaseRequest', N'ApprovalFlowSource') IS NULL THEN 0 ELSE 1 END AS HasPrFlowSource,
    CASE WHEN COL_LENGTH(N'dbo.PurchaseRequestLine', N'AssetSubTypeId') IS NULL THEN 0 ELSE 1 END AS HasLineTaxonomy,
    (SELECT COUNT(*) FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'100_CatalogRequisitionFlow.sql') AS HistoryRows;
GO
