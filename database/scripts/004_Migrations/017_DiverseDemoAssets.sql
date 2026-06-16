-- Diverse multi-department demo assets (runs after multitenancy migrations).
-- Reference categories/types are seeded in 002_Seed/001_ReferenceData.sql with OrganizationId.
-- Diverse demo assets and linked operational history
IF NOT EXISTS (SELECT 1 FROM [Asset] WHERE [AssetTag] = N'IT-LTP-001')
BEGIN
    DECLARE @now DATETIME = GETUTCDATE();
    DECLARE @orgId INT = (SELECT TOP 1 [Id] FROM [Organization] WHERE [Slug] = N'default' ORDER BY [Id]);
    IF @orgId IS NULL
        SET @orgId = (SELECT TOP 1 [Id] FROM [Organization] ORDER BY [Id]);

    -- Remove legacy homogeneous laptop-only seed if upgrading in place during init
    IF EXISTS (SELECT 1 FROM [Asset] WHERE [AssetTag] LIKE N'AST-2026-%')
    BEGIN
        DELETE ic FROM [InsuranceClaim] ic INNER JOIN [Asset] a ON a.[Id] = ic.[AssetId] WHERE a.[AssetTag] LIKE N'AST-2026-%';
        DELETE ip FROM [InsurancePolicy] ip INNER JOIN [Asset] a ON a.[Id] = ip.[AssetId] WHERE a.[AssetTag] LIKE N'AST-2026-%';
        DELETE dr FROM [DepreciationRecord] dr INNER JOIN [Asset] a ON a.[Id] = dr.[AssetId] WHERE a.[AssetTag] LIKE N'AST-2026-%';
        DELETE mr FROM [AssetMaintenanceRecord] mr INNER JOIN [Asset] a ON a.[Id] = mr.[AssetId] WHERE a.[AssetTag] LIKE N'AST-2026-%';
        DELETE ai FROM [AssetIncident] ai INNER JOIN [Asset] a ON a.[Id] = ai.[AssetId] WHERE a.[AssetTag] LIKE N'AST-2026-%';
        DELETE ce FROM [AssetCustodyEvent] ce INNER JOIN [Asset] a ON a.[Id] = ce.[AssetId] WHERE a.[AssetTag] LIKE N'AST-2026-%';
        DELETE aa FROM [AssetAssignment] aa INNER JOIN [Asset] a ON a.[Id] = aa.[AssetId] WHERE a.[AssetTag] LIKE N'AST-2026-%';
        DELETE FROM [Asset] WHERE [AssetTag] LIKE N'AST-2026-%';
    END

    -- Remove partial diverse seed from a previously failed init attempt
    IF EXISTS (SELECT 1 FROM [Asset] WHERE [AssetTag] IN (
        N'IT-LTP-002', N'IT-DTP-001', N'IT-RTR-001', N'IT-PRT-001', N'FIN-DTP-001', N'FIN-CHR-001', N'FIN-DTP-002',
        N'HR-LTP-001', N'HR-PRJ-001', N'HR-CHR-001', N'OPS-RTR-001', N'OPS-MED-001', N'OPS-MED-002', N'OPS-VHC-001',
        N'ADMIN-CHR-001', N'ADMIN-DESK-001', N'IT-LTP-003', N'OPS-PRT-001', N'FIN-PRT-001'))
    BEGIN
        DELETE ic FROM [InsuranceClaim] ic INNER JOIN [Asset] a ON a.[Id] = ic.[AssetId] WHERE a.[AssetTag] IN (
            N'IT-LTP-002', N'IT-DTP-001', N'IT-RTR-001', N'IT-PRT-001', N'FIN-DTP-001', N'FIN-CHR-001', N'FIN-DTP-002',
            N'HR-LTP-001', N'HR-PRJ-001', N'HR-CHR-001', N'OPS-RTR-001', N'OPS-MED-001', N'OPS-MED-002', N'OPS-VHC-001',
            N'ADMIN-CHR-001', N'ADMIN-DESK-001', N'IT-LTP-003', N'OPS-PRT-001', N'FIN-PRT-001');
        DELETE ip FROM [InsurancePolicy] ip INNER JOIN [Asset] a ON a.[Id] = ip.[AssetId] WHERE a.[AssetTag] IN (
            N'IT-LTP-002', N'IT-DTP-001', N'IT-RTR-001', N'IT-PRT-001', N'FIN-DTP-001', N'FIN-CHR-001', N'FIN-DTP-002',
            N'HR-LTP-001', N'HR-PRJ-001', N'HR-CHR-001', N'OPS-RTR-001', N'OPS-MED-001', N'OPS-MED-002', N'OPS-VHC-001',
            N'ADMIN-CHR-001', N'ADMIN-DESK-001', N'IT-LTP-003', N'OPS-PRT-001', N'FIN-PRT-001');
        DELETE dr FROM [DepreciationRecord] dr INNER JOIN [Asset] a ON a.[Id] = dr.[AssetId] WHERE a.[AssetTag] IN (
            N'IT-LTP-002', N'IT-DTP-001', N'IT-RTR-001', N'IT-PRT-001', N'FIN-DTP-001', N'FIN-CHR-001', N'FIN-DTP-002',
            N'HR-LTP-001', N'HR-PRJ-001', N'HR-CHR-001', N'OPS-RTR-001', N'OPS-MED-001', N'OPS-MED-002', N'OPS-VHC-001',
            N'ADMIN-CHR-001', N'ADMIN-DESK-001', N'IT-LTP-003', N'OPS-PRT-001', N'FIN-PRT-001');
        DELETE mr FROM [AssetMaintenanceRecord] mr INNER JOIN [Asset] a ON a.[Id] = mr.[AssetId] WHERE a.[AssetTag] IN (
            N'IT-LTP-002', N'IT-DTP-001', N'IT-RTR-001', N'IT-PRT-001', N'FIN-DTP-001', N'FIN-CHR-001', N'FIN-DTP-002',
            N'HR-LTP-001', N'HR-PRJ-001', N'HR-CHR-001', N'OPS-RTR-001', N'OPS-MED-001', N'OPS-MED-002', N'OPS-VHC-001',
            N'ADMIN-CHR-001', N'ADMIN-DESK-001', N'IT-LTP-003', N'OPS-PRT-001', N'FIN-PRT-001');
        DELETE ai FROM [AssetIncident] ai INNER JOIN [Asset] a ON a.[Id] = ai.[AssetId] WHERE a.[AssetTag] IN (
            N'IT-LTP-002', N'IT-DTP-001', N'IT-RTR-001', N'IT-PRT-001', N'FIN-DTP-001', N'FIN-CHR-001', N'FIN-DTP-002',
            N'HR-LTP-001', N'HR-PRJ-001', N'HR-CHR-001', N'OPS-RTR-001', N'OPS-MED-001', N'OPS-MED-002', N'OPS-VHC-001',
            N'ADMIN-CHR-001', N'ADMIN-DESK-001', N'IT-LTP-003', N'OPS-PRT-001', N'FIN-PRT-001');
        DELETE ce FROM [AssetCustodyEvent] ce INNER JOIN [Asset] a ON a.[Id] = ce.[AssetId] WHERE a.[AssetTag] IN (
            N'IT-LTP-002', N'IT-DTP-001', N'IT-RTR-001', N'IT-PRT-001', N'FIN-DTP-001', N'FIN-CHR-001', N'FIN-DTP-002',
            N'HR-LTP-001', N'HR-PRJ-001', N'HR-CHR-001', N'OPS-RTR-001', N'OPS-MED-001', N'OPS-MED-002', N'OPS-VHC-001',
            N'ADMIN-CHR-001', N'ADMIN-DESK-001', N'IT-LTP-003', N'OPS-PRT-001', N'FIN-PRT-001');
        DELETE aa FROM [AssetAssignment] aa INNER JOIN [Asset] a ON a.[Id] = aa.[AssetId] WHERE a.[AssetTag] IN (
            N'IT-LTP-002', N'IT-DTP-001', N'IT-RTR-001', N'IT-PRT-001', N'FIN-DTP-001', N'FIN-CHR-001', N'FIN-DTP-002',
            N'HR-LTP-001', N'HR-PRJ-001', N'HR-CHR-001', N'OPS-RTR-001', N'OPS-MED-001', N'OPS-MED-002', N'OPS-VHC-001',
            N'ADMIN-CHR-001', N'ADMIN-DESK-001', N'IT-LTP-003', N'OPS-PRT-001', N'FIN-PRT-001');
        DELETE FROM [Asset] WHERE [AssetTag] IN (
            N'IT-LTP-002', N'IT-DTP-001', N'IT-RTR-001', N'IT-PRT-001', N'FIN-DTP-001', N'FIN-CHR-001', N'FIN-DTP-002',
            N'HR-LTP-001', N'HR-PRJ-001', N'HR-CHR-001', N'OPS-RTR-001', N'OPS-MED-001', N'OPS-MED-002', N'OPS-VHC-001',
            N'ADMIN-CHR-001', N'ADMIN-DESK-001', N'IT-LTP-003', N'OPS-PRT-001', N'FIN-PRT-001');
    END

    DECLARE @deptIt INT, @deptFin INT, @deptHr INT, @deptOps INT, @deptAdmin INT;
    DECLARE @supTech INT, @supOffice INT, @supMed INT;

    -- Reference data (seed scripts run after migrations; ensure prerequisites here)
    DECLARE @catIt INT = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [Name] = N'IT Equipment' AND [OrganizationId] = @orgId ORDER BY [Id]);
    IF @catIt IS NULL
    BEGIN
        INSERT INTO [AssetCategory] ([Name],[Description],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (N'IT Equipment', N'Computing and peripheral assets', @orgId, @now, 1);
        SET @catIt = SCOPE_IDENTITY();
    END
    DECLARE @catOffice INT = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [Name] = N'Office Equipment' AND [OrganizationId] = @orgId ORDER BY [Id]);
    IF @catOffice IS NULL
    BEGIN
        INSERT INTO [AssetCategory] ([Name],[Description],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (N'Office Equipment', N'Printers, projectors, and general office devices', @orgId, @now, 1);
        SET @catOffice = SCOPE_IDENTITY();
    END
    DECLARE @catFurn INT = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [Name] = N'Furniture' AND [OrganizationId] = @orgId ORDER BY [Id]);
    IF @catFurn IS NULL
    BEGIN
        INSERT INTO [AssetCategory] ([Name],[Description],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (N'Furniture', N'Office furniture assets', @orgId, @now, 1);
        SET @catFurn = SCOPE_IDENTITY();
    END
    DECLARE @catNet INT = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [Name] = N'Networking' AND [OrganizationId] = @orgId ORDER BY [Id]);
    IF @catNet IS NULL
    BEGIN
        INSERT INTO [AssetCategory] ([Name],[Description],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (N'Networking', N'Network and communication assets', @orgId, @now, 1);
        SET @catNet = SCOPE_IDENTITY();
    END
    DECLARE @catMed INT = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [Name] = N'Medical/Lab Equipment' AND [OrganizationId] = @orgId ORDER BY [Id]);
    IF @catMed IS NULL
    BEGIN
        INSERT INTO [AssetCategory] ([Name],[Description],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (N'Medical/Lab Equipment', N'Healthcare and laboratory assets', @orgId, @now, 1);
        SET @catMed = SCOPE_IDENTITY();
    END
    DECLARE @catVeh INT = (SELECT TOP 1 [Id] FROM [AssetCategory] WHERE [Name] = N'Vehicles' AND [OrganizationId] = @orgId ORDER BY [Id]);
    IF @catVeh IS NULL
    BEGIN
        INSERT INTO [AssetCategory] ([Name],[Description],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (N'Vehicles', N'Fleet and transport assets', @orgId, @now, 1);
        SET @catVeh = SCOPE_IDENTITY();
    END

    IF NOT EXISTS (SELECT 1 FROM [Department] WHERE [Code] = N'IT' AND [OrganizationId] = @orgId)
        INSERT INTO [Department] ([Name],[Code],[Description],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (N'Information Technology', N'IT', N'IT department', @orgId, @now, 1);
    IF NOT EXISTS (SELECT 1 FROM [Department] WHERE [Code] = N'FIN' AND [OrganizationId] = @orgId)
        INSERT INTO [Department] ([Name],[Code],[Description],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (N'Finance', N'FIN', N'Finance department', @orgId, @now, 1);
    IF NOT EXISTS (SELECT 1 FROM [Department] WHERE [Code] = N'HR' AND [OrganizationId] = @orgId)
        INSERT INTO [Department] ([Name],[Code],[Description],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (N'Human Resources', N'HR', N'HR department', @orgId, @now, 1);
    IF NOT EXISTS (SELECT 1 FROM [Department] WHERE [Code] = N'OPS' AND [OrganizationId] = @orgId)
        INSERT INTO [Department] ([Name],[Code],[Description],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (N'Operations', N'OPS', N'Operations department', @orgId, @now, 1);
    IF NOT EXISTS (SELECT 1 FROM [Department] WHERE [Code] = N'ADMIN' AND [OrganizationId] = @orgId)
        INSERT INTO [Department] ([Name],[Code],[Description],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (N'Administration', N'ADMIN', N'Administration department', @orgId, @now, 1);

    SET @deptIt = (SELECT TOP 1 [Id] FROM [Department] WHERE [Code] = N'IT' AND [OrganizationId] = @orgId ORDER BY [Id]);
    SET @deptFin = (SELECT TOP 1 [Id] FROM [Department] WHERE [Code] = N'FIN' AND [OrganizationId] = @orgId ORDER BY [Id]);
    SET @deptHr = (SELECT TOP 1 [Id] FROM [Department] WHERE [Code] = N'HR' AND [OrganizationId] = @orgId ORDER BY [Id]);
    SET @deptOps = (SELECT TOP 1 [Id] FROM [Department] WHERE [Code] = N'OPS' AND [OrganizationId] = @orgId ORDER BY [Id]);
    SET @deptAdmin = (SELECT TOP 1 [Id] FROM [Department] WHERE [Code] = N'ADMIN' AND [OrganizationId] = @orgId ORDER BY [Id]);

    IF NOT EXISTS (SELECT 1 FROM [Supplier] WHERE [SupplierName] = N'Tech Source Ltd' AND [OrganizationId] = @orgId)
        INSERT INTO [Supplier] ([SupplierName],[ContactPerson],[Email],[Phone],[Address],[RegistrationNumber],[Notes],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (N'Tech Source Ltd', N'Mary Wanjiku', N'sales@techsource.example', N'+254700000001', N'Nairobi', N'TSL-001', N'Primary IT supplier', @orgId, @now, 1);
    IF NOT EXISTS (SELECT 1 FROM [Supplier] WHERE [SupplierName] = N'Office Works Hub' AND [OrganizationId] = @orgId)
        INSERT INTO [Supplier] ([SupplierName],[ContactPerson],[Email],[Phone],[Address],[RegistrationNumber],[Notes],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (N'Office Works Hub', N'David Mwangi', N'contact@officeworks.example', N'+254700000002', N'Mombasa', N'OWH-003', N'Furniture and office equipment', @orgId, @now, 1);
    IF NOT EXISTS (SELECT 1 FROM [Supplier] WHERE [SupplierName] = N'MedEquip Africa' AND [OrganizationId] = @orgId)
        INSERT INTO [Supplier] ([SupplierName],[ContactPerson],[Email],[Phone],[Address],[RegistrationNumber],[Notes],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (N'MedEquip Africa', N'Anne Njeri', N'support@medequip.example', N'+254700000003', N'Kisumu', N'MEA-018', N'Medical and lab equipment', @orgId, @now, 1);

    SET @supTech = (SELECT TOP 1 [Id] FROM [Supplier] WHERE [SupplierName] = N'Tech Source Ltd' AND [OrganizationId] = @orgId ORDER BY [Id]);
    SET @supOffice = (SELECT TOP 1 [Id] FROM [Supplier] WHERE [SupplierName] = N'Office Works Hub' AND [OrganizationId] = @orgId ORDER BY [Id]);
    SET @supMed = (SELECT TOP 1 [Id] FROM [Supplier] WHERE [SupplierName] = N'MedEquip Africa' AND [OrganizationId] = @orgId ORDER BY [Id]);

    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Laptop' AND [OrganizationId] = @orgId) AND @catIt IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (@catIt, N'Laptop', N'Portable computer', @orgId, @now, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Desktop' AND [OrganizationId] = @orgId) AND @catIt IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (@catIt, N'Desktop', N'Desktop computer', @orgId, @now, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Router' AND [OrganizationId] = @orgId) AND @catNet IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (@catNet, N'Router', N'Router and gateway', @orgId, @now, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Printer' AND [OrganizationId] = @orgId) AND @catOffice IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (@catOffice, N'Printer', N'Office printer or MFP', @orgId, @now, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Projector' AND [OrganizationId] = @orgId) AND @catOffice IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (@catOffice, N'Projector', N'Conference room projector', @orgId, @now, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Office Chair' AND [OrganizationId] = @orgId) AND @catFurn IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (@catFurn, N'Office Chair', N'Ergonomic chair', @orgId, @now, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Office Desk' AND [OrganizationId] = @orgId) AND @catFurn IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (@catFurn, N'Office Desk', N'Office desk or workstation', @orgId, @now, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Lab Microscope' AND [OrganizationId] = @orgId) AND @catMed IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (@catMed, N'Lab Microscope', N'Microscope device', @orgId, @now, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Lab Centrifuge' AND [OrganizationId] = @orgId) AND @catMed IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (@catMed, N'Lab Centrifuge', N'Benchtop laboratory centrifuge', @orgId, @now, 1);
    IF NOT EXISTS (SELECT 1 FROM [AssetType] WHERE [Name] = N'Vehicle' AND [OrganizationId] = @orgId) AND @catVeh IS NOT NULL
        INSERT INTO [AssetType] ([AssetCategoryId],[Name],[Description],[OrganizationId],[CreatedAt],[IsActive])
        VALUES (@catVeh, N'Vehicle', N'Company fleet vehicle', @orgId, @now, 1);

    DECLARE @typeLaptop INT = (SELECT TOP 1 [Id] FROM [AssetType] WHERE [Name] = N'Laptop' AND [OrganizationId] = @orgId ORDER BY [Id]);
    DECLARE @typeDesktop INT = (SELECT TOP 1 [Id] FROM [AssetType] WHERE [Name] = N'Desktop' AND [OrganizationId] = @orgId ORDER BY [Id]);
    DECLARE @typeRouter INT = (SELECT TOP 1 [Id] FROM [AssetType] WHERE [Name] = N'Router' AND [OrganizationId] = @orgId ORDER BY [Id]);
    DECLARE @typePrinter INT = (SELECT TOP 1 [Id] FROM [AssetType] WHERE [Name] = N'Printer' AND [OrganizationId] = @orgId ORDER BY [Id]);
    DECLARE @typeProjector INT = (SELECT TOP 1 [Id] FROM [AssetType] WHERE [Name] = N'Projector' AND [OrganizationId] = @orgId ORDER BY [Id]);
    DECLARE @typeChair INT = (SELECT TOP 1 [Id] FROM [AssetType] WHERE [Name] = N'Office Chair' AND [OrganizationId] = @orgId ORDER BY [Id]);
    DECLARE @typeDesk INT = (SELECT TOP 1 [Id] FROM [AssetType] WHERE [Name] = N'Office Desk' AND [OrganizationId] = @orgId ORDER BY [Id]);
    DECLARE @typeMicroscope INT = (SELECT TOP 1 [Id] FROM [AssetType] WHERE [Name] = N'Lab Microscope' AND [OrganizationId] = @orgId ORDER BY [Id]);
    DECLARE @typeCentrifuge INT = (SELECT TOP 1 [Id] FROM [AssetType] WHERE [Name] = N'Lab Centrifuge' AND [OrganizationId] = @orgId ORDER BY [Id]);
    DECLARE @typeVehicle INT = (SELECT TOP 1 [Id] FROM [AssetType] WHERE [Name] = N'Vehicle' AND [OrganizationId] = @orgId ORDER BY [Id]);

    DECLARE @userAdmin NVARCHAR(128) = N'seed-user-001';
    DECLARE @userAssetMgr NVARCHAR(128) = N'seed-user-002';
    DECLARE @userFinance NVARCHAR(128) = N'seed-user-004';
    DECLARE @userStaff NVARCHAR(128) = N'seed-user-005';
    DECLARE @userDeptHead NVARCHAR(128) = N'seed-user-007';
    DECLARE @userItStaff NVARCHAR(128) = N'seed-user-008';
    DECLARE @userOpsStaff NVARCHAR(128) = N'seed-user-009';
    DECLARE @userLabTech NVARCHAR(128) = N'seed-user-010';

    DECLARE @assetId INT;

    -- Helper pattern: insert asset, assignment/custody when assigned, depreciation, optional insurance
    INSERT INTO [Asset]
        ([AssetName],[AssetTag],[CategoryId],[AssetTypeId],[Brand],[Model],[SerialNumber],[BarcodeOrQRCode],[Specifications],[Condition],[CurrentStatus],[Description],
         [PurchaseDate],[AcquisitionCost],[TaxAmount],[Currency],[SupplierId],[DepartmentId],[CurrentCustodianId],[ConditionOnReceipt],[UsefulLifeMonths],[SalvageValue],
         [DepreciationMethod],[DepreciationStartDate],[CurrentBookValue],[AccumulatedDepreciation],[IsLeased],[IsInsured],[InsuredValue],[PolicyReference],
         [WarrantyStartDate],[WarrantyEndDate],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'HP EliteBook 840 G9', N'IT-LTP-001', @catIt, @typeLaptop, N'HP', N'EliteBook 840 G9', N'SN-HP-840-001', N'BC-IT-LTP-001', N'Core i7, 16GB RAM, 512GB SSD', 2, 6, N'Primary IT analyst laptop', DATEADD(MONTH, -14, @now), 165000, 26400, N'KES', @supTech, @deptIt, @userItStaff, N'New', 48, 12000, 0, DATEADD(MONTH, -14, @now), 142000, 23000, 0, 1, 160000, N'POL-IT-2026-001', DATEADD(MONTH, -14, @now), DATEADD(MONTH, 10, @now), @orgId, @now, 1);
    SET @assetId = SCOPE_IDENTITY();
    INSERT INTO [AssetAssignment] ([AssetId],[ToDepartmentId],[ToUserId],[AssignmentType],[AssignedDate],[ConditionBeforeHandover],[HandedOverById],[ReceivedById],[RecipientAcknowledged],[AcknowledgedAt],[OrganizationId],[CreatedAt],[IsActive])
    VALUES (@assetId, @deptIt, @userItStaff, 1, DATEADD(MONTH, -13, @now), N'Good', @userAssetMgr, @userItStaff, 1, DATEADD(MONTH, -13, @now), @orgId, @now, 1);

    INSERT INTO [AssetCustodyEvent] ([AssetId],[ActionType],[ActionDate],[ToUserId],[ToDepartmentId],[ConditionBefore],[ConditionAfter],[Reason],[Notes],[ApprovedById],[OrganizationId],[CreatedAt],[IsActive])
    VALUES (@assetId, 1, DATEADD(MONTH, -13, @now), @userItStaff, @deptIt, N'New', N'Good', N'New hire provisioning', N'Seed assignment', @userAssetMgr, @orgId, @now, 1);
    INSERT INTO [DepreciationRecord] ([AssetId],[PeriodStartDate],[PeriodEndDate],[Method],[OpeningBookValue],[DepreciationAmount],[ClosingBookValue],[AccumulatedDepreciation],[IsPosted],[PostedAt],[OrganizationId],[CreatedAt],[IsActive])
    VALUES (@assetId, DATEADD(MONTH, -1, @now), @now, 0, 144500, 2500, 142000, 23000, 1, @now, @orgId, @now, 1);
    INSERT INTO [InsurancePolicy] ([AssetId],[InsurerName],[PolicyNumber],[PolicyStartDate],[PolicyEndDate],[InsuredValue],[ValuationDate],[ClaimEligibility],[DeductibleAmount],[ClaimNotes],[OrganizationId],[CreatedAt],[IsActive])
    VALUES (@assetId, N'Global Insurance Plc', N'GIP-IT-001', DATEADD(MONTH, -12, @now), DATEADD(MONTH, 12, @now), 160000, DATEADD(DAY, -10, @now), 1, 15000, N'IT equipment cover', @orgId, @now, 1);
    INSERT INTO [AssetIncident] ([IncidentNumber],[AssetId],[ReportedById],[IncidentType],[IncidentDate],[Description],[Severity],[ResolutionStatus],[OrganizationId],[CreatedAt],[IsActive])
    VALUES (N'INC-2026-001', @assetId, @userItStaff, 8, DATEADD(DAY, -45, @now), N'Minor keyboard key stuck after spill', 1, N'Resolved - cleaned', @orgId, @now, 1);

    INSERT INTO [Asset]
        ([AssetName],[AssetTag],[CategoryId],[AssetTypeId],[Brand],[Model],[SerialNumber],[BarcodeOrQRCode],[Specifications],[Condition],[CurrentStatus],[Description],
         [PurchaseDate],[AcquisitionCost],[TaxAmount],[Currency],[SupplierId],[DepartmentId],[CurrentCustodianId],[ConditionOnReceipt],[UsefulLifeMonths],[SalvageValue],
         [DepreciationMethod],[DepreciationStartDate],[CurrentBookValue],[AccumulatedDepreciation],[IsLeased],[IsInsured],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'Dell Latitude 7430', N'IT-LTP-002', @catIt, @typeLaptop, N'Dell', N'Latitude 7430', N'SN-DL-743-002', N'BC-IT-LTP-002', N'Core i5, 16GB RAM, 256GB SSD', 2, 6, N'Asset manager field laptop', DATEADD(MONTH, -10, @now), 155000, 24800, N'KES', @supTech, @deptIt, @userAssetMgr, N'New', 48, 10000, 0, DATEADD(MONTH, -10, @now), 138000, 17000, 0, 1, @orgId, @now, 1);
    SET @assetId = SCOPE_IDENTITY();
    INSERT INTO [AssetAssignment] ([AssetId],[ToDepartmentId],[ToUserId],[AssignmentType],[AssignedDate],[ConditionBeforeHandover],[HandedOverById],[ReceivedById],[RecipientAcknowledged],[OrganizationId],[CreatedAt],[IsActive])
    VALUES (@assetId, @deptIt, @userAssetMgr, 1, DATEADD(MONTH, -9, @now), N'Good', @userAdmin, @userAssetMgr, 0, @orgId, @now, 1);

    INSERT INTO [AssetCustodyEvent] ([AssetId],[ActionType],[ActionDate],[ToUserId],[ToDepartmentId],[ConditionBefore],[ConditionAfter],[Reason],[OrganizationId],[CreatedAt],[IsActive])
    VALUES (@assetId, 1, DATEADD(MONTH, -9, @now), @userAssetMgr, @deptIt, N'New', N'Good', N'Manager provisioning', @orgId, @now, 1);

    INSERT INTO [Asset]
        ([AssetName],[AssetTag],[CategoryId],[AssetTypeId],[Brand],[Model],[SerialNumber],[BarcodeOrQRCode],[Specifications],[Condition],[CurrentStatus],[Description],
         [PurchaseDate],[AcquisitionCost],[TaxAmount],[Currency],[SupplierId],[DepartmentId],[ConditionOnReceipt],[UsefulLifeMonths],[SalvageValue],
         [DepreciationMethod],[DepreciationStartDate],[CurrentBookValue],[AccumulatedDepreciation],[IsLeased],[IsInsured],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'Dell OptiPlex 7090', N'IT-DTP-001', @catIt, @typeDesktop, N'Dell', N'OptiPlex 7090', N'SN-DO-709-003', N'BC-IT-DTP-001', N'Core i5, 8GB RAM, 512GB SSD', 1, 5, N'Spare IT desktop in store', DATEADD(MONTH, -6, @now), 98000, 15680, N'KES', @supTech, @deptIt, N'New', 60, 8000, 0, DATEADD(MONTH, -6, @now), 92000, 6000, 0, 0, @orgId, @now, 1);

    INSERT INTO [Asset]
        ([AssetName],[AssetTag],[CategoryId],[AssetTypeId],[Brand],[Model],[SerialNumber],[BarcodeOrQRCode],[Specifications],[Condition],[CurrentStatus],[Description],
         [PurchaseDate],[AcquisitionCost],[TaxAmount],[Currency],[SupplierId],[DepartmentId],[ConditionOnReceipt],[UsefulLifeMonths],[SalvageValue],
         [DepreciationMethod],[DepreciationStartDate],[CurrentBookValue],[AccumulatedDepreciation],[IsLeased],[IsInsured],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'Cisco ISR 4331 Router', N'IT-RTR-001', @catNet, @typeRouter, N'Cisco', N'ISR 4331', N'SN-CS-4331-004', N'BC-IT-RTR-001', N'Branch edge router', 2, 5, N'Data centre edge router spare', DATEADD(MONTH, -24, @now), 420000, 67200, N'KES', @supTech, @deptIt, N'Good', 84, 40000, 0, DATEADD(MONTH, -24, @now), 310000, 110000, 0, 0, @orgId, @now, 1);

    INSERT INTO [Asset]
        ([AssetName],[AssetTag],[CategoryId],[AssetTypeId],[Brand],[Model],[SerialNumber],[BarcodeOrQRCode],[Specifications],[Condition],[CurrentStatus],[Description],
         [PurchaseDate],[AcquisitionCost],[TaxAmount],[Currency],[SupplierId],[DepartmentId],[CurrentCustodianId],[ConditionOnReceipt],[UsefulLifeMonths],[SalvageValue],
         [DepreciationMethod],[DepreciationStartDate],[CurrentBookValue],[AccumulatedDepreciation],[IsLeased],[IsInsured],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'HP LaserJet Pro M404', N'IT-PRT-001', @catOffice, @typePrinter, N'HP', N'LaserJet M404dn', N'SN-HP-404-005', N'BC-IT-PRT-001', N'Mono laser, network', 2, 6, N'IT department printer', DATEADD(MONTH, -18, @now), 85000, 13600, N'KES', @supTech, @deptIt, @userItStaff, N'New', 60, 5000, 0, DATEADD(MONTH, -18, @now), 62000, 23000, 0, 0, @orgId, @now, 1);
    SET @assetId = SCOPE_IDENTITY();
    INSERT INTO [AssetAssignment] ([AssetId],[ToDepartmentId],[ToUserId],[AssignmentType],[AssignedDate],[HandedOverById],[RecipientAcknowledged],[OrganizationId],[CreatedAt],[IsActive])
    VALUES (@assetId, @deptIt, @userItStaff, 3, DATEADD(MONTH, -17, @now), @userAssetMgr, 0, @orgId, @now, 1);

    INSERT INTO [Asset]
        ([AssetName],[AssetTag],[CategoryId],[AssetTypeId],[Brand],[Model],[SerialNumber],[BarcodeOrQRCode],[Specifications],[Condition],[CurrentStatus],[Description],
         [PurchaseDate],[AcquisitionCost],[TaxAmount],[Currency],[SupplierId],[DepartmentId],[CurrentCustodianId],[ConditionOnReceipt],[UsefulLifeMonths],[SalvageValue],
         [DepreciationMethod],[DepreciationStartDate],[CurrentBookValue],[AccumulatedDepreciation],[IsLeased],[IsInsured],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'Lenovo ThinkCentre M90q', N'FIN-DTP-001', @catIt, @typeDesktop, N'Lenovo', N'ThinkCentre M90q', N'SN-LN-M90-006', N'BC-FIN-DTP-001', N'Core i5, 16GB RAM', 2, 6, N'Finance officer workstation', DATEADD(MONTH, -20, @now), 112000, 17920, N'KES', @supTech, @deptFin, @userFinance, N'New', 60, 9000, 0, DATEADD(MONTH, -20, @now), 88000, 24000, 0, 0, @orgId, @now, 1);
    SET @assetId = SCOPE_IDENTITY();
    INSERT INTO [AssetAssignment] ([AssetId],[ToDepartmentId],[ToUserId],[AssignmentType],[AssignedDate],[HandedOverById],[ReceivedById],[RecipientAcknowledged],[OrganizationId],[CreatedAt],[IsActive])
    VALUES (@assetId, @deptFin, @userFinance, 1, DATEADD(MONTH, -19, @now), @userAssetMgr, @userFinance, 0, @orgId, @now, 1);

    INSERT INTO [AssetCustodyEvent] ([AssetId],[ActionType],[ActionDate],[ToUserId],[ToDepartmentId],[ConditionBefore],[ConditionAfter],[Reason],[OrganizationId],[CreatedAt],[IsActive])
    VALUES (@assetId, 1, DATEADD(MONTH, -19, @now), @userFinance, @deptFin, N'New', N'Good', N'Finance desk setup', @orgId, @now, 1);

    INSERT INTO [Asset]
        ([AssetName],[AssetTag],[CategoryId],[AssetTypeId],[Brand],[Model],[SerialNumber],[BarcodeOrQRCode],[Specifications],[Condition],[CurrentStatus],[Description],
         [PurchaseDate],[AcquisitionCost],[TaxAmount],[Currency],[SupplierId],[DepartmentId],[CurrentCustodianId],[ConditionOnReceipt],[UsefulLifeMonths],[SalvageValue],
         [DepreciationMethod],[DepreciationStartDate],[CurrentBookValue],[AccumulatedDepreciation],[IsLeased],[IsInsured],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'Steelcase Series 1 Chair', N'FIN-CHR-001', @catFurn, @typeChair, N'Steelcase', N'Series 1', N'SN-ST-CHR-007', N'BC-FIN-CHR-001', N'Ergonomic task chair', 2, 6, N'Finance officer chair', DATEADD(MONTH, -20, @now), 45000, 7200, N'KES', @supOffice, @deptFin, @userFinance, N'New', 84, 3000, 0, DATEADD(MONTH, -20, @now), 38000, 7000, 0, 0, @orgId, @now, 1);
    SET @assetId = SCOPE_IDENTITY();
    INSERT INTO [AssetAssignment] ([AssetId],[ToDepartmentId],[ToUserId],[AssignmentType],[AssignedDate],[RecipientAcknowledged],[OrganizationId],[CreatedAt],[IsActive])
    VALUES (@assetId, @deptFin, @userFinance, 1, DATEADD(MONTH, -19, @now), 0, @orgId, @now, 1);

    INSERT INTO [Asset]
        ([AssetName],[AssetTag],[CategoryId],[AssetTypeId],[Brand],[Model],[SerialNumber],[BarcodeOrQRCode],[Specifications],[Condition],[CurrentStatus],[Description],
         [PurchaseDate],[AcquisitionCost],[TaxAmount],[Currency],[SupplierId],[DepartmentId],[ConditionOnReceipt],[UsefulLifeMonths],[SalvageValue],
         [DepreciationMethod],[DepreciationStartDate],[CurrentBookValue],[AccumulatedDepreciation],[IsLeased],[IsInsured],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'HP ProDesk 400 G9', N'FIN-DTP-002', @catIt, @typeDesktop, N'HP', N'ProDesk 400 G9', N'SN-HP-PD4-008', N'BC-FIN-DTP-002', N'Core i3, 8GB RAM', 1, 5, N'Spare finance desktop', DATEADD(MONTH, -4, @now), 89000, 14240, N'KES', @supTech, @deptFin, N'New', 60, 7000, 0, DATEADD(MONTH, -4, @now), 86000, 3000, 0, 0, @orgId, @now, 1);

    INSERT INTO [Asset]
        ([AssetName],[AssetTag],[CategoryId],[AssetTypeId],[Brand],[Model],[SerialNumber],[BarcodeOrQRCode],[Specifications],[Condition],[CurrentStatus],[Description],
         [PurchaseDate],[AcquisitionCost],[TaxAmount],[Currency],[SupplierId],[DepartmentId],[CurrentCustodianId],[ConditionOnReceipt],[UsefulLifeMonths],[SalvageValue],
         [DepreciationMethod],[DepreciationStartDate],[CurrentBookValue],[AccumulatedDepreciation],[IsLeased],[IsInsured],[InsuredValue],[PolicyReference],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'Apple MacBook Air M2', N'HR-LTP-001', @catIt, @typeLaptop, N'Apple', N'MacBook Air M2', N'SN-AP-MBA-009', N'BC-HR-LTP-001', N'M2, 16GB RAM, 512GB SSD', 2, 6, N'HR department head laptop', DATEADD(MONTH, -8, @now), 185000, 29600, N'KES', @supTech, @deptHr, @userDeptHead, N'New', 48, 15000, 0, DATEADD(MONTH, -8, @now), 168000, 17000, 0, 1, 175000, N'POL-HR-2026-001', @orgId, @now, 1);
    SET @assetId = SCOPE_IDENTITY();
    INSERT INTO [AssetAssignment] ([AssetId],[ToDepartmentId],[ToUserId],[AssignmentType],[AssignedDate],[HandedOverById],[RecipientAcknowledged],[OrganizationId],[CreatedAt],[IsActive])
    VALUES (@assetId, @deptHr, @userDeptHead, 1, DATEADD(MONTH, -7, @now), @userAssetMgr, 0, @orgId, @now, 1);

    INSERT INTO [InsurancePolicy] ([AssetId],[InsurerName],[PolicyNumber],[PolicyStartDate],[PolicyEndDate],[InsuredValue],[ClaimEligibility],[DeductibleAmount],[OrganizationId],[CreatedAt],[IsActive])
    VALUES (@assetId, N'Global Insurance Plc', N'GIP-HR-001', DATEADD(MONTH, -8, @now), DATEADD(MONTH, 16, @now), 175000, 1, 12000, @orgId, @now, 1);

    INSERT INTO [Asset]
        ([AssetName],[AssetTag],[CategoryId],[AssetTypeId],[Brand],[Model],[SerialNumber],[BarcodeOrQRCode],[Specifications],[Condition],[CurrentStatus],[Description],
         [PurchaseDate],[AcquisitionCost],[TaxAmount],[Currency],[SupplierId],[DepartmentId],[CurrentCustodianId],[ConditionOnReceipt],[UsefulLifeMonths],[SalvageValue],
         [DepreciationMethod],[DepreciationStartDate],[CurrentBookValue],[AccumulatedDepreciation],[IsLeased],[IsInsured],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'Epson EB-X49 Projector', N'HR-PRJ-001', @catOffice, @typeProjector, N'Epson', N'EB-X49', N'SN-EP-X49-010', N'BC-HR-PRJ-001', N'3600 lumens, HDMI', 2, 6, N'HR training room projector', DATEADD(MONTH, -15, @now), 78000, 12480, N'KES', @supTech, @deptHr, @userDeptHead, N'New', 60, 4000, 0, DATEADD(MONTH, -15, @now), 58000, 20000, 0, 0, @orgId, @now, 1);
    SET @assetId = SCOPE_IDENTITY();
    INSERT INTO [AssetAssignment] ([AssetId],[ToDepartmentId],[ToUserId],[AssignmentType],[AssignedDate],[RecipientAcknowledged],[OrganizationId],[CreatedAt],[IsActive])
    VALUES (@assetId, @deptHr, @userDeptHead, 3, DATEADD(MONTH, -14, @now), 0, @orgId, @now, 1);

    INSERT INTO [Asset]
        ([AssetName],[AssetTag],[CategoryId],[AssetTypeId],[Brand],[Model],[SerialNumber],[BarcodeOrQRCode],[Specifications],[Condition],[CurrentStatus],[Description],
         [PurchaseDate],[AcquisitionCost],[TaxAmount],[Currency],[SupplierId],[DepartmentId],[ConditionOnReceipt],[UsefulLifeMonths],[SalvageValue],
         [DepreciationMethod],[DepreciationStartDate],[CurrentBookValue],[AccumulatedDepreciation],[IsLeased],[IsInsured],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'Herman Miller Aeron Chair', N'HR-CHR-001', @catFurn, @typeChair, N'Herman Miller', N'Aeron', N'SN-HM-AER-011', N'BC-HR-CHR-001', N'Size B, graphite', 1, 5, N'Spare executive chair', DATEADD(MONTH, -3, @now), 95000, 15200, N'KES', @supOffice, @deptHr, N'New', 84, 5000, 0, DATEADD(MONTH, -3, @now), 93000, 2000, 0, 0, @orgId, @now, 1);

    INSERT INTO [Asset]
        ([AssetName],[AssetTag],[CategoryId],[AssetTypeId],[Brand],[Model],[SerialNumber],[BarcodeOrQRCode],[Specifications],[Condition],[CurrentStatus],[Description],
         [PurchaseDate],[AcquisitionCost],[TaxAmount],[Currency],[SupplierId],[DepartmentId],[CurrentCustodianId],[ConditionOnReceipt],[UsefulLifeMonths],[SalvageValue],
         [DepreciationMethod],[DepreciationStartDate],[CurrentBookValue],[AccumulatedDepreciation],[IsLeased],[IsInsured],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'Ubiquiti Dream Machine Pro', N'OPS-RTR-001', @catNet, @typeRouter, N'Ubiquiti', N'UDM-Pro', N'SN-UB-UDM-012', N'BC-OPS-RTR-001', N'Ops site gateway', 2, 6, N'Operations warehouse network gateway', DATEADD(MONTH, -11, @now), 98000, 15680, N'KES', @supTech, @deptOps, @userOpsStaff, N'New', 60, 6000, 0, DATEADD(MONTH, -11, @now), 82000, 16000, 0, 0, @orgId, @now, 1);
    SET @assetId = SCOPE_IDENTITY();
    INSERT INTO [AssetAssignment] ([AssetId],[ToDepartmentId],[ToUserId],[AssignmentType],[AssignedDate],[RecipientAcknowledged],[OrganizationId],[CreatedAt],[IsActive])
    VALUES (@assetId, @deptOps, @userOpsStaff, 3, DATEADD(MONTH, -10, @now), 0, @orgId, @now, 1);

    -- Microscope under active vendor maintenance
    INSERT INTO [Asset]
        ([AssetName],[AssetTag],[CategoryId],[AssetTypeId],[Brand],[Model],[SerialNumber],[BarcodeOrQRCode],[Specifications],[Condition],[CurrentStatus],[Description],
         [PurchaseDate],[AcquisitionCost],[TaxAmount],[Currency],[SupplierId],[DepartmentId],[CurrentCustodianId],[ConditionOnReceipt],[UsefulLifeMonths],[SalvageValue],
         [DepreciationMethod],[DepreciationStartDate],[CurrentBookValue],[AccumulatedDepreciation],[IsLeased],[IsInsured],[InsuredValue],[PolicyReference],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'Olympus CX23 Microscope', N'OPS-MED-001', @catMed, @typeMicroscope, N'Olympus', N'CX23', N'SN-OL-CX23-013', N'BC-OPS-MED-001', N'Binocular lab microscope', 4, 7, N'QC lab microscope - calibration in progress', DATEADD(MONTH, -30, @now), 210000, 33600, N'KES', @supMed, @deptOps, @userLabTech, N'Good', 96, 15000, 0, DATEADD(MONTH, -30, @now), 165000, 45000, 0, 1, 200000, N'POL-OPS-MED-001', @orgId, @now, 1);
    SET @assetId = SCOPE_IDENTITY();
    INSERT INTO [AssetAssignment] ([AssetId],[ToDepartmentId],[ToUserId],[AssignmentType],[AssignedDate],[RecipientAcknowledged],[OrganizationId],[CreatedAt],[IsActive])
    VALUES (@assetId, @deptOps, @userLabTech, 1, DATEADD(MONTH, -28, @now), 0, @orgId, @now, 1);

    INSERT INTO [AssetCustodyEvent] ([AssetId],[ActionType],[ActionDate],[ToUserId],[ToDepartmentId],[ConditionBefore],[ConditionAfter],[Reason],[OrganizationId],[CreatedAt],[IsActive])
    VALUES (@assetId, 1, DATEADD(MONTH, -28, @now), @userLabTech, @deptOps, N'Good', N'Good', N'Lab technician custody', @orgId, @now, 1);
    INSERT INTO [InsurancePolicy] ([AssetId],[InsurerName],[PolicyNumber],[PolicyStartDate],[PolicyEndDate],[InsuredValue],[ClaimEligibility],[DeductibleAmount],[OrganizationId],[CreatedAt],[IsActive])
    VALUES (@assetId, N'Global Insurance Plc', N'GIP-OPS-MED-001', DATEADD(MONTH, -12, @now), DATEADD(MONTH, 12, @now), 200000, 1, 20000, @orgId, @now, 1);
    INSERT INTO [AssetMaintenanceRecord]
        ([MaintenanceTicketNumber],[AssetId],[ReportedIssue],[MaintenanceType],[ReportedById],[AssignedTechnicianOrVendor],[ServiceDate],[Cost],[Status],[Outcome],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'MNT-2026-002', @assetId, N'Objective lens alignment drift detected during QC audit', 3, @userLabTech, N'MedEquip Africa Service Desk', DATEADD(DAY, -5, @now), 0, 2, N'Awaiting vendor onsite calibration', @orgId, @now, 1);

    INSERT INTO [Asset]
        ([AssetName],[AssetTag],[CategoryId],[AssetTypeId],[Brand],[Model],[SerialNumber],[BarcodeOrQRCode],[Specifications],[Condition],[CurrentStatus],[Description],
         [PurchaseDate],[AcquisitionCost],[TaxAmount],[Currency],[SupplierId],[DepartmentId],[ConditionOnReceipt],[UsefulLifeMonths],[SalvageValue],
         [DepreciationMethod],[DepreciationStartDate],[CurrentBookValue],[AccumulatedDepreciation],[IsLeased],[IsInsured],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'Eppendorf Centrifuge 5424', N'OPS-MED-002', @catMed, @typeCentrifuge, N'Eppendorf', N'5424 R', N'SN-EP-5424-014', N'BC-OPS-MED-002', N'Benchtop centrifuge', 1, 5, N'Spare lab centrifuge in store', DATEADD(MONTH, -5, @now), 320000, 51200, N'KES', @supMed, @deptOps, N'New', 96, 20000, 0, DATEADD(MONTH, -5, @now), 310000, 10000, 0, 0, @orgId, @now, 1);

    INSERT INTO [Asset]
        ([AssetName],[AssetTag],[CategoryId],[AssetTypeId],[Brand],[Model],[SerialNumber],[BarcodeOrQRCode],[Specifications],[Condition],[CurrentStatus],[Description],
         [PurchaseDate],[AcquisitionCost],[TaxAmount],[Currency],[SupplierId],[DepartmentId],[CurrentCustodianId],[ConditionOnReceipt],[UsefulLifeMonths],[SalvageValue],
         [DepreciationMethod],[DepreciationStartDate],[CurrentBookValue],[AccumulatedDepreciation],[IsLeased],[IsInsured],[InsuredValue],[PolicyReference],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'Toyota Hilux Double Cab', N'OPS-VHC-001', @catVeh, @typeVehicle, N'Toyota', N'Hilux DC', N'KDA-456X', N'BC-OPS-VHC-001', N'2.4L diesel, fleet unit', 2, 6, N'Operations field service vehicle', DATEADD(MONTH, -36, @now), 3200000, 512000, N'KES', @supOffice, @deptOps, @userOpsStaff, N'Good', 84, 400000, 0, DATEADD(MONTH, -36, @now), 2450000, 750000, 0, 1, 2800000, N'POL-FLEET-2026-001', @orgId, @now, 1);
    SET @assetId = SCOPE_IDENTITY();
    INSERT INTO [AssetAssignment] ([AssetId],[ToDepartmentId],[ToUserId],[AssignmentType],[AssignedDate],[ExpectedReturnDate],[RecipientAcknowledged],[OrganizationId],[CreatedAt],[IsActive])
    VALUES (@assetId, @deptOps, @userOpsStaff, 2, DATEADD(MONTH, -30, @now), DATEADD(MONTH, 6, @now), 0, @orgId, @now, 1);

    INSERT INTO [InsurancePolicy] ([AssetId],[InsurerName],[PolicyNumber],[PolicyStartDate],[PolicyEndDate],[InsuredValue],[ClaimEligibility],[DeductibleAmount],[OrganizationId],[CreatedAt],[IsActive])
    VALUES (@assetId, N'Global Insurance Plc', N'GIP-FLEET-001', DATEADD(MONTH, -6, @now), DATEADD(MONTH, 6, @now), 2800000, 1, 50000, @orgId, @now, 1);

    INSERT INTO [Asset]
        ([AssetName],[AssetTag],[CategoryId],[AssetTypeId],[Brand],[Model],[SerialNumber],[BarcodeOrQRCode],[Specifications],[Condition],[CurrentStatus],[Description],
         [PurchaseDate],[AcquisitionCost],[TaxAmount],[Currency],[SupplierId],[DepartmentId],[CurrentCustodianId],[ConditionOnReceipt],[UsefulLifeMonths],[SalvageValue],
         [DepreciationMethod],[DepreciationStartDate],[CurrentBookValue],[AccumulatedDepreciation],[IsLeased],[IsInsured],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'Ergonomic Staff Chair', N'ADMIN-CHR-001', @catFurn, @typeChair, N'Featherlite', N'Optima', N'SN-FL-OPT-015', N'BC-ADMIN-CHR-001', N'Standard task chair', 2, 6, N'Admin assistant workstation chair', DATEADD(MONTH, -16, @now), 28000, 4480, N'KES', @supOffice, @deptAdmin, @userStaff, N'New', 84, 2000, 0, DATEADD(MONTH, -16, @now), 22000, 6000, 0, 0, @orgId, @now, 1);
    SET @assetId = SCOPE_IDENTITY();
    INSERT INTO [AssetAssignment] ([AssetId],[ToDepartmentId],[ToUserId],[AssignmentType],[AssignedDate],[RecipientAcknowledged],[OrganizationId],[CreatedAt],[IsActive])
    VALUES (@assetId, @deptAdmin, @userStaff, 1, DATEADD(MONTH, -15, @now), 0, @orgId, @now, 1);

    INSERT INTO [Asset]
        ([AssetName],[AssetTag],[CategoryId],[AssetTypeId],[Brand],[Model],[SerialNumber],[BarcodeOrQRCode],[Specifications],[Condition],[CurrentStatus],[Description],
         [PurchaseDate],[AcquisitionCost],[TaxAmount],[Currency],[SupplierId],[DepartmentId],[ConditionOnReceipt],[UsefulLifeMonths],[SalvageValue],
         [DepreciationMethod],[DepreciationStartDate],[CurrentBookValue],[AccumulatedDepreciation],[IsLeased],[IsInsured],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'Executive Work Desk', N'ADMIN-DESK-001', @catFurn, @typeDesk, N'Featherlite', N'Executive 1600', N'SN-FL-DESK-016', N'BC-ADMIN-DESK-001', N'1600mm workstation desk', 1, 5, N'Spare admin desk in store', DATEADD(MONTH, -2, @now), 65000, 10400, N'KES', @supOffice, @deptAdmin, N'New', 120, 5000, 0, DATEADD(MONTH, -2, @now), 64000, 1000, 0, 0, @orgId, @now, 1);

    -- Damaged laptop: incident -> completed maintenance -> insurance claim under review
    INSERT INTO [Asset]
        ([AssetName],[AssetTag],[CategoryId],[AssetTypeId],[Brand],[Model],[SerialNumber],[BarcodeOrQRCode],[Specifications],[Condition],[CurrentStatus],[Description],
         [PurchaseDate],[AcquisitionCost],[TaxAmount],[Currency],[SupplierId],[DepartmentId],[CurrentCustodianId],[ConditionOnReceipt],[UsefulLifeMonths],[SalvageValue],
         [DepreciationMethod],[DepreciationStartDate],[CurrentBookValue],[AccumulatedDepreciation],[IsLeased],[IsInsured],[InsuredValue],[PolicyReference],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'Lenovo ThinkPad T14 Gen 3', N'IT-LTP-003', @catIt, @typeLaptop, N'Lenovo', N'ThinkPad T14', N'SN-LN-T14-017', N'BC-IT-LTP-003', N'Core i7, 32GB RAM, 1TB SSD', 4, 8, N'Damaged - display assembly failure after drop', DATEADD(MONTH, -12, @now), 178000, 28480, N'KES', @supTech, @deptIt, @userStaff, N'Good', 48, 12000, 0, DATEADD(MONTH, -12, @now), 150000, 28000, 0, 1, 170000, N'POL-IT-2026-003', @orgId, @now, 1);
    SET @assetId = SCOPE_IDENTITY();
    INSERT INTO [AssetAssignment] ([AssetId],[ToDepartmentId],[ToUserId],[AssignmentType],[AssignedDate],[RecipientAcknowledged],[OrganizationId],[CreatedAt],[IsActive])
    VALUES (@assetId, @deptAdmin, @userStaff, 2, DATEADD(MONTH, -11, @now), 0, @orgId, @now, 1);

    INSERT INTO [InsurancePolicy] ([AssetId],[InsurerName],[PolicyNumber],[PolicyStartDate],[PolicyEndDate],[InsuredValue],[ClaimEligibility],[DeductibleAmount],[OrganizationId],[CreatedAt],[IsActive])
    VALUES (@assetId, N'Global Insurance Plc', N'GIP-IT-003', DATEADD(MONTH, -12, @now), DATEADD(MONTH, 12, @now), 170000, 1, 15000, @orgId, @now, 1);

    DECLARE @damagedIncidentId INT;
    INSERT INTO [AssetIncident]
        ([IncidentNumber],[AssetId],[ReportedById],[IncidentType],[IncidentDate],[Description],[Severity],[ResolutionStatus],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'INC-2026-003', @assetId, @userStaff, 1, DATEADD(DAY, -18, @now), N'Display cracked after laptop fell from desk during office move', 3, N'Under insurer review', @orgId, @now, 1);
    SET @damagedIncidentId = SCOPE_IDENTITY();

    INSERT INTO [AssetMaintenanceRecord]
        ([MaintenanceTicketNumber],[AssetId],[ReportedIssue],[MaintenanceType],[ReportedById],[AssignedTechnicianOrVendor],[ServiceDate],[CompletionDate],[Cost],[Status],[Outcome],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'MNT-2026-003', @assetId, N'Cracked LCD panel and bent hinge', 2, @userStaff, N'Tech Source Ltd Repair Centre', DATEADD(DAY, -16, @now), DATEADD(DAY, -8, @now), 48500, 3, N'Display assembly replaced; device functional pending claim settlement', @orgId, @now, 1);

    INSERT INTO [InsuranceClaim]
        ([ClaimNumber],[AssetId],[IncidentId],[ClaimDate],[ClaimType],[Insurer],[Assessor],[DocumentsSubmitted],[ClaimStatus],[ApprovedAmount],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'CLM-2026-003', @assetId, @damagedIncidentId, DATEADD(DAY, -7, @now), N'Accidental damage', N'Global Insurance Plc', N'Jane Muriuki', N'Incident report, repair invoice, photos', 3, 0, @orgId, @now, 1);

    INSERT INTO [Asset]
        ([AssetName],[AssetTag],[CategoryId],[AssetTypeId],[Brand],[Model],[SerialNumber],[BarcodeOrQRCode],[Specifications],[Condition],[CurrentStatus],[Description],
         [PurchaseDate],[AcquisitionCost],[TaxAmount],[Currency],[SupplierId],[DepartmentId],[ConditionOnReceipt],[UsefulLifeMonths],[SalvageValue],
         [DepreciationMethod],[DepreciationStartDate],[CurrentBookValue],[AccumulatedDepreciation],[IsLeased],[IsInsured],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'Epson WorkForce Pro WF-4830', N'OPS-PRT-001', @catOffice, @typePrinter, N'Epson', N'WF-4830', N'SN-EP-WF4-018', N'BC-OPS-PRT-001', N'Colour MFP for ops office', 1, 5, N'Spare operations printer', DATEADD(MONTH, -7, @now), 72000, 11520, N'KES', @supTech, @deptOps, N'New', 60, 4000, 0, DATEADD(MONTH, -7, @now), 69000, 3000, 0, 0, @orgId, @now, 1);

    INSERT INTO [Asset]
        ([AssetName],[AssetTag],[CategoryId],[AssetTypeId],[Brand],[Model],[SerialNumber],[BarcodeOrQRCode],[Specifications],[Condition],[CurrentStatus],[Description],
         [PurchaseDate],[AcquisitionCost],[TaxAmount],[Currency],[SupplierId],[DepartmentId],[ConditionOnReceipt],[UsefulLifeMonths],[SalvageValue],
         [DepreciationMethod],[DepreciationStartDate],[CurrentBookValue],[AccumulatedDepreciation],[IsLeased],[IsInsured],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'Canon imageCLASS MF445dw', N'FIN-PRT-001', @catOffice, @typePrinter, N'Canon', N'MF445dw', N'SN-CN-MF4-019', N'BC-FIN-PRT-001', N'Mono MFP for finance', 1, 5, N'Spare finance printer', DATEADD(MONTH, -9, @now), 68000, 10880, N'KES', @supTech, @deptFin, N'New', 60, 3500, 0, DATEADD(MONTH, -9, @now), 64000, 4000, 0, 0, @orgId, @now, 1);

    -- Asset request: pending chair for HR (follows department + category)
    IF OBJECT_ID(N'[AssetRequest]', N'U') IS NOT NULL
    BEGIN
        INSERT INTO [AssetRequest]
            ([RequestedById],[DepartmentId],[CategoryId],[RequestedAssetTag],[Justification],[Status],[OrganizationId],[CreatedAt],[IsActive])
        VALUES
        (@userDeptHead, @deptHr, @catFurn, N'HR-CHR-002', N'Additional interview room seating required for recruitment drive', 0, @orgId, @now, 1);
    END

    -- Purchase request: pending approval for fleet GPS units (procurement channel)
    IF OBJECT_ID(N'[PurchaseRequest]', N'U') IS NOT NULL
    BEGIN
        INSERT INTO [PurchaseRequest]
            ([RequestNumber],[RequestedById],[ApprovalStatus],[CurrentApprovalStage],[DepartmentId],[Justification],[EstimatedUnitCost],[Quantity],[Currency],[Notes],[OrganizationId],[CreatedAt],[IsActive])
        VALUES
        (N'PR-2026-004', @userOpsStaff, 1, 1, @deptOps, N'GPS trackers for fleet vehicles to improve dispatch visibility', 18500, 3, N'KES', N'Awaiting procurement approval', @orgId, @now, 1);
    END

    INSERT INTO [AuditLog]
        ([ActorUserId],[Action],[EntityType],[EntityId],[NewValues],[Timestamp],[IPAddress],[OrganizationId],[CreatedAt],[IsActive])
    VALUES
    (N'seed-user-001', N'Seed.DiverseDemoLoad', N'System', N'Seed', N'Diverse multi-department demo data loaded', @now, N'127.0.0.1', @orgId, @now, 1);
END
GO
