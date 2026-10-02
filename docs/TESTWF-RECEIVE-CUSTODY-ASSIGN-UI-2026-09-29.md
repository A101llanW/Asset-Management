# Receive custody Assign removed from Receive UI (2026-09-29)

## Finding
Purchases **Receive** showed a custody **Placement** choice at the top ("Assign to [requisition department]" vs company custody). Product rule: custody Assign is optional and belongs **after** assets are received/created (Purchases Details).

## Distinction (verified)
| Control | Location | Purpose | Action |
|---------|----------|---------|--------|
| `#receive-subtype-change` **Assign** button | `Views/Purchases/Receive.cshtml` ~67 | Asset **sub-type classification** → `#assetSubTypePickerModal` | **KEPT** |
| Placement radios `ReceivePlacementChoice` | Receive.cshtml (was ~71–98) | Custody/dept placement during receive | **REMOVED** from Receive UI |
| **Assign these units** | `Views/Purchases/Details.cshtml` (~157) via `ViewBag.AssignableAssetIds` | Post-receive custody Assign (A461) | **KEPT** |

## Fix (SoT)
- File: `src/AssetManagement.Web/Views/Purchases/Receive.cshtml`
- Removed: placement card with "Assign to …" / company-custody radios (custody Assign during receive).
- Added: hidden `ReceivePlacementChoice=CompanyCustody` so receive always lands **In Store / company custody**; optional assign later from Details.
- Sub-type **Assign** button and `_AssetSubTypePickerModal` unchanged.

## Product rule
After Receive → assign is **optional** (Assign these units on Details, or keep In Store). Receive does not force custody assign.

## IIS
- Published view only; `Web.config` preserved.
- Stamp: Receive.cshtml mtime **2026-09-29 15:28:15 EAT**; bak `C:\inetpub\AssetManagement\_bak\receive-custody-assign-20260929-152930\`
- Login probe: 200. Pool restarted Started→Started.

## READY_FOR_QA
**yes** — confirm Receive has no custody Assign/placement radios; subtype Assign still works when required; Details still shows Assign these units for InStore+unassigned received units.