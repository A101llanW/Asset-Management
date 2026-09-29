# REPORTS PREVIEW DBNull — FIX — 2026-09-29 EAT

**Product:** Nanosoft Asset Management (NIS / CodexAsset)
**Tenant trigger:** L35160674 (OrganizationId=10) — 540/540 active assets `PurchaseDate` SQL NULL
**SoT:** `C:\Users\allan\Documents\Examples\CodexAsset\`
**IIS:** `C:\inetpub\AssetManagement`
**Related dig:** `Docs\REPORTS-PREVIEW-DBNULL-2026-09-29.md`

---

## What changed

1. **`SqlQueryHelper`** — added `GetDateTimeNullable`, `GetDecimal` (DBNull → default), `GetInt32` (DBNull → default).
2. **`MapExportRow`** (`AssetQueryService.cs`) — null-safe:
   - `PurchaseDate` via `GetDateTimeNullable`
   - `AcquisitionCost` via `GetDecimal`
   - `CurrentStatus` via `GetInt32`
3. **`AssetExportRowVm.PurchaseDate`** — `DateTime` → `DateTime?`
4. **Consumers** — CSV / report HTML / `BuildAssetRegisterContext` format blank when null.
5. **Period filter semantics (asset-register):** **INCLUDE** rows with null/blank `PurchaseDate`; apply From/To only when `PurchaseDate` has a value. **Do not** coerce to `DateTime.MinValue`.
6. **No DB backfill** of purchase dates.

---

## Period semantics

| PurchaseDate | Period From/To | Included in asset-register preview? |
|--------------|----------------|-------------------------------------|
| SQL NULL / C# null | any | **YES** (blank Purchased cell) |
| has value inside range | applies | YES |
| has value outside range | applies | NO |

---

## Publish

| Item | Value |
|------|-------|
| Result | **PASS** |
| Build | Release rebuild via `tools\deploy\Build-WebForIis.ps1` |
| Deploy | Elevated robocopy mirror (Web.config / secrets excluded via `/XF`) + app pool recycle |
| Pool | Started |
| Smoke | `http://127.0.0.1:8080/nanosoft/Account/Login` → 200 |

### DLL stamps (EAT)

| DLL | Path (IIS) | mtime EAT | Length |
|-----|------------|-----------|--------|
| Infrastructure | `C:\inetpub\AssetManagement\bin\AssetManagement.Infrastructure.dll` | 2026-09-29 10:09:32 | 385024 |
| Application | `C:\inetpub\AssetManagement\bin\AssetManagement.Application.dll` | 2026-09-29 10:09:31 | 733184 |
| Web | `C:\inetpub\AssetManagement\bin\AssetManagement.Web.dll` | 2026-09-29 10:09:32 | 529408 |
| Domain | `C:\inetpub\AssetManagement\bin\AssetManagement.Domain.dll` | 2026-09-29 10:09:30 | 85504 |

SoT bin mirrors the same stamps under `src\AssetManagement.Web\bin\`.

---

## Files changed (code)

- `src\AssetManagement.Infrastructure\Queries\SqlQueryHelper.cs`
- `src\AssetManagement.Infrastructure\Queries\AssetQueryService.cs`
- `src\AssetManagement.Application\ViewModels\Assets\AssetExportViewModels.cs`
- `src\AssetManagement.Application\Services\ReportDocumentService.cs`
- `src\AssetManagement.Application\Services\ReportService.cs`
- `Docs\REPORTS-PREVIEW-FIX-2026-09-29.md` (this board)

---

## QA next

- POST `/L35160674/Reports/Preview` reportType=`asset-register` → expect `success:true`, rowCount reflecting **included** null-PurchaseDate rows (up to export max), not DBNull cast error.
- Confirm Purchased column blank for null dates.
- Confirm dated assets still respect period From/To.

**READY_FOR_QA:** yes
