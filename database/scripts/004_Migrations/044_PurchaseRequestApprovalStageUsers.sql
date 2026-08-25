IF COL_LENGTH(N'[PurchaseRequest]', N'ApprovalStageUserIds') IS NULL
BEGIN
    ALTER TABLE [PurchaseRequest] ADD [ApprovalStageUserIds] NVARCHAR(500) NULL;
END
GO
