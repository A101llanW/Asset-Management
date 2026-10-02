# Blank em-dash mojibake dig — 2026-10-02

**Scope:** DIG ONLY (no code change, no IIS, no GitHub, no printer/Zebra).  
**SoT:** `C:\Users\allan\Documents\Examples\CodexAsset\` (machine `alLAN` / `1572f63d-d693-4e0e-b376-d0cfd8950a9d`)  
**Symptom:** Blank UI cells show mojibake `â€"` (= UTF-8 em dash U+2014 `—` bytes `E2 80 94` misread as Windows-1252/Latin-1).

---

## 1. Shared helper(s) / pattern

### Central sources (real U+2014 in source)

| Role | File:line | What it is |
|------|-----------|------------|
| **Shared const (PIC/owner blanks)** | `src/AssetManagement.Application/Helpers/DepartmentPicDisplay.cs:33` | `public const string Empty = "—";` (bytes `E2 80 94`). Returned by `FormatRequisitionOwner` when no teacher/custodian. |
| **Shared HTML detail-row blank** | `src/AssetManagement.Application/Helpers/ReportHtmlBuilder.cs:281` | `Encode(string.IsNullOrWhiteSpace(value) ? "—" : value)` — blank filler for requisition HTML documents; also literals at :145, :185, :187, :198. |
| **JS grouped-assets fallback** | `src/AssetManagement.Web/Scripts/app/assets-grouped.js:108` | `return config.emptyLabel \|\| "—";` |

### Important non-source: `DisplayText.Empty`

| File:line | Value | Notes |
|-----------|-------|-------|
| `src/AssetManagement.Application/DisplayText.cs:9` | `public const string Empty = "";` | **Empty string, not em dash.** ~62 call sites use `DisplayText.Empty` for blanks (Assets/Index, Details, Scan, Search, Users, PurchaseRequests, Platform views, etc.). Those Razor sites render **blank**, not `—`. |

**Catch:** `Assets/Index.cshtml` passes `emptyLabel: DisplayText.Empty` (`""`) into `assets-grouped.js`. In JS, `""` is falsy, so `config.emptyLabel \|\| "—"` **still emits the literal U+2014** from the script file. Grouped Assets empty cells therefore still mojibake even though the C# constant is `""`.

### Not found

- **No** `NullDisplayText` / MVC display-template NullDisplayText usage anywhere under `src/`.
- **No** `&mdash;` / `&#8212;` / `&#x2014;` used as empty-cell placeholders (only Search result-count separator — see §5).
- **No** single OrDash/FormatEmpty helper covering all screens — most blanks are **scattered ternary/coalesce literals** with a real U+2014 in the `.cshtml` / `.js` / controller JSON.

### Encoding note (config vs observed mojibake)

- `Web.config` has `<globalization requestEncoding="utf-8" responseEncoding="utf-8" ... />`.
- Layouts declare `<meta charset="utf-8" />` (`_Layout`, `_PublicLayout`, `_PrintLayout`, `_PlatformLayout`).
- Blank-placeholder sources store **clean UTF-8 U+2014** (`E2 80 94`), not pre-corrupted `â€"` sequences (except unrelated prose — see §4).
- Console/tools often *display* U+2014 as ASCII `-`; byte dumps were used to confirm `E2 80 94`.

---

## 2. Full screen list

Blank/null/empty → U+2014 dash (mojibake risk). Printer/Zebra paths omitted.

| Route / Views path | Column / field | How empty dash is produced (file:line) |
|--------------------|----------------|----------------------------------------|
| **Assignments/BatchCreate** | Serial | Razor ternary `IsNullOrWhiteSpace(row.SerialNumber) ? "—" : …` — `Views/Assignments/BatchCreate.cshtml:118` |
| **Departments/Index** | Class teacher / Room custodian (owner col) | `DepartmentPicDisplay.FormatRequisitionOwner` → `Empty` — helper `DepartmentPicDisplay.cs:33`; call sites Index.cshtml:117,123,147,153,174,180,216,222 |
| **Departments/Index** | Effective requisition flow summary | Ternary → `"—"` — Index.cshtml:118,148,175,217 |
| **Departments/Index** | Bare empty table cells | Literal `<td>—</td>` — Index.cshtml:122,124,152,154,179,181,221,223 |
| **AssetSubTypes/Index** | Brand/Model combined; Category | Ternary / `?? "—"` — Index.cshtml:74,76 |
| **Roles/Index** | Workflow note | `workflowNote ?? "—"` — Index.cshtml:69 |
| **Assets/Details** (Audit tab) | IP address | `_AssetAuditTab.cshtml:34` via `Assets/Details.cshtml` partial |
| **Assets/Index** (grouped JS members) | emptyLabel fallback cells | `assets-grouped.js:108` (`\|\| "—"` because `DisplayText.Empty` is `""`) — wired from Index.cshtml:~442 |
| **Suppliers/Create** | Tagged-asset `<option>` placeholder | Create.cshtml:138; also JS `supplier-create.js:264` |
| **Suppliers/Details** | Category, Type, SKU, Lead time, empty subtype `<text>`, option | Details.cshtml:127,128,139,142,144,216; JS subtype/serial `supplier-create.js:424,425` |
| **UserInvitations/Index** | Email, Role, Department | Index.cshtml:52,53,54 |
| **PurchaseRequests/Create** (asset search row) | SubTypeName in JS table | `purchase-request-create.js:361` (`\|\| "—"`); server JSON also uses em dash — `PurchaseRequestsController.cs:348,355` |
| **Purchases/Create** | LeadTimeDays cell | `purchase-create.js:118` (`: '—'`) |
| **Purchases/Receive** (serial wizard modal) | PO, supplier, item, brand, model, subtype; currency helper | `receive-serial-wizard.js:51,56,57,58,60,61,62` |
| **SuppliersController** JSON (tagged-asset search) | subTypeName, custodianName | SuppliersController.cs:81,84 |
| **Report HTML** (requisition document) | Department, Qty in stock, Required by, Justification, generic detail row | ReportHtmlBuilder.cs:145,185,187,198,281 |

### Related but NOT producing em-dash blanks today

| Path | Why listed |
|------|------------|
| Many screens using `DisplayText.Empty` (Assets Index/Details/Scan/Search, Users, AssetRequests, PurchaseRequests Details/Index, Platform Users/Orgs/Licenses, Claims/Incidents tags, document/incident/maintenance tabs, etc.) | Constant is `""` at `DisplayText.cs:9` — blanks render empty, not `—`. |
| `Departments/RequisitionFlows.cshtml:72` | Same ternary pattern but placeholder is **ASCII hyphen** `0x2D`, not U+2014 — will not produce `â€"` mojibake. |

**Approx. screens/views directly emitting U+2014 blanks:** ~14 UI surfaces (+ requisition HTML report builder + 2 JSON controller payloads). **~17 source files** contain blank-placeholder U+2014 (helpers/views/js/controllers).

---

## 3. BatchCreate Serial specifically

**Exact site:** `src/AssetManagement.Web/Views/Assignments/BatchCreate.cshtml:118`

```cshtml
<td>@(string.IsNullOrWhiteSpace(row.SerialNumber) ? "—" : row.SerialNumber)</td>
```

- Bytes at the quoted placeholder: **`E2 80 94`** (UTF-8 encoding of U+2014). Confirmed by offset scan (one em dash in this file, at that ternary).
- Route: `AssignmentsController.BatchCreate` → view `BatchCreate` (GET ~line 184, POST ~207).
- When `row.SerialNumber` is null/whitespace, Razor emits the em dash into the HTML cell. If that response (or any hop) is interpreted as Windows-1252/Latin-1, the browser shows **`â€"`** (classic UTF-8→cp1252 mojibake of `E2 80 94`).
- This is a **local literal**, not `DisplayText.Empty` / `DepartmentPicDisplay.Empty`.

---

## 4. Root cause hypothesis

**Primary:** Source contains a **real U+2014** em dash used as the empty placeholder. It is **not** (for blank cells) already stored as mojibake bytes `â€"`.

Evidence:

1. BatchCreate.cshtml:118 and DepartmentPicDisplay.cs:33 hex = `E2 80 94`.
2. Dozens of other blank ternaries/`\|\|`/`??` use the same code point.
3. `DisplayText.Empty` was set to `""` (UTF-8 BOM file, value length 0) — intentional move away from a dash for the shared helper, but **literals and `DepartmentPicDisplay.Empty` were left as U+2014**.
4. App declares UTF-8 (`Web.config` globalization + `<meta charset="utf-8">`). Declared encoding alone does not guarantee every response path is decoded as UTF-8 (missing/wrong `Content-Type` charset at IIS, intermediate transform, or client forced encoding would still produce `â€"` from clean `E2 80 94`).
5. **DIG only — no IIS/runtime Content-Type capture in this pass.** Config *claims* utf-8; UI symptom implies a decode mismatch somewhere downstream of the UTF-8 source bytes.

**Secondary (already-corrupted prose, not blank placeholders):** some files already contain mojibake *in source* for narrative text (not the blank-cell pattern):

| File:line | Note |
|-----------|------|
| `Views/Purchases/Receive.cshtml:61,108` | Prose already shows corrupted dash sequences in file |
| `Controllers/DepartmentsController.cs:611` | `"Bulk grades 1A…"` text already corrupted |
| `Helpers/AssetSubTypeNormalizer.cs:34–40` | **Intentional** normalizer replacing `\u2014` and known mojibake sequences with ASCII `-` for subtype names (import/display cleanup) — proves the team already knows UTF-8 dash↔cp1252 mojibake |

**NullDisplayText / Response Encoding as sole cause?** Unlikely as the *source* of the character: the character is authored into views/JS. Encoding config is relevant to *whether* U+2014 survives to the browser as `—` vs `â€"`, not to why a dash appears for blanks.

---

## 5. Out of scope / intentional dashes

### Skipped (per brief)

- All **printer / Zebra / label-print** paths (`asset-label-zebra.js`, `asset-scan-batch-print.js`, label views/controllers, etc.) — not inventoried as blank-UI cells.

### Intentional separators / prose (U+2014 or `&mdash;`, not empty placeholders)

Examples (not exhaustive):

| Location | Use |
|----------|-----|
| `Views/Search/Index.cshtml:33` | `&mdash; @Model.TotalCount result(s) found` — result-count separator |
| `Views/Roles/Create.cshtml:50,70` | `— Select a role —` / `— Select a template —` — select chrome |
| `Views/Claims/Index.cshtml` / `Incidents/Index.cshtml` | `AssetTag — AssetName` style join (may mix DisplayText.Empty with separator) |
| `BaseController.cs:902`, approval pickers, disposal tab stage labels, Login DEMO banners, Import instructions, many `// comment — …` lines | Prose / title / status separators |
| `AssetImportTemplateInstructions.cs` | Instruction list using em dashes |
| `AssetSubTypeNormalizer` | Strips em/en dashes from names (data normalize, not UI blank) |

### ASCII hyphen blanks (no `â€"` from U+2014)

- `Departments/RequisitionFlows.cshtml:72` — placeholder `"-"` is `0x2D`.

---

## Dig method (evidence)

- Byte scan under `src/` for UTF-8 `E2 80 94`; classify lines as blank-placeholder vs intentional.
- `Select-String` for `NullDisplayText`, `&mdash;`, `DisplayText`, `DepartmentPicDisplay`, BatchCreate, JS `\|\|` fillers.
- Hex confirmation on BatchCreate.cshtml and DepartmentPicDisplay.cs.
- Read `DisplayText.cs` / `Web.config` globalization / layout charset.
- Printer/Zebra filename patterns excluded from blank inventory.

**No code changes made.**