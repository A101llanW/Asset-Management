-- Per-room (department) requisition approval stage overrides.

IF COL_LENGTH(N'[Department]', N'UseCustomRequisitionApproval') IS NULL
BEGIN
    ALTER TABLE [Department]
        ADD [UseCustomRequisitionApproval] BIT NOT NULL
        CONSTRAINT [DF_Department_UseCustomRequisitionApproval] DEFAULT (0);
END
GO

IF COL_LENGTH(N'[Department]', N'RequisitionApprovalStageRoleIds') IS NULL
BEGIN
    ALTER TABLE [Department]
        ADD [RequisitionApprovalStageRoleIds] NVARCHAR(500) NULL;
END
GO

IF COL_LENGTH(N'[Department]', N'RequisitionApprovalStageUserIds') IS NULL
BEGIN
    ALTER TABLE [Department]
        ADD [RequisitionApprovalStageUserIds] NVARCHAR(1000) NULL;
END
GO
