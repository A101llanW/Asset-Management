IF COL_LENGTH(N'[PurchaseRequest]', N'ItemDescription') IS NULL
    ALTER TABLE [PurchaseRequest] ADD [ItemDescription] NVARCHAR(2000) NULL;
GO

IF COL_LENGTH(N'[PurchaseRequest]', N'QuantityInStock') IS NULL
    ALTER TABLE [PurchaseRequest] ADD [QuantityInStock] INT NULL;
GO

IF COL_LENGTH(N'[PurchaseRequest]', N'RequiredDate') IS NULL
    ALTER TABLE [PurchaseRequest] ADD [RequiredDate] DATETIME NULL;
GO

IF COL_LENGTH(N'[PurchaseRequest]', N'OrderByUserId') IS NULL
    ALTER TABLE [PurchaseRequest] ADD [OrderByUserId] NVARCHAR(128) NULL;
GO

IF COL_LENGTH(N'[PurchaseRequest]', N'AttachmentFileName') IS NULL
    ALTER TABLE [PurchaseRequest] ADD [AttachmentFileName] NVARCHAR(260) NULL;
GO

IF COL_LENGTH(N'[PurchaseRequest]', N'AttachmentFilePath') IS NULL
    ALTER TABLE [PurchaseRequest] ADD [AttachmentFilePath] NVARCHAR(500) NULL;
GO

IF COL_LENGTH(N'[PurchaseRequest]', N'AttachmentContentType') IS NULL
    ALTER TABLE [PurchaseRequest] ADD [AttachmentContentType] NVARCHAR(100) NULL;
GO

IF COL_LENGTH(N'[PurchaseRequest]', N'AttachmentFileSizeBytes') IS NULL
    ALTER TABLE [PurchaseRequest] ADD [AttachmentFileSizeBytes] BIGINT NULL;
GO

-- Backfill item description from justification for existing rows
UPDATE [PurchaseRequest]
SET [ItemDescription] = LEFT([Justification], 2000)
WHERE [ItemDescription] IS NULL AND [Justification] IS NOT NULL;
GO
