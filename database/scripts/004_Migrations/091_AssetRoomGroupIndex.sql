-- 091: Assets Index "Group by room" read index (schema only, idempotent, no data changes).
-- Supports grant-free AssetRoomGroupQueryService (Assets/Index?view=room):
--   * Level 1 (rooms): COUNT/SUM grouped by DepartmentId stream-aggregate in key order.
--   * Level 2 (categories in a room): seek OrganizationId + IsActive + DepartmentId, stream-aggregate
--     by CategoryId in key order (no hash / sort memory grant).
--   * Level 3 (assets in a room + category): seek ... + CategoryId and read AssetTag (+ clustered Id)
--     in key order, so OFFSET/FETCH paging needs no sort.
--   INCLUDE carries the columns the existing list filters test (status, type, sub-type, supplier,
--   custodian) plus AcquisitionCost for the priced totals, so filtered levels avoid key lookups.
-- Note for the AcquisitionCost nullability migration: it scripts/drops/recreates every Asset index
-- that carries AcquisitionCost, so this index is handled the same way as 084 / 086.
-- Rollback:
--   DROP INDEX [IX_Asset_Org_Active_Dept_Cat_Tag] ON [dbo].[Asset];
--   DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'091_AssetRoomGroupIndex.sql';  -- if recorded

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'[dbo].[Asset]') AND name = N'IX_Asset_Org_Active_Dept_Cat_Tag')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Asset_Org_Active_Dept_Cat_Tag]
        ON [dbo].[Asset] ([OrganizationId], [IsActive], [DepartmentId], [CategoryId], [AssetTag])
        INCLUDE ([CurrentStatus], [AcquisitionCost], [AssetTypeId], [AssetSubTypeId], [SupplierId], [CurrentCustodianId]);
END
GO