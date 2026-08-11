-- Repair AssetSubType names corrupted by UTF-8 en-dash bytes misread as Windows-1252.
IF OBJECT_ID(N'[AssetSubType]', N'U') IS NOT NULL
BEGIN
    UPDATE [AssetSubType]
    SET [Name] = LTRIM(RTRIM(
        REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
            [Name],
            NCHAR(0x00E2) + NCHAR(0x20AC) + NCHAR(0x2019), N' - '),
            NCHAR(0x00E2) + NCHAR(0x20AC) + NCHAR(0x201C), N' - '),
            NCHAR(0x00E2) + NCHAR(0x20AC) + NCHAR(0x201D), N' - '),
            N' – ', N' - '),
            N' — ', N' - '),
            N'  ', N' ')))
    WHERE [Name] LIKE N'%' + NCHAR(0x00E2) + NCHAR(0x20AC) + N'%'
       OR [Name] LIKE N'% – %'
       OR [Name] LIKE N'% — %';
END
GO
