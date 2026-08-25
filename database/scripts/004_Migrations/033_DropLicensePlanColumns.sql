-- Remove unused license plan columns (limits tracked via MaxUsers only)
IF COL_LENGTH(N'[OrganizationLicense]', N'PlanCode') IS NOT NULL
BEGIN
    ALTER TABLE [OrganizationLicense] DROP COLUMN [PlanCode];
END
GO

IF COL_LENGTH(N'[OrganizationLicense]', N'PlanName') IS NOT NULL
BEGIN
    ALTER TABLE [OrganizationLicense] DROP COLUMN [PlanName];
END
GO
