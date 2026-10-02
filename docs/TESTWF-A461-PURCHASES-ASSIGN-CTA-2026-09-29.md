# TESTWF-A461 Finding 1 — Purchases Details Assign CTA (2026-09-29)

## Symptom
Purchases Details ids 3/4: **Assign these units** NOT_SEEN.
Assets Index InStore+Unassigned CTA FOUND.

## Root cause
`Views/Purchases/Details.cshtml` only rendered the CTA when `TempData["CreatedAssetIds"]` was set (post-Receive redirect). Navigating to existing Details drops TempData.

## Fix
`PurchasesController.Details` resolves InStore+unassigned asset IDs from purchase receivings (`ResolveAssignableAssetIdsForPurchase`) into `ViewBag.AssignableAssetIds`. View shows CTA from TempData (fresh receive) or persistent IDs.

## Files
- `src/AssetManagement.Web/Controllers/PurchasesController.cs` (Details + helper)
- `src/AssetManagement.Web/Views/Purchases/Details.cshtml`

## Base
`origin/cursor/multi-row-assign-b374` (`0ce9c11`) — CTA targets BatchCreate from Option C.
