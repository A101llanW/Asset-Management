# Production Security Phase B — 2026-10-05

- **SoT:** `C:\Users\allan\Documents\Examples\CodexAsset\`
- **Scope:** LOCAL SoT only. **IIS publish HOLD** — `C:\inetpub\AssetManagement` was not modified.
- **Out of scope this pass:** GitHub PR/push/merge, CloudAgent, Zebra, demo MFA off, customErrors/YSOD, HTTPS flags, test-tenant purge, data deletes.

Related audit: `docs/PROD-SECURITY-AUDIT-2026-10-05.md`

---

## Status summary

| Item | Status | Notes |
|------|--------|-------|
| #2 Seeded accounts force password change + remove demo credentials banner | **DONE (SoT)** | Migration `083` + seed updates; Login banner credentials removed; MFA any-code banner kept; error-message password leak removed; Global.asax already forced Change Password |
| #3 /Sys/Database audit + IP allowlist | **DONE (SoT)** | Richer audit (SQL up to 8k, rows, error, duration, IP); `DatabaseConsoleAllowedIps` default loopback; IIS IP Restriction steps below |
| #8 Reduce hardcoded role-name checks | **PARTIAL** | AuthorizationService Company Admin name-only bypass removed; Platform/Company Admin checks now require `IsSystemRole`; RoleService blocks reserved rename/create + ignores client `IsSystemRole`; leftovers listed below |
| #9 Lockout alerting | **DONE (SoT)** | On new lockout: `AUTH_LOCKOUT` SecurityEvents row + soft-fail email to platform admins via `TrySendSecurityAlert` |
| #6 Least-privilege login + backup | **PREPARED ONLY** | Scripts under `database/scripts/ops/` — **DO NOT RUN until Allan approves** |
| #7 aspnet_regiis DPAPI | **PREPARED ONLY** | Steps under `database/scripts/ops/007_*.txt` and `docs/PROD-SECURITY-ASPNET-REGIIS-DPAPI-2026-10-05.txt` — **DO NOT RUN on live** |

---

## #2 — Force password change / demo banner

### What changed
- `database/scripts/004_Migrations/083_SeededAccountsRequirePasswordChange.sql` — `UPDATE` only: sets `RequirePasswordChange=1` for `@asset.local` / known seed ids; **does not** change hashes or delete users.
- Seed / reset scripts now also set the flag when the column exists: `002_Seed/002_DemoData.sql`, `004_Migrations/005_Multitenancy.sql`, `013_SecondDemoOrg.sql`, `014_DemoLoginUsers.sql`.
- `Views/Account/Login.cshtml` — removed platform demo credentials banner (`superadmin@…` / `P@ssw0rd!`). **Kept** `DEMO - MFA any-code enabled` when `MfaAllowAnyCode` is true.
- `Helpers/AuthenticationErrorMessages.cs` — detailed login failure no longer appends demo password / Unlock-Logins hints.
- **Unchanged (by design):** `MfaAllowAnyCode` / DEMO MFA banner behavior; MFA any-code validator remains available when configured.

### Enforcement already present
- `Global.asax.cs` reads `RequirePasswordChange` from the forms ticket and redirects to `Account/ChangePassword` unless the path is exempt (`IsPasswordChangeExemptPath`).
- Login cookie is issued with the flag via `CurrentUserExtensions.SetAuthCookie(..., user.RequirePasswordChange)`.
- Change-password success clears `RequirePasswordChange` on the user.

### Operator note
Apply migration `083` on the SoT/live DB when ready (app startup `ApplyMigrations`, or `sqlcmd`). Until applied, existing rows keep `RequirePasswordChange=0`.

---

## #3 — Database console audit + IP allowlist

### What changed
- `Controllers/DatabaseConsoleController.cs`
  - Enforces `DatabaseConsoleAllowedIps` on **Index** and **Execute** (default `127.0.0.1,::1`). Uses `Request.UserHostAddress` (does **not** trust `X-Forwarded-For` for this gate).
  - Audit `NewValues` now includes `ok|error`, client IP, `rowsAffected`, `durationMs`, and error text; SQL truncate raised to **8000** chars in `OldValues`.
- `Web.config` appSetting: `DatabaseConsoleAllowedIps` = `127.0.0.1,::1`.

### IIS IP and Domain Restrictions (HOLD until publish) — `/Sys` path

Do this on the **live** site only after publish approval:

1. Install feature (elevated PowerShell):  
   `Install-WindowsFeature Web-IP-Security`  
   (or Server Manager → Web Server → Security → IP and Domain Restrictions).
2. IIS Manager → Sites → AssetManagement → select the `/Sys` folder (or add a hidden segment folder if `/Sys` is route-only — prefer **URL-based** rule via `web.config` location, or site-level deny with allow list).
3. **IP and Domain Restrictions** → Edit Feature Settings → **Access for unspecified clients: Deny**.
4. Add Allow entries for loopback and Allan’s admin workstation IP(s): `127.0.0.1`, `::1`, plus LAN IP as needed.
5. Recycle app pool `AssetManagement`.
6. Verify: from a non-allowed IP (or spoofed host binding), `/Sys/Database` returns 403; from allowlisted IP + Platform Admin, console still works.
7. Keep the **appSetting allowlist** as defense-in-depth even with IIS restrictions.

If `/Sys` is not a physical folder, add under the site `web.config`:

```xml
<location path="Sys">
  <system.webServer>
    <security>
      <ipSecurity allowUnlisted="false">
        <add ipAddress="127.0.0.1" allowed="true" />
        <add ipAddress="::1" allowed="true" />
      </ipSecurity>
    </security>
  </system.webServer>
</location>
```

(Requires the IP Restriction feature; do not paste into SoT as a live change without approval.)

---

## #8 — Hardcoded role names (what changed + leftovers)

### Changed
- New `Application/Security/SystemRoleNames.cs` constants + `IsPlatformAdmin` / `IsCompanyAdmin` helpers requiring **`IsSystemRole`**.
- `AuthorizationService` — **removed** name-only Company Admin bypass; grants only when `IsSystemRole && Name == Company Admin`. Platform Admin path also requires `IsSystemRole`.
- `OrganizationScopeService.IsActualPlatformAdmin` / `IsCompanyAdmin` — same `IsSystemRole` requirement via `TryGetRoleInfoByUserId`.
- `PlatformAdminHelper.IsPlatformAdmin` — uses `TryGetRoleInfoByUserId` + `SystemRoleNames.IsPlatformAdmin` (constant kept for callers).
- `AccountSecurityService.RequiresPrivilegedMfa` — `IsSystemRole` aware.
- `RoleService.Create` — ignores client `IsSystemRole` (always false); blocks reserved names.
- `RoleService.Update` — cannot rename system roles; cannot rename a custom role **to** a reserved system name.
- `UserAccountRepository.TryGetRoleInfoByUserId` / `ListActivePlatformAdminEmails`.

### Deliberately left as name compares (and why)

| Location | Why left |
|----------|----------|
| `OrganizationApprovalDefaults` / `DepartmentHeadAssignmentRules` / `ApprovalWorkflowViewModels` (`Department Head`, `Asset Manager`, `Staff`) | Workflow template labels / default stage assignee role names — behavior is “match the org’s role named X”, not a security bypass. Renaming those system roles is now blocked. |
| `AssetRequestService` (`Asset Manager`) | Notification / assignee lookup by conventional role name within tenant. |
| `OrganizationService` template clone skipping `Platform Admin`; creating `Company Admin` with `IsSystemRole=true` | Bootstrap of system roles for new orgs — must target canonical names. |
| `LegalPolicyDefaults`, `PlatformUserViewModels` | Display / default copy, not authorization. |
| `TenantFilterAttribute` `User.IsInRole("Platform Admin")` | Principal roles array is empty in `Global.asax` SetupPrincipal today; real gate is elsewhere. Changing IsInRole wiring is a larger auth refactor — left alone to avoid tenant-routing regressions. |
| `BaseController` / `DepartmentScopeService` / `MetricsService` | Already combined `IsSystemRole` + name — left as-is (already correct pattern). |
| `OrganizationsController` string for Company Admin | Org-admin provisioning label / lookup. |

Follow-up (not this pass): permission-code based ceilings for Company Admin instead of “all non-Platform permissions”; migrate workflow defaults to role **codes** if added later.

---

## #9 — Lockout alerting

### Evidence lockout still works (code path)
- `LoginLockoutEnabled` defaults **true** when unset (`AccountSecurityService.IsLoginLockoutEnabled`).
- Thresholds unchanged: **5** failures / **30** minutes per username+org; **20** / **15** minutes per IP.
- `RecordLoginAttempt` still writes `LoginAttempts` and calls `InvalidateSessionsForLockedAccount` when crossing into locked state.
- Security Logs UI continues to show `LoginAttempts` via `SecurityLogService`.

### What was added
- On transition into lockout: `SecurityEvents` row with `EventType = AUTH_LOCKOUT`.
- Email to active Platform Admin addresses (`ListActivePlatformAdminEmails`) via `IEmailService.TrySendSecurityAlert` — **fails soft** if SMTP missing or send throws (Security Log still written).

---

## #6 — Prepared scripts (do not run)

Absolute paths:

- `C:\Users\allan\Documents\Examples\CodexAsset\database\scripts\ops\006_AppPool_LeastPrivilege.sql`  
  Creates `IIS APPPOOL\AssetManagement` login/user if missing; ensures `db_datareader` + `db_datawriter` + `EXECUTE` on `dbo`. **Does not** grant sysadmin; **does not** remove existing `db_owner`. Documents separate deploy login for DDL/migrations.
- `C:\Users\allan\Documents\Examples\CodexAsset\database\scripts\ops\006_Backup_AssetManagementModuleDb.sql`  
  sqlcmd-friendly `BACKUP DATABASE` to `C:\Backup\AssetManagement\`.
- `C:\Users\allan\Documents\Examples\CodexAsset\database\scripts\ops\006_RestoreTest_ScratchDb.sql`  
  Restore into **`AssetManagementModuleDb_RestoreTest` only** (edit `@bakFile` first).
- `C:\Users\allan\Documents\Examples\CodexAsset\database\scripts\ops\006_Register-NightlyBackupTask.ps1.txt`  
  Task Scheduler example (documentation).

### Backup / restore steps (operator)
1. Create `C:\Backup\AssetManagement`.
2. After approval, test backup once via sqlcmd.
3. Register nightly task (02:00 EAT) per the `.ps1.txt` example.
4. Weekly: restore-test to scratch DB, smoke `sys.tables` count, drop scratch.

---

## #7 — Prepared aspnet_regiis steps (do not run on live)

- `C:\Users\allan\Documents\Examples\CodexAsset\database\scripts\ops\007_aspnet_regiis_DPAPI_steps.txt`
- `C:\Users\allan\Documents\Examples\CodexAsset\docs\PROD-SECURITY-ASPNET-REGIIS-DPAPI-2026-10-05.txt`

Summary: on `C:\inetpub\AssetManagement`, elevated `aspnet_regiis -pef connectionStrings "." -prov DataProtectionConfigurationProvider` (and appSettings if SMTP moved inline). `smtp.secrets.config` remains gitignored — encrypt/ACL on server; never commit. SoT `SystemEncryptionKey` default left in place pending a rotation plan (blanking would break local decrypt).

---

## IIS hold confirmation

- No publish performed.
- No files under `C:\inetpub\AssetManagement` were edited by this Phase B work.
- SoT `Web.config` gained `DatabaseConsoleAllowedIps` only (local SoT).

---

## Files changed (SoT)

### Code / config
- `src/AssetManagement.Application/Security/SystemRoleNames.cs` **(new)**
- `src/AssetManagement.Application/Contracts/IEmailService.cs`
- `src/AssetManagement.Application/Services/Security/RoleService.cs`
- `src/AssetManagement.Infrastructure/Services/AuthorizationService.cs`
- `src/AssetManagement.Infrastructure/Services/OrganizationScopeService.cs`
- `src/AssetManagement.Infrastructure/Services/AccountSecurityService.cs`
- `src/AssetManagement.Infrastructure/Services/EmailService.cs`
- `src/AssetManagement.Infrastructure/Identity/UserAccountRepository.cs`
- `src/AssetManagement.Web/Controllers/DatabaseConsoleController.cs`
- `src/AssetManagement.Web/Helpers/AuthenticationErrorMessages.cs`
- `src/AssetManagement.Web/Helpers/PlatformAdminHelper.cs`
- `src/AssetManagement.Web/Views/Account/Login.cshtml`
- `src/AssetManagement.Web/Web.config`
- `tests/AssetManagement.Tests/Security/DeploymentSecuritySettingsTests.cs`

### SQL / ops / docs
- `database/scripts/004_Migrations/083_SeededAccountsRequirePasswordChange.sql` **(new)**
- `database/scripts/004_Migrations/014_DemoLoginUsers.sql`
- `database/scripts/004_Migrations/005_Multitenancy.sql`
- `database/scripts/004_Migrations/013_SecondDemoOrg.sql`
- `database/scripts/002_Seed/002_DemoData.sql`
- `database/scripts/ops/006_*.sql` / `006_Register-NightlyBackupTask.ps1.txt` / `007_aspnet_regiis_DPAPI_steps.txt` **(new)**
- `docs/PROD-SECURITY-PHASE-B-2026-10-05.md` **(this file)**
- `docs/PROD-SECURITY-ASPNET-REGIIS-DPAPI-2026-10-05.txt` **(new)**
- `docs/PROD-SECURITY-AUDIT-2026-10-05.md` (Phase B status appendix)

### Explicitly NOT changed
- Demo MFA (`MfaAllowAnyCode` / any-code validator)
- `customErrors` / YSOD / `compilation debug`
- HTTPS / secure cookie flags
- Test tenants / live IIS site
- Zebra

---

## Follow-ups for Allan

1. Approve applying migration `083` on SQLEXPRESS `AssetManagementModuleDb` (and later live).
2. Approve running `#6` least-privilege + backup scripts; then schedule removing `db_owner` from app pool after soak.
3. Approve `#7` aspnet_regiis maintenance window; rotate Gmail app password after encrypt/ACL.
4. Later Phase: turn off live `MfaAllowAnyCode`, customErrors, HTTPS — **held** per this pass.
5. Optional: apply SoT build publish to IIS when ready (currently held).
