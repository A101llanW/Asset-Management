IF COL_LENGTH(N'[PurchaseRequest]', N'TargetAssetId') IS NULL
    ALTER TABLE [PurchaseRequest] ADD [TargetAssetId] INT NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE [name] = N'FK_PurchaseRequest_TargetAsset'
      AND [parent_object_id] = OBJECT_ID(N'[PurchaseRequest]'))
BEGIN
    ALTER TABLE [PurchaseRequest]
        ADD CONSTRAINT FK_PurchaseRequest_TargetAsset
        FOREIGN KEY ([TargetAssetId]) REFERENCES [Asset]([Id]);
END
GO
