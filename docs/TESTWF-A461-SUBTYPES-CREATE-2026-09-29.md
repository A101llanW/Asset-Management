# TESTWF-A461 Finding 4 — AssetSubTypes Create needs assetTypeId (2026-09-29)

## Symptom
`/AssetSubTypes/Create` without `assetTypeId` fails (Int32 required / not found).
Assets/Create classification picker PASS. Assets Index edit probe SKIP_NO_EDIT_LINK (list already has Edit when Assets.Edit).

## Root cause
`Create(int assetTypeId, ...)` required non-nullable Int32; no type-picker landing page for bare Create URL.

## Fix
`Create(int? assetTypeId = null)` — when missing, show `CreateSelectType` dropdown then continue. Subtype Edit remains on Edit action; Index PR adds list Edit links.

## Files
- `src/AssetManagement.Web/Controllers/AssetSubTypesController.cs`
- `src/AssetManagement.Web/Views/AssetSubTypes/CreateSelectType.cshtml`
- `src/AssetManagement.Web/AssetManagement.Web.csproj`

## Base
`origin/main` (`44915ec`)
