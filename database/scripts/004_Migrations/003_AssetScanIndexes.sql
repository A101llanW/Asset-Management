-- Ensure scan columns are indexable, then create lookup indexes (safe for legacy databases).
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
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Asset_BarcodeOrQRCode_NotNull' AND object_id = OBJECT_ID('Asset'))
BEGIN
    CREATE UNIQUE INDEX IX_Asset_BarcodeOrQRCode_NotNull ON [Asset]([BarcodeOrQRCode]) WHERE [BarcodeOrQRCode] IS NOT NULL;
END
GO
