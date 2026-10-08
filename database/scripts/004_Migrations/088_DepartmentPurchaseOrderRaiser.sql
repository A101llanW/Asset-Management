-- 088: "Purchase order raised by" per Department / Sub-department / Room / Class (Custom requisition flow).
-- Schema only, additive, idempotent. NULL = not configured, so every existing row keeps today's behaviour
-- (anyone with Purchases permission can record the PO). The effective raiser is resolved from the unit the
-- requisition flow is taken from (same resolver as the approval stages, incl. RequisitionInheritFrom / 085).
-- The optional per-stage "Approver (person)" needs no schema change: it uses the existing positional
-- Department.CustomStageUserIds / RequisitionApprovalStageUserIds and PurchaseRequest.ApprovalStageUserIds.
-- Apply BEFORE deploying the DLLs that map these columns.

IF COL_LENGTH(N'[Department]', N'PurchaseOrderRaiserRoleId') IS NULL
BEGIN
    ALTER TABLE [Department]
        ADD [PurchaseOrderRaiserRoleId] INT NULL;
END
GO

IF COL_LENGTH(N'[Department]', N'PurchaseOrderRaiserUserId') IS NULL
BEGIN
    ALTER TABLE [Department]
        ADD [PurchaseOrderRaiserUserId] NVARCHAR(128) NULL;
END
GO

-- Rollback (deploy previous DLLs first: the entity maps these columns):
-- ALTER TABLE [Department] DROP COLUMN [PurchaseOrderRaiserUserId];
-- ALTER TABLE [Department] DROP COLUMN [PurchaseOrderRaiserRoleId];
