-- 092: Asset export/register covering index (schema only, idempotent, no data changes).
-- Supports AssetQueryService.StreamExport (Reports > Asset Register preview/CSV/PDF and the Assets
-- export): the export now selects only Asset columns (category/department names come from small IN
-- lookups), so this index covers it and only the organization's index pages are read instead of a
-- scan of the whole clustered table (548 pages at 2026-10-08) plus ~2 lookups per row.
-- Rollback:
--   DROP INDEX [IX_Asset_Org_Active_Dept_Export] ON [dbo].[Asset];
--   DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'092_AssetExportRegisterIndex.sql';  -- if recorded

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'[dbo].[Asset]') AND name = N'IX_Asset_Org_Active_Dept_Export')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Asset_Org_Active_Dept_Export]
        ON [dbo].[Asset] ([OrganizationId], [IsActive], [DepartmentId])
        INCLUDE ([AssetTag], [AssetName], [CurrentStatus], [CategoryId], [AssetTypeId], [AssetSubTypeId],
                 [SupplierId], [CurrentCustodianId], [AcquisitionCost], [PurchaseDate], [SerialNumber], [CreatedAt])
        WITH (MAXDOP = 1);
END
GO