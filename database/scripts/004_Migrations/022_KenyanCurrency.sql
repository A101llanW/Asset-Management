-- Normalize stored currency codes to Kenyan Shilling (KES).
UPDATE [SystemSetting]
SET [SettingValue] = N'KES'
WHERE [SettingKey] = N'Finance.DefaultCurrency'
  AND ([SettingValue] IS NULL OR LTRIM(RTRIM([SettingValue])) = N'' OR [SettingValue] <> N'KES');
GO

UPDATE [Organization]
SET [CurrencyCode] = N'KES'
WHERE [CurrencyCode] IS NULL OR LTRIM(RTRIM([CurrencyCode])) = N'' OR [CurrencyCode] = N'USD';
GO

IF COL_LENGTH(N'[Asset]', N'Currency') IS NOT NULL
BEGIN
    UPDATE [Asset]
    SET [Currency] = N'KES'
    WHERE [Currency] IS NULL OR LTRIM(RTRIM([Currency])) = N'' OR [Currency] = N'USD';
END
GO

IF COL_LENGTH(N'[PurchaseRequest]', N'Currency') IS NOT NULL
BEGIN
    UPDATE [PurchaseRequest]
    SET [Currency] = N'KES'
    WHERE [Currency] IS NULL OR LTRIM(RTRIM([Currency])) = N'' OR [Currency] = N'USD';
END
GO

IF COL_LENGTH(N'[PurchaseRecord]', N'Currency') IS NOT NULL
BEGIN
    UPDATE [PurchaseRecord]
    SET [Currency] = N'KES'
    WHERE [Currency] IS NULL OR LTRIM(RTRIM([Currency])) = N'' OR [Currency] = N'USD';
END
GO
