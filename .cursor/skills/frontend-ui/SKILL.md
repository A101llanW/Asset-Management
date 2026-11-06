---
name: frontend-ui
description: Builds and styles CodexAsset Web UI (Razor views, Bootstrap, am-* CSS). Use when adding or changing pages, forms, lists, partials, layouts, or styles under src/AssetManagement.Web/.
---

# Frontend UI (CodexAsset)

ASP.NET MVC 3 Razor + Bootstrap 5 + project CSS (`Content/css/`). Match existing pages before inventing new patterns.

## Before you finish

1. Follow [razor-controllers rule](.cursor/rules/razor-controllers.mdc): `@model` matches the action; grep views for `Model.`; set `ViewData` before partials that need it; valid `@if` / `else if` after `@Html.Partial`.
2. Gate actions with `@Html.HasPermission("Feature.Action")` (see existing Index/Create views).
3. Prefer shared partials over copy-paste markup.
4. Note in PR: full **AssetManagement.Web** build needs Windows/IIS; cloud agents validate with unit tests only unless manual UI test is run.

## Stylesheets

| File | Use |
|------|-----|
| `Content/css/site.css` | Tokens (`:root` `--am-*`), shell, tables, cards, most UI |
| `Content/css/am-modules.css` | Module/submodule nav chrome |
| `Content/css/am-animations.css` | Authenticated shell motion |
| `Content/css/auth-portal.css` | Login/auth only (`ViewBag.ShowLoginBackground`) |

- Reuse CSS variables (`--am-primary`, `--am-surface`, etc.); avoid hard-coded hex except where the codebase already does (e.g. impersonation bar).
- Class prefix: `am-` for app-specific; Bootstrap utilities for layout (`d-flex`, `gap-2`, grid).
- Cache-bust edits via existing `?v=@File.GetLastWriteTimeUtc` pattern in `_Layout.cshtml`.

## Standard page building blocks

**List page** (see `Views/Suppliers/Index.cshtml`):

- `@Html.Partial("_PageHeader", new PageHeaderViewModel { Title, Subtitle, PrimaryActionUrl, PrimaryActionLabel })`
- `<form class="am-list-toolbar" method="get">` with search + page size (default options often `10, 25, 50`; app convention ~10 rows + pagination)
- Empty: `card card-kpi` + `am-empty-state`
- Data: `card card-kpi` → `table am-filterable-table` + `@Html.SortLink` where sorting exists
- `@Html.Partial("_Pagination", new PaginationViewModel { Page, TotalPages, TotalCount, StartItem, EndItem })`

**Create/Edit form**:

- `_PageHeader` with `BackUrl` / `BackLabel` when applicable
- `@using (Html.BeginForm(...))` + Bootstrap `form-control` / `form-select`
- Validation: `@Html.ValidationMessageFor`, `@Html.ValidationSummary`
- `@Html.Partial("_FormActions", new FormActionsViewModel { SubmitLabel, BackUrl, BackLabel })`

**Details**: `_PageHeader`; use badges via `StatusHtmlHelpers.ToBadgeClass` for status chips.

**Workflow/asset flows**: reuse `_AssetWorkflowContext`, `_WorkflowFormScripts`, `_DepartmentDispositionPrompt` when the feature is asset-linked.

For partial names and view models, see [reference.md](reference.md).

## Layouts

- Tenant app: `Views/Shared/_Layout.cshtml` (sidebar, alerts, help bot)
- Platform admin: `Areas/Platform/Views/Shared/_PlatformLayout.cshtml`
- Print/public: `_PrintLayout`, `_PublicLayout`

Do not add new global script/CSS links in individual views unless the pattern already exists for that area (prefer layout or a shared partial).

## UX conventions

- Primary actions: `btn btn-primary am-click-spark`; secondary: `btn btn-outline-primary` or `btn-outline-secondary`
- Row actions: `btn btn-sm btn-outline-primary` / `Edit` secondary
- Preserve `returnUrl` on links that leave list context (`Request.RawUrl`)
- Tables: `table-light` header, `mb-0` on table inside card body `p-0`
- Accessible labels on icon-only controls; pagination uses `aria-label`

## Manual verification

When UI changes are non-trivial, exercise the page on IIS (`http://localhost:8080/nanosoft/...`) or document that Windows CI is required. Demo login: `nanosoft@asset.local` / `P@ssw0rd!` (MFA bypass in dev/cloud).
