-- Assets Index product-grouped list (AssetQueryService.GetProductGroupedListPage)
-- Schema only (no data change). Reversible: see rollback below.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Asset_Org_Active_ProductGroup' AND object_id = OBJECT_ID('dbo.Asset'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Asset_Org_Active_ProductGroup]
    ON [dbo].[Asset] ([OrganizationId], [IsActive], [AssetName], [AssetSubTypeId], [DepartmentId], [CurrentStatus], [AssetTypeId])
    INCLUDE ([CategoryId], [AcquisitionCost]);
END
-- Rollback:
-- DROP INDEX [IX_Asset_Org_Active_ProductGroup] ON [dbo].[Asset];
