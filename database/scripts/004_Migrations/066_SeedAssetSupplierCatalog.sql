-- Supplier catalog comparison seed for demo assets IT-RTR-001 and FIN-PRT-001 (Nanosoft org).
-- Idempotent: safe to re-run; uses SEED-CMP-* SKUs and tagged-asset linkage.

DECLARE @now DATETIME = GETUTCDATE();
DECLARE @orgId INT = (
    SELECT TOP 1 [Id] FROM [Organization]
    WHERE [Slug] IN (N'nanosoft', N'default') AND [IsActive] = 1
    ORDER BY CASE WHEN [Slug] = N'nanosoft' THEN 0 ELSE 1 END, [Id]);

IF @orgId IS NULL
    SET @orgId = (SELECT TOP 1 [Id] FROM [Organization] WHERE [IsActive] = 1 ORDER BY [Id]);

IF @orgId IS NULL
    RETURN;

DECLARE @assetRouterId INT = (
    SELECT TOP 1 [Id] FROM [Asset]
    WHERE [AssetTag] = N'IT-RTR-001' AND [OrganizationId] = @orgId AND [IsActive] = 1
    ORDER BY [Id]);
DECLARE @assetPrinterId INT = (
    SELECT TOP 1 [Id] FROM [Asset]
    WHERE [AssetTag] = N'FIN-PRT-001' AND [OrganizationId] = @orgId AND [IsActive] = 1
    ORDER BY [Id]);

IF @assetRouterId IS NULL AND @assetPrinterId IS NULL
    RETURN;

DECLARE @catNet INT = (
    SELECT TOP 1 [Id] FROM [AssetCategory]
    WHERE [OrganizationId] = @orgId AND [Name] = N'Networking' ORDER BY [Id]);
DECLARE @catOffice INT = (
    SELECT TOP 1 [Id] FROM [AssetCategory]
    WHERE [OrganizationId] = @orgId AND [Name] = N'Office Equipment' ORDER BY [Id]);
DECLARE @typeRouter INT = (
    SELECT TOP 1 [Id] FROM [AssetType]
    WHERE [OrganizationId] = @orgId AND [Name] = N'Router' ORDER BY [Id]);
DECLARE @typePrinter INT = (
    SELECT TOP 1 [Id] FROM [AssetType]
    WHERE [OrganizationId] = @orgId AND [Name] = N'Printer' ORDER BY [Id]);

DECLARE @supTech INT;
DECLARE @supOffice INT;
DECLARE @supNetwork INT;

IF NOT EXISTS (
    SELECT 1 FROM [Supplier]
    WHERE [OrganizationId] = @orgId AND [SupplierName] = N'Tech Source Ltd')
BEGIN
    INSERT INTO [Supplier]
        ([SupplierName],[ContactPerson],[Email],[Phone],[Address],[TaxId],[PaymentTerms],[DefaultLeadTimeDays],
         [Website],[IsPreferred],[Country],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
        (N'Tech Source Ltd', N'James Otieno', N'procurement@techsource.co.ke', N'+254700111222',
         N'Industrial Area, Nairobi', N'P051234567X', N'Net 30', 7,
         N'https://techsource.co.ke', 1, N'Kenya', @orgId, @now, 1);
END
SET @supTech = (
    SELECT TOP 1 [Id] FROM [Supplier]
    WHERE [OrganizationId] = @orgId AND [SupplierName] = N'Tech Source Ltd' ORDER BY [Id]);

IF NOT EXISTS (
    SELECT 1 FROM [Supplier]
    WHERE [OrganizationId] = @orgId AND [SupplierName] = N'Office Works Hub')
BEGIN
    INSERT INTO [Supplier]
        ([SupplierName],[ContactPerson],[Email],[Phone],[Address],[TaxId],[PaymentTerms],[DefaultLeadTimeDays],
         [Website],[IsPreferred],[Country],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
        (N'Office Works Hub', N'Grace Wanjiku', N'sales@officeworks.co.ke', N'+254700333444',
         N'Westlands, Nairobi', N'P059876543Y', N'Net 14', 5,
         N'https://officeworks.co.ke', 0, N'Kenya', @orgId, @now, 1);
END
SET @supOffice = (
    SELECT TOP 1 [Id] FROM [Supplier]
    WHERE [OrganizationId] = @orgId AND [SupplierName] = N'Office Works Hub' ORDER BY [Id]);

IF NOT EXISTS (
    SELECT 1 FROM [Supplier]
    WHERE [OrganizationId] = @orgId AND [SupplierName] = N'Network Solutions Kenya')
BEGIN
    INSERT INTO [Supplier]
        ([SupplierName],[ContactPerson],[Email],[Phone],[Address],[TaxId],[PaymentTerms],[DefaultLeadTimeDays],
         [Website],[IsPreferred],[Country],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
        (N'Network Solutions Kenya', N'Peter Kamau', N'orders@netsol.co.ke', N'+254700555666',
         N'Upper Hill, Nairobi', N'P051112223Z', N'Net 30', 14,
         N'https://netsol.co.ke', 0, N'Kenya', @orgId, @now, 1);
END
SET @supNetwork = (
    SELECT TOP 1 [Id] FROM [Supplier]
    WHERE [OrganizationId] = @orgId AND [SupplierName] = N'Network Solutions Kenya' ORDER BY [Id]);

IF @assetRouterId IS NOT NULL AND @supTech IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SupplierCatalogItem] WHERE [OrganizationId] = @orgId AND [Sku] = N'SEED-CMP-IT-RTR-001-A')
    BEGIN
        INSERT INTO [SupplierCatalogItem]
            ([OrganizationId],[SupplierId],[ItemName],[ItemDescription],[Sku],[AssetCategoryId],[AssetTypeId],[TaggedAssetId],
             [UnitPrice],[Currency],[MinimumOrderQuantity],[LeadTimeDays],[EffectiveFrom],[IsActive],[CreatedAt])
        VALUES
            (@orgId, @supTech, N'Cisco ISR 4331 Router', N'Branch edge router — Cisco ISR 4331/K9 with 4 GE ports',
             N'SEED-CMP-IT-RTR-001-A', @catNet, @typeRouter, @assetRouterId,
             415000.00, N'KES', 1, 10, DATEADD(MONTH, -6, @now), 1, @now);
    END

    IF @supNetwork IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM [SupplierCatalogItem] WHERE [OrganizationId] = @orgId AND [Sku] = N'SEED-CMP-IT-RTR-001-B')
    BEGIN
        INSERT INTO [SupplierCatalogItem]
            ([OrganizationId],[SupplierId],[ItemName],[ItemDescription],[Sku],[AssetCategoryId],[AssetTypeId],[TaggedAssetId],
             [UnitPrice],[Currency],[MinimumOrderQuantity],[LeadTimeDays],[EffectiveFrom],[IsActive],[CreatedAt])
        VALUES
            (@orgId, @supNetwork, N'Cisco ISR 4331/K9', N'Integrated services router for branch WAN edge',
             N'SEED-CMP-IT-RTR-001-B', @catNet, @typeRouter, @assetRouterId,
             438000.00, N'KES', 1, 14, DATEADD(MONTH, -6, @now), 1, @now);
    END

    UPDATE [Asset]
    SET [SupplierId] = @supTech
    WHERE [Id] = @assetRouterId AND [SupplierId] IS NULL;
END

IF @assetPrinterId IS NOT NULL AND @supOffice IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SupplierCatalogItem] WHERE [OrganizationId] = @orgId AND [Sku] = N'SEED-CMP-FIN-PRT-001-A')
    BEGIN
        INSERT INTO [SupplierCatalogItem]
            ([OrganizationId],[SupplierId],[ItemName],[ItemDescription],[Sku],[AssetCategoryId],[AssetTypeId],[TaggedAssetId],
             [UnitPrice],[Currency],[MinimumOrderQuantity],[LeadTimeDays],[EffectiveFrom],[IsActive],[CreatedAt])
        VALUES
            (@orgId, @supOffice, N'Canon imageCLASS MF445dw', N'Mono laser MFP — print, scan, copy, fax',
             N'SEED-CMP-FIN-PRT-001-A', @catOffice, @typePrinter, @assetPrinterId,
             65000.00, N'KES', 1, 5, DATEADD(MONTH, -6, @now), 1, @now);
    END

    IF @supTech IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM [SupplierCatalogItem] WHERE [OrganizationId] = @orgId AND [Sku] = N'SEED-CMP-FIN-PRT-001-B')
    BEGIN
        INSERT INTO [SupplierCatalogItem]
            ([OrganizationId],[SupplierId],[ItemName],[ItemDescription],[Sku],[AssetCategoryId],[AssetTypeId],[TaggedAssetId],
             [UnitPrice],[Currency],[MinimumOrderQuantity],[LeadTimeDays],[EffectiveFrom],[IsActive],[CreatedAt])
        VALUES
            (@orgId, @supTech, N'Canon MF445dw MFP', N'Finance-grade mono MFP with duplex and network printing',
             N'SEED-CMP-FIN-PRT-001-B', @catOffice, @typePrinter, @assetPrinterId,
             68500.00, N'KES', 1, 7, DATEADD(MONTH, -6, @now), 1, @now);
    END

    UPDATE [Asset]
    SET [SupplierId] = @supOffice
    WHERE [Id] = @assetPrinterId AND [SupplierId] IS NULL;
END
GO
