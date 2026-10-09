-- 098 (rel1009i): purchase order lines linked to requisition lines, and per-line receipts (additive only).
-- History:
--   * PurchaseRecord (the PO) already had PurchaseRequestId (the requisition link, 002_Domain); it is reused, not
--     duplicated. A PO was a single quantity with no lines, so a multi-line requisition could not be ordered or
--     received per line and nothing stopped a PO quantity above the requisition quantity.
--   * PurchaseOrderLine: one row per ordered requisition line (PurchaseRequestLineId NULL for a single-item
--     requisition), with the classification Receiving auto-fills (category / type / sub-type + source).
--   * PurchaseOrderLineReceipt: one row per received unit (AssetReceiving), so received and outstanding are tracked
--     per line and over-receipt is blocked per line.
--   * No existing row is changed. Existing POs have no lines and stay "legacy" (received against the PO quantity).
-- Idempotent: every object is created only when missing; re-running is a no-op.
-- Rollback (lines and per-line receipts are lost; POs and AssetReceiving rows are untouched):
--   IF OBJECT_ID(N'[dbo].[PurchaseOrderLineReceipt]', N'U') IS NOT NULL DROP TABLE [dbo].[PurchaseOrderLineReceipt];
--   IF OBJECT_ID(N'[dbo].[PurchaseOrderLine]', N'U') IS NOT NULL DROP TABLE [dbo].[PurchaseOrderLine];
--   DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'098_PurchaseOrderLines.sql';

IF OBJECT_ID(N'[dbo].[PurchaseOrderLine]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PurchaseOrderLine] (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_PurchaseOrderLine] PRIMARY KEY,
        [OrganizationId] INT NULL,
        [PurchaseRecordId] INT NOT NULL,
        [PurchaseRequestLineId] INT NULL,
        [LineNumber] INT NOT NULL,
        [Description] NVARCHAR(2000) NULL,
        [Quantity] INT NOT NULL,
        [UnitCost] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_PurchaseOrderLine_UnitCost] DEFAULT (0),
        [AssetCategoryId] INT NULL,
        [AssetTypeId] INT NULL,
        [AssetSubTypeId] INT NULL,
        [ClassificationSource] NVARCHAR(40) NULL,
        [CreatedAt] DATETIME NOT NULL CONSTRAINT [DF_PurchaseOrderLine_CreatedAt] DEFAULT (GETUTCDATE()),
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_PurchaseOrderLine_IsActive] DEFAULT (1),
        CONSTRAINT [CK_PurchaseOrderLine_Quantity] CHECK ([Quantity] > 0),
        CONSTRAINT [FK_PurchaseOrderLine_PurchaseRecord] FOREIGN KEY ([PurchaseRecordId]) REFERENCES [dbo].[PurchaseRecord]([Id]),
        CONSTRAINT [FK_PurchaseOrderLine_PurchaseRequestLine] FOREIGN KEY ([PurchaseRequestLineId]) REFERENCES [dbo].[PurchaseRequestLine]([Id]),
        CONSTRAINT [FK_PurchaseOrderLine_AssetCategory] FOREIGN KEY ([AssetCategoryId]) REFERENCES [dbo].[AssetCategory]([Id]),
        CONSTRAINT [FK_PurchaseOrderLine_AssetType] FOREIGN KEY ([AssetTypeId]) REFERENCES [dbo].[AssetType]([Id]),
        CONSTRAINT [FK_PurchaseOrderLine_AssetSubType] FOREIGN KEY ([AssetSubTypeId]) REFERENCES [dbo].[AssetSubType]([Id])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_PurchaseOrderLine_PurchaseRecordId' AND [object_id] = OBJECT_ID(N'[dbo].[PurchaseOrderLine]'))
    CREATE NONCLUSTERED INDEX [IX_PurchaseOrderLine_PurchaseRecordId] ON [dbo].[PurchaseOrderLine] ([PurchaseRecordId], [LineNumber]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_PurchaseOrderLine_PurchaseRequestLineId' AND [object_id] = OBJECT_ID(N'[dbo].[PurchaseOrderLine]'))
    CREATE NONCLUSTERED INDEX [IX_PurchaseOrderLine_PurchaseRequestLineId] ON [dbo].[PurchaseOrderLine] ([PurchaseRequestLineId]) INCLUDE ([Quantity], [IsActive]);
GO

IF OBJECT_ID(N'[dbo].[PurchaseOrderLineReceipt]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PurchaseOrderLineReceipt] (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_PurchaseOrderLineReceipt] PRIMARY KEY,
        [OrganizationId] INT NULL,
        [PurchaseRecordId] INT NOT NULL,
        [PurchaseOrderLineId] INT NOT NULL,
        [AssetReceivingId] INT NOT NULL,
        [AssetId] INT NOT NULL,
        [QuantityReceived] INT NOT NULL CONSTRAINT [DF_PurchaseOrderLineReceipt_Qty] DEFAULT (1),
        [CreatedAt] DATETIME NOT NULL CONSTRAINT [DF_PurchaseOrderLineReceipt_CreatedAt] DEFAULT (GETUTCDATE()),
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_PurchaseOrderLineReceipt_IsActive] DEFAULT (1),
        CONSTRAINT [CK_PurchaseOrderLineReceipt_Qty] CHECK ([QuantityReceived] > 0),
        CONSTRAINT [FK_PurchaseOrderLineReceipt_PurchaseRecord] FOREIGN KEY ([PurchaseRecordId]) REFERENCES [dbo].[PurchaseRecord]([Id]),
        CONSTRAINT [FK_PurchaseOrderLineReceipt_Line] FOREIGN KEY ([PurchaseOrderLineId]) REFERENCES [dbo].[PurchaseOrderLine]([Id]),
        CONSTRAINT [FK_PurchaseOrderLineReceipt_AssetReceiving] FOREIGN KEY ([AssetReceivingId]) REFERENCES [dbo].[AssetReceiving]([Id]),
        CONSTRAINT [FK_PurchaseOrderLineReceipt_Asset] FOREIGN KEY ([AssetId]) REFERENCES [dbo].[Asset]([Id])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'UX_PurchaseOrderLineReceipt_AssetReceivingId' AND [object_id] = OBJECT_ID(N'[dbo].[PurchaseOrderLineReceipt]'))
    CREATE UNIQUE NONCLUSTERED INDEX [UX_PurchaseOrderLineReceipt_AssetReceivingId] ON [dbo].[PurchaseOrderLineReceipt] ([AssetReceivingId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_PurchaseOrderLineReceipt_Line' AND [object_id] = OBJECT_ID(N'[dbo].[PurchaseOrderLineReceipt]'))
    CREATE NONCLUSTERED INDEX [IX_PurchaseOrderLineReceipt_Line] ON [dbo].[PurchaseOrderLineReceipt] ([PurchaseOrderLineId]) INCLUDE ([QuantityReceived], [IsActive]);
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'098_PurchaseOrderLines.sql')
BEGIN
    INSERT INTO [dbo].[SchemaMigrationHistory] ([ScriptName], [Checksum], [AppliedAt], [AppliedBy])
    VALUES (N'098_PurchaseOrderLines.sql', NULL, SYSUTCDATETIME(), SUSER_SNAME());
END
GO
