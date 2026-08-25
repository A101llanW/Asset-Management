-- Remove demo/seed suppliers and related catalog lines from all tenants.

IF OBJECT_ID(N'[SupplierCatalogItem]', N'U') IS NOT NULL
BEGIN
    DELETE sci
    FROM [SupplierCatalogItem] sci
    INNER JOIN [Supplier] s ON s.[Id] = sci.[SupplierId]
    WHERE s.[SupplierName] IN (N'Tech Source Ltd', N'Office Works Hub', N'MedEquip Africa', N'Beta Supply Co')
       OR sci.[Sku] LIKE N'SEED-CMP-%';
END
GO

IF OBJECT_ID(N'[Asset]', N'U') IS NOT NULL AND COL_LENGTH(N'[Asset]', N'SupplierId') IS NOT NULL
BEGIN
    UPDATE a
    SET a.[SupplierId] = NULL
    FROM [Asset] a
    INNER JOIN [Supplier] s ON s.[Id] = a.[SupplierId]
    WHERE s.[SupplierName] IN (N'Tech Source Ltd', N'Office Works Hub', N'MedEquip Africa', N'Beta Supply Co');
END
GO

IF OBJECT_ID(N'[PurchaseRecord]', N'U') IS NOT NULL AND COL_LENGTH(N'[PurchaseRecord]', N'SupplierId') IS NOT NULL
BEGIN
    UPDATE p
    SET p.[SupplierId] = NULL
    FROM [PurchaseRecord] p
    INNER JOIN [Supplier] s ON s.[Id] = p.[SupplierId]
    WHERE s.[SupplierName] IN (N'Tech Source Ltd', N'Office Works Hub', N'MedEquip Africa', N'Beta Supply Co');
END
GO

IF OBJECT_ID(N'[Supplier]', N'U') IS NOT NULL
BEGIN
    DELETE FROM [Supplier]
    WHERE [SupplierName] IN (N'Tech Source Ltd', N'Office Works Hub', N'MedEquip Africa', N'Beta Supply Co');
END
GO
