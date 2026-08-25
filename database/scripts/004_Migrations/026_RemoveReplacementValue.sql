IF COL_LENGTH(N'[Asset]', N'ReplacementValue') IS NOT NULL
BEGIN
    ALTER TABLE [Asset] DROP COLUMN [ReplacementValue];
END
GO

IF COL_LENGTH(N'[InsurancePolicy]', N'ReplacementValue') IS NOT NULL
BEGIN
    ALTER TABLE [InsurancePolicy] DROP COLUMN [ReplacementValue];
END
GO
