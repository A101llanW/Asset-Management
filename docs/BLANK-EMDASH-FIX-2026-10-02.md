# Blank em-dash fix — 2026-10-02

**When:** 2026-10-02 16:40:10 EAT (Africa/Nairobi)  
**SoT:** `C:\Users\allan\Documents\Examples\CodexAsset\` (machine alLAN)  
**Dig:** `docs\BLANK-EMDASH-MOJIBAKE-DIG-2026-10-02.md`  
**Authorized:** Allan via PM — blank U+2014 placeholders → empty string; IIS publish; no GitHub PR; skip Zebra/printer.

## Change summary

Replace empty-cell U+2014 em dash (`—`, UTF-8 `E2 80 94`) with `""` / blank so UI cells cannot mojibake to `â€"`. Prefer empty string over ASCII hyphen. Keep intentional separators / prose (dig §5).

### Helpers
| File | Change |
|------|--------|
| `DepartmentPicDisplay.cs` | `Empty = ""` (was U+2014) |
| `ReportHtmlBuilder.cs` | Blank fillers at L145/185/187/198/281 → `""`; **kept** L181 AssetTag — AssetName separator |
| `DisplayText.Empty` | Already `""` — unchanged |

### Views
| File | Change |
|------|--------|
| `Assignments/BatchCreate.cshtml` | Serial ternary empty → `""` |
| `Departments/Index.cshtml` | Flow summary ternaries + bare `<td>` blanks → empty |
| `AssetSubTypes/Index.cshtml` | Brand/Model + Category blanks → `""` |
| `Roles/Index.cshtml` | workflowNote coalesce → `""` |
| `Shared/_AssetAuditTab.cshtml` | IP blank → `""` |
| `Suppliers/Create.cshtml` | tagged-asset option placeholder → empty |
| `Suppliers/Details.cshtml` | Category/Type/SKU/LeadTime/subtype/option blanks → empty |
| `UserInvitations/Index.cshtml` | Email/Role/Department blanks → `""` |

### Scripts
| File | Change |
|------|--------|
| `assets-grouped.js` | `config.emptyLabel \|\| "—"` → `config.emptyLabel != null ? config.emptyLabel : ""` (respects `DisplayText.Empty` `""`) |
| `supplier-create.js` | option + subtype/serial blanks → `""` |
| `purchase-request-create.js` | SubTypeName blank L361 → `""`; prose L467 left |
| `purchase-create.js` | LeadTimeDays blank → `""` |
| `receive-serial-wizard.js` | PO/supplier/item/brand/model/subtype/cost blanks → `""` |

### Controllers (JSON blanks only)
| File | Change |
|------|--------|
| `PurchaseRequestsController.cs` | custodianName / subTypeName JSON blanks L348/355 → `""`; prose L151 left |
| `SuppliersController.cs` | subTypeName / custodianName JSON blanks L81/84 → `""` |

## Build
- Domain / Application / Infrastructure / Web **Release** — **PASS**
- Full solution Tests project has pre-existing failures (unrelated) — not blocking publish

## IIS publish
- **When:** 2026-10-02 ~16:41:35–16:41:47 EAT
- **Bak:** `C:\inetpub\AssetManagement\_bak\blank_emdash_20261002_164135`
- **Web.config:** preserved (SHA256 `3B664DD516C7A5CE973A0658C97895C695AAD9571558178BE222E3EC2B5339F7` unchanged)
- **Pool:** AssetManagement stopped → copied → Started
- **Commit published:** `c7d26515bc1b41afb96062582d2c2ad2678d4851`
- SoT↔IIS DLL SHA256: **match** (Web/Application/Domain/Infrastructure)
- Markers on IIS: BatchCreate Serial no U+2014; `assets-grouped.js` uses `emptyLabel != null`

### IIS DLL / key view stamps (EAT)
| Artifact | Size | LastWrite (EAT) |
|---|---:|---|
| AssetManagement.Web.dll | 552448 | 2026-10-02 16:39:28 |
| AssetManagement.Application.dll | 772096 | 2026-10-02 16:39:26 |
| AssetManagement.Infrastructure.dll | 387584 | 2026-10-02 16:39:27 |
| AssetManagement.Domain.dll | 88576 | 2026-10-02 16:39:25 |
| BatchCreate.cshtml | 8288 | 2026-10-02 16:38:51 |
| Departments/Index.cshtml | 15704 | 2026-10-02 16:38:51 |
| assets-grouped.js | 22369 | 2026-10-02 16:38:51 |

## Smoke (brief HTTP)
| URL | Result |
|---|---|
| /L35160674/Account/Login | **200** |
| /L35160674/Assignments/BatchCreate | **302** → login (OK) |
| /L35160674/Departments | **302** → login (OK) |
| /L35160674/Assets | **302** → login (OK) |
| /L35160674/AssetSubTypes | **302** → login (OK) |
| /L35160674/Suppliers | **302** → login (OK) |
| /L35160674/PurchaseRequests/Create | **302** → login (OK) |

**Smoke PASS.** No GitHub PR/push. Zebra/printer skipped.
## SoT DLL stamps (pre-publish)
| AssetManagement.Application.dll | 772096 | 2026-10-02 16:39:26 |
| AssetManagement.Domain.dll | 88576 | 2026-10-02 16:39:25 |
| AssetManagement.Infrastructure.dll | 387584 | 2026-10-02 16:39:27 |
| AssetManagement.Web.dll | 552448 | 2026-10-02 16:39:28 |

## READY_FOR_QA checklist
1. **BatchCreate Serial empty** — Assignments/BatchCreate row with blank Serial shows empty cell (not `—` / `â€"`).
2. **Departments/Index owner** — Class teacher / Room custodian empty → blank; flow summary empty → blank; non-flow kind bare cells blank.
3. **Assets Index grouped** — empty member cells use `emptyLabel` `""` (no JS em-dash fallback).
4. **AssetSubTypes/Index** — missing Brand/Model or Category → blank.
5. **Suppliers/Details** — missing Category/Type/SKU/Lead time / empty subtype → blank.
6. **PurchaseRequests/Create** — asset search row SubTypeName empty → blank (JSON + JS).