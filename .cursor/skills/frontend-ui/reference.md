# Frontend UI reference

## Shared partials (`Views/Shared/`)

| Partial | View model / notes |
|---------|-------------------|
| `_PageHeader` | `PageHeaderViewModel` — title, subtitle, breadcrumbs, back link, up to 3 header actions (URL or modal target) |
| `_Pagination` | `PaginationViewModel` — preserves query string, shows when `TotalPages > 1` |
| `_FormActions` | `FormActionsViewModel` — submit + optional back link |
| `_Alerts` | TempData success/error (included in layouts) |
| `_SidebarNav` | Main nav (layout) |
| `_ListBulkToolbar` | Bulk list operations |
| `_ApprovalActions`, `_ApprovalHistory`, `_ApprovalProcessMatrix` | Approval UI |
| `_AssetWorkflowContext` | Asset context banner on workflow forms |
| `_WorkflowFormScripts` | Shared workflow JS |
| `_CatalogAssetPickerModal`, `_AssetClassificationPickerModal`, `_AssetSubTypePickerModal` | Pickers |
| `_AttachmentUpload`, asset `_*Tab` partials | Asset detail tabs and uploads |

Platform area: `Areas/Platform/Views/Shared/_PlatformNav.cshtml`, `_PlatformUserList`, etc.

## View models (`AssetManagement.Web.ViewModels`)

- `PageHeaderViewModel`, `FormActionsViewModel`, `PaginationViewModel`
- `ListPageViewModel<T>` for paginated lists
- Breadcrumb items: label + optional URL (empty URL = current page)

## Helpers

- `Html.HasPermission("Resource.Action")` — show/hide buttons and links
- `Html.SortLink(...)` — sortable column headers on list pages
- `StatusHtmlHelpers.ToBadgeClass` — map bool/status to Bootstrap badge classes

## List/filter JS hooks

- `data-am-table-filter` on search inputs with `am-filterable-table` / `data-am-filterable` on tables (client-side filter where used)
