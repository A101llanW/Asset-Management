-- Depreciation life (months) and annual rate (%) defaults per category, with type and asset overrides.
IF COL_LENGTH(N'[AssetCategory]', N'DefaultDepreciationLifeMonths') IS NULL
BEGIN
    ALTER TABLE [AssetCategory] ADD [DefaultDepreciationLifeMonths] INT NULL;
END
GO

IF COL_LENGTH(N'[AssetCategory]', N'DefaultDepreciationRatePercent') IS NULL
BEGIN
    ALTER TABLE [AssetCategory] ADD [DefaultDepreciationRatePercent] DECIMAL(5,2) NULL;
END
GO

IF COL_LENGTH(N'[AssetType]', N'DepreciationLifeMonths') IS NULL
BEGIN
    ALTER TABLE [AssetType] ADD [DepreciationLifeMonths] INT NULL;
END
GO

IF COL_LENGTH(N'[AssetType]', N'DepreciationRatePercent') IS NULL
BEGIN
    ALTER TABLE [AssetType] ADD [DepreciationRatePercent] DECIMAL(5,2) NULL;
END
GO

IF COL_LENGTH(N'[Asset]', N'DepreciationLifeMonths') IS NULL
BEGIN
    ALTER TABLE [Asset] ADD [DepreciationLifeMonths] INT NULL;
END
GO

IF COL_LENGTH(N'[Asset]', N'DepreciationRatePercent') IS NULL
BEGIN
    ALTER TABLE [Asset] ADD [DepreciationRatePercent] DECIMAL(5,2) NULL;
END
GO
