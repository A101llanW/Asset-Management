-- Remove unused estimated unit cost from purchase requisitions
IF COL_LENGTH(N'[PurchaseRequest]', N'EstimatedUnitCost') IS NOT NULL
BEGIN
    ALTER TABLE [PurchaseRequest] DROP COLUMN [EstimatedUnitCost];
END
GO
