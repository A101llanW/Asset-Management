-- Room kind + per-department requisition flow (inherit parent/org or custom stages).

IF COL_LENGTH(N'[Department]', N'RequisitionFlowMode') IS NULL
BEGIN
    ALTER TABLE [Department]
        ADD [RequisitionFlowMode] INT NOT NULL
        CONSTRAINT [DF_Department_RequisitionFlowMode] DEFAULT (0);
END
GO

IF COL_LENGTH(N'[Department]', N'CustomStageRoleIds') IS NULL
BEGIN
    ALTER TABLE [Department] ADD [CustomStageRoleIds] NVARCHAR(200) NULL;
END
GO

IF COL_LENGTH(N'[Department]', N'CustomStageUserIds') IS NULL
BEGIN
    ALTER TABLE [Department] ADD [CustomStageUserIds] NVARCHAR(500) NULL;
END
GO
