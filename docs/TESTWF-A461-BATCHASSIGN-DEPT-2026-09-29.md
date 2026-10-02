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
