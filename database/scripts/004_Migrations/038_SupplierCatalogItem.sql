IF OBJECT_ID(N'[SupplierCatalogItem]', N'U') IS NULL
BEGIN
    CREATE TABLE [SupplierCatalogItem] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [OrganizationId] INT NULL,
        [SupplierId] INT NOT NULL,
        [ItemName] NVARCHAR(200) NOT NULL,
        [ItemDescription] NVARCHAR(2000) NULL,
        [Sku] NVARCHAR(100) NULL,
        [AssetCategoryId] INT NULL,
        [UnitPrice] DECIMAL(18,2) NOT NULL,
        [Currency] NVARCHAR(10) NULL,
        [MinimumOrderQuantity] INT NULL,
        [LeadTimeDays] INT NULL,
        [EffectiveFrom] DATETIME NULL,
        [EffectiveTo] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_SupplierCatalogItem_IsActive DEFAULT(1),
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        CONSTRAINT FK_SupplierCatalogItem_Supplier FOREIGN KEY ([SupplierId]) REFERENCES [Supplier]([Id]),
        CONSTRAINT FK_SupplierCatalogItem_AssetCategory FOREIGN KEY ([AssetCategoryId]) REFERENCES [AssetCategory]([Id])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SupplierCatalogItem_Org_Supplier' AND object_id = OBJECT_ID(N'[SupplierCatalogItem]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_SupplierCatalogItem_Org_Supplier
        ON [SupplierCatalogItem]([OrganizationId], [SupplierId], [IsActive]);
END
GO
