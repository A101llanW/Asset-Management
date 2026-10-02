-- Normalize NIS Classrooms taxonomy: broad types, specific sub-types.
-- Principle: type = generic (Desks, Tables, Shelves, Boards); sub-type = specific variant.
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
    SELECT 1
    FROM [AssetType]
    WHERE [OrganizationId] = @NisOrgId
      AND [AssetCategoryId] = @ClassroomsCatId
      AND [Name] = N'Tables'
)
BEGIN
    INSERT INTO [AssetType] (
        [OrganizationId],
        [AssetCategoryId],
        [Name],
        [CreatedAt],
        [IsActive]
    )
    VALUES (@NisOrgId, @ClassroomsCatId, N'Tables', GETUTCDATE(), 1);
END

IF NOT EXISTS (
    SELECT 1
    FROM [AssetType]
    WHERE [OrganizationId] = @NisOrgId
      AND [AssetCategoryId] = @ClassroomsCatId
      AND [Name] = N'Shelves'
)
BEGIN
    INSERT INTO [AssetType] (
        [OrganizationId],
        [AssetCategoryId],
        [Name],
        [CreatedAt],
        [IsActive]
    )
    VALUES (@NisOrgId, @ClassroomsCatId, N'Shelves', GETUTCDATE(), 1);
END

IF NOT EXISTS (
    SELECT 1
    FROM [AssetType]
    WHERE [OrganizationId] = @NisOrgId
      AND [AssetCategoryId] = @ClassroomsCatId
      AND [Name] = N'Boards'
)
BEGIN
    INSERT INTO [AssetType] (
        [OrganizationId],
        [AssetCategoryId],
        [Name],
        [CreatedAt],
        [IsActive]
    )
    VALUES (@NisOrgId, @ClassroomsCatId, N'Boards', GETUTCDATE(), 1);
END

SELECT @TablesTypeId = [Id]
FROM [AssetType]
WHERE [OrganizationId] = @NisOrgId
  AND [AssetCategoryId] = @ClassroomsCatId
  AND [Name] = N'Tables';

SELECT @ShelvesTypeId = [Id]
FROM [AssetType]
WHERE [OrganizationId] = @NisOrgId
  AND [AssetCategoryId] = @ClassroomsCatId
  AND [Name] = N'Shelves';

SELECT @BoardsTypeId = [Id]
FROM [AssetType]
WHERE [OrganizationId] = @NisOrgId
  AND [AssetCategoryId] = @ClassroomsCatId
  AND [Name] = N'Boards';

DECLARE @Normalize TABLE (
    [TargetTypeId] INT NOT NULL,
    [SubTypeName] NVARCHAR(200) NOT NULL,
    [Brand] NVARCHAR(100) NOT NULL,
    [Model] NVARCHAR(100) NOT NULL,
    [LegacyTypeName] NVARCHAR(200) NOT NULL,
    [LegacyModels] NVARCHAR(400) NOT NULL
);

INSERT INTO @Normalize (
    [TargetTypeId],
    [SubTypeName],
    [Brand],
    [Model],
    [LegacyTypeName],
    [LegacyModels]
) VALUES
    (@DesksTypeId, N'Student desk (oval white)', N'', N'Student desk (oval white)', N'Student desks', N'Student desk (oval white)'),
    (@DesksTypeId, N'IM 3000', N'', N'IM 3000', N'Student desks', N'IM 3000'),
    (@TablesTypeId, N'Teacher''s table', N'', N'Teacher''s table', N'Teachers table', N'Teacher table,Teacher''s table'),
    (@BoardsTypeId, N'Soft board', N'', N'Soft board', N'Soft boards', N'Soft board'),
    (@ShelvesTypeId, N'Bag shelf', N'', N'Bag shelf', N'Bag shelves', N'Bag shelf'),
    (@ShelvesTypeId, N'Wooden bookshelf', N'', N'Wooden bookshelf', N'wooden Book shelves', N'Bookshelf,Wooden bookshelf');

DECLARE @TargetTypeId INT;
DECLARE @SubTypeName NVARCHAR(200);
DECLARE @Brand NVARCHAR(100);
DECLARE @Model NVARCHAR(100);
DECLARE @LegacyTypeName NVARCHAR(200);
DECLARE @LegacyModels NVARCHAR(400);
DECLARE @CanonicalSubTypeId INT;
DECLARE @LegacySubTypeId INT;

DECLARE normalize_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT [TargetTypeId], [SubTypeName], [Brand], [Model], [LegacyTypeName], [LegacyModels]
FROM @Normalize;

OPEN normalize_cursor;
FETCH NEXT FROM normalize_cursor
INTO @TargetTypeId, @SubTypeName, @Brand, @Model, @LegacyTypeName, @LegacyModels;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @CanonicalSubTypeId = NULL;
    SET @LegacySubTypeId = NULL;

    SELECT TOP 1 @LegacySubTypeId = st.[Id]
    FROM [AssetSubType] st
    INNER JOIN [AssetType] t
        ON t.[Id] = st.[AssetTypeId]
    WHERE st.[OrganizationId] = @NisOrgId
      AND st.[IsActive] = 1
      AND t.[OrganizationId] = @NisOrgId
      AND t.[AssetCategoryId] = @ClassroomsCatId
      AND t.[Name] = @LegacyTypeName
      AND (
          st.[Model] = @Model
          OR st.[Model] IN (
              SELECT LTRIM(RTRIM(value))
              FROM STRING_SPLIT(@LegacyModels, N',')
          )
      )
    ORDER BY (
        SELECT COUNT(*)
        FROM [Asset] a
        WHERE a.[AssetSubTypeId] = st.[Id]
          AND a.[IsActive] = 1
    ) DESC, st.[Id];

    SELECT TOP 1 @CanonicalSubTypeId = st.[Id]
    FROM [AssetSubType] st
    WHERE st.[OrganizationId] = @NisOrgId
      AND st.[IsActive] = 1
      AND st.[AssetTypeId] = @TargetTypeId
      AND st.[Brand] = @Brand
      AND st.[Model] = @Model;

    IF @LegacySubTypeId IS NOT NULL
    BEGIN
        IF @CanonicalSubTypeId IS NULL
        BEGIN
            UPDATE [AssetSubType]
            SET
                [AssetTypeId] = @TargetTypeId,
                [Name] = @SubTypeName,
                [Brand] = @Brand,
                [Model] = @Model,
                [UpdatedAt] = GETUTCDATE()
            WHERE [Id] = @LegacySubTypeId;

            SET @CanonicalSubTypeId = @LegacySubTypeId;
        END
        ELSE IF @LegacySubTypeId <> @CanonicalSubTypeId
        BEGIN
            UPDATE [Asset]
            SET
                [AssetSubTypeId] = @CanonicalSubTypeId,
                [UpdatedAt] = GETUTCDATE()
            WHERE [OrganizationId] = @NisOrgId
              AND [IsActive] = 1
              AND [AssetSubTypeId] = @LegacySubTypeId;

            UPDATE [AssetSubType]
            SET
                [IsActive] = 0,
                [UpdatedAt] = GETUTCDATE()
            WHERE [Id] = @LegacySubTypeId;
        END
    END
    ELSE IF @CanonicalSubTypeId IS NULL
    BEGIN
        INSERT INTO [AssetSubType] (
            [OrganizationId],
            [AssetTypeId],
            [Name],
            [Brand],
            [Model],
            [CreatedAt],
            [IsActive]
        )
        VALUES (
            @NisOrgId,
            @TargetTypeId,
            @SubTypeName,
            @Brand,
            @Model,
            GETUTCDATE(),
            1
        );

        SET @CanonicalSubTypeId = SCOPE_IDENTITY();
    END

    FETCH NEXT FROM normalize_cursor
    INTO @TargetTypeId, @SubTypeName, @Brand, @Model, @LegacyTypeName, @LegacyModels;
END

CLOSE normalize_cursor;
DEALLOCATE normalize_cursor;

-- Align teacher desk sub-type naming under Desks.
UPDATE st
SET
    st.[Name] = N'Teacher''s desk',
    st.[Model] = N'Teacher''s desk',
    st.[UpdatedAt] = GETUTCDATE()
FROM [AssetSubType] st
WHERE st.[OrganizationId] = @NisOrgId
  AND st.[IsActive] = 1
  AND st.[AssetTypeId] = @DesksTypeId
  AND st.[Model] IN (N'Teacher desk', N'Teacher''s desk');

-- Sync assets to their sub-type's broad type and brand/model.
UPDATE a
SET
    a.[AssetTypeId] = st.[AssetTypeId],
    a.[Brand] = st.[Brand],
    a.[Model] = st.[Model],
    a.[UpdatedAt] = GETUTCDATE()
FROM [Asset] a
INNER JOIN [AssetSubType] st
    ON st.[Id] = a.[AssetSubTypeId]
WHERE a.[OrganizationId] = @NisOrgId
  AND a.[IsActive] = 1
  AND st.[OrganizationId] = @NisOrgId
  AND st.[IsActive] = 1
  AND (
      a.[AssetTypeId] <> st.[AssetTypeId]
      OR ISNULL(a.[Brand], N'') <> st.[Brand]
      OR ISNULL(a.[Model], N'') <> st.[Model]
  );

-- Remove empty duplicate sub-types left on broad types.
UPDATE st
SET
    st.[IsActive] = 0,
    st.[UpdatedAt] = GETUTCDATE()
FROM [AssetSubType] st
INNER JOIN [AssetType] t
    ON t.[Id] = st.[AssetTypeId]
WHERE st.[OrganizationId] = @NisOrgId
  AND st.[IsActive] = 1
  AND t.[OrganizationId] = @NisOrgId
  AND t.[AssetCategoryId] = @ClassroomsCatId
  AND t.[Name] IN (N'Desks', N'Tables', N'Shelves', N'Boards')
  AND NOT EXISTS (
      SELECT 1
      FROM [Asset] a
      WHERE a.[OrganizationId] = @NisOrgId
        AND a.[IsActive] = 1
        AND a.[AssetSubTypeId] = st.[Id]
  )
  AND EXISTS (
      SELECT 1
      FROM [AssetSubType] other
      WHERE other.[OrganizationId] = @NisOrgId
        AND other.[IsActive] = 1
        AND other.[Brand] = st.[Brand]
        AND other.[Model] = st.[Model]
        AND other.[Id] <> st.[Id]
        AND EXISTS (
            SELECT 1
            FROM [Asset] a2
            WHERE a2.[OrganizationId] = @NisOrgId
              AND a2.[IsActive] = 1
              AND a2.[AssetSubTypeId] = other.[Id]
        )
  );

-- Retire overly-specific legacy types once empty.
UPDATE t
SET
    t.[IsActive] = 0,
    t.[UpdatedAt] = GETUTCDATE()
FROM [AssetType] t
WHERE t.[OrganizationId] = @NisOrgId
  AND t.[AssetCategoryId] = @ClassroomsCatId
  AND t.[Name] IN (
      N'Student desks',
      N'Teachers table',
      N'Bag shelves',
      N'Soft boards',
      N'wooden Book shelves'
  )
  AND NOT EXISTS (
      SELECT 1
      FROM [Asset] a
      WHERE a.[OrganizationId] = @NisOrgId
        AND a.[IsActive] = 1
        AND a.[AssetTypeId] = t.[Id]
  );
GO
