# Requisition decisions log (catalog requisition flows)

Branch `feature/catalog-requisition-flows` (worktree `CodexAsset-catalog-req`, based on `fix/role-access-scope` `9a908b8`, the build published to local IIS on 2026-10-12).
Plan: `C:\Users\allan\.cursor\plans\catalog_requisition_flows_c041f2a2.plan.md`. Newest decisions are at the bottom of each section.

## Owner decisions (given, 2026-10-12)

| # | Decision |
|---|----------|
| D1 | Lines carry Category, Type, Sub-type. Org setting `Purchases.Requisition.ClassificationMode`: `Optional` (default), `RequiredPerLine`, `RequiredHeader`. |
| D2 | Admin-settable approval paths on Category / Type / Sub-type. Resolution: sub-type > type > category > department inherit chain (`DepartmentRequisitionFlowResolver`) > org matrix. The PO raiser follows the winning source. |
| D3 | `Purchases.Requisition.MixedFlowPolicy`: `AutoSplit` (default) or `Block`. AutoSplit groups lines by path fingerprint into separate requisitions (own PR numbers) under one `PurchaseRequestGroup` sharing department, justification and attachment. |
| D4 | Sub-type mandatory at receiving, server-enforced, plus a constraint for new rows; `Asset.AssetSubTypeId` keeps legacy NULLs (no blanket NOT NULL). Stage (d), not started. |

## Implementation decisions (made by the agent, stages a + b)

| # | Stage | Decision | Why |
|---|-------|----------|-----|
| A1 | a | Migration number **100** (`100_CatalogRequisitionFlow.sql`); plan said "099 (name TBD)" but 099 is taken by `099_AssetsViewAllPermission.sql`. | Numbering. |
| A2 | a | One migration covers the whole data model of the plan (CatalogRequisitionFlow, PurchaseRequestGroup, PR group FK + ApprovalFlowSource, line taxonomy) so stages c/d need no further schema for flows. | Fewer deploy steps. |
| A3 | a | Catalog row `FlowMode` reuses `RequisitionFlowMode` (0 Inherit = no override / continue cascade, 1 Custom, 2 AutoApprove). Custom with zero stages = auto-approve, same as departments. Inactive rows are ignored. | Same semantics admins already know from department units. |
| A4 | a | Catalog Custom ignores the org "Approval.Purchase.Enabled" flag (RequiresApproval = has stages), exactly like a department Custom unit. | Consistency with `DepartmentRequisitionFlowResolver`. |
| A5 | a | Fingerprint = SHA-256 (first 16 bytes, hex) of: source **key** (ids, e.g. `Catalog:Type:12`, `Department:5`, `OrganizationMatrix`, not the display name) + auto/requires flags + ordered stage roles + stage users (trimmed, lower-case, blank = any holder) + PO raiser role/user. | Plan asks for source + stages + raiser. Using ids means a rename never splits a form. Two catalog nodes with identical stages still split, so each requisition has one unambiguous source and PO raiser. |
| A6 | a | All lines of one form share the target department, so every line without a catalog hit lands in one department/org-matrix group. | Single DepartmentId per batch (plan, out of scope). |
| A7 | a | Classification is normalized server-side: sub-type implies type, type implies category. Contradictions, inactive rows and other-organization ids are rejected ("Line N: ..."). Zero/negative ids count as "not picked". | Never trust the client; forged ids cannot pick a path. |
| A8 | a | `PurchaseOrderRaiserRules.ResolveForRequest` (live resolution, as before) now resolves through the first classified line of the requisition; unclassified requisitions resolve exactly as before. No PO-raiser snapshot column. | All lines of one requisition share a fingerprint, so one line represents it. Keeps the existing live-resolution design. |
| A9 | a | The catalog table is queried only for classified lines. | Unclassified (all current) submits do not touch the new table. |
| A10 | a | Org policy settings are **not seeded**; a missing or unknown value means `Optional` / `AutoSplit`. | No data change for existing orgs; behaviour identical to today. |
| A11 | a | No filtered indexes in migration 100. | Filtered indexes break writes from sessions with QUOTED_IDENTIFIER OFF (e.g. plain sqlcmd scripts). |
| A12 | a | `OrganizationPurgeSql` deletes `PurchaseRequestGroup` (after PurchaseRequest) and `CatalogRequisitionFlow`. | Tenant purge stays complete; its guards tolerate older schemas. |
| B1 | b | `int Submit(...)` stays (returns the first id) and `SubmitBatch(...)` returns `PurchaseRequestSubmitResult` (ids, numbers, group id, batch label, sources). | Existing callers/tests keep working. |
| B2 | b | Partitions keep first-appearance order; line numbers restart at 1 in each requisition. | Line 1 stays in the first PR; PO line numbering per PR stays 1..n. |
| B3 | b | Every path is validated (stage-1 role configured) before anything is created; a split submit runs in one `ExecuteInTransaction`, so a failure leaves no partial batch. Single-path submit is unchanged (no transaction, as before). | Atomic batch. |
| B4 | b | `BatchLabel` = `BATCH-` + group id (D6), assigned after insert like `PR-` numbers. | Same pattern as RequestNumber. |
| B5 | b | Attachment: one upload; the controller saves the same stored path on every requisition of the batch. | Plan preference ("single upload, multiple PR rows pointing at same path"). Note: nothing deletes requisition attachments today; if deletion is added later it must check for other references. |
| B6 | b | `TargetAssetId` stays only on the requisition that holds line 1. It pre-fills line 1's classification when line 1 has none, **best effort**: an asset whose type is inactive/unusable just does not pre-fill (submit is not blocked). In `RequiredHeader` mode the tagged asset can stand in for a missing header pick. It never satisfies `RequiredPerLine` for other lines. | Plan section 4; existing tests with a tagged asset of an unseeded type kept passing. |
| B7 | b | `RequiredPerLine` / `RequiredHeader` minimum level = Type (category alone is not enough). In `RequiredHeader`, a line that has its own pick keeps it; others inherit the header. | Plan default "min = Type". |
| B8 | b | `Block` error lists the distinct path labels: "These items use different approval paths (A, B). Remove lines or submit them separately." | Clear message. |
| B9 | b | Until the batch summary view exists (stage c), a split submit redirects to the first requisition with a message listing all PR numbers and the batch label. | No UI in this dispatch. |
| B10 | b | `PurchaseRequest.ApprovalFlowSource` stores the winning source label at submit (max 200 chars); `PurchaseRequestDetailVm` exposes it and the group id (not rendered yet). | Audit trail for the path used. |

## Housekeeping

| # | Note |
|---|------|
| H1 | `tests/AssetManagement.Tests` did not build at `9a908b8` (43 errors). Minimal fix in `474c715`: tests referencing removed members are wrapped in `#if LEGACY_*` (ApprovalWorkflowSelfApproveTests: 2 + 4 tests; DepartmentHierarchyRulesTests: 2 tests; DepartmentRequisitionFlowResolverTests: 7 tests that used ResolveOptions/AcademicFlowMode). 3 resolver tests were ported from `ResolvePurchaseStages(x, f, org, null)` to `ResolveDetailed(x, f, org)`. No production code changed. |
| H2 | After the fix the suite has 26 failing tests that fail the same way before and after this work (role matrix, batch assign, relocation, department editor, self-approval message tests and others). Not touched. |
| H3 | Building the test project from this worktree needs the packages of the main tree: `dotnet build ... -p:NuGetPackageRoot=C:\Users\allan\Documents\Examples\CodexAsset\.nuget\packages\` (the worktree has no `.nuget`). |

## Open questions

1. Fingerprint includes the source key: two catalog nodes with identical stages/raiser still split into two PRs. Should identical paths from different nodes merge instead (then the PR records one of the sources)?
2. Should catalog flows also respect the org "Approval.Purchase.Enabled" switch, or stay independent like department Custom units (current)?
3. Org settings default rows: seed explicit `Optional` / `AutoSplit` rows per organization when the Settings UI lands (stage c), or keep "missing = default"?
4. Approver scope for catalog stages still uses the requisition's department (`StageApproverScopeResolver` with `request.DepartmentId`), i.e. "role holders of that department, else nearest ancestor, else no-department holders, else org-wide". So an ICT Manager who sits only in ICT is reached through the org-wide fallback when the requesting department chain has no holder, but a holder inside the requesting chain would take precedence. Is that right for catalog approvers (e.g. an ICT Manager who sits in ICT, not in the requesting department)? With a named person per stage it is unaffected.
5. Stage (d) legacy handling for `Asset.AssetSubTypeId` NULLs: CHECK constraint `WITH NOCHECK` on new rows vs trigger vs app-only enforcement. To decide in stage (d).
