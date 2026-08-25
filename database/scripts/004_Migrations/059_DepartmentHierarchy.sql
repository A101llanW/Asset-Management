-- Department hierarchy: grade parents, class leaves, and requisition targets.

IF COL_LENGTH(N'[Department]', N'ParentDepartmentId') IS NULL
BEGIN
    ALTER TABLE [Department] ADD [ParentDepartmentId] INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Department_ParentDepartment'
)
BEGIN
    ALTER TABLE [Department]
        ADD CONSTRAINT [FK_Department_ParentDepartment]
        FOREIGN KEY ([ParentDepartmentId]) REFERENCES [Department]([Id]);
END
GO

IF COL_LENGTH(N'[Department]', N'DepartmentKind') IS NULL
BEGIN
    ALTER TABLE [Department]
        ADD [DepartmentKind] INT NOT NULL
        CONSTRAINT [DF_Department_DepartmentKind] DEFAULT (0);
END
GO

IF COL_LENGTH(N'[Department]', N'IsRequisitionTarget') IS NULL
BEGIN
    ALTER TABLE [Department]
        ADD [IsRequisitionTarget] BIT NOT NULL
        CONSTRAINT [DF_Department_IsRequisitionTarget] DEFAULT (1);
END
GO

UPDATE [Department]
SET [DepartmentKind] = 0,
    [IsRequisitionTarget] = 1
WHERE [DepartmentKind] IS NULL OR [IsRequisitionTarget] IS NULL;
GO
