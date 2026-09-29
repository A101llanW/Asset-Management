# Receive subtype Assign removed from Receive UI (2026-09-29)

## Finding
Purchases **Receive** showed a classification **Assign** button (#receive-subtype-change) that opened #assetSubTypePickerModal during receive. Product rule: **classification only after create** (Assets Create/Edit picker / AssetSubTypes). Custody Assign was already moved post-receive.

## Distinction (verified)
| Control | Location | Purpose | Action |
|---------|----------|---------|--------|
| `#receive-subtype-change` **Assign** button | `Views/Purchases/Receive.cshtml` | Asset **sub-type classification** during receive | **REMOVED** |
| `_AssetSubTypePickerModal` + `asset-subtype-picker.js` on Receive | Receive.cshtml | Opens picker during receive | **REMOVED** from Receive only |
| Assets Create/Edit `_AssetClassificationPickerModal` | Assets views | Post-create classification | **KEPT** |
| Placement radios / custody Assign on Receive | (prior fix) | Custody during receive | Still **absent**; hidden `CompanyCustody` |
| **Assign these units** | Purchases Details | Post-receive custody Assign | **KEPT** |

## How receive still gets / doesn't need subtype
1. **Line / catalog / target default:** `AssetSubTypeId` hidden is prefilled from `PurchaseReceiveDetailVm.AssetSubTypeId` when context resolves a subtype (target asset, confirmed catalog match, or prior line match). Submit uses that id.
2. **Deferred blank:** When no subtype is pre-resolved but **asset type** is known, receive creates assets with `AllowDeferredSubTypeClassification` (null `AssetSubTypeId`). Classify later on Assets Edit.
3. **Type from subtype fallback:** If subtype id is posted/resolved but context lacked type, type is taken from the subtype.
4. **Still required for create:** `CategoryId` / `AssetTypeId` remain required on `Asset`. Bare POs with **no** catalog match, target asset, or other type context still cannot create units until classification context exists (confirm catalog or link target). Subtype Assign on Receive is no longer available to supply type+subtype mid-receive.

## Fix (SoT)
- `src/AssetManagement.Web/Views/Purchases/Receive.cshtml` — removed Assign button (~was 65-68), `_AssetSubTypePickerModal`, `asset-subtype-picker.js`; deferred messaging; catalog decline label; kept hidden `AssetSubTypeId` + `CompanyCustody`.
- `ReceivingService.cs` — allow null subtype on create; type-from-subtype fallback; drop `RequiresSubTypeAssignment` hard throw.
- `AssetCreateVm.AllowDeferredSubTypeClassification` + `AssetService.ApplySubType` — allow null subtype only when flag set (Assets Create/Edit still require subtype).

## IIS
- Published `Receive.cshtml` + `AssetManagement.Application.dll`; `Web.config` preserved.
- Stamp: Receive.cshtml mtime **2026-09-29 15:40:06 EAT**; Application.dll **2026-09-29 15:41:04 EAT**
- bak `C:\inetpub\AssetManagement\_bak\receive-subtype-assign-20260929-154201\`
- Login probe: **200**. Pool Started.

## READY_FOR_QA
**yes** — confirm Receive has no subtype Assign / picker; custody placement still absent; Details Assign these units remains; receive with pre-resolved subtype still works; Assets Create/Edit classification picker unchanged.

## Blockers
- Receive with **no asset-type context** (no catalog confirm, no target asset, no resolved subtype that carries a type) still cannot create assets (type required). Classify-after-create covers **subtype** deferral when type is already known.