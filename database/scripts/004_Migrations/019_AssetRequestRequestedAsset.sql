-- Link asset requests to a specific in-store asset selected by name at submission time.
IF COL_LENGTH(N'[AssetRequest]', N'RequestedAssetId') IS NULL
BEGIN
    ALTER TABLE [AssetRequest] ADD [RequestedAssetId] INT NULL;
    ALTER TABLE [AssetRequest] ADD CONSTRAINT FK_AssetRequest_RequestedAsset
        FOREIGN KEY ([RequestedAssetId]) REFERENCES [Asset]([Id]);
END
GO
