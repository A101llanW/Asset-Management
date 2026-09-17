-- Merge plural/synonym duplicates for asset types, sub-types, and IT/ICT departments.
-- Idempotent: safe to re-run.

IF OBJECT_ID(N'[AssetType]', N'U') IS NULL
   OR OBJECT_ID(N'[Asset]', N'U') IS NULL
BEGIN
    RETURN;
END
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

-- Asset types: merge plural pairs (Laptops/Laptop, Desktops/Desktop, etc.) to singular canonical name.
DECLARE @TypeMerge TABLE (
    OrganizationId INT NOT NULL,
    KeepTypeId INT NOT NULL,
    DropTypeId INT NOT NULL,
    CanonicalName NVARCHAR(200) NOT NULL
);

INSERT INTO @TypeMerge (OrganizationId, KeepTypeId, DropTypeId, CanonicalName)
SELECT
    t1.OrganizationId,
    CASE
        WHEN RIGHT(LOWER(LTRIM(RTRIM(t1.Name))), 1) <> 's' THEN t1.Id
        ELSE t2.Id
    END AS KeepTypeId,
    CASE
        WHEN RIGHT(LOWER(LTRIM(RTRIM(t1.Name))), 1) <> 's' THEN t2.Id
        ELSE t1.Id
    END AS DropTypeId,
    CASE
        WHEN RIGHT(LOWER(LTRIM(RTRIM(t1.Name))), 1) <> 's' THEN t1.Name
        ELSE t2.Name
    END AS CanonicalName
FROM [AssetType] t1
INNER JOIN [AssetType] t2
    ON t1.[OrganizationId] = t2.[OrganizationId]
   AND t1.[AssetCategoryId] = t2.[AssetCategoryId]
   AND t1.[Id] < t2.[Id]
   AND t1.[IsActive] = 1
   AND t2.[IsActive] = 1
WHERE (
        LOWER(LTRIM(RTRIM(t1.[Name]))) + N's' = LOWER(LTRIM(RTRIM(t2.[Name])))
     OR LOWER(LTRIM(RTRIM(t2.[Name]))) + N's' = LOWER(LTRIM(RTRIM(t1.[Name])))
      );

UPDATE keeper
SET keeper.[Name] = mergeRow.[CanonicalName],
    keeper.[UpdatedAt] = GETUTCDATE()
FROM [AssetType] keeper
INNER JOIN @TypeMerge mergeRow ON mergeRow.[KeepTypeId] = keeper.[Id]
WHERE keeper.[Name] <> mergeRow.[CanonicalName];

UPDATE a
SET a.[AssetTypeId] = mergeRow.[KeepTypeId],
    a.[UpdatedAt] = GETUTCDATE()
FROM [Asset] a
INNER JOIN @TypeMerge mergeRow ON mergeRow.[DropTypeId] = a.[AssetTypeId]
WHERE a.[IsActive] = 1;

IF OBJECT_ID(N'[AssetSubType]', N'U') IS NOT NULL
BEGIN
    UPDATE st
    SET st.[AssetTypeId] = mergeRow.[KeepTypeId],
        st.[UpdatedAt] = GETUTCDATE()
    FROM [AssetSubType] st
    INNER JOIN @TypeMerge mergeRow ON mergeRow.[DropTypeId] = st.[AssetTypeId]
    WHERE st.[IsActive] = 1;
END

IF OBJECT_ID(N'[SupplierCatalogItem]', N'U') IS NOT NULL
BEGIN
    UPDATE sci
    SET sci.[AssetTypeId] = mergeRow.[KeepTypeId],
        sci.[UpdatedAt] = GETUTCDATE()
    FROM [SupplierCatalogItem] sci
    INNER JOIN @TypeMerge mergeRow ON mergeRow.[DropTypeId] = sci.[AssetTypeId]
    WHERE sci.[IsActive] = 1;
END

UPDATE t
SET t.[IsActive] = 0,
    t.[UpdatedAt] = GETUTCDATE()
FROM [AssetType] t
INNER JOIN @TypeMerge mergeRow ON mergeRow.[DropTypeId] = t.[Id];

-- Asset sub-types: merge plural name pairs within the same type.
IF OBJECT_ID(N'[AssetSubType]', N'U') IS NOT NULL
BEGIN
    DECLARE @SubTypeMerge TABLE (
        KeepSubTypeId INT NOT NULL,
        DropSubTypeId INT NOT NULL,
        CanonicalName NVARCHAR(200) NOT NULL
    );

    INSERT INTO @SubTypeMerge (KeepSubTypeId, DropSubTypeId, CanonicalName)
    SELECT
        CASE
            WHEN RIGHT(LOWER(LTRIM(RTRIM(s1.[Name]))), 1) <> 's' THEN s1.[Id]
            ELSE s2.[Id]
        END,
        CASE
            WHEN RIGHT(LOWER(LTRIM(RTRIM(s1.[Name]))), 1) <> 's' THEN s2.[Id]
            ELSE s1.[Id]
        END,
        CASE
            WHEN RIGHT(LOWER(LTRIM(RTRIM(s1.[Name]))), 1) <> 's' THEN s1.[Name]
            ELSE s2.[Name]
        END
    FROM [AssetSubType] s1
    INNER JOIN [AssetSubType] s2
        ON s1.[OrganizationId] = s2.[OrganizationId]
       AND s1.[AssetTypeId] = s2.[AssetTypeId]
       AND s1.[Id] < s2.[Id]
       AND s1.[IsActive] = 1
       AND s2.[IsActive] = 1
    WHERE (
            LOWER(LTRIM(RTRIM(s1.[Name]))) + N's' = LOWER(LTRIM(RTRIM(s2.[Name])))
         OR LOWER(LTRIM(RTRIM(s2.[Name]))) + N's' = LOWER(LTRIM(RTRIM(s1.[Name])))
          );

    UPDATE keeper
    SET keeper.[Name] = mergeRow.[CanonicalName],
        keeper.[UpdatedAt] = GETUTCDATE()
    FROM [AssetSubType] keeper
    INNER JOIN @SubTypeMerge mergeRow ON mergeRow.[KeepSubTypeId] = keeper.[Id]
    WHERE keeper.[Name] <> mergeRow.[CanonicalName];

    UPDATE a
    SET a.[AssetSubTypeId] = mergeRow.[KeepSubTypeId],
        a.[UpdatedAt] = GETUTCDATE()
    FROM [Asset] a
    INNER JOIN @SubTypeMerge mergeRow ON mergeRow.[DropSubTypeId] = a.[AssetSubTypeId]
    WHERE a.[IsActive] = 1;

    UPDATE st
    SET st.[IsActive] = 0,
        st.[UpdatedAt] = GETUTCDATE()
    FROM [AssetSubType] st
    INNER JOIN @SubTypeMerge mergeRow ON mergeRow.[DropSubTypeId] = st.[Id];
END

-- Departments: ensure ICT sub-department sits under Information Technology (IT).
IF OBJECT_ID(N'[Department]', N'U') IS NOT NULL
BEGIN
    UPDATE ict
    SET ict.[ParentDepartmentId] = itDept.[Id],
        ict.[DepartmentKind] = 1,
        ict.[IsRequisitionTarget] = 1,
        ict.[UpdatedAt] = GETUTCDATE()
    FROM [Department] ict
    INNER JOIN [Department] itDept
        ON itDept.[OrganizationId] = ict.[OrganizationId]
       AND itDept.[Code] = N'IT'
       AND itDept.[IsActive] = 1
       AND itDept.[ParentDepartmentId] IS NULL
    WHERE ict.[Code] = N'IT-ICT'
      AND ict.[IsActive] = 1
      AND (ict.[ParentDepartmentId] IS NULL OR ict.[ParentDepartmentId] <> itDept.[Id]);

    UPDATE itDept
    SET itDept.[IsRequisitionTarget] = 0,
        itDept.[UpdatedAt] = GETUTCDATE()
    FROM [Department] itDept
    WHERE itDept.[Code] = N'IT'
      AND itDept.[IsActive] = 1
      AND itDept.[ParentDepartmentId] IS NULL
      AND itDept.[IsRequisitionTarget] = 1;

    DECLARE @ItDup TABLE (OrganizationId INT NOT NULL, SourceDeptId INT NOT NULL, TargetDeptId INT NOT NULL);

    INSERT INTO @ItDup (OrganizationId, SourceDeptId, TargetDeptId)
    SELECT dup.[OrganizationId], dup.[Id], ict.[Id]
    FROM [Department] dup
    INNER JOIN [Department] ict
        ON ict.[OrganizationId] = dup.[OrganizationId]
       AND ict.[Code] = N'IT-ICT'
       AND ict.[IsActive] = 1
    WHERE dup.[IsActive] = 1
      AND dup.[Code] IN (N'IT-101', N'ICT', N'ICT-ALL')
      AND dup.[Id] <> ict.[Id];

    UPDATE a
    SET a.[DepartmentId] = mergeDept.[TargetDeptId],
        a.[UpdatedAt] = GETUTCDATE()
    FROM [Asset] a
    INNER JOIN @ItDup mergeDept ON mergeDept.[SourceDeptId] = a.[DepartmentId]
    WHERE a.[IsActive] = 1;

    UPDATE d
    SET d.[IsActive] = 0,
        d.[UpdatedAt] = GETUTCDATE()
    FROM [Department] d
    INNER JOIN @ItDup mergeDept ON mergeDept.[SourceDeptId] = d.[Id];
END

COMMIT TRANSACTION;
GO
