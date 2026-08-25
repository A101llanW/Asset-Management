# JSON actions (MVC UI endpoints)

This application is **ASP.NET MVC 3**, not a standalone Web API. JSON responses are returned by selected controller actions to support same-origin browser UI (`fetch`, jQuery). **CORS is not configured**; these endpoints are not intended for cross-origin SPAs unless a dedicated API is added later.

## Semi-public endpoints (hardened)

| Action | Method | Tenant URL required | Auth | Purpose |
|--------|--------|---------------------|------|---------|
| `/{tenant}/AssetScan/LookupJson` | GET | Yes | No | Scan lookup JSON (rate-limited; minimal payload when anonymous) |
| `/{tenant}/Captcha/Generate` | GET | Recommended on tenant login | No | Create captcha challenge |
| `/{tenant}/Captcha/Refresh` | GET | Recommended on tenant login | No | Refresh captcha |
| `/{tenant}/Captcha/Validate` | POST | Recommended on tenant login | No | Validate captcha (anti-forgery + rate limit) |

Platform login at `/Account/Login` may use `/Captcha/*` without a tenant prefix when captcha is enabled.

## Authenticated JSON actions

| Controller | Action | Method | Permission / notes |
|------------|--------|--------|-------------------|
| `Account` | `SendSetupMfaCode` | POST | MFA setup session |
| `Account` | `ResendMfaCode` | POST | MFA verify session |
| `Dashboard` | `GetPendingRequests` | GET | Company admin; impersonation requests |
| `Dashboard` | `GetImpersonationStatus` | GET | Support session lock polling |
| `Dashboard` | `GetMyImpersonationStatus` | GET | Impersonation countdown |
| `Dashboard` | `CheckRequestStatus` | GET | Impersonation request status |
| `Platform/Organizations` | `GetMyImpersonationStatus` | GET | Platform impersonation countdown |
| `Platform/Organizations` | `CheckRequestStatus` | GET | Platform request status |
| `Platform/Organizations` | `RequestImpersonation` | POST | AJAX JSON when `X-Requested-With: XMLHttpRequest` |
| `Platform/Organizations` | `CancelImpersonationRequest` | POST | AJAX JSON when XMLHttpRequest |
| `AssetRequests` | `AvailableAssets` | GET | `Assets.Request` |
| `Suppliers` | `AvailableAssetTypes` | GET | `Suppliers.View` |
| `Suppliers` | `AvailableTaggedAssets` | GET | `Suppliers.View` |
| `PurchaseRequests` | `AvailableTargetAssets` | GET | `Purchases.Create` |
| `PurchaseRequests` | `DocumentFragment` | GET | `Purchases.View` |
| `Purchases` | `SupplierPriceComparison` | GET | `Purchases.View` |
| `Roles` | `TemplatePermissions` | GET | `Roles.Create` |
| `Reports` | `Preview` | POST | `Reports.View` |

## Implicit JSON responses

| Source | When | Shape |
|--------|------|-------|
| `BusinessExceptionFilter` | AJAX + business rule failure | `{ error: "..." }` |
| `TenantFilterAttribute` | License expired/suspended on API-like request | `{ error: "license_expired" \| "license_suspended" }` |

## Client scripts

| Script | Endpoints |
|--------|-----------|
| `asset-scan.js` | `LookupJson` |
| `_CaptchaPartial.cshtml` | `Captcha/*` |
| `impersonation-ui.js` | Dashboard / Platform impersonation JSON |
| `asset-request-create.js` | `AvailableAssets` |
| `supplier-create.js` | Supplier lookup JSON |
| `purchase-create.js` | `SupplierPriceComparison` |
| `purchase-request-create.js` | `AvailableTargetAssets` |
| `purchase-request-details.js` | `DocumentFragment` |
| `reports.js` | `Reports/Preview` |
| `role-templates.js` | `TemplatePermissions` |

## Security notes

- Prefer tenant-scoped URLs: `https://host/{tenant}/Controller/Action`.
- Do not add blanket `Access-Control-Allow-Origin` in `Web.config`.
- Scan and captcha endpoints are rate-limited per client IP (and tenant for scan).
