-- Clear legacy import placeholder brand/model values written before imports stored NULL.
-- Idempotent: safe to re-run; only rows with exact legacy placeholders are updated.

IF OBJECT_ID(N'[Asset]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'[Asset]', N'Brand') IS NOT NULL
    BEGIN
        UPDATE [Asset]
        SET [Brand] = NULL
        WHERE [Brand] IS NOT NULL
          AND UPPER(LTRIM(RTRIM([Brand]))) = N'UNKNOWN';
    END

    IF COL_LENGTH(N'[Asset]', N'Model') IS NOT NULL
    BEGIN
        UPDATE [Asset]
        SET [Model] = NULL
        WHERE [Model] IS NOT NULL
          AND UPPER(LTRIM(RTRIM([Model]))) = N'LEGACY IMPORT';
    END
END
GO

-- AssetSubType brand/model are NOT NULL; normalize placeholders to empty string.
IF OBJECT_ID(N'[AssetSubType]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'[AssetSubType]', N'Brand') IS NOT NULL
    BEGIN
        UPDATE [AssetSubType]
        SET [Brand] = N''
        WHERE UPPER(LTRIM(RTRIM([Brand]))) = N'UNKNOWN';
    END

    IF COL_LENGTH(N'[AssetSubType]', N'Model') IS NOT NULL
    BEGIN
        UPDATE [AssetSubType]
        SET [Model] = N''
        WHERE UPPER(LTRIM(RTRIM([Model]))) = N'LEGACY IMPORT';
    END
END
GO
