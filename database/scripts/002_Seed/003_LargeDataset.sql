-- ============================================================================
-- DEV-ONLY: Large dataset generator for performance benchmarks (100k assets).
-- Disabled by default. Enable with initialize-database.ps1 -IncludeLargeDataset
-- or set @RunLargeDatasetSeed = 1 below before running this script manually.
-- Requires multitenancy (OrganizationId on Asset). Do NOT run in production.
-- ============================================================================
DECLARE @RunLargeDatasetSeed BIT = 0;

IF @RunLargeDatasetSeed = 0
BEGIN
    PRINT 'Skipping 003_LargeDataset.sql (dev-only; use -IncludeLargeDataset or set @RunLargeDatasetSeed = 1).';
END
ELSE IF COL_LENGTH(N'[Asset]', N'OrganizationId') IS NULL
BEGIN
    RAISERROR('003_LargeDataset.sql requires OrganizationId on Asset. Run after multitenancy migrations.', 16, 1);
END
ELSE
BEGIN
    DECLARE @defaultOrgId INT = (SELECT TOP 1 [Id] FROM [Organization] WHERE [Slug] = N'default' ORDER BY [Id]);
    DECLARE @orgBId INT = (SELECT TOP 1 [Id] FROM [Organization] WHERE [Slug] = N'demo-b' ORDER BY [Id]);
    IF @defaultOrgId IS NULL
    BEGIN
        SET @defaultOrgId = (SELECT TOP 1 [Id] FROM [Organization] ORDER BY [Id]);
    END

    IF @orgBId IS NULL
    BEGIN
        RAISERROR('003_LargeDataset.sql requires demo-b organization (013_SecondDemoOrg.sql).', 16, 1);
    END

    DECLARE @marker NVARCHAR(50) = N'LargeDatasetSeed_v1';
    IF EXISTS (SELECT 1 FROM [Asset] WHERE [AssetTag] = @marker)
    BEGIN
        PRINT 'Large dataset already seeded.';
    END
    ELSE
    BEGIN
        DECLARE @now DATETIME = GETUTCDATE();
        DECLARE @targetPerOrg INT = 50000;
        DECLARE @batchSize INT = 1000;
        DECLARE @orgId INT;
        DECLARE @orgIndex INT = 0;
        DECLARE @deptId INT;
        DECLARE @categoryId INT;
        DECLARE @typeId INT;
        DECLARE @supplierId INT;
        DECLARE @counter INT;
        DECLARE @batch INT;
        DECLARE @assetName NVARCHAR(200);
        DECLARE @assetTag NVARCHAR(50);
        DECLARE @serial NVARCHAR(80);

        WHILE @orgIndex < 2
        BEGIN
            SET @orgId = CASE WHEN @orgIndex = 0 THEN @defaultOrgId ELSE @orgBId END;
            SET @deptId = (SELECT TOP 1 [Id] FROM [Department] WHERE [OrganizationId] = @orgId ORDER BY [Id]);
            SET @categoryId = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [OrganizationId] = @orgId ORDER BY [Id]);
            SET @typeId = (SELECT TOP 1 [Id] FROM [AssetType] WHERE [OrganizationId] = @orgId ORDER BY [Id]);
            SET @supplierId = (SELECT TOP 1 [Id] FROM [Supplier] WHERE [OrganizationId] = @orgId ORDER BY [Id]);

            IF @deptId IS NULL OR @categoryId IS NULL OR @typeId IS NULL OR @supplierId IS NULL
            BEGIN
                RAISERROR('Missing reference data for large dataset seed on organization.', 16, 1);
            END

            SET @counter = 1;
            WHILE @counter <= @targetPerOrg
            BEGIN
                SET @batch = CASE WHEN @batchSize < (@targetPerOrg - @counter + 1) THEN @batchSize ELSE (@targetPerOrg - @counter + 1) END;

                ;WITH seq AS (
                    SELECT TOP (@batch) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
                    FROM sys.all_objects a CROSS JOIN sys.all_objects b
                )
                INSERT INTO [Asset]
                    ([AssetName],[AssetTag],[CategoryId],[AssetTypeId],[Brand],[Model],[SerialNumber],[CurrentStatus],[Description],
                     [PurchaseDate],[AcquisitionCost],[TaxAmount],[Currency],[SupplierId],[DepartmentId],[ConditionOnReceipt],[UsefulLifeMonths],[SalvageValue],
                     [DepreciationMethod],[DepreciationStartDate],[CurrentBookValue],[AccumulatedDepreciation],[ReplacementValue],[OrganizationId],[CreatedAt],[IsActive])
                SELECT
                    N'Bench Asset ' + CAST(@counter + n - 1 AS NVARCHAR(20)),
                    N'LDS-' + CAST(@orgId AS NVARCHAR(10)) + N'-' + RIGHT(N'000000' + CAST(@counter + n - 1 AS NVARCHAR(10)), 6),
                    @categoryId,
                    @typeId,
                    N'Brand',
                    N'Model',
                    N'LDS-SN-' + CAST(@orgId AS NVARCHAR(10)) + N'-' + CAST(@counter + n - 1 AS NVARCHAR(20)),
                    6,
                    N'Performance benchmark seed row',
                    DATEADD(DAY, -((@counter + n - 1) % 365), @now),
                    100000,
                    16000,
                    N'KES',
                    @supplierId,
                    @deptId,
                    N'New',
                    48,
                    5000,
                    0,
                    @now,
                    90000,
                    10000,
                    110000,
                    @orgId,
                    @now,
                    1
                FROM seq;

                SET @counter = @counter + @batch;
            END

            SET @orgIndex = @orgIndex + 1;
        END

        INSERT INTO [Asset]
            ([AssetName],[AssetTag],[CategoryId],[AssetTypeId],[Brand],[Model],[SerialNumber],[CurrentStatus],[OrganizationId],[CreatedAt],[IsActive],
             [PurchaseDate],[AcquisitionCost],[TaxAmount],[Currency],[SupplierId],[DepartmentId],[ConditionOnReceipt],[UsefulLifeMonths],[SalvageValue],
             [DepreciationMethod],[DepreciationStartDate],[CurrentBookValue],[AccumulatedDepreciation],[ReplacementValue])
        VALUES
            (N'Large dataset marker', @marker, @categoryId, @typeId, N'Marker', N'Marker', N'MARKER', 6, @defaultOrgId, @now, 0,
             @now, 0, 0, N'KES', @supplierId, @deptId, N'New', 48, 0, 0, @now, 0, 0, 0);

        PRINT 'Large dataset seed complete (~100k assets across default + demo-b orgs).';
    END
END
GO
