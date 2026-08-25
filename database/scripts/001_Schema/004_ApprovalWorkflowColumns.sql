-- Legacy databases created before multi-stage approval may lack workflow columns.
-- 002_Domain.sql only creates tables when missing; it does not alter existing schemas.

IF OBJECT_ID(N'[AssetTransfer]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'[AssetTransfer]', N'RequestedById') IS NULL
    BEGIN
        ALTER TABLE [AssetTransfer] ADD [RequestedById] NVARCHAR(128) NULL;
    END

    IF COL_LENGTH(N'[AssetTransfer]', N'CurrentApprovalStage') IS NULL
    BEGIN
        ALTER TABLE [AssetTransfer] ADD [CurrentApprovalStage] INT NOT NULL
            CONSTRAINT DF_AssetTransfer_CurrentApprovalStage DEFAULT(0);
    END

    IF COL_LENGTH(N'[AssetTransfer]', N'ApprovalStageRoleIds') IS NULL
    BEGIN
        ALTER TABLE [AssetTransfer] ADD [ApprovalStageRoleIds] NVARCHAR(200) NULL;
    END

    UPDATE [AssetTransfer]
    SET [RequestedById] = [ApprovedById]
    WHERE [RequestedById] IS NULL AND [ApprovedById] IS NOT NULL;
END
GO

IF OBJECT_ID(N'[DisposalRecord]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'[DisposalRecord]', N'RequestedById') IS NULL
    BEGIN
        ALTER TABLE [DisposalRecord] ADD [RequestedById] NVARCHAR(128) NULL;
    END

    IF COL_LENGTH(N'[DisposalRecord]', N'CurrentApprovalStage') IS NULL
    BEGIN
        ALTER TABLE [DisposalRecord] ADD [CurrentApprovalStage] INT NOT NULL
            CONSTRAINT DF_DisposalRecord_CurrentApprovalStage DEFAULT(0);
    END

    IF COL_LENGTH(N'[DisposalRecord]', N'ApprovalStageRoleIds') IS NULL
    BEGIN
        ALTER TABLE [DisposalRecord] ADD [ApprovalStageRoleIds] NVARCHAR(200) NULL;
    END

    UPDATE [DisposalRecord]
    SET [RequestedById] = [DisposalApprovedById]
    WHERE [RequestedById] IS NULL AND [DisposalApprovedById] IS NOT NULL;
END
GO

IF OBJECT_ID(N'[PurchaseRequest]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'[PurchaseRequest]', N'RequestedById') IS NULL
    BEGIN
        ALTER TABLE [PurchaseRequest] ADD [RequestedById] NVARCHAR(128) NULL;
    END

    IF COL_LENGTH(N'[PurchaseRequest]', N'CurrentApprovalStage') IS NULL
    BEGIN
        ALTER TABLE [PurchaseRequest] ADD [CurrentApprovalStage] INT NOT NULL
            CONSTRAINT DF_PurchaseRequest_CurrentApprovalStage DEFAULT(0);
    END

    IF COL_LENGTH(N'[PurchaseRequest]', N'ApprovalStageRoleIds') IS NULL
    BEGIN
        ALTER TABLE [PurchaseRequest] ADD [ApprovalStageRoleIds] NVARCHAR(200) NULL;
    END
END
GO
