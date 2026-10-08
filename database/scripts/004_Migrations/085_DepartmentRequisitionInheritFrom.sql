-- 085: Requisition flow Inherit source choice on Department (Sub-department vs Department flow).
-- Schema only, additive, idempotent. NULL = current behaviour (nearest ancestor / org matrix),
-- so every existing row resolves exactly as before. No data change.
-- Values: NULL/0 = Nearest (default), 1 = SubDepartment ancestor, 2 = top-level Department ancestor.
-- (Unrelated legacy 081 column [RoomInheritTarget] is left untouched and remains unused by code.)

IF COL_LENGTH(N'[Department]', N'RequisitionInheritFrom') IS NULL
BEGIN
    ALTER TABLE [Department]
        ADD [RequisitionInheritFrom] INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Department_RequisitionInheritFrom'
)
BEGIN
    ALTER TABLE [Department] WITH CHECK
        ADD CONSTRAINT [CK_Department_RequisitionInheritFrom]
        CHECK ([RequisitionInheritFrom] IS NULL OR [RequisitionInheritFrom] IN (0, 1, 2));
END
GO

-- Rollback (deploy previous DLLs first: the entity maps this column):
-- ALTER TABLE [Department] DROP CONSTRAINT [CK_Department_RequisitionInheritFrom];
-- ALTER TABLE [Department] DROP COLUMN [RequisitionInheritFrom];
