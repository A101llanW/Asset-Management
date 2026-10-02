-- 078: RequisitionFlowMode.AutoApprove (= 2)
-- Column RequisitionFlowMode INT already exists (076). Default remains 0 (InheritParent).
-- No destructive data changes. Empty Custom rows keep working via resolve fallback to AutoApprove;
-- new empty Custom saves are rejected in application validation.
-- Optional one-time cleanup (commented): convert legacy Custom+empty -> AutoApprove without wipe.
--
-- UPDATE d
-- SET d.RequisitionFlowMode = 2,  -- AutoApprove
--     d.CustomStageRoleIds = NULL,
--     d.CustomStageUserIds = NULL
-- FROM [Department] d
-- WHERE d.RequisitionFlowMode = 1  -- Custom
--   AND (d.CustomStageRoleIds IS NULL OR LTRIM(RTRIM(d.CustomStageRoleIds)) = N'');
--
-- Kind scope for Custom/AutoApprove UI today: Administrative, SubDepartment, Room.
-- Class/Grade flow pickers remain out of scope.

IF COL_LENGTH(N'[Department]', N'RequisitionFlowMode') IS NULL
BEGIN
    ALTER TABLE [Department]
        ADD [RequisitionFlowMode] INT NOT NULL
        CONSTRAINT [DF_Department_RequisitionFlowMode] DEFAULT (0);
END
GO