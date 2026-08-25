-- Asset sub-type taxonomy and stock balances
IF OBJECT_ID(N'[AssetSubType]', N'U') IS NULL
BEGIN
    CREATE TABLE [AssetSubType] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [OrganizationId] INT NULL,
        [AssetTypeId] INT NOT NULL,
        [Name] NVARCHAR(200) NOT NULL,
        [Brand] NVARCHAR(100) NOT NULL CONSTRAINT DF_AssetSubType_Brand DEFAULT(N''),
        [Model] NVARCHAR(100) NOT NULL CONSTRAINT DF_AssetSubType_Model DEFAULT(N''),
        [TrackingMode] INT NOT NULL CONSTRAINT DF_AssetSubType_TrackingMode DEFAULT(1),
        [Specifications] NVARCHAR(MAX) NULL,
        [Sku] NVARCHAR(100) NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_AssetSubType_IsActive DEFAULT(1),
        CONSTRAINT FK_AssetSubType_AssetType FOREIGN KEY ([AssetTypeId]) REFERENCES [AssetType]([Id])
    );
END
GO

IF COL_LENGTH(N'[AssetSubType]', N'TrackingMode') IS NULL
BEGIN
    ALTER TABLE [AssetSubType]
        ADD [TrackingMode] INT NOT NULL CONSTRAINT DF_AssetSubType_TrackingMode DEFAULT(1);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_AssetSubType_OrgTypeBrandModelMode' AND object_id = OBJECT_ID(N'[AssetSubType]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_AssetSubType_OrgTypeBrandModelMode
        ON [AssetSubType]([OrganizationId], [AssetTypeId], [Brand], [Model], [TrackingMode])
        WHERE [IsActive] = 1;
END
GO

IF OBJECT_ID(N'[AssetStockBalance]', N'U') IS NULL
BEGIN
    CREATE TABLE [AssetStockBalance] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [OrganizationId] INT NULL,
        [AssetSubTypeId] INT NOT NULL,
        [DepartmentId] INT NULL,
        [QuantityOnHand] INT NOT NULL CONSTRAINT DF_AssetStockBalance_QuantityOnHand DEFAULT(0),
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_AssetStockBalance_IsActive DEFAULT(1),
        CONSTRAINT FK_AssetStockBalance_AssetSubType FOREIGN KEY ([AssetSubTypeId]) REFERENCES [AssetSubType]([Id]),
        CONSTRAINT FK_AssetStockBalance_Department FOREIGN KEY ([DepartmentId]) REFERENCES [Department]([Id])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_AssetStockBalance_SubTypeDepartment' AND object_id = OBJECT_ID(N'[AssetStockBalance]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_AssetStockBalance_SubTypeDepartment
        ON [AssetStockBalance]([AssetSubTypeId], [DepartmentId])
        WHERE [IsActive] = 1;
END
GO

IF COL_LENGTH(N'[Asset]', N'AssetSubTypeId') IS NULL
BEGIN
    ALTER TABLE [Asset] ADD [AssetSubTypeId] INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE [name] = N'FK_Asset_AssetSubType'
      AND [parent_object_id] = OBJECT_ID(N'[Asset]'))
BEGIN
    ALTER TABLE [Asset]
        ADD CONSTRAINT FK_Asset_AssetSubType FOREIGN KEY ([AssetSubTypeId]) REFERENCES [AssetSubType]([Id]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Asset_AssetSubTypeId' AND object_id = OBJECT_ID(N'[Asset]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Asset_AssetSubTypeId
        ON [Asset]([AssetSubTypeId])
        WHERE [IsActive] = 1;
END
GO

-- Backfill sub-types from distinct (OrganizationId, AssetTypeId, Brand, Model)
IF OBJECT_ID(N'[AssetSubType]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[Asset]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM [AssetSubType])
BEGIN
    ;WITH DistinctGroups AS (
        SELECT
            a.[OrganizationId],
            a.[AssetTypeId],
            UPPER(LTRIM(RTRIM(ISNULL(a.[Brand], N'')))) AS BrandKey,
            UPPER(LTRIM(RTRIM(ISNULL(a.[Model], N'')))) AS ModelKey,
            MIN(LTRIM(RTRIM(ISNULL(a.[Brand], N'')))) AS BrandDisplay,
            MIN(LTRIM(RTRIM(ISNULL(a.[Model], N'')))) AS ModelDisplay
        FROM [Asset] a
        WHERE a.[IsActive] = 1
        GROUP BY
            a.[OrganizationId],
            a.[AssetTypeId],
            UPPER(LTRIM(RTRIM(ISNULL(a.[Brand], N'')))),
            UPPER(LTRIM(RTRIM(ISNULL(a.[Model], N''))))
    )
    INSERT INTO [AssetSubType] (
        [OrganizationId],
        [AssetTypeId],
        [Name],
        [Brand],
        [Model],
        [TrackingMode],
        [CreatedAt],
        [IsActive])
    SELECT
        g.[OrganizationId],
        g.[AssetTypeId],
        CASE
            WHEN NULLIF(g.[BrandDisplay], N'') IS NOT NULL AND NULLIF(g.[ModelDisplay], N'') IS NOT NULL
                THEN g.[BrandDisplay] + N' - ' + g.[ModelDisplay]
            WHEN NULLIF(g.[BrandDisplay], N'') IS NOT NULL THEN g.[BrandDisplay]
            WHEN NULLIF(g.[ModelDisplay], N'') IS NOT NULL THEN g.[ModelDisplay]
            ELSE N'Unspecified item'
        END,
        ISNULL(g.[BrandDisplay], N''),
        ISNULL(g.[ModelDisplay], N''),
        1,
        GETUTCDATE(),
        1
    FROM DistinctGroups g;

    UPDATE a
    SET a.[AssetSubTypeId] = st.[Id]
    FROM [Asset] a
    INNER JOIN [AssetSubType] st
        ON (st.[OrganizationId] = a.[OrganizationId] OR (st.[OrganizationId] IS NULL AND a.[OrganizationId] IS NULL))
       AND st.[AssetTypeId] = a.[AssetTypeId]
       AND UPPER(LTRIM(RTRIM(ISNULL(st.[Brand], N'')))) = UPPER(LTRIM(RTRIM(ISNULL(a.[Brand], N''))))
       AND UPPER(LTRIM(RTRIM(ISNULL(st.[Model], N'')))) = UPPER(LTRIM(RTRIM(ISNULL(a.[Model], N''))))
       AND st.[TrackingMode] = 1
       AND st.[IsActive] = 1
    WHERE a.[IsActive] = 1
      AND a.[AssetSubTypeId] IS NULL;
END
GO
