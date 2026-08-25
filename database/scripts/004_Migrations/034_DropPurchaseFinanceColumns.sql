-- Remove unused purchase finance columns (never wired to UI or services)
IF COL_LENGTH(N'[PurchaseRecord]', N'FundingSource') IS NOT NULL
BEGIN
    ALTER TABLE [PurchaseRecord] DROP COLUMN [FundingSource];
END
GO

IF COL_LENGTH(N'[PurchaseRecord]', N'BudgetCode') IS NOT NULL
BEGIN
    ALTER TABLE [PurchaseRecord] DROP COLUMN [BudgetCode];
END
GO
