# TESTWF-A461 Finding 2 — BatchAssign department skip (2026-09-29)

## Symptom
Option C multi-row different custodians: Save returned **0 assigned / 2 skipped**.
Prove paraphrased skip as "Asset not belonging to the current department".
Deployed message: **Selected user does not belong to the target department.**

## Root cause
- `AssignmentService.BatchAssign` (`AssignmentService.cs` ~286 on Option C tip `0ce9c11`):
  `ToDepartmentId = request.ToDepartmentId ?? asset.DepartmentId`
- InStore assets under Test-WF sat in **class departments** (e.g. asset 24967 → Dept 282 Grade 9A; 25002 → 290 Grade 11D).
- Custodians `wambua9912@gmail.com` / `allannwambua@gmail.com` are **Company Admin with null DepartmentId**.
- `EnsureUserBelongsToDepartment` (`AssignmentService.cs` ~349) threw when `!user.DepartmentId.HasValue`.

## Fix
1. `ResolveBatchAssignDepartmentId`: page override → custodian home dept → **null** (do not force asset dept for person assign).
2. Soften `EnsureUserBelongsToDepartment` + `DepartmentUserWorkflowHelper.UserBelongsToDepartment` for org-level (null home dept) users.

## Files
- `src/AssetManagement.Application/Services/Assignments/AssignmentService.cs`
- `src/AssetManagement.Web/Helpers/DepartmentUserWorkflowHelper.cs`
- `tests/AssetManagement.Tests/Assignments/BatchAssignServiceTests.cs`

## Base
Branch based on `origin/cursor/multi-row-assign-b374` (`0ce9c11`) because `origin/main` does not contain Option C BatchAssign.

## Follow-up 2026-09-29 — SqlDateTime overflow on BatchAssign Save (QA re-prove)

### Symptom
After dept-skip fix (`3d4abe2`), BatchAssign Save YSOD:
`SqlDateTime overflow. Must be between 1/1/1753 and 12/31/9999.`
Stack: `AuditLogAttribute.OnActionExecuted` → `AuditWriter.Write` → `EntitySqlWriter.Update`.
Preferred assets 24967 / 25002 remained Unassigned.

### Root cause (file:line)
1. Live DB `Asset.PurchaseDate` / `DepreciationStartDate` are **NULLABLE** (schema drift vs scripts) and NULL on Test-WF InStore rows 24967/25002.
2. `EntitySqlAccess.ReadRow` (~187-189) maps `DBNull` → `GetDefault(DateTime)` = **`DateTime.MinValue`** (`0001-01-01`).
3. `AssignWithoutSave` marks that `Asset` Modified; `ExecuteInTransaction` → `EntitySqlWriter.Update` → `AddParameter` (~401-416) sent `DateTime.MinValue` → SqlDateTime overflow.
4. Exception is not a `BusinessException`, so it bubbled; `OnActionExecuted` still wrote HTTP audit and `SaveChanges` re-flushed the dirty Asset (`Update`), producing the YSOD stack on the audit filter.

### Fix
1. `EntitySqlWriter.AddParameter` / `ToSqlDateTimeValue`: out-of-range `DateTime` (incl. `MinValue`) → `DBNull` (omit unset dates).
2. `AuditWriter` sync path: `ClearTracking()` before inserting `AuditLog` so HTTP audit never flushes leftover Modified entities.

### Files
- `src/AssetManagement.Infrastructure/Persistence/EntitySqlAccess.cs`
- `src/AssetManagement.Infrastructure/Services/AuditWriter.cs`
