-- Unified unit tracking: every physical unit is an Asset with its own tag/QR.
-- Greenfield reset (2C): discard quantity stock balances; remove TrackingMode.
IF OBJECT_ID(N'[AssetStockBalance]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [AssetStockBalance];
END
GO
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'UX_AssetSubType_OrgTypeBrandModelMode'
      AND [object_id] = OBJECT_ID(N'[AssetSubType]'))
BEGIN
    DROP INDEX UX_AssetSubType_OrgTypeBrandModelMode ON [AssetSubType];
END
GO
IF COL_LENGTH(N'[AssetSubType]', N'TrackingMode') IS NOT NULL
BEGIN
    DECLARE @dfName NVARCHAR(256);
    SELECT @dfName = dc.[name]
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.[default_object_id] = dc.[object_id]
    WHERE dc.[parent_object_id] = OBJECT_ID(N'[AssetSubType]')
      AND c.[name] = N'TrackingMode';
    IF @dfName IS NOT NULL
    BEGIN
        EXEC(N'ALTER TABLE [AssetSubType] DROP CONSTRAINT [' + @dfName + N']');
    END
    ALTER TABLE [AssetSubType] DROP COLUMN [TrackingMode];
END
GO
IF OBJECT_ID(N'[AssetSubType]', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes
       WHERE [name] = N'UX_AssetSubType_OrgTypeBrandModel'
         AND [object_id] = OBJECT_ID(N'[AssetSubType]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_AssetSubType_OrgTypeBrandModel
        ON [AssetSubType]([OrganizationId], [AssetTypeId], [Brand], [Model])
        WHERE [IsActive] = 1;
END
GO
