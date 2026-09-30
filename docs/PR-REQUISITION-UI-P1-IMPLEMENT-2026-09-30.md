# PR Requisition UI P1 implement — 2026-09-30

**SoT:** `C:\Users\allan\Documents\Examples\CodexAsset\` on alLAN  
**Branch:** `cursor/multi-row-assign-b374`  
**Pre-merge HEAD:** `232fcf1`  
**Self-approve merge:** `127e08d` (merge `ab6c72b` / `fix/purchase-request-self-approve-2026-09-29`, no conflicts)  
**Implement commit:** `f94957e`

## What landed

### Merge
- Merged `fix/purchase-request-self-approve-2026-09-29` (`ab6c72b`) into current SoT branch.
- Purchase creators who match the current stage may approve own requisitions; Transfer/Disposal unchanged.

### P1-1 Path honesty
- Create banner uses `DepartmentRequisitionFlowResolver.ResolveDetailed` for the selected target (Custom / Inherited / Organization matrix) plus stage list (role + person).
- Create refreshes path via `GET PurchaseRequests/PreviewApprovalPath?departmentId=`.
- Details shows **snapshotted** stages from the request (`ApprovalStageRoleIds` / `UserIds`), highlights current, shows pinned `CurrentStageUserName`.
- Create subtitle no longer claims Settings→Approval Matrix / Procurement Officer only.

### P1-2 Target control
- Label **Requisition target**; help text covers room / leaf sub-unit / leaf admin / class-stream.
- `BuildRequisitionDepartmentSelectList` walks full tree (grandchildren rooms) with `Parent → Child` path labels; optional `scopeRootDepartmentId`.
- Non–Create-for-any users get leaves under their branch (not locked to a non-target parent). Single leaf still locks.
- Tagged-asset department only applied when it is a leaf option; otherwise warning and leave target unchanged.

### P1-3 Approval card + self-approve UI
- Details Approve stage visible for eligible creator (server already from merge).
- Replaced “or you submitted this request” with wrong-role / pinned-user reasons.
- `_RequisitionWorkflowGuide.cshtml` updated for self-approve rule and Requisition flows / Inherit–Custom.
- No separate Self-approve button; no HodUserId picker.

### Cheap extras
- Removed redundant `RequestForSelf` hidden + VM property.
- Index/Details show target kind badge when available.

## Files changed (implement)
- `src/AssetManagement.Application/Helpers/DepartmentRequisitionFlowResolver.cs`
- `src/AssetManagement.Application/ViewModels/Purchases/PurchaseRequestViewModels.cs`
- `src/AssetManagement.Application/Services/Purchases/PurchaseRequestService.cs`
- `src/AssetManagement.Infrastructure/Queries/OperationsQueryRepository.cs`
- `src/AssetManagement.Web/Controllers/BaseController.cs`
- `src/AssetManagement.Web/Controllers/PurchaseRequestsController.cs`
- `src/AssetManagement.Web/Views/PurchaseRequests/Create.cshtml`
- `src/AssetManagement.Web/Views/PurchaseRequests/Details.cshtml`
- `src/AssetManagement.Web/Views/PurchaseRequests/Index.cshtml`
- `src/AssetManagement.Web/Views/Shared/_RequisitionWorkflowGuide.cshtml`
- `src/AssetManagement.Web/Scripts/app/purchase-request-create.js`
- `docs/PR-REQUISITION-UI-P1-IMPLEMENT-2026-09-30.md` (this file)
- Plus merge: ApprovalWorkflowHelper, PendingApprovalQueryService, PurchaseRequestsController self-approve flag, self-approve doc

## Build
- MSBuild `AssetManagementModule.sln` Debug: **Application / Infrastructure / Web / Tests PASS**
- **PerformanceTests FAIL** (pre-existing): `FixedOrganizationScopeService` missing `SetExecutionContext` / `ClearExecutionContext` — unrelated to this change.

## Left for IIS + QA (parent must ask Allan before publish)
1. Elevated IIS publish of Web (preserve Web.config) — **not done**.
2. QA Create: pick Admin→Sub-unit→Room; banner shows Custom/Inherited/Matrix + stages; change target refreshes banner.
3. QA Create: scoped user on non-target parent sees leaf list under branch; cannot submit non-leaf.
4. QA tagged asset on non-leaf: warning; target not overwritten.
5. QA Details: snapshotted stages + current highlight + pinned name; creator with matching role sees Approve stage; wrong role sees honest reason.
6. QA pending inbox still Action Required for eligible self-approver.
7. Smoke guide text on Settings / Roles.

## Notes / stash
- Unrelated WIP was stashed before merge: `WIP before PR UI P1 merge+impl 2026-09-30` — restore when Allan wants those files back.
- No GitHub PR, no push, no CloudAgent, no IIS publish.
