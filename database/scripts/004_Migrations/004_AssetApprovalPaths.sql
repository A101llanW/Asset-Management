IF COL_LENGTH(N'[Asset]', N'RequireTransferApproval') IS NULL
BEGIN
    ALTER TABLE [Asset] ADD
        [RequireTransferApproval] BIT NOT NULL CONSTRAINT DF_Asset_RequireTransferApproval DEFAULT(0),
        [TransferApprovalStageRoleIds] NVARCHAR(200) NULL,
        [RequireDisposalApproval] BIT NOT NULL CONSTRAINT DF_Asset_RequireDisposalApproval DEFAULT(0),
        [DisposalApprovalStageRoleIds] NVARCHAR(200) NULL;
END
GO
