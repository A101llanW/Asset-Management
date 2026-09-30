# REPORTS PREVIEW DBNull — DIG ONLY — 2026-09-29 EAT

**Product:** Nanosoft Asset Management (NIS / CodexAsset) — NOT HireHub  
**Tenant:** L35160674 (OrganizationId=10, Name=`asset-import-template`) on `http://localhost:8080/`  
**Repro symptom:** `POST /L35160674/Reports/Preview` → 200 JSON `{"success":false,"message":"Object cannot be cast from DBNull to other types."}`  
**ReportType:** `asset-register`  
**SoT:** `C:\Users\allan\Documents\Examples\CodexAsset\` (wins)  
**QA evidence:** `C:\Users\allan\AppData\Local\Temp\nis-reports-2026-09-29\BOARD.md` + `preview-post.html`  
**Scope:** dig only — no implement, no PR, no IIS publish

---

## Call chain (asset-register)

1. `ReportsController.Preview` (`src/AssetManagement.Web/Controllers/ReportsController.cs:30-51`)  
   - catches Exception → returns `{ success:false, message: ex.Message }` (matches QA JSON exactly).
2. `_reportService.GenerateReportDocument(model, …)`  
   → `ReportDocumentService` partial / `ReportService.GenerateReportDocument` switch `case "asset-register":`  
   → `BuildAssetRegisterContext` (`ReportDocumentService.cs:84-163`).
3. `BuildAssetRegisterContext` calls `_assetQueryService.StreamExport(filter, sort, direction, asset => { … })` (`:107`).
4. `AssetQueryService.StreamExport` (`AssetQueryService.cs:753-802`) runs `ExportSelectColumns` SQL including `a.[PurchaseDate]`, then `writeRow(MapExportRow(reader))` (`:787`).
5. **Throws inside `MapExportRow` before any preview HTML is built.**

Client: `Scripts/app/reports.js` expects `{ success, html, title, rowCount }` — never reached on success path.

---

## CONFIRMED cause

**`Convert.ToDateTime(record["PurchaseDate"])` in `MapExportRow` throws `InvalidCastException` ("Object cannot be cast from DBNull to other types.") when `Asset.PurchaseDate` is SQL NULL.**

| Rank | File:line | Cast | Throws for L35160674? |
|------|-----------|------|------------------------|
| **1 PRIMARY** | `src/AssetManagement.Infrastructure/Queries/AssetQueryService.cs:827` | `PurchaseDate = Convert.ToDateTime(record["PurchaseDate"])` | **YES** — 540/540 active rows NULL |
| 2 | same file `:826` | `AcquisitionCost = Convert.ToDecimal(record["AcquisitionCost"])` | No for this tenant (column NOT NULL; values `0.00`) — still null-unsafe |
| 3 | same file `:822` | `(AssetStatus)Convert.ToInt32(record["CurrentStatus"])` | No for this tenant (column NOT NULL) — still null-unsafe |

Strings already safe via `SqlQueryHelper.GetString` (`:820-825, :828`).

---

## SQL / data evidence (read-only, localhost\SQLEXPRESS / AssetManagementModuleDb)

Schema (`INFORMATION_SCHEMA`):

- `Asset.PurchaseDate` **datetime NULL** (nullable)
- `Asset.AcquisitionCost` **decimal NOT NULL**
- `Asset.CurrentStatus` **int NOT NULL**

Tenant L35160674 → OrganizationId **10**:

| Metric | Value |
|--------|-------|
| Active assets (`IsActive=1`) | **540** |
| `PurchaseDate IS NULL` | **540** (100%) |
| `AcquisitionCost IS NULL` | **0** |
| `CurrentStatus IS NULL` | **0** |

Sample NULL-PurchaseDate rows: Id 6501+ (`LPWBZ3DKEY`, `3MVM25B6A7`, …) AcquisitionCost=`0.00`, CurrentStatus=`5`, SerialNumber NULL (string path OK).

Domain mismatch: `Asset.PurchaseDate` / `AssetExportRowVm.PurchaseDate` are non-nullable `DateTime` in C#, but DB allows NULL (import path left dates unset).

---

## Secondary note (after MapExportRow is fixed)

`BuildAssetRegisterContext` (`ReportDocumentService.cs:109`) filters:

```csharp
if (asset.PurchaseDate.Date < period.From || asset.PurchaseDate.Date > period.To) return;
```

If nulls are coalesced to `default(DateTime)` / `DateTime.MinValue`, **all 540 rows may be excluded** by the period filter → empty successful preview. Fix must define intentional semantics for NULL purchase dates (include with blank date vs exclude vs backfill data).

Related list mapper (`AssetQueryService.cs:996`) also does unsafe `Convert.ToDecimal(AcquisitionCost)` but does **not** read `PurchaseDate` — not this Preview failure.

---

## Diagnosis: **both**

1. **Code null-safety gap (root of exception):** `MapExportRow` uses `Convert.ToDateTime` / `Convert.ToDecimal` / `Convert.ToInt32` without `DBNull` / `IsDBNull` checks, unlike `GetString`.
2. **Data issue (trigger for this tenant):** bulk-imported assets for L35160674 have **no** `PurchaseDate` (540 NULLs) — valid per schema, fatal per mapper.

---

## FIX PLAN (do NOT implement until Allan authorizes)

1. **Harden `MapExportRow` (`AssetQueryService.cs:816-829`):** before `Convert.ToDateTime`, treat `record["PurchaseDate"] == DBNull.Value` (or `IsDBNull`) — prefer adding `SqlQueryHelper.GetDateTime` / `GetDecimal` / `GetInt32` helpers mirroring `GetString`, returning nullable or explicit defaults.
2. **Choose NULL PurchaseDate semantics for asset-register:** (A) map to `DateTime?` on `AssetExportRowVm` and show blank; period filter skips nulls as “include” OR “exclude” — document choice; (B) coalesce to a sentinel only if product agrees; do **not** silently use `DateTime.MinValue` without updating the period filter.
3. **Defensive casts:** also guard `AcquisitionCost` and `CurrentStatus` in the same mapper even though currently NOT NULL.
4. **Optional data remediation (separate from code):** backfill `PurchaseDate` for Org 10 import rows if business has a known acquisition date; do not invent dates in dig.
5. **Regression:** unit/integration test — reader row with `PurchaseDate=DBNull` must not throw; Preview JSON `success:true` for L35160674 asset-register (rowCount per chosen semantics).
6. **Out of scope for this dig:** IIS publish, GitHub PR, other ReportTypes (same MapExportRow is shared by CSV export paths that call `StreamExport`).

---

## Paths

| Item | Path |
|------|------|
| Board (SoT) | `C:\Users\allan\Documents\Examples\CodexAsset\Docs\REPORTS-PREVIEW-DBNULL-2026-09-29.md` |
| Board (box) | `/workspace/nis-debug-2026-09-08/REPORTS-PREVIEW-DBNULL-2026-09-29.md` |
| PRIMARY code | `AssetQueryService.cs:827` (`MapExportRow`) |
| Entry | `ReportsController.cs:30` Preview |
| Builder | `ReportDocumentService.cs:84` `BuildAssetRegisterContext` |

**READY_FOR_IMPLEMENT?** no — Allan must authorize.
