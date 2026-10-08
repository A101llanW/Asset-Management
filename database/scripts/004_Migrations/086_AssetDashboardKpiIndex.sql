-- Tenant Dashboard KPIs (DashboardQueryService): narrow covering index so the KPI
-- aggregates read ~1/20th of the pages and can stream-aggregate by DepartmentId
-- without a sort/hash memory grant (SQL Express RESOURCE_SEMAPHORE timeouts).
-- Schema only (no data change). Idempotent. Reversible: see rollback below.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Asset_Org_Active_Dept_DashboardKpi' AND object_id = OBJECT_ID('dbo.Asset'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Asset_Org_Active_Dept_DashboardKpi]
    ON [dbo].[Asset] ([OrganizationId], [IsActive], [DepartmentId])
    INCLUDE ([CurrentStatus], [AcquisitionCost], [TaxAmount], [CurrentBookValue], [AccumulatedDepreciation],
             [DepreciationRatePercent], [DepreciationLifeMonths], [UsefulLifeMonths], [AssetTypeId], [CategoryId],
             [WarrantyEndDate]);
END
-- Rollback:
-- DROP INDEX [IX_Asset_Org_Active_Dept_DashboardKpi] ON [dbo].[Asset];
