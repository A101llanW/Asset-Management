IF COL_LENGTH(N'[Asset]', N'TransferApprovalStageUserIds') IS NULL
BEGIN
    ALTER TABLE [Asset] ADD
        [TransferApprovalStageUserIds] NVARCHAR(500) NULL,
        [DisposalApprovalStageUserIds] NVARCHAR(500) NULL;
END
GO

IF COL_LENGTH(N'[AssetTransfer]', N'ApprovalStageUserIds') IS NULL
BEGIN
    ALTER TABLE [AssetTransfer] ADD [ApprovalStageUserIds] NVARCHAR(500) NULL;
END
GO

IF COL_LENGTH(N'[DisposalRecord]', N'ApprovalStageUserIds') IS NULL
BEGIN
    ALTER TABLE [DisposalRecord] ADD [ApprovalStageUserIds] NVARCHAR(500) NULL;
END
GO
