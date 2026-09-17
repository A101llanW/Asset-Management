-- Backfill NIS legacy classroom furniture assets missing AssetSubTypeId.
-- Uses broad types (Desks, Tables, Shelves, Boards) with specific sub-types.
-- Idempotent: safe to re-run.

IF OBJECT_ID(N'[Asset]', N'U') IS NULL
   OR OBJECT_ID(N'[AssetSubType]', N'U') IS NULL
   OR OBJECT_ID(N'[AssetType]', N'U') IS NULL
   OR OBJECT_ID(N'[Organization]', N'U') IS NULL
BEGIN
    RETURN;
END
GO

DECLARE @NisOrgId INT = (
    SELECT TOP 1 [Id]
    FROM [Organization]
    WHERE [Slug] = N'nis'
);

IF @NisOrgId IS NULL
BEGIN
    RETURN;
END

DECLARE @ClassroomsCatId INT = (
    SELECT TOP 1 [Id]
    FROM [AssetCategory]
    WHERE [OrganizationId] = @NisOrgId
      AND [Name] = N'Classrooms'
);

IF @ClassroomsCatId IS NULL
BEGIN
    RETURN;
END

DECLARE @DesksTypeId INT;
DECLARE @TablesTypeId INT;
DECLARE @ShelvesTypeId INT;
DECLARE @BoardsTypeId INT;

SELECT @DesksTypeId = [Id]
FROM [AssetType]
WHERE [OrganizationId] = @NisOrgId
  AND [AssetCategoryId] = @ClassroomsCatId
  AND [Name] = N'Desks';

IF @DesksTypeId IS NULL
BEGIN
    INSERT INTO [AssetType] (
        [OrganizationId],
        [AssetCategoryId],
        [Name],
        [CreatedAt],
        [IsActive]
    )
    VALUES (@NisOrgId, @ClassroomsCatId, N'Desks', GETUTCDATE(), 1);

    SET @DesksTypeId = SCOPE_IDENTITY();
END

IF NOT EXISTS (
    SELECT 1 FROM [AssetType]
    WHERE [OrganizationId] = @NisOrgId AND [AssetCategoryId] = @ClassroomsCatId AND [Name] = N'Tables'
)
BEGIN
    INSERT INTO [AssetType] ([OrganizationId], [AssetCategoryId], [Name], [CreatedAt], [IsActive])
    VALUES (@NisOrgId, @ClassroomsCatId, N'Tables', GETUTCDATE(), 1);
END

IF NOT EXISTS (
    SELECT 1 FROM [AssetType]
    WHERE [OrganizationId] = @NisOrgId AND [AssetCategoryId] = @ClassroomsCatId AND [Name] = N'Shelves'
)
BEGIN
    INSERT INTO [AssetType] ([OrganizationId], [AssetCategoryId], [Name], [CreatedAt], [IsActive])
    VALUES (@NisOrgId, @ClassroomsCatId, N'Shelves', GETUTCDATE(), 1);
END

IF NOT EXISTS (
    SELECT 1 FROM [AssetType]
    WHERE [OrganizationId] = @NisOrgId AND [AssetCategoryId] = @ClassroomsCatId AND [Name] = N'Boards'
)
BEGIN
    INSERT INTO [AssetType] ([OrganizationId], [AssetCategoryId], [Name], [CreatedAt], [IsActive])
    VALUES (@NisOrgId, @ClassroomsCatId, N'Boards', GETUTCDATE(), 1);
END

SELECT @TablesTypeId = [Id] FROM [AssetType]
WHERE [OrganizationId] = @NisOrgId AND [AssetCategoryId] = @ClassroomsCatId AND [Name] = N'Tables';

SELECT @ShelvesTypeId = [Id] FROM [AssetType]
WHERE [OrganizationId] = @NisOrgId AND [AssetCategoryId] = @ClassroomsCatId AND [Name] = N'Shelves';

SELECT @BoardsTypeId = [Id] FROM [AssetType]
WHERE [OrganizationId] = @NisOrgId AND [AssetCategoryId] = @ClassroomsCatId AND [Name] = N'Boards';

DECLARE @Map TABLE (
    [AssetTypeId] INT NOT NULL,
    [SubTypeName] NVARCHAR(200) NOT NULL,
    [Brand] NVARCHAR(100) NOT NULL,
    [Model] NVARCHAR(100) NOT NULL,
    [LegacyTypeName] NVARCHAR(200) NOT NULL
);

INSERT INTO @Map ([AssetTypeId], [SubTypeName], [Brand], [Model], [LegacyTypeName]) VALUES
    (@DesksTypeId, N'Student desk (oval white)', N'', N'Student desk (oval white)', N'Student desks'),
    (@TablesTypeId, N'Teacher''s table', N'', N'Teacher''s table', N'Teachers table'),
    (@BoardsTypeId, N'Soft board', N'', N'Soft board', N'Soft boards'),
    (@ShelvesTypeId, N'Bag shelf', N'', N'Bag shelf', N'Bag shelves'),
    (@ShelvesTypeId, N'Wooden bookshelf', N'', N'Wooden bookshelf', N'wooden Book shelves');

INSERT INTO [AssetSubType] (
    [OrganizationId],
    [AssetTypeId],
    [Name],
    [Brand],
    [Model],
    [CreatedAt],
    [IsActive]
)
SELECT
    @NisOrgId,
    m.[AssetTypeId],
    m.[SubTypeName],
    m.[Brand],
    m.[Model],
    GETUTCDATE(),
    1
FROM @Map m
WHERE NOT EXISTS (
    SELECT 1
    FROM [AssetSubType] st
    WHERE st.[OrganizationId] = @NisOrgId
      AND st.[Brand] = m.[Brand]
      AND st.[Model] = m.[Model]
      AND st.[IsActive] = 1
);

UPDATE a
SET
    a.[AssetSubTypeId] = st.[Id],
    a.[AssetTypeId] = st.[AssetTypeId],
    a.[Brand] = st.[Brand],
    a.[Model] = st.[Model],
    a.[UpdatedAt] = GETUTCDATE()
FROM [Asset] a
INNER JOIN [AssetType] legacyType
    ON legacyType.[Id] = a.[AssetTypeId]
INNER JOIN @Map m
    ON m.[LegacyTypeName] = legacyType.[Name]
INNER JOIN [AssetSubType] st
    ON st.[OrganizationId] = @NisOrgId
   AND st.[AssetTypeId] = m.[AssetTypeId]
   AND st.[Brand] = m.[Brand]
   AND st.[Model] = m.[Model]
   AND st.[IsActive] = 1
WHERE a.[OrganizationId] = @NisOrgId
  AND a.[IsActive] = 1
  AND legacyType.[OrganizationId] = @NisOrgId
  AND legacyType.[AssetCategoryId] = @ClassroomsCatId
  AND a.[AssetSubTypeId] IS NULL;
GO
