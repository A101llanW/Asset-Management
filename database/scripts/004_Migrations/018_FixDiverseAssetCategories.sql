-- Correct demo asset categories for visible variety (printers/projectors -> Office Equipment, lab centrifuge type).
DECLARE @orgId INT = (SELECT TOP 1 [Id] FROM [Organization] WHERE [Slug] IN (N'nanosoft', N'default') ORDER BY CASE WHEN [Slug] = N'nanosoft' THEN 0 ELSE 1 END, [Id]);
IF @orgId IS NULL
    SET @orgId = (SELECT TOP 1 [Id] FROM [Organization] ORDER BY [Id]);

IF @orgId IS NOT NULL
BEGIN
    DECLARE @now DATETIME = GETUTCDATE();

    IF NOT EXISTS (SELECT 1 FROM [AssetCategory] WHERE [Name] = N'Office Equipment' AND [OrganizationId] = @orgId)
    BEGIN
        INSERT INTO [AssetCategory] ([Name],[Description],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (N'Office Equipment', N'Printers, projectors, and general office devices', @orgId, @now, 1);
    END

    IF NOT EXISTS (SELECT 1 FROM [AssetCategory] WHERE [Name] = N'Vehicles' AND [OrganizationId] = @orgId)
    BEGIN
        INSERT INTO [AssetCategory] ([Name],[Description],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (N'Vehicles', N'Fleet and transport assets', @orgId, @now, 1);
    END

    UPDATE [AssetCategory]
    SET [OrganizationId] = @orgId
    WHERE [Name] = N'Vehicles' AND [OrganizationId] IS NULL;

    DECLARE @catIt INT = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [Name] = N'IT Equipment' AND [OrganizationId] = @orgId ORDER BY [Id]);
    DECLARE @catOffice INT = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [Name] = N'Office Equipment' AND [OrganizationId] = @orgId ORDER BY [Id]);
    DECLARE @catFurn INT = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [Name] = N'Furniture' AND [OrganizationId] = @orgId ORDER BY [Id]);
    DECLARE @catNet INT = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [Name] = N'Networking' AND [OrganizationId] = @orgId ORDER BY [Id]);
    DECLARE @catMed INT = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [Name] = N'Medical/Lab Equipment' AND [OrganizationId] = @orgId ORDER BY [Id]);
    DECLARE @catVeh INT = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [Name] = N'Vehicles' AND [OrganizationId] = @orgId ORDER BY [Id]);

    IF @catOffice IS NOT NULL
    BEGIN
        UPDATE [AssetType]
        SET [AssetCategoryId] = @catOffice
        WHERE [Name] IN (N'Printer', N'Projector')
          AND [OrganizationId] = @orgId
          AND [AssetCategoryId] <> @catOffice;

        UPDATE a
        SET a.[CategoryId] = @catOffice
        FROM [Asset] a
        WHERE a.[OrganizationId] = @orgId
          AND a.[AssetTag] IN (N'IT-PRT-001', N'HR-PRJ-001', N'OPS-PRT-001', N'FIN-PRT-001')
          AND (a.[CategoryId] IS NULL OR a.[CategoryId] <> @catOffice);
    END

    IF @catIt IS NOT NULL
    BEGIN
        UPDATE a SET a.[CategoryId] = @catIt
        FROM [Asset] a
        WHERE a.[OrganizationId] = @orgId
          AND a.[AssetTag] IN (N'IT-LTP-001', N'IT-LTP-002', N'IT-LTP-003', N'IT-DTP-001', N'FIN-DTP-001', N'FIN-DTP-002', N'HR-LTP-001')
          AND (a.[CategoryId] IS NULL OR a.[CategoryId] <> @catIt);
    END

    IF @catFurn IS NOT NULL
    BEGIN
        UPDATE a SET a.[CategoryId] = @catFurn
        FROM [Asset] a
        WHERE a.[OrganizationId] = @orgId
          AND a.[AssetTag] IN (N'FIN-CHR-001', N'HR-CHR-001', N'ADMIN-CHR-001', N'ADMIN-DESK-001')
          AND (a.[CategoryId] IS NULL OR a.[CategoryId] <> @catFurn);
    END

    IF @catNet IS NOT NULL
    BEGIN
        UPDATE a SET a.[CategoryId] = @catNet
        FROM [Asset] a
        WHERE a.[OrganizationId] = @orgId
          AND a.[AssetTag] IN (N'IT-RTR-001', N'OPS-RTR-001')
          AND (a.[CategoryId] IS NULL OR a.[CategoryId] <> @catNet);
    END

    IF @catMed IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Lab Centrifuge' AND [OrganizationId] = @orgId)
        BEGIN
            INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[OrganizationId],[CreatedAt],[IsActive])
            VALUES (@catMed, N'Lab Centrifuge', N'Benchtop laboratory centrifuge', @orgId, @now, 1);
        END

        DECLARE @typeCentrifuge INT = (SELECT TOP 1 [Id] FROM [AssetType] WHERE [Name] = N'Lab Centrifuge' AND [OrganizationId] = @orgId ORDER BY [Id]);

        UPDATE a SET a.[CategoryId] = @catMed
        FROM [Asset] a
        WHERE a.[OrganizationId] = @orgId
          AND a.[AssetTag] IN (N'OPS-MED-001', N'OPS-MED-002')
          AND (a.[CategoryId] IS NULL OR a.[CategoryId] <> @catMed);

        IF @typeCentrifuge IS NOT NULL
        BEGIN
            UPDATE a SET a.[AssetTypeId] = @typeCentrifuge
            FROM [Asset] a
            WHERE a.[OrganizationId] = @orgId
              AND a.[AssetTag] = N'OPS-MED-002'
              AND a.[AssetTypeId] <> @typeCentrifuge;
        END
    END

    IF @catVeh IS NOT NULL
    BEGIN
        UPDATE a SET a.[CategoryId] = @catVeh
        FROM [Asset] a
        WHERE a.[OrganizationId] = @orgId
          AND a.[AssetTag] = N'OPS-VHC-001'
          AND (a.[CategoryId] IS NULL OR a.[CategoryId] <> @catVeh);
    END
END
GO
