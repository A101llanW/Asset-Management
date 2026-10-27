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

## Stage (c) and (d) decisions (2026-10-12)

| # | Decision |
|---|----------|
| C1 | No new category/type/sub-type screens and no new category data. The existing Organization > Asset Categories setup (AssetCategories / AssetTypes / AssetSubTypes Edit pages) was extended in place with a "Requisition approval" section (`_TaxonomyRequisitionApprovalFields`): Inherit / Custom stages / Auto-approve, the shared approval matrix and PO raiser picker. |
| C2 | Editing that section needs `Settings.Manage`; without it the section is read-only and the rest of the Edit page saves as before. |
| C3 | Custom with "requires approval" unticked is stored as AutoApprove. Inherit with no stored row is a no-op (no row written). |
| C4 | Org policies (ClassificationMode, MixedFlowPolicy) are on Settings > Index with a list of configured catalog flows linking to the Edit pages. Saving Settings always writes both rows. |
| C5 | Create.cshtml gets per-line (or header, in RequiredHeader mode) cascading Category/Type/Sub-type selects and a live "approval paths" preview (POST PreviewLinePaths, same resolver as submit). A split submit opens the new Batch summary page; Details shows path, batch link and sibling requisitions. |
| D1 | (PM) Identical paths merge: the fingerprint no longer includes the source node, so lines from different catalog nodes with the same stages and the same PO raiser become one requisition. A different PO raiser still splits. (Answers open question 1.) |
| D2 | (PM) Catalog paths obey the org purchase approval switch (`Approval.Process.Purchase.Enabled`). When it is off a catalog path has no stages, keeps its PO raiser and is labelled "(organisation approvals off)". Department custom paths are unchanged. (Answers open question 2.) |
| D3 | (PM) Migration 101 seeds `Optional` / `AutoSplit` rows per organization where missing. (Answers open question 3.) |
| D4 | (PM) Legacy NULL sub-types are left untouched. Sub-type is enforced only for new receipts: in `ReceivingService` (ReceiveOrder and legacy single Receive; a typed line without sub-type needs a receiver-picked sub-type of that same type) and in the DB by trigger `TR_AssetReceiving_RequireSubType` (AFTER INSERT on AssetReceiving). A CHECK constraint on Asset was rejected: it would re-check legacy rows on any UPDATE and break asset import copies. (Answers open question 5.) |
| D5 | Open question 4 (ICT Manager reached via approver-scope fallback) is unchanged and still open; a named person per stage avoids it. |
| D6 | (PM) Migration 101 is NOT applied to `.\SQLEXPRESS AssetManagementModuleDb` (local IIS 8080). It was applied twice (idempotent) only to the throwaway copy `AssetManagementModuleDb_catreq_test`, restored from `AssetManagementModuleDb_pre101_catreq_20261012.bak`. Migration 100 had been applied to the main DB earlier (before the PM directive); its rollback `database/scripts/ops/100_Rollback_CatalogRequisitionFlow_PREPARE_DO_NOT_RUN.sql` is prepared and parse-checked, not run. |

## Owner decision (2026-10-12 21:10 EAT) - supersedes D1

| # | Decision |
|---|----------|
| D1a | **Supersedes D1 (PM merge).** Identical approval paths coming from different catalog nodes are NOT merged: the group-and-split fingerprint again includes the winning source node id (original stage b design). Lines resolving to the same node with the same stages/PO raiser still share one requisition; a different PO raiser still splits. Test `IdenticalPaths_FromDifferentNodes_MergeIntoOneRequisition` replaced by `IdenticalPaths_FromDifferentNodes_StaySeparateRequisitions`; added `SameNode_TwoLines_ShareOneRequisition`. No DB change. |
| D2a | **Supersedes D2 (PM: obey approvals switch).** Catalog-level paths (sub-type/type/category) ALWAYS apply regardless of the org-wide purchase-approval switch; the "(organisation approvals off)" behaviour is removed. Department / org-matrix behaviour unchanged. Test `OrgApprovalsOff_CatalogPathHasNoStages_PoRaiserKept` replaced by `OrgApprovalsOff_CatalogPathStillApplies`. |
| D5a | **Owner decision, supersedes D5 / answers open question 4.** For catalog-sourced stages (requisition ApprovalFlowSource names a Category/Type/Sub-type node) approvers resolve catalog-path FIRST: 1. named person on the stage; 2. every active holder of the stage role configured on the catalog path (catalog nodes have no owning department, so the path scope is organisation-wide); only if there is none, 3. requesting department holders, 4. ancestors, 5. no-department holders, 6. org-wide. Applied identically to approve/reject eligibility, Details 'can approve', pending-approval lists (row now carries ApprovalFlowSource), stage notifications and the PO raiser. Department / org-matrix stages unchanged. Path resolution order (sub-type > type > category > department > matrix) unchanged. Removes the ICT Manager caveat. Caveat: a department-type role (e.g. Head of Department) placed on a catalog path now means every holder of that role org-wide; use a named person there. Catalog detection uses the stored label (no DB change). Tests: CatalogApproverFirstTests (11). |

## Asset name length (migration 108, branch feat/req-line-asset-name) - pending Allan's confirmation (relayed by PM 20:58 EAT)

| # | Decision |
|---|----------|
| N1 | Asset name length is **200** (`AssetNameRules.MaxLength = 200`), the same as the `Asset.AssetName` / `ItemName` NVARCHAR(200) columns; replaces the provisional 150. Pending Allan's confirmation (relayed by PM 20:58 EAT): Allan's own words choosing 200 have not been seen yet. The code stays at 200; changing it later needs no schema change while it is at most 200. |

## Owner decisions (2026-10-27 21:20 EAT, Allan in the Debugging bot's chat) - C3 and C5 (migration 108)

| # | Decision |
|---|----------|
| N2 | **Supersedes the C4 classification lock (review item C3).** At PO creation and at receiving, Category / Type / Sub-type are pre-filled from the approved requisition line (tagged lines too) and shown read-only with an **Edit** button. Sub-type stays mandatory after Edit; the category always follows the type. The server checks that the type and sub-type are active, in the organization (or shared) and that the sub-type belongs to the type. Every change is audit-logged (`Purchases.ClassificationEdited` on the PO line, `Purchases.Receive.ClassificationEdited` at receiving; old vs new, user, PO / line, requisition or receipt). An edit at receiving goes on the new asset only; the PO line keeps what was ordered. Description stays mandatory; receiving against a tagged asset always creates a new asset. Per-line trail: see N3. |
| N3 | **Override trail per line.** Migration 108 also adds five NULLable columns on `PurchaseOrderLine` and on `PurchaseOrderLineReceipt`: `ClassificationOverriddenById` NVARCHAR(128), `ClassificationOverriddenAt` DATETIME (UTC), `OriginalAssetCategoryId` / `OriginalAssetTypeId` / `OriginalAssetSubTypeId` INT (snapshot ids, no FK). Set when the classification is changed on the PO (original = the prefill) or at receiving (original = the PO line's; the new asset has the new one). PO Details shows "changed by / when (EAT) / old -> new" on the order line and on the receiving row. Same COL_LENGTH guards, mirrored in the rollback. The receipt also stores the "new" classification (`NewAssetCategoryId` / `NewAssetTypeId` / `NewAssetSubTypeId` INT, no FK; re-review item 4), so the trail does not follow a later reclassification of the asset; receipts stamped before these columns show the asset's classification labelled "current". A PO line needs no New* columns: its own type / sub-type columns are written once when the PO is raised. Type and sub-type must belong to the PO's organisation, else the current tenant's (re-review item 1). |
| N4 | **C5 unchanged in design.** Types with no sub-types still cannot be requisitioned untagged (wording kept). Receiving blocks unless Category, Type and Sub-type are all set, client-side and server-side (`ReceivingService.EnsureCompleteClassification`), including after Edit. A tagged asset still always gets a NEW asset. |
| N5 | **Intended (re-review item 3):** posting the same type with a newly picked sub-type (the source had none) counts as an override and is stamped (who / when / original with no sub-type) and audit-logged. The trail stays conservative: any change to type or sub-type is recorded, including filling a missing sub-type. |