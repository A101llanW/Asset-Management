IF COL_LENGTH(N'[SupplierCatalogItem]', N'AssetTypeId') IS NULL
BEGIN
    ALTER TABLE [SupplierCatalogItem] ADD [AssetTypeId] INT NULL;
END
GO

IF COL_LENGTH(N'[SupplierCatalogItem]', N'TaggedAssetId') IS NULL
BEGIN
    ALTER TABLE [SupplierCatalogItem] ADD [TaggedAssetId] INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE [name] = N'FK_SupplierCatalogItem_AssetType'
      AND [parent_object_id] = OBJECT_ID(N'[SupplierCatalogItem]'))
BEGIN
    ALTER TABLE [SupplierCatalogItem]
        ADD CONSTRAINT FK_SupplierCatalogItem_AssetType
        FOREIGN KEY ([AssetTypeId]) REFERENCES [AssetType]([Id]);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE [name] = N'FK_SupplierCatalogItem_TaggedAsset'
      AND [parent_object_id] = OBJECT_ID(N'[SupplierCatalogItem]'))
BEGIN
    ALTER TABLE [SupplierCatalogItem]
        ADD CONSTRAINT FK_SupplierCatalogItem_TaggedAsset
        FOREIGN KEY ([TaggedAssetId]) REFERENCES [Asset]([Id]);
END
GO
