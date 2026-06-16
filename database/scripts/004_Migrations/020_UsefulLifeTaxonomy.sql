-- Operational useful-life defaults per category, with optional per-type overrides.
IF COL_LENGTH(N'[AssetCategory]', N'DefaultUsefulLifeMonths') IS NULL
BEGIN
    ALTER TABLE [AssetCategory] ADD [DefaultUsefulLifeMonths] INT NULL;
END
GO

IF COL_LENGTH(N'[AssetType]', N'UsefulLifeMonths') IS NULL
BEGIN
    ALTER TABLE [AssetType] ADD [UsefulLifeMonths] INT NULL;
END
GO
