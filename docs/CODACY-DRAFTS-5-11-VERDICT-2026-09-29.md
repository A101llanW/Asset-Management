# Codacy dig - draft PRs #5-#11 vs local CodexAsset SoT

**Date:** 2026-09-29 (Africa/Nairobi)
**Audience:** Allan (plain English)
**Mode:** DIG ONLY - no code fix, no PR merge/push
**Local SoT:** `C:\Users\allan\Documents\Examples\CodexAsset\`
**Local tip branch (when dug):** `cursor/multi-row-assign-b374` @ `a438b82` + dirty WIP
**GitHub:** `A101llanW/Asset-Management` drafts #5-#11

---

## Bottom line for Allan

**No real reachable bugs** showed up in the Codacy "Potential null dereference" samples we opened on the PR heads and re-checked on local tip.

Codacy is failing the drafts because the org gate allows **0 issues** of at least minor severity. That means **style / BestPractice / markdownlint / complexity** also block, not just crashes.

**Recommendation:** treat patterns 2-5 as **NOISE for merge gate** (exclude docs from Codacy or lower severity / accept known noise). Pattern 1 is mostly **false positive** null-flow noise - do **not** churn code just to silence Codacy unless a specific site is proven reachable.

`READY_FOR_ALLAN: yes`

---

## PRs inspected

| PR | Title (short) | Draft | Codacy conclusion | Annotation count (GitHub Checks API) |
|----|---------------|-------|-------------------|--------------------------------------|
| **#5** | Multi-row batch assign | yes | action_required | 50 (API capped; bot said 68 issues) |
| **#6** | Reports Preview DBNull / purchase date | yes | action_required | 4 |
| **#7** | BatchAssign cross-department | yes | action_required | 23 |
| **#8** | SubTypes Create type picker | yes | action_required | 9 |
| **#9** | Purchases "Assign these units" CTA | yes | action_required | 17 |
| **#10** | SubTypes Index list | yes | action_required | 12 |
| **#11** | Purchase request self-approve | yes | action_required | 10 |

Sources: Codacy bot issue comments + `Codacy Static Code Analysis` check-run annotations (public GitHub API). No human review comments. No local prior Codacy board under `docs/` or `Temp/`.

Codacy app UI (`app.codacy.com/.../issues`) needs login - not used. Annotations from Checks API are the file:line SoT for this dig.

---

## Real reachable bugs (call out first)

**None found** in the focus samples.

Closest "sounds scary" items that are **not** bugs when you read the guards:

- `DepartmentUserWorkflowHelper` `user.DepartmentId.Value` - only after `HasValue` / null-user returns.
- `AssignmentService.EnsureUserBelongsToDepartment` same pattern.
- `ReportDocumentService` `period.From` / `period.To` - `period` is a **struct** (`ReportPeriodHelper.DateRange`), cannot be null.
- `AssetQueryService` `filter.UnassignedOnly` - inside `if (filter != null)`.
- Purchases / SubTypes "null deref" on field declarations or after explicit null checks - analyzer noise (worsened where the PR file is double-spaced).

---

## Per focus pattern (1-5)

### 1) Potential null dereference

| | |
|--|--|
| **Sample (PR head Codacy cite)** | `AssignmentsController.cs:171` on PR #5 (`0ce9c11`) - `LoadBatchAssignRows(...)` right after `BatchCreate(...)` |
| **Local tip nearest** | `AssignmentsController.cs:166` - same call; BatchCreate GET now starts ~**L164** (drift ~5 lines) |
| **Classification** | **false positive** |
| **Verdict** | **NOISE for merge gate** |

**Why:** Sonar/Codacy "Potential null dereference" fires heavily on these PRs without a concrete NRE path. Spot checks:

| Codacy cite | What the line actually is | Class |
|-------------|---------------------------|-------|
| `AssetQueryService.cs:488` (#5) | `filter.UnassignedOnly` inside `if (filter != null)` | FP - **local tip now ~L906** (file grew) |
| `ReportDocumentService.cs:111` (#6) | `period.From` / `period.To` after `PurchaseDate.HasValue` | FP - `DateRange` is **struct**; **local tip still ~L111** |
| `AssignmentService.cs:345/352/386` (#7) | `HasValue` / `GetById` / `.Value` after guards | FP - **drift on local WIP** |
| `DepartmentUserWorkflowHelper.cs:80` (#7) | `.Value == .Value` after `HasValue` | FP - **local tip ~L79** |
| `PurchasesController.cs:35` (#9) | **field declaration** `_receivingService` | FP (nonsense cite; double-spaced file) |
| `PurchasesController.cs:91` (#9) | `if (model == null)` null **check** | FP |
| `AssetSubTypesController.cs:23/25` (#10) | `HasValue` then `.Value` | FP |
| `ApprovalWorkflowHelper.cs:278` (#11) | `string.Equals(processCode, ...)` in `AllowsEligibleSelfApproval` | FP - Equals tolerates null; **local tip differs** (self-approve overload not on this WIP the same way) |
| `PendingApprovalQueryService.cs:145` (#11) | self-approve / `CanUserActOnStage` call site | FP / flow noise - **local tip L145 is `CanUserActOnStage`** |

**CHANGE needed?** No, not for merge. Optional later: suppress S2259 / teach Codacy, or add unnecessary null guards only if Allan wants a green Codacy badge for cosmetics.

---

### 2) Prefer System.Uri (Security) - mainly #5

| | |
|--|--|
| **Sample** | `BatchAssignViewModels.cs:77` - `public string ReturnUrl { get; set; }` (+ controller `returnUrl` string / `ResolveBatchAssignReturnUrl`) |
| **Local tip** | **Same L77** - still `string ReturnUrl` |
| **Also cited** | `AssignmentsController` BatchCreate `returnUrl` + `ResolveBatchAssignReturnUrl` return type (PR5 ~L169 / ~L388) |
| **Classification** | **false positive** for this app (MVC open-redirect already mitigated) |
| **Verdict** | **NOISE for merge gate** |

**Why:** Code already does `Url.IsLocalUrl(returnUrl)` before trusting a return URL. That is the normal ASP.NET MVC pattern. Forcing `System.Uri` on query-string return URLs is awkward and does not improve the actual redirect safety here.

**CHANGE needed?** No.

---

### 3) Method length / complexity - BatchAssign / SubTypes Index

| | |
|--|--|
| **Sample A** | `AssignmentService.BatchAssign` - PR5 `AssignmentService.cs:226` (91 LOC / complexity 14) |
| **Sample B** | `AssetSubTypesController.Index` - PR10 `AssetSubTypesController.cs:20` (56 LOC / complexity 14) |
| **Local tip** | BatchAssign still present (line map drifted with WIP). Index exists on PR10 SHA; **local tip `AssetSubTypesController` is heavily reformatted (~716 lines, blank-line inflation)** - Codacy L20 is **not** Index on dirty tip |
| **Classification** | **style-only** |
| **Verdict** | **NOISE for merge gate** |

**CHANGE needed?** Not to ship. Optional later refactor (extract helpers) if Allan wants cleaner Codacy metrics - not a product defect.

Also on #5: `BatchCreate` POST complexity / `PopulateBatchLookups` complexity - same bucket.

---

### 4) Optional-params BestPractice

| | |
|--|--|
| **Sample** | PR5 `AssignmentsController.BatchCreate(..., bool fromFilter = false, string returnUrl = null)` @ **L169** |
| **Also** | PR10 `Index(..., assetTypeId = null, search = null, activeOnly = true)`; PR6 `SqlQueryHelper.GetDecimal/GetInt32` defaults; PR11 `CanUserActOnStage(..., stageUserId = null, allowEligibleSelfApproval = false)` |
| **Local tip** | BatchCreate optional params ~**L164**; ApprovalHelper still has `stageUserId = null` ~**L242/L270** (self-approve flag may differ on this WIP vs PR #11 head) |
| **Classification** | **style-only** |
| **Verdict** | **NOISE for merge gate** |

**Why:** MVC actions **need** optional parameters for query-string binding. Replacing with overloads is worse here. Helper defaults (`GetDecimal(..., defaultValue = 0m)`) are normal C#.

**CHANGE needed?** No.

---

### 5) Markdownlint on docs

| | |
|--|--|
| **Sample** | `docs/TESTWF-A461-BATCHASSIGN-DEPT-2026-09-29.md:3` - "Expected: 1; Actual: 0; Below" (blank lines around headings/lists); same family on #7-#11 board docs |
| **Local tip** | File present; still compact heading/list style |
| **Classification** | **style-only** |
| **Verdict** | **NOISE for merge gate** |

**CHANGE needed?** No for product. Prefer **exclude `docs/**` from Codacy** (or ignore markdownlint) so dig boards and TESTWF notes do not fail PRs.

---

## Branch drift notes (Codacy line vs local tip)

| Finding | PR head | Local tip |
|---------|---------|-----------|
| BatchCreate GET signature | #5 L169 | ~**L164** (same method) |
| `filter.UnassignedOnly` | #5 L488 | ~**L906** |
| `BatchAssign` method start | #5 L226 | drifted (WIP / longer file) |
| `ReturnUrl` property | #5 L77 | **L77** (stable) |
| Report `period.From` | #6 L111 | **L111** (stable) |
| Dept helper `.Value` | #7 L80 | ~**L79** |
| SubTypes `Index` | #10 L20 | **Codacy L20 != Index on dirty tip** (file reformat / WIP) |
| Self-approve optional params | #11 L233-234 | signatures differ on current WIP vs `ab6c72b1` |
| PendingApproval cite | #11 L145 | local L145 is `CanUserActOnStage` call |

**Rule used:** PR-head blob = what Codacy scanned; local tip = merge/truth for "does the issue still exist."

---

## Codacy counts by PR (bot summary)

| PR | Critical | High | Medium | Minor | Dominant categories |
|----|----------|------|--------|-------|---------------------|
| #5 | 16 | 33 | 18 | 1 | ErrorProne, Security (Uri), Complexity, JS style |
| #6 | 1 | - | 2 | 1 | ErrorProne + BestPractice + markdown Bare URL |
| #7 | 4 | - | 3 | 16 | ErrorProne + markdownlint |
| #8 | 3 | - | - | 6 | ErrorProne + markdownlint |
| #9 | 11 | - | - | 6 | ErrorProne (Purchases double-space FPs) + markdownlint |
| #10 | 3 | - | 3 | 6 | ErrorProne + Complexity + BestPractice + markdownlint |
| #11 | 2 | - | 2 | 6 | ErrorProne + BestPractice + markdownlint |

---

## Suggested Allan policy (not done - dig only)

1. **Do not block merge** of #5-#11 on these Codacy findings as written.
2. **Exclude docs** from Codacy / markdownlint for this repo.
3. **Accept or suppress** Sonar "prefer Uri" + "no optional params" on MVC controllers.
4. Re-run Codacy only after intentional cleanup PRs if a green badge is required for optics.
5. If one null-deref ever looks real, prove it with a failing test or repro - none of today's samples cleared that bar.

---

## Success checklist

- [x] READY_FOR_ALLAN: **yes**
- [x] Board path: `docs/CODACY-DRAFTS-5-11-VERDICT-2026-09-29.md`
- [x] Patterns 1-5: sample file:line, classification, CHANGE vs NOISE
- [x] Real bugs called out first: **none**
- [x] Branch drift noted
- [x] PRs inspected: **#5, #6, #7, #8, #9, #10, #11**