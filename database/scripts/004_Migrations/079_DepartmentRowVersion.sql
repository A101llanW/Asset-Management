-- 079: Optimistic concurrency RowVersion on Department (and prepared for DepartmentApprovalStage in 080).
-- Non-destructive: ADD rowversion column only. No data wipe.

IF COL_LENGTH(N'[Department]', N'RowVersion') IS NULL
BEGIN
    ALTER TABLE [Department] ADD [RowVersion] ROWVERSION NOT NULL;
END
GO