-- Normalize asset scan columns so filtered unique indexes can be created (legacy DBs may use NVARCHAR(MAX)).
IF OBJECT_ID(N'[Asset]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('Asset', 'BarcodeOrQRCode') IS NULL
    BEGIN
        ALTER TABLE [Asset] ADD [BarcodeOrQRCode] NVARCHAR(120) NULL;
    END
    ELSE IF EXISTS (
        SELECT 1
        FROM sys.columns c
        INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID(N'Asset')
          AND c.name = N'BarcodeOrQRCode'
          AND t.name IN (N'nvarchar', N'varchar')
          AND c.max_length = -1
    )
    BEGIN
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Asset_BarcodeOrQRCode_NotNull' AND object_id = OBJECT_ID(N'Asset'))
        BEGIN
            DROP INDEX [IX_Asset_BarcodeOrQRCode_NotNull] ON [Asset];
        END

        ALTER TABLE [Asset] ALTER COLUMN [BarcodeOrQRCode] NVARCHAR(120) NULL;
    END

    IF COL_LENGTH('Asset', 'SerialNumber') IS NOT NULL
       AND EXISTS (
        SELECT 1
        FROM sys.columns c
        INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID(N'Asset')
          AND c.name = N'SerialNumber'
          AND t.name IN (N'nvarchar', N'varchar')
          AND c.max_length = -1
    )
    BEGIN
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Asset_SerialNumber_NotNull' AND object_id = OBJECT_ID(N'Asset'))
        BEGIN
            DROP INDEX [IX_Asset_SerialNumber_NotNull] ON [Asset];
        END

        IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SerialNumber' AND object_id = OBJECT_ID(N'Asset'))
        BEGIN
            DROP INDEX [IX_SerialNumber] ON [Asset];
        END

        ALTER TABLE [Asset] ALTER COLUMN [SerialNumber] NVARCHAR(120) NULL;
    END

    IF COL_LENGTH('Asset', 'AssetName') IS NOT NULL
       AND EXISTS (
        SELECT 1
        FROM sys.columns c
        INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID(N'Asset')
          AND c.name = N'AssetName'
          AND t.name IN (N'nvarchar', N'varchar')
          AND c.max_length = -1
    )
    BEGIN
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Asset_Org_AssetName' AND object_id = OBJECT_ID(N'Asset'))
        BEGIN
            DROP INDEX [IX_Asset_Org_AssetName] ON [Asset];
        END

        IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Asset_Org_IsActive_Dept_List' AND object_id = OBJECT_ID(N'Asset'))
        BEGIN
            DROP INDEX [IX_Asset_Org_IsActive_Dept_List] ON [Asset];
        END

        ALTER TABLE [Asset] ALTER COLUMN [AssetName] NVARCHAR(200) NOT NULL;
    END
END
GO

IF OBJECT_ID(N'[Notification]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('Notification', 'UserId') IS NOT NULL
       AND EXISTS (
        SELECT 1
        FROM sys.columns c
        INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID(N'Notification')
          AND c.name = N'UserId'
          AND t.name IN (N'nvarchar', N'varchar')
          AND c.max_length = -1
    )
    BEGIN
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notification_User_Status_CreatedAt' AND object_id = OBJECT_ID(N'Notification'))
        BEGIN
            DROP INDEX [IX_Notification_User_Status_CreatedAt] ON [Notification];
        END

        DELETE FROM [Notification] WHERE [UserId] IS NULL;

        ALTER TABLE [Notification] ALTER COLUMN [UserId] NVARCHAR(128) NOT NULL;
    END
END
GO

IF OBJECT_ID(N'[Roles]', N'U') IS NOT NULL
   AND COL_LENGTH('Roles', 'Name') IS NOT NULL
   AND EXISTS (
    SELECT 1
    FROM sys.columns c
    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID(N'Roles')
      AND c.name = N'Name'
      AND t.name IN (N'nvarchar', N'varchar')
      AND c.max_length = -1
)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Roles_Org_Name' AND object_id = OBJECT_ID(N'Roles'))
    BEGIN
        DROP INDEX [IX_Roles_Org_Name] ON [Roles];
    END

    ALTER TABLE [Roles] ALTER COLUMN [Name] NVARCHAR(200) NOT NULL;
END
GO

IF OBJECT_ID(N'[Department]', N'U') IS NOT NULL
   AND COL_LENGTH('Department', 'Code') IS NOT NULL
   AND EXISTS (
    SELECT 1
    FROM sys.columns c
    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID(N'Department')
      AND c.name = N'Code'
      AND t.name IN (N'nvarchar', N'varchar')
      AND c.max_length = -1
)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Department_Org_Code' AND object_id = OBJECT_ID(N'Department'))
    BEGIN
        DROP INDEX [IX_Department_Org_Code] ON [Department];
    END

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Department_Org_Code_Unique' AND object_id = OBJECT_ID(N'Department'))
    BEGIN
        DROP INDEX [IX_Department_Org_Code_Unique] ON [Department];
    END

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Department_Code' AND object_id = OBJECT_ID(N'Department'))
    BEGIN
        DROP INDEX [IX_Department_Code] ON [Department];
    END

    ALTER TABLE [Department] ALTER COLUMN [Code] NVARCHAR(40) NOT NULL;
END
GO

IF OBJECT_ID(N'[Asset]', N'U') IS NOT NULL
   AND COL_LENGTH('Asset', 'AssetTag') IS NOT NULL
   AND EXISTS (
    SELECT 1
    FROM sys.columns c
    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID(N'Asset')
      AND c.name = N'AssetTag'
      AND t.name IN (N'nvarchar', N'varchar')
      AND c.max_length = -1
)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Asset_Org_AssetTag' AND object_id = OBJECT_ID(N'Asset'))
    BEGIN
        DROP INDEX [IX_Asset_Org_AssetTag] ON [Asset];
    END

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetTag' AND object_id = OBJECT_ID(N'Asset'))
    BEGIN
        DROP INDEX [IX_AssetTag] ON [Asset];
    END

    ALTER TABLE [Asset] ALTER COLUMN [AssetTag] NVARCHAR(60) NOT NULL;
END
GO
