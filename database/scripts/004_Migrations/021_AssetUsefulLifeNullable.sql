-- Useful life on assets is only set when category or type rules provide a value.
IF COL_LENGTH(N'[Asset]', N'UsefulLifeMonths') IS NOT NULL
BEGIN
    ALTER TABLE [Asset] ALTER COLUMN [UsefulLifeMonths] INT NULL;
END
GO
