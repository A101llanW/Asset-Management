# TESTWF-A461 Four Fixes — 2026-09-29

Local-only dig + fix for Test-WF A46138179 PARTIAL. Four draft branches pushed (no GitHub PRs). IIS published full set from SoT with all four fixes; login 200.

## Root causes (file:line)

### 1) Purchases Details missing “Assign these units”
- **Cause:** CTA only when `TempData["CreatedAssetIds"]` set after Receive redirect (`Views/Purchases/Details.cshtml` ~22 on pre-fix). Details 3/4 had no TempData.
- **Fix lines (SoT):** `PurchasesController.cs` Details sets `ViewBag.AssignableAssetIds` via `ResolveAssignableAssetIdsForPurchase` (~43–90); Details.cshtml uses TempData or persistent InStore+unassigned receiving IDs.

### 2) Option C multi-row different custodians — 0 assigned / 2 skipped
- **Cause:** `AssignmentService.BatchAssign` set `ToDepartmentId = request.ToDepartmentId ?? asset.DepartmentId` (Option C tip `0ce9c11` ~286). Assets 24967/25002 in class depts 282/290; custodians Company Admin with **null** `DepartmentId`. `EnsureUserBelongsToDepartment` threw “Selected user does not belong to the target department.”
- **Fix:** `ResolveBatchAssignDepartmentId` returns custodian home dept or **null** (not asset dept); soften membership for org-level users. SoT: `AssignmentService.cs` ResolveBatchAssignDepartmentId + EnsureUserBelongsToDepartment; `DepartmentUserWorkflowHelper.cs`.

### 3) `/AssetSubTypes/Index` → 404
- **Cause:** No `Index` action on `AssetSubTypesController` (only Create/Edit/ByType/Lookup/CreateFromAsset).
- **Fix:** `Index` action + `Views/AssetSubTypes/Index.cshtml` with Edit links + `AssetSubTypeIndexItemVm`.

### 4) `/AssetSubTypes/Create` needs assetTypeId
- **Cause:** `Create(int assetTypeId)` required Int32.
- **Fix:** `Create(int? assetTypeId = null)` → `CreateSelectType` picker when missing.

## Branches (head / base)

| Branch | Head | Base |
|--------|------|------|
| fix/testwf-a461-batchassign-dept-2026-09-29 | 3d4abe20f78c44e02446f36a8c3fc36ac96257c8 | 0ce9c117717c97d57d4a88604528c723d60d6710 (Option C; origin/main has no BatchAssign) |
| fix/testwf-a461-purchases-assign-cta-2026-09-29 | 5dc557d0a1aad4bc7977646135d6b4cc7c5cba23 | 0ce9c11 |
| fix/testwf-a461-subtypes-index-2026-09-29 | 57511600bf07673e96afb47a52b168cb0d13d48c | origin/main 44915ec0c226f8c557f34c48e791fdea7d3356f4 |
| fix/testwf-a461-subtypes-create-2026-09-29 | bbb8e78aa15cbd492e253841da1483c9f99f652a | origin/main 44915ec |

## IIS publish
- **PASS** login 200 at `/A46138179/Account/Login`
- Web.config preserved (SQLEXPRESS)
- DLL stamps EAT under `C:\inetpub\AssetManagement\bin\`: Application/Domain 13:48:33, Infrastructure/Web 13:48:34 (2026-09-29)
- Backup prior DLLs: `C:\Users\allan\AppData\Local\Temp\iis-am-bin-bak-a461-20260929-134501\`
- CodexAsset-reports-dbnull worktree untouched

## READY_FOR_QA
**yes** — re-prove Option C multi-custodian save, Purchases Details 3/4 Assign CTA, AssetSubTypes Index + Create bare URL.
