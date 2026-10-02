# IIS publish — PR UI + self-approve — 2026-09-30

**When:** 2026-09-30 ~11:23–11:25 EAT (Africa/Nairobi)  
**Machine:** alLAN — `C:\inetpub\AssetManagement` (http://localhost:8080/)  
**Branch:** `cursor/multi-row-assign-b374`  
**HEAD tip:** `61c2edb` (docs tip)  
**Impl:** `4d9291c` feat(pr): P1 requisition UI path honesty, target control, self-approve UX  
**Merge:** `127e08d` merge purchase-request self-approve into multi-row-assign SoT  

## Dirty note
Working tree had uncommitted WIP (phase1 hierarchy / resolver / etc.) at publish time; **not** restored from stash. SoT `bin` used was already rebuilt ~11:22 EAT from that tree (tip + WIP). Views published include small uncommitted Create.cshtml validation tweaks (Justification required + Qty validation message).

## Bak
`C:\inetpub\AssetManagement\_bak\pr_ui_selfapprove_20260930_112345`

## Copied (Web.config preserved — hash unchanged)
- bin: AssetManagement.Web / Application / Domain / Infrastructure (+ pdbs)
- Views\PurchaseRequests\Create.cshtml, Details.cshtml, Index.cshtml
- Views\Shared\_RequisitionWorkflowGuide.cshtml
- Scripts\app\purchase-request-create.js

## DLL / view stamps (EAT on IIS)
| Artifact | Size | LastWrite (EAT) |
|---|---:|---|
| AssetManagement.Web.dll | 579584 | 2026-09-30 11:22:46 |
| AssetManagement.Application.dll | 794112 | 2026-09-30 11:22:44 |
| AssetManagement.Infrastructure.dll | 402432 | 2026-09-30 11:22:45 |
| AssetManagement.Domain.dll | 90112 | 2026-09-30 11:04:49 |
| Create.cshtml | 8684 | 2026-09-30 11:21:02 |
| Details.cshtml | 9404 | 2026-09-30 09:05:16 |
| Index.cshtml | 3136 | 2026-09-30 09:05:27 |
| _RequisitionWorkflowGuide.cshtml | 3618 | 2026-09-30 09:05:27 |
| purchase-request-create.js | 20943 | 2026-09-30 09:05:51 |

SoT↔IIS DLL SHA256: Web/Application/Infrastructure **match**.

## Smoke (after elevated recycle)
| URL | Result |
|---|---|
| /L35160674/Account/Login | **200** Login |
| /L35160674/PurchaseRequests/Create | **302** → login (OK) |
| /L35160674/PurchaseRequests | **302** → login (OK) |
| /L35160674/PurchaseRequests/Details/1 | **302** → login (OK) |

Brief 503 right after first pool start; recycle fixed. **Smoke PASS.**

## Markers on IIS
Create: `PreviewApprovalPath`, `Requisition target`, `purchase-approval-path-banner`  
Details: `snapshotted on submit`, `Pinned:`  
Guide: Requisition flows / Inherit / Custom  

## READY_FOR_QA (Asset Management)
1. **Create path banner** — pick Admin→Sub-unit→Room (grandchild); banner shows Custom/Inherited/Matrix + stages; change target refreshes via PreviewApprovalPath.
2. **Grandchild rooms** — leaf list under branch for scoped user; cannot submit non-leaf; tagged asset on non-leaf warns and does not overwrite target.
3. **Details snapshot / self-approve** — snapshotted stages + current highlight + pinned name; eligible creator sees Approve stage; wrong role sees honest reason; pending inbox still Action Required for eligible self-approver.
4. **Guide** — Settings/Roles guide mentions self-approve + Requisition flows Inherit/Custom.

No GitHub PR/push. No CloudAgent.
