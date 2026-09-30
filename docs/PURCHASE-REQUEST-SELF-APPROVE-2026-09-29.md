# Purchase Request / Requisition self-approval (2026-09-29)

## Problem
Creators of Purchase Requests (Requisitions) could not approve their own requests even when they held the configured stage role / user assignment. `ApprovalWorkflowHelper.CanUserActOnStage` and `EnsureUserCanApprove` blocked any `isMine` / self-approval unless SuperAdmin **and** `Settings.AllowAdminSelfApproval`.

## Fix (Purchase / Requisition only)
- `EnsureUserCanApprove`: when `processCode` is Purchase, self-approval falls through to the same stage role/user checks used for other approvers (Transfer/Disposal unchanged).
- `CanUserActOnStage`: optional `allowEligibleSelfApproval` flag; when true, own requests use stage/role checks instead of the hard self-block.
- Wired for Purchase Details UI and pending-inbox Requisition rows only.

## Scope
- In scope: Purchase Request / Requisition approve UI + server decision path (via shared `EnsureUserCanApprove` keyed on Purchase process code).
- Out of scope: Transfer and Disposal self-approval remain blocked except SuperAdmin + AllowAdminSelfApproval.

## Branch
`fix/purchase-request-self-approve-2026-09-29` (no PR opened by this change).