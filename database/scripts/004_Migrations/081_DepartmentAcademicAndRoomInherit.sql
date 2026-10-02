-- 081: Class/Grade academic flow mode + Room inherit Sub-vs-Dept target (UI slice).
-- Non-destructive ADD only. Defaults preserve prior resolve behavior when unset.

IF COL_LENGTH(N'[Department]', N'RoomInheritTarget') IS NULL
BEGIN
    ALTER TABLE [Department]
        ADD [RoomInheritTarget] INT NOT NULL
        CONSTRAINT [DF_Department_RoomInheritTarget] DEFAULT (0);
END
GO

IF COL_LENGTH(N'[Department]', N'AcademicFlowMode') IS NULL
BEGIN
    ALTER TABLE [Department]
        ADD [AcademicFlowMode] INT NOT NULL
        CONSTRAINT [DF_Department_AcademicFlowMode] DEFAULT (0);
END
GO
