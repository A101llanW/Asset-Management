# TESTWF-A461 Finding 3 — AssetSubTypes Index 404 (2026-09-29)

## Symptom
`/AssetSubTypes` and `/AssetSubTypes/Index` → ASP.NET 404.

## Root cause
`AssetSubTypesController` had Create/Edit/ByType/Lookup/CreateFromAsset only — **no Index action** and no `Views/AssetSubTypes/Index.cshtml`.

## Fix
Add `Index` action (filter by asset type / search / active) + Index view with **Edit** links per row. Register view in `.csproj`.

## Files
- `src/AssetManagement.Web/Controllers/AssetSubTypesController.cs` (Index)
- `src/AssetManagement.Web/Views/AssetSubTypes/Index.cshtml`
- `src/AssetManagement.Application/ViewModels/Assets/AssetSubTypeViewModels.cs` (`AssetSubTypeIndexItemVm`)
- `src/AssetManagement.Web/AssetManagement.Web.csproj`

## Base
`origin/main` (`44915ec`)
