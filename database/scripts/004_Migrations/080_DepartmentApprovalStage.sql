-- 080: DepartmentApprovalStage + ordered CSV migrate.
-- Behavior-preserving dual-read. NO hierarchy parent changes. NO org/asset wipe.

IF OBJECT_ID(N'[dbo].[DepartmentApprovalStage]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DepartmentApprovalStage] (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_DepartmentApprovalStage] PRIMARY KEY,
        [OrganizationId] INT NULL,
        [DepartmentId] INT NOT NULL,
        [StageNumber] INT NOT NULL,
        [RoleId] INT NOT NULL,
        [UserId] NVARCHAR(128) NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_DepartmentApprovalStage_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_DepartmentApprovalStage_IsActive] DEFAULT (1),
        [RowVersion] ROWVERSION NOT NULL,
        CONSTRAINT [FK_DepartmentApprovalStage_Department] FOREIGN KEY ([DepartmentId]) REFERENCES [Department]([Id]),
        CONSTRAINT [UQ_DepartmentApprovalStage_Dept_Stage] UNIQUE ([DepartmentId], [StageNumber])
    );
    CREATE INDEX [IX_DepartmentApprovalStage_DepartmentId] ON [dbo].[DepartmentApprovalStage]([DepartmentId]);
    CREATE INDEX [IX_DepartmentApprovalStage_OrganizationId] ON [dbo].[DepartmentApprovalStage]([OrganizationId]);
END
GO

;WITH CustomDepts AS (
    SELECT d.[Id], d.[OrganizationId],
           CAST(N',' + d.[CustomStageRoleIds] + N',' AS NVARCHAR(400)) AS Csv
    FROM [Department] d
    WHERE d.[RequisitionFlowMode] = 1
      AND d.[CustomStageRoleIds] IS NOT NULL
      AND LTRIM(RTRIM(d.[CustomStageRoleIds])) <> N''
      AND NOT EXISTS (
          SELECT 1 FROM [DepartmentApprovalStage] s WHERE s.[DepartmentId] = d.[Id] AND s.[IsActive] = 1
      )
),
Splitter AS (
    SELECT
        c.[Id] AS DepartmentId,
        c.[OrganizationId],
        1 AS StageNumber,
        SUBSTRING(c.Csv, 2, CHARINDEX(N',', c.Csv, 2) - 2) AS RoleToken,
        CHARINDEX(N',', c.Csv, 2) AS NextPos,
        c.Csv
    FROM CustomDepts c
    WHERE CHARINDEX(N',', c.Csv, 2) > 2
    UNION ALL
    SELECT
        s.DepartmentId,
        s.OrganizationId,
        s.StageNumber + 1,
        SUBSTRING(s.Csv, s.NextPos + 1, CHARINDEX(N',', s.Csv, s.NextPos + 1) - s.NextPos - 1),
        CHARINDEX(N',', s.Csv, s.NextPos + 1),
        s.Csv
    FROM Splitter s
    WHERE CHARINDEX(N',', s.Csv, s.NextPos + 1) > s.NextPos + 1
)
INSERT INTO [DepartmentApprovalStage] ([OrganizationId], [DepartmentId], [StageNumber], [RoleId], [UserId], [CreatedAt], [IsActive])
SELECT
    s.[OrganizationId],
    s.[DepartmentId],
    s.[StageNumber],
    CAST(LTRIM(RTRIM(s.[RoleToken])) AS INT),
    NULL,
    SYSUTCDATETIME(),
    1
FROM Splitter s
WHERE TRY_CAST(LTRIM(RTRIM(s.[RoleToken])) AS INT) IS NOT NULL
  AND TRY_CAST(LTRIM(RTRIM(s.[RoleToken])) AS INT) > 0
OPTION (MAXRECURSION 100);
GO