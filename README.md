# Asset Management Module (ASP.NET MVC 5, .NET Framework 4.7)

Enterprise asset lifecycle, chain-of-custody, permission-driven, audit-ready module.

## Solution Structure

- `AssetManagementModule.sln`
- `src/AssetManagement.Web`
  - ASP.NET MVC 5 web application (Razor, Bootstrap, jQuery)
  - Authentication, authorization filters, controllers, views
- `src/AssetManagement.Application`
  - Service contracts and business services
  - ViewModels and application-level rules
- `src/AssetManagement.Domain`
  - Core entities and enums
  - Audit base model
- `src/AssetManagement.Infrastructure`
  - EF6 DbContext + migrations seed
  - Identity user model
  - Repository/UnitOfWork
  - Audit/file/auth services
- `tests/AssetManagement.Tests`
  - MSTest unit tests for core business rules

## Implemented Scope

### Phase 1 (Completed)
- Authentication scaffold (login/logout/forgot/reset)
- Dynamic roles + permissions (editable in DB)
- Permission-based action protection (`[PermissionAuthorize("Module.Action")]`)
- Departments, suppliers, users (role assignment), permission catalog
- Asset CRUD with key validation rules
- Tabbed asset details page with custody timeline support
- Audit logging service + audit log report screen
- Purchase record capture (foundation)
- EF6 code-first model + seed data

### Phase 2 (Expanded)
- Assignment, transfer, return services/controllers/views
- Canonical custody event stream (`AssetCustodyEvent`) with previous-custodian context
- Hardened custody workflow rules (status checks, actor consistency, transfer delta validation)
- Workflow forms upgraded from raw IDs to user/department selects
- Controller-level business exception handling for workflow actions

### Phase 3 (Expanded)
- Maintenance, incident, and claim workflows hardened with business-rule validation and controller exception handling
- Phase 3 forms upgraded to enum-driven dropdowns with validation feedback
- Depreciation engine improved with month-level idempotency and depreciation start-date gating
- Notification engine upgraded to generate warranty/insurance/due/overdue alerts with duplicate suppression
- Asset details page and navigation now expose maintenance/incident/claim/notification workflows directly
- Service-level tests expanded to cover new Phase 3 rules and safeguards

### Phase 4 foundations (Included)
- Disposal/insurance/depreciation entities and schema support for upcoming financial lifecycle extensions

## Key Business Rules Enforced

- `AssetTag` uniqueness
- `SerialNumber` uniqueness when provided (index + filtered migration SQL hook)
- Disposed assets cannot be assigned
- Lost/stolen assets cannot be transferred unless recovered
- Temporary assignment requires expected return date
- Assignment/transfer/return writes custody events
- Claims can link to incidents
- Critical writes create audit logs
- Depreciation stops for retired/disposed assets
- Super Admin role has full permission bypass; other access is permission-driven

## Seed Data

The migration seed adds:
- Permissions catalog (`Module.Action` style)
- Roles: `Super Admin` + editable demo roles
- Departments, suppliers, categories, types
- Demo users
- At least 10 sample assets
- Custody events, depreciation records, sample incident + claim

## Demo Credentials

- `superadmin@asset.local` / `P@ssw0rd!`
- Additional seeded users include asset manager, procurement, finance, staff, auditor.

## Local Setup (Visual Studio 2019/2022)

1. Open `AssetManagementModule.sln` in Visual Studio.
2. Ensure `.NET Framework 4.7 Developer Pack` and `ASP.NET and web development` workload are installed.
3. Ensure SQL Server Express is installed (`.\SQLEXPRESS`), or use LocalDB if you prefer.
4. Update connection string in `src/AssetManagement.Web/Web.config` if needed:
   - `AssetManagementConnection`
5. Open **Package Manager Console**:
   - Set default project: `AssetManagement.Infrastructure`
   - Run:
     - `Enable-Migrations` (if needed)
     - `Update-Database`
6. Set `AssetManagement.Web` as startup project.
7. Run with IIS Express.

## Build/Test Status in This Environment

- `dotnet build` succeeds for:
  - `AssetManagement.Domain`
  - `AssetManagement.Application`
  - `AssetManagement.Infrastructure`
  - `AssetManagement.Tests`
- `dotnet test` passes (14/14 tests).
- MVC5 web project build requires full Visual Studio WebApplication targets and MVC5 assemblies; use Visual Studio build/run (not `dotnet` CLI alone) for web runtime.

## Important Notes for Production Hardening

- Replace placeholder forgot/reset password email flow with secure tokenized email provider.
- Add anti-XSS/content-security hardening and strict upload validation.
- Add robust pagination/search on large lists.
- Expand unit/integration coverage for approval workflows and reporting exports.
- Add dedicated reporting export pipeline (ClosedXML/PDF library) where required.

## SQL Server Guidance

See `docs/SQL_SERVER_CONFIGURATION.md`.
