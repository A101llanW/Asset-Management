-- Phase 3: optimistic concurrency tokens and approval-stage uniqueness

IF COL_LENGTH(N'[DisposalRecord]', N'RowVersion') IS NULL
BEGIN
    ALTER TABLE [DisposalRecord] ADD [RowVersion] ROWVERSION NOT NULL;
END
GO

IF COL_LENGTH(N'[PurchaseRequest]', N'RowVersion') IS NULL
BEGIN
    ALTER TABLE [PurchaseRequest] ADD [RowVersion] ROWVERSION NOT NULL;
END
GO

IF COL_LENGTH(N'[AssetRequest]', N'RowVersion') IS NULL
BEGIN
    ALTER TABLE [AssetRequest] ADD [RowVersion] ROWVERSION NOT NULL;
END
GO

IF COL_LENGTH(N'[AssetTransfer]', N'OriginalAssetStatus') IS NULL
BEGIN
    ALTER TABLE [AssetTransfer] ADD [OriginalAssetStatus] INT NOT NULL
        CONSTRAINT DF_AssetTransfer_OriginalAssetStatus DEFAULT(2);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_PurchaseApprovalAction_Request_Stage' AND object_id = OBJECT_ID(N'[PurchaseApprovalAction]'))
BEGIN
    CREATE UNIQUE INDEX UX_PurchaseApprovalAction_Request_Stage
        ON [PurchaseApprovalAction] ([PurchaseRequestId], [StageNumber]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_TransferApprovalAction_Request_Stage' AND object_id = OBJECT_ID(N'[TransferApprovalAction]'))
BEGIN
    CREATE UNIQUE INDEX UX_TransferApprovalAction_Request_Stage
        ON [TransferApprovalAction] ([AssetTransferId], [StageNumber]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_DisposalApprovalAction_Request_Stage' AND object_id = OBJECT_ID(N'[DisposalApprovalAction]'))
BEGIN
    CREATE UNIQUE INDEX UX_DisposalApprovalAction_Request_Stage
        ON [DisposalApprovalAction] ([DisposalRecordId], [StageNumber]);
END
GO
