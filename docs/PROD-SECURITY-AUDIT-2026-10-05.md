# Production Security Audit: Asset Management (CodexAsset)

- **Date:** 2026-10-05 (audit run ~11:50–12:30 EAT)
- **Source of truth (SoT):** `C:\Users\allan\Documents\Examples\CodexAsset\` (branch `main`, HEAD `88548c8`, 2026-10-03 23:11 EAT)
- **Live IIS (compared read-only):** `C:\inetpub\AssetManagement` (http://localhost:8080/). `bin\AssetManagement.Web.dll` dated 2026-10-03 23:10 EAT. Live `Web.config` dated 2026-09-08 11:39 EAT.
- **READ-ONLY:** no code, config, data, IIS, or GitHub changes. The only SQL run was a set of `SELECT` metadata/row-count queries against `.\SQLEXPRESS` (Integrated auth as `DESKTOP-V47PGAM\allan`). Secret values are **redacted** in this document.

Paths below are relative to the SoT root unless they start with `C:\inetpub`. Line numbers come from SoT HEAD as of today.

## Summary

| # | Item | Verdict | One-line finding |
|---|------|---------|------------------|
| 1 | Demo MFA any-code | **PARTIAL** | SoT base and Release set `MfaAllowAnyCode=false`, but Debug sets `true` and **live IIS runs `true`**. The live `App_Data\dev-security-output.log` is still being written (87 KB, last write today 09:28 EAT). |
| 2 | Seeded accounts / demo banner | **FAIL** | 13+ seeded `@asset.local` accounts use the shared password `P@ssw0rd!`. The Login page banner shows Platform Admin credentials whenever any-code is on (live: on). Failed-login messages leak the demo password. **14 active live users still have the demo password hash**, all with `RequirePasswordChange=0`. |
| 3 | /Sys/Database exposure + audit | **PARTIAL** | Gated to Platform Admin with CSRF protection. Each Execute is audited (actor, UTC time, IP, SQL truncated to 1000 chars, ok/error). Missing: rows affected, error text, IP allowlist, MFA step-up, and statement restrictions. It runs arbitrary SQL as a **db_owner** login. Three URL aliases exist. |
| 4 | customErrors / YSOD | **FAIL** | `customErrors mode="Off"` and `compilation debug="true"` in base, Debug, **Release** and live IIS, so remote users see the yellow screen (YSOD) with stack traces. |
| 5 | HTTPS / secure cookies / HSTS | **FAIL** | `RequireSecureCookies=false` and `RequireHttpsRedirect=false` everywhere. No `requireSSL`, no HSTS header. The Release hardening was removed in commit `9a92b10` (2026-08-25). |
| 6 | DB login privilege | **FAIL** | Integrated Security (no `sa` in configs), but `IIS AppPool\AssetManagement` **and** `IIS APPPOOL\DefaultAppPool` are `db_owner`. Mixed-mode auth is on and `sa` is enabled (sysadmin). The SoT `connectionStrings.config` used by Release points at **E2E LocalDB**. |
| 7 | Secrets in Web.config | **FAIL** | `SystemEncryptionKey` default literal is committed (`Web.config:40`, code fallback `EncryptionHelper.cs:12`, static salt) and is identical on live. It encrypts temporary credentials. The Gmail SMTP password is plaintext in `smtp.secrets.config` (gitignored, never committed; SoT and live). No `machineKey`. Nothing is DPAPI-encrypted. |
| 8 | Company Admin bypass / hardcoded roles | **FAIL** | `AuthorizationService.cs:70` grants every tenant permission to any role **named** "Company Admin" with no `IsSystemRole` check. `RoleService.Update` lets a role be renamed with no reserved-name check, which is a possible escalation path. About 25 hardcoded role-name comparisons. |
| 9 | Lockout + Security Logs + alerting | **PARTIAL** | Lockout is on by default (5 failures per user in 30 min, 20 per IP in 15 min). Failed logins land in `LoginAttempts` and show in Security Logs. **No alerting.** No explicit lockout event. `X-Forwarded-For` is trusted, which bypasses the per-IP limit. Migrations 029/030/062 `DELETE FROM [LoginAttempts]`. |
| 10 | Test tenants / debug endpoints | **FAIL** | Live DB has demo or test orgs (`demo-b`, template 1/3, asset-import-template, Test-WF). `SecurityDiagnostics` still ships. The `/Sys/DataConsole` and `/DatabaseConsole/Index` aliases exist. Demo-credential helpers, hints and scripts are in the product code. |

**Totals:** PASS 0 · PARTIAL 3 · FAIL 7

> **Doc drift:** `docs/DEPLOYMENT-READINESS.md:13-19` and `docs/AUTH-ARCHITECTURE.md:68-78` say Release sets HTTPS redirect, secure cookies, HSTS, `requireSSL`, `customErrors=RemoteOnly`, `debug=false`, `GenericAuthMessagesEnabled=true`, `LoginCaptchaEnabled=true` and `machineKey.config`. **None of these are in the current `Web.Release.config`.** Commit `9a92b10` (2026-08-25, "Add SQL-backed queries, distributed rate limits, and hybrid file storage.") removed them and added the comment "Other hardening (HTTPS, captcha, machineKey) is deferred — debug and full error detail stay on for post-host testing." (`Web.Release.config:2-7`).

> **Critical chain (live, today):** `superadmin@asset.local` exists, and live `MfaAllowAnyCode=true` means any MFA code is accepted. A Platform Admin session then reaches `/Sys/Database`, which runs arbitrary T-SQL as a `db_owner` login. The live superadmin hash is **not** the demo hash (it was rotated), so the immediate exposure is: anyone who knows or guesses the superadmin password skips MFA entirely. Exposure is limited by the site being bound to `localhost:8080` and plain HTTP.

---

## 1. Demo MFA any-code (`MfaAllowAnyCode`)

**Verdict:** PARTIAL

**Evidence**
- `src/AssetManagement.Web/Web.config:26-27`: base `MfaAllowAnyCode=false` (the comment says "Hardened base"). `:28`: `RequireMfaForAllUsers=false`.
- `src/AssetManagement.Web/Web.Release.config:11-12`: `RequireMfaForAllUsers=true`, `MfaAllowAnyCode=false` (correct).
- `src/AssetManagement.Web/Web.Debug.config:7-8`: `RequireMfaForAllUsers=false`, **`MfaAllowAnyCode=true`**.
- **Live** `C:\inetpub\AssetManagement\Web.config:15`: **`MfaAllowAnyCode=true`**. `:16`: `RequireMfaForAllUsers=false`.
- Enforcement:
  - `src/AssetManagement.Application/Security/DeploymentSecuritySettings.cs:29-32`: reads the key and defaults to false.
  - `src/AssetManagement.Infrastructure/Security/MfaCodeValidator.cs:19-22`: `if (allowAnyCode) return true;` accepts any non-empty code.
  - `src/AssetManagement.Infrastructure/Services/AccountSecurityService.cs:329-353`: `ValidateMfaCode` passes the relaxed flag to the validator.
  - `AccountSecurityService.cs:227-313`: `SendMfaCode` returns success even without SMTP when relaxed.
- MFA is required for privileged roles regardless of the flag: `AccountSecurityService.cs:79-88, 93-105` (Platform Admin and Company Admin). With any-code on, that requirement does nothing.
- Dev diagnostics tied to the flag:
  - `src/AssetManagement.Infrastructure/Security/SecurityDiagnostics.cs:72-97` writes MFA codes, reset links and invite links to `App_Data/dev-security-output.log` when the flag is true.
  - Call sites: `AccountSecurityService.cs:267,339`, `MembershipService.cs:283`, `UserInvitationService.cs:285,296,306`, `AccountController.cs:373,497`.
  - **Live** `C:\inetpub\AssetManagement\App_Data\dev-security-output.log` exists: 87,386 bytes, last write 2026-10-05 09:28 EAT. Contents were not opened because they contain codes and links.
- `Global.asax.cs:38-71` adds Console and Default trace listeners when the flag is true.

**Risk:** live MFA is cosmetic. Anyone with a valid password, including privileged accounts, passes MFA with any code, and live one-time codes and reset links are written to disk.

**Suggested Phase B action:** publish Release (or set live `MfaAllowAnyCode=false` once SMTP is verified). Delete or rotate `App_Data\dev-security-output.log`. Remove or compile out `SecurityDiagnostics` per `.cursor/rules/production-dev-security-cleanup.mdc`.

## 2. Seeded accounts / demo credential banner

**Verdict:** FAIL

**Evidence: seed scripts** (all use the same PBKDF2 hash for `P@ssw0rd!`; hash not reproduced here)
- `database/scripts/002_Seed/002_DemoData.sql:56-59` and following: `nanosoft@asset.local` (Company Admin), `assetmanager@`, `auditor@`, `departmenthead@`, `finance@`, `itstaff@`, `labtech@`, `opsstaff@`, `procurement@`, `staff@asset.local`. They are seeded with `LockoutEnabled=0`, `TwoFactorEnabled=0`.
- `database/scripts/004_Migrations/005_Multitenancy.sql:148-159`: `superadmin@asset.local` (Platform Admin, `seed-user-platform`).
- `database/scripts/004_Migrations/013_SecondDemoOrg.sql:8,83-100`: org `demo-b`, `demo@asset.local`, `staff@demo-b.asset.local` (comment "password: P@ssw0rd!").
- `database/scripts/004_Migrations/014_DemoLoginUsers.sql:1-24`: "Ensure demo login accounts exist with the documented password (P@ssw0rd!)". Resets the hash for superadmin, nanosoft, default, demo, platform, companyadmin, companyadmin@demo-b, assetmanager, procurement, finance, staff, auditor and departmenthead.
- `database/scripts/004_Migrations/029_ResetDemoLoginAccess.sql:3-21`: deletes `LoginAttempts`, then resets **every** `%@asset.local` user to the demo password and unlocks it. `:55-78` recreates or resets superadmin.
- `database/scripts/004_Migrations/030_UnlockLoginAttempts.sql:3-6,14-93`: same pattern (deletes all LoginAttempts, resets all `%@asset.local` to the demo hash). Re-runnable via `tools/database/Unlock-Logins.ps1:17,48-49`.
- `RequirePasswordChange` column: `004_Migrations/046_RecruitmentAuthAndOrgProfile.sql:9-11` (default 0). Seed users never set it. New-org admins do get it: `OrganizationService.cs:486-496` (`SecurePasswordGenerator` + `RequirePasswordChange = true`). Enforced in `Global.asax.cs:129-168`.

**Evidence: UI and code**
- `src/AssetManagement.Web/Views/Account/Login.cshtml:125-133`: platform login banner "DEMO - MFA any-code enabled; shared demo passwords" with **`superadmin@asset.local` / `P@ssw0rd!`** in plain text. It is gated by `DeploymentSecuritySettings.ShowDemoBanner`, which falls back to `MfaAllowAnyCode` when `ShowDemoSecurityBanner`/`DemoMode` is unset (`DeploymentSecuritySettings.cs:37-55`). **Live has any-code=true, so the banner is shown on live.**
- `Login.cshtml:136-140`: secondary "DEMO - MFA any-code enabled" banner.
- `src/AssetManagement.Web/Helpers/AuthenticationErrorMessages.cs:223-251`: failed platform-login message appends "Check that the password is P@ssw0rd! for demo accounts" or "...use superadmin@asset.local / P@ssw0rd!". It is gated **only** by `GenericAuthMessagesEnabled` (`:21-30`), which no config sets (it was removed from Release in `9a92b10`). It also confirms whether a platform-admin email exists (account enumeration).
- `src/AssetManagement.Web/Views/Account/OrganizationNotFound.cshtml:19-20`: "Demo tenants include /nanosoft".
- `src/AssetManagement.Application/Helpers/DemoLoginEmailHelper.cs:7-9,65-80,102-106`: hardcoded demo emails and slugs.
- `e2e/fixtures/users.ts:1` and about 20 `e2e/_qa_*.js` scripts: `PASSWORD = 'P@ssw0rd!'` (test-only, but committed).

**Evidence: live DB** (read-only SELECT, hash comparison only)
- 36 active `%asset.local` users.
- **14 active users still have the demo password hash**, all with `RequirePasswordChange=0`:
  - demo-b: `demo@`, `staff@demo-b.asset.local`
  - K53262685: `Sample@asset.local`
  - nanosoft: `assetmanager@`, `auditor@`, `departmenthead@`, `eeeeelik@`, `finance@`, `itstaff@`, `labtech@`, `opsstaff@`, `procurement@`, `quelofi@`, `staff@asset.local`
- `superadmin@asset.local` and `nanosoft@asset.local` **do not** match the demo hash (they were rotated).

**Risk:** a publicly known password works on 14 live tenant accounts, and the login page and error messages advertise it. Any rerun of migrations 014/029/030 resets every `@asset.local` account, superadmin included, back to `P@ssw0rd!`.

**Suggested Phase B action:**
- Rotate or disable all `@asset.local` users and set `RequirePasswordChange=1`.
- Delete the Login banner and the `AuthenticationErrorMessages` demo hints, or gate them on an explicit `DemoMode` key only.
- Set `GenericAuthMessagesEnabled=true`.
- Move 014/029/030 out of `004_Migrations` into a dev-only folder.

## 3. /Sys/Database exposure + audit logging

**Verdict:** PARTIAL

**Evidence**
- Routes: `src/AssetManagement.Web/App_Start/RouteConfig.cs:13-19` (`Sys/Database/{action}/{id}`) and `:21-26` (`Sys/DataConsole/...`), registered before the tenant route. The default route also exposes `/DatabaseConsole/Index` (`DatabaseConsoleController.cs:12`). `Sys` and `DatabaseConsole` are reserved tenant segments (`Helpers/TenantReservedSegments.cs:26,47`).
- Authorization:
  - `DatabaseConsoleController.cs:16`: `[Authorize]`.
  - `:64-79`: `GateConsoleAccess()` requires `IOrganizationScopeService.IsActualPlatformAdmin()`. Otherwise the user is redirected to Forbidden.
  - `IsActualPlatformAdmin` (`Infrastructure/Services/OrganizationScopeService.cs:180-195`) is a string comparison against `"Platform Admin"`.
  - `:43-45`: `Execute` is `[HttpPost][ValidateAntiForgeryToken]`.
- Execution: `src/AssetManagement.Web/Services/DatabaseConsoleService.cs:27-81` runs the raw text through `cmd.ExecuteReader()`. There is no statement allow- or deny-list (DDL, DML, `xp_`, `DROP` all allowed), a 120 s timeout, and a 500-row display cap. It runs as the app pool identity, which is **db_owner** (see #6).
- Audit:
  - `DatabaseConsoleController.cs:81-102` writes action `DB_CONSOLE_EXECUTE`, entity `ApplicationUser/<actorId>`, `OldValues` = SQL truncated to 1000 chars (`:104-116`), and `NewValues` = `"ok"` or `"error"`.
  - `AuditWriter.cs:37-87` adds `ActorUserId`, `Timestamp` (UTC), `IPAddress`, and `OrganizationId`. A null org is allowed for platform users (`:119-128`). `SyncAuditWrites=true` makes the write synchronous (`Web.config:25`).
  - IP source: `HttpCurrentUserContext.cs:29-32` uses `Request.UserHostAddress` (not spoofable, but shows the proxy IP when behind a proxy).
  - Gaps:
    - **rows affected** and error message are not logged;
    - the audit write happens *after* execution;
    - audit failures are silently swallowed (`:98-101`);
    - there is no separate high-sensitivity log.
- Live DB check: `AuditLog` has 2 `DB_CONSOLE_EXECUTE` rows, the latest at 2026-10-03 23:13 EAT. Logging works on live.
- IP allowlist: **none** in controller, route or Web.config. No MFA step-up or re-auth before Execute. No feature flag to disable the console in production. Live IIS includes `Views\DatabaseConsole` and the controller (compiled into `bin` on 2026-10-03).

**Risk:** one compromised Platform Admin session (see #1) gives full read and write access to the database, including DDL, with only a truncated after-the-fact audit trail that can fail silently.

**Suggested Phase B action:**
- Put the console behind an `EnableDatabaseConsole` appSetting that is false in Release.
- Add a loopback or IP allowlist and require re-entering MFA.
- Log rows affected, error text and duration; audit *before* execution and fail closed.
- Optionally run SELECT-only through a read-only connection.
- Drop the `/Sys/DataConsole` and `/DatabaseConsole` aliases.

## 4. customErrors / YSOD

**Verdict:** FAIL

**Evidence**
- `src/AssetManagement.Web/Web.config:45`: `compilation debug="true"`. `:52`: `customErrors mode="Off"`.
- `Web.Debug.config:11-12`: debug true, customErrors Off.
- `Web.Release.config:15-16`: **debug true, customErrors Off**. Before commit `9a92b10` this was `debug=false` and `customErrors=RemoteOnly`.
- Live `C:\inetpub\AssetManagement\Web.config:22,28`: `debug="true"`, `customErrors mode="Off"`.
- `App_Start/FilterConfig.cs:13` registers `HandleErrorAttribute`, but MVC only renders `Error.cshtml` when customErrors is On or RemoteOnly. With Off, remote clients get the full YSOD.
- No `<httpErrors>` section in `system.webServer` (base or live).
- Repo root shows the YSOD has already been hit: `_tmp_ysod_publish*.ps1/log`, `_tmp_a461_error.html`.

**Risk:** stack traces, source snippets, file paths, SQL errors and framework versions are disclosed to any remote user. Debug compilation also hurts performance and enables batch-less compiles.

**Suggested Phase B action:** in Release and live, set `customErrors mode="RemoteOnly"` (or `On`) with `defaultRedirect` and `compilation debug="false"`, and add `httpErrors errorMode="DetailedLocalOnly"`.

## 5. HTTPS / secure cookies / HSTS

**Verdict:** FAIL

**Evidence**
- `src/AssetManagement.Web/Web.config:38-39`: `RequireSecureCookies=false`, `RequireHttpsRedirect=false`. `Web.Debug.config:5-6`: same. `Web.Release.config` does **not** override them (the overrides were removed in `9a92b10`).
- `Web.config:49`: `<forms ... protection="All" cookieless="UseCookies" />` has **no `requireSSL`**. `:51`: `<httpCookies httpOnly="true" sameSite="Lax" />` has **no `requireSSL`**.
- HSTS: no `Strict-Transport-Security` anywhere in configs or code (searched `*.cs`/`*.config`). The Release `customHeaders` block (HSTS, `X-Content-Type-Options`, `X-Frame-Options`) was removed in `9a92b10`. There are no security headers at all (CSP, XFO, nosniff, Referrer-Policy).
- Code paths exist and are dormant:
  - `Global.asax.cs:279-287` + `Security/HttpsRedirectHelper.cs:28` (HTTPS redirect when the flag is on);
  - `Security/AuthSessionHelper.cs:50-62` and `Security/CurrentUserExtensions.cs:78-88` (Secure flag when the request is HTTPS or the flag is on).
- Live `C:\inetpub\AssetManagement\Web.config:17-18`: both false. `:26`: forms has no `requireSSL` (also lacks `protection`/`cookieless`). **No `<httpCookies>` element at all**, so the HttpOnly/SameSite defaults are not enforced by config. Site is served on `http://localhost:8080/`.

**Risk:** auth cookies and credentials travel in cleartext. There is no downgrade protection and no clickjacking or MIME-sniffing protection once the site is reachable beyond localhost.

**Suggested Phase B action:** restore the pre-`9a92b10` Release block (`RequireSecureCookies`/`RequireHttpsRedirect=true`, forms `requireSSL`, `httpCookies requireSSL`, HSTS + XFO + nosniff), and bind an HTTPS certificate on IIS before flipping the flags.

## 6. DB login privilege

**Verdict:** FAIL

**Evidence: configs**
- SoT `src/AssetManagement.Web/Web.config:12`: `AssetManagementConnection` = `Data Source=.\SQLEXPRESS;Initial Catalog=AssetManagementModuleDb;Integrated Security=True;...`. `:13`: `AssetManagementConnectionE2E` = `(localdb)\MSSQLLocalDB;...AssetManagementModuleDb_E2E;Integrated Security=True`.
- Live `C:\inetpub\AssetManagement\Web.config:4`: same live string, Integrated Security. So the runtime identity is the IIS app pool identity, presumably `IIS AppPool\AssetManagement`. The app pool config could not be read: `appcmd` returned "insufficient permissions" when not elevated.
- `Web.Release.config:9`: `<connectionStrings configSource="connectionStrings.config" xdt:Transform="Replace" />`. SoT `src/AssetManagement.Web/connectionStrings.config:4` points at **`(localdb)\MSSQLLocalDB;Initial Catalog=AssetManagementModuleDb_E2E`**. A Release publish from SoT without swapping this file would point production at the E2E LocalDB. Live has **no** `connectionStrings.config`.
- No `sa`, `User ID=` or SQL-auth password in any config or script (searched configs, `*.ps1`, `*.cs`, `*.json`, `*.md` under SoT root and `tools/`).

**Evidence: SQL Server** (read-only metadata SELECT on `.\SQLEXPRESS`)

| Principal | Type | Server roles | DB `AssetManagementModuleDb` roles |
|---|---|---|---|
| `IIS AppPool\AssetManagement` | Windows login | — | **db_owner** |
| `IIS APPPOOL\DefaultAppPool` | Windows login | — | **db_owner** |
| `IIS APPPOOL\.NET v4.5` | Windows login | — | (no DB user) |
| `BUILTIN\Users` | Windows group | — | (no DB user) |
| `DESKTOP-V47PGAM\allan` | Windows login | **sysadmin** | dbo (DB owner) |
| `sa` | SQL login, **enabled** | **sysadmin** | — |
| `NT AUTHORITY\SYSTEM` | Windows login | — | — |

- Server auth mode: **Mixed** (SQL logins allowed). `sa` `is_disabled=0`.

**Risk:**
- The web app identity (and any other site on DefaultAppPool) can run DDL, drop tables and change permissions. Combined with `/Sys/Database` this means full DB control from the web tier.
- An enabled `sa` under Mixed mode is a brute-force target if TCP or SQL Browser is exposed.
- The Release `connectionStrings.config` trap could silently connect production to an empty E2E database.

**Suggested Phase B action** (prepare only; Allan approved scope):
- Script `IIS APPPOOL\AssetManagement` with `db_datareader` + `db_datawriter` + `GRANT EXECUTE`.
- Migrations (`004_Migrations`, run in-app via `DatabaseConfig.ApplyMigrations` and by `AssetManagement.Runner migrate`) do `ALTER TABLE`/`CREATE INDEX`, so they need **`db_ddladmin`**, or better, run migrations under a separate deploy login and keep the app pool DML-only.
- Remove `DefaultAppPool` from db_owner later (do **not** drop or alter now). Consider disabling `sa` / Windows-only auth.
- Fix the SoT `connectionStrings.config` target before any Release publish.
- Add a nightly sqlcmd backup task plus a restore-test to a scratch DB.

## 7. Secrets in Web.config

**Verdict:** FAIL

**Evidence**
- Connection strings: Integrated Security only, so there are **no DB passwords** in SoT or live configs (`Web.config:12-13`, live `:4`).
- `SystemEncryptionKey`:
  - `src/AssetManagement.Web/Web.config:40`: the default literal value is committed to git (present since commit `edce908`).
  - Live `C:\inetpub\AssetManagement\Web.config:19`: **same default value**.
  - `src/AssetManagement.Web/Helpers/EncryptionHelper.cs:11-12`: hardcoded fallback to the same literal. `:14`: static hardcoded PBKDF2 salt. `:25-28`: key and IV are both derived from the password, so the IV is static.
  - Used to encrypt temporary org-admin credentials: `Areas/Platform/Controllers/OrganizationsController.cs:700-703` (encrypt) and `Controllers/AccountController.cs:1747` (decrypt).
- SMTP:
  - Base `Web.config:17` uses `appSettings file="smtp.secrets.config"`.
  - SoT `src/AssetManagement.Web/smtp.secrets.config:3-10` and live `C:\inetpub\AssetManagement\smtp.secrets.config:3-10` both hold `SmtpHost=smtp.gmail.com`, `SmtpUser=nanosoft.africa@gmail.com`, and **`SmtpPassword` in plaintext** (value redacted). The two files differ by 1 byte.
  - Gitignored (`.gitignore:51`, verified with `git check-ignore`), and `git log --all` shows it was **never committed**. Same for `connectionStrings.config` (`.gitignore:49`) and `machineKey.config` (`.gitignore:50`).
  - Platform Settings → Email can also store SMTP settings in the DB (`Areas/Platform/Views/PlatformSettings/Email.cshtml:111`). The DB storage/encryption path was not verified in this audit.
- `machineKey`: absent in SoT base, Release and live (no `machineKey.config` on live). Release no longer inserts `machineKey configSource` (removed in `9a92b10`). `Security/MachineKeyDeploymentValidator.cs:22-40` only logs a warning. Forms-auth cookies use auto-generated keys.
- No `aspnet_regiis` / DPAPI-protected sections (`configProtectionProvider`) anywhere.

**Risk:** anyone with repo access and a DB backup can decrypt stored temporary credentials (known key, static salt and IV). The Gmail account password is readable by anyone with file access to the site folder or the dev workspace. Without a stable machineKey, cookies break on recycle or farm and the keys cannot be rotated deliberately.

**Suggested Phase B action** (prepare only):
- Generate a unique `SystemEncryptionKey` outside git (a re-encryption plan is needed for existing `EncryptedData`) and remove the code fallback.
- Use `aspnet_regiis -pef connectionStrings` and appSettings/SMTP with DPAPI on `C:\inetpub\AssetManagement` (not yet; Allan to schedule).
- Rotate the Gmail app password after encrypting.
- Add a `machineKey.config` from `machineKey.config.example`.

## 8. Company Admin bypass + hardcoded role names

**Verdict:** FAIL

**Evidence: bypass**
- `src/AssetManagement.Infrastructure/Services/AuthorizationService.cs:46-58`: `"Platform Admin"` string gets every `Platform.*` permission, plus **every** permission while impersonating.
- `AuthorizationService.cs:70-73`: **`if (roleName == "Company Admin") return true;`** checks the name only. It runs **before** the role is loaded, so `IsSystemRole`/`IsActive` are never checked.
- `AuthorizationService.cs:81-84`: a correct `role.IsSystemRole && Name=="Company Admin"` check exists but is unreachable for that name (dead code).
- `src/AssetManagement.Application/Services/Security/RoleService.cs:182-200` (`Update`): `role.Name = model.Name;` with no uniqueness or reserved-name check. A user who can edit roles could rename a custom role to "Company Admin" and get full tenant access through line 70 (**potential, not exploited or verified end-to-end**).
- `RoleService.cs:159-170` (`Create`): the uniqueness check blocks a duplicate "Company Admin", but `IsSystemRole = model.IsSystemRole` is taken from the posted model (mass-assignment risk).
- `UserService.cs:134-139`: system-role assignment is limited to Company Admin and Platform Admin.

**Evidence: hardcoded role-name comparisons (file:line)**
- "Platform Admin":
  - `AuthorizationService.cs:46`
  - `OrganizationScopeService.cs:194`
  - `AccountSecurityService.cs:85`
  - `OrganizationService.cs:178`
  - `TenantFilterAttribute.cs:114` (`User.IsInRole`)
  - constant `Web/Helpers/PlatformAdminHelper.cs:14`
- "Company Admin":
  - `AuthorizationService.cs:70,81`
  - `OrganizationScopeService.cs:215`
  - `AccountSecurityService.cs:87`
  - `TenantFilterAttribute.cs:115`
  - `BaseController.cs:646` (with IsSystemRole)
  - `DepartmentScopeService.cs:49` (with IsSystemRole)
  - `MetricsService.cs:152` (with IsSystemRole)
  - `LegalPolicyDefaults.cs:72`
  - `PlatformUserViewModels.cs:21`
  - `OrganizationService.cs:454,460`
  - `Areas/Platform/Controllers/OrganizationsController.cs:125`
  - constant `OrganizationApprovalDefaults.cs:14`
- "Department Head":
  - `ApprovalWorkflowViewModels.cs:440`
  - constants `OrganizationApprovalDefaults.cs:15`, `DepartmentHeadAssignmentRules.cs:8`
- "Asset Manager":
  - `AssetRequestService.cs:198`
  - `ApprovalWorkflowViewModels.cs:444`
  - constant `OrganizationApprovalDefaults.cs:16`
- "Staff":
  - `AccountController.cs:1644`
  - `ApprovalWorkflowViewModels.cs:448`
- `IsSystemRole` is used correctly at `BaseController.cs:646`, `DepartmentScopeService.cs:48`, `MetricsService.cs:151` and `UserService.cs:134`, but **not** at `AuthorizationService.cs:70`, `OrganizationScopeService.cs:194/215` or `TenantFilterAttribute.cs:114-115`.

**Risk:** privilege decisions depend on editable display names. Renaming a role, or a tenant creating a lookalike, can grant full tenant admin, and Platform Admin detection is a pure string comparison.

**Suggested Phase B action:**
- Remove the name-only bypass at `AuthorizationService.cs:70` (keep the `IsSystemRole` check at `:81`).
- Block renaming to or from reserved system role names and ignore client-posted `IsSystemRole`.
- Put all role names in a single `SystemRoleNames` constants class and key privileged checks on `IsSystemRole` + `OrganizationId IS NULL`, or on a role code.

## 9. Lockout + Security Logs + alerting

**Verdict:** PARTIAL

**Evidence**
- Lockout switch: `AccountSecurityService.cs:833-850`. `LoginLockoutEnabled` defaults to **true** when the key is absent, and no config sets it (base, Release, Debug, live). Enabled everywhere.
- Thresholds: `AccountSecurityService.cs:39` (`MaxFailedAttempts = 5`), `:41` (`LockoutDurationMinutes = 30`), `:43` (`MaxIpFailures = 20`), `:45` (`IpWindowMinutes = 15`). Checks at `:484-524`. The window slides over recent failures. `ApplicationUser.LockoutEnabled` is not used by this logic, so the seed value `LockoutEnabled=0` has no effect.
- Recording: `AccountController.cs:130,150,164,170` calls `RecordLoginAttempt` with reasons "Account locked", "Invalid credentials" and success, written to `[LoginAttempts]` (`AccountSecurityService.cs:582-610`). On reaching the threshold, sessions are invalidated (`:602-607`, `Global.asax.cs:163`).
- Security Logs page: `Application/Services/SecurityLogService.cs:21-40` shows `LoginAttempts` (total, success, failed) plus `SecurityEvents`. `SecurityEvents` only records `AUTH_FORGOT_REQUEST` (`AccountSecurityService.cs:31,442`). There is **no explicit "ACCOUNT_LOCKED" event**; lockout is only implied by the count. MFA-verify and reset-password limits use `AuthFlowRateLimiterService.cs:14,27` (30-minute lockouts).
- Alerting: **none**. No email, webhook or notification on repeated failures or lockouts (searched for alert/notify patterns).
- Client IP: `AccountController.cs:1677-1690` (`GetClientIpAddress`) and `Helpers/AuthFlowClientAddressHelper.cs:14-21` trust the **first `X-Forwarded-For` value unconditionally**. An attacker can rotate the header to evade the 20-per-IP limit and forge the IP in logs.
- Log tampering by scripts: `004_Migrations/029_ResetDemoLoginAccess.sql:3-6` and `030_UnlockLoginAttempts.sql:3-6` run `DELETE FROM [LoginAttempts]`. `062_ClearDefaultLoginAttempts.sql:6` deletes rows for `default@` and `nanosoft@`. `AccountSecurityService.cs:646-649` (`ClearAllLoginLockouts`) and `Runner/Program.cs:47` (`PurgeStaleSecurityRecords`) also purge.
- Detailed errors reveal remaining attempts (`AuthenticationErrorMessages.cs:253+`) because `GenericAuthMessagesEnabled` is unset. `LoginCaptchaEnabled=false` (`Web.config:23`, live `:12`).

**Risk:** brute force is slowed per account, but no one is told about it. The per-IP limit is bypassable, and the security log can be wiped by migrations or scripts.

**Suggested Phase B action:**
- Write an explicit `AUTH_LOCKOUT` SecurityEvent and email Platform or Company Admins after N lockouts.
- Trust `X-Forwarded-For` only from configured proxy IPs.
- Turn on `GenericAuthMessagesEnabled` and captcha in Release.
- Move the LoginAttempts-wiping scripts out of `004_Migrations`.

## 10. Test tenants / debug endpoints

**Verdict:** FAIL

**Evidence**
- Live orgs (read-only SELECT on `Organization`), all `Active`:
  - `1 nanosoft` (demo primary)
  - `2 demo-b` ("Demo Organization B")
  - `9 K53262685` ("template 1")
  - `10 L35160674` ("asset-import-template")
  - `11 E93491564` ("Template 3")
  - `13 A46138179` ("Test-WF")
  - `18 Y10504930` ("Boarding")
  - `21 nis` (Nairobi International School)
- Demo org seed: `004_Migrations/013_SecondDemoOrg.sql:8`. Hardcoded slugs: `DemoLoginEmailHelper.cs:9,75`, `SchoolOrganizationBootstrapService.cs:57`, `Runner/Program.cs:134`, `Login.cshtml:132`, `OrganizationNotFound.cshtml:19-20`.
- Debug or diagnostic code that ships:
  - `Infrastructure/Security/SecurityDiagnostics.cs` (whole class) and its call sites (see #1). Live `App_Data\dev-security-output.log` exists.
  - `Global.asax.cs:38-71` adds dev trace listeners.
  - `Web.Debug.config:14-20` adds `system.diagnostics` trace (Debug only).
- SQL console aliases: `/Sys/Database`, `/Sys/DataConsole` (`RouteConfig.cs:14-26`) and `/DatabaseConsole/Index` (default route) (see #3).
- E2E switches:
  - `AssetManagementConnectionE2E` in `Web.config:13`;
  - `Web.Debug.config:4` (`AutoInitializeDatabase=true`);
  - `tools/database/Reset-E2eDatabase.ps1`, `Restore-E2eWebConfig.ps1`;
  - `tools/database/Unlock-Logins.ps1:17,48-49` (reruns migration 030 and prints the demo credentials).
  - The `.gitignore:28,48` backup files (`Web.config.iis-host-backup`, `Web.config.e2e-backup`).
- Other relaxed switches:
  - `FreshPortalMode` (`FilterConfig.cs:20-30`; unset, so off);
  - `MigrationContinueOnError=true` (`Web.config:24`; Release no longer sets it to false), so a failed migration is skipped silently at startup;
  - `AuthenticationErrorMessages.cs:249` points users to `tools\database\Unlock-Logins.ps1`.
- No `[AllowAnonymous]` or `#if DEBUG` blocks found in Web. No dedicated "Debug"/"Test" controllers.
- Repo root clutter (not deployed, not tracked; 0 `_tmp*` files in git): many `_tmp_*.ps1/.py/.cs/.exe`, `publish-*.log`, `artifacts\publish-db-backup-2026-10-02\` (contains a copy of the source).

**Risk:** demo and test tenants with known passwords live next to the real school tenant (`nis`). Dev diagnostics leak secrets to disk, and hidden admin endpoints widen the attack surface.

**Suggested Phase B action:**
- Deactivate or purge `demo-b`, the template orgs and `Test-WF` after Allan confirms which are still needed (`asset-import-template` may be functional).
- Remove `SecurityDiagnostics`, the demo helpers and banners.
- Feature-flag the console off in Release.
- Set `MigrationContinueOnError=false` in Release.
- Move demo-reset SQL and scripts to a `dev/` folder excluded from publish.

---

## Live IIS vs SoT (items 1, 4, 5, 6, 7)

| Item | SoT base `Web.config` | SoT Release (transform) | Live `C:\inetpub\AssetManagement\Web.config` | Material? |
|---|---|---|---|---|
| 1 MFA | `MfaAllowAnyCode=false`, `RequireMfaForAllUsers=false` | false / **true** | **`MfaAllowAnyCode=true`**, `RequireMfaForAllUsers=false` | **Yes.** Live is relaxed, and the live login banner shows superadmin creds. |
| 4 Errors | debug=true, customErrors Off | debug=true, Off | debug=true, Off (+ `tempDirectory`) | No (all bad) |
| 5 HTTPS | flags false; forms `protection="All"`; `httpCookies httpOnly sameSite=Lax` | no change | flags false; forms has no `protection`/`cookieless`; **no `<httpCookies>`** | **Slightly.** Live is weaker: no config-level HttpOnly/SameSite. |
| 6 DB | `.\SQLEXPRESS` Integrated + E2E alias | **`configSource=connectionStrings.config` → E2E LocalDB in SoT** | `.\SQLEXPRESS` Integrated (no E2E alias, no `connectionStrings.config`) | **Yes.** A Release publish would repoint to LocalDB unless the server file exists. |
| 7 Secrets | `SystemEncryptionKey` default; `smtp.secrets.config` | same | same default key; own `smtp.secrets.config` (plaintext SMTP password, 1 byte different from SoT) | No (both bad) |

Live `Web.config` is about 4 weeks older than SoT (2026-09-08 vs 2026-10-02). Publishes have updated `bin` but not `Web.config` (consistent with "do not overwrite live Web.config" practice).

## Scope notes

- **Not changed:** no source, config, transform, SQL data, IIS site, app pool or file on the live site was modified. No GitHub or remote operations (local `git log`/`ls-files`/`check-ignore`/`diff` only). No CloudAgent. **Zebra skipped.**
- **SQL:** only read-only `SELECT`s were run (server and database principals, auth mode, demo-hash match count, org list, `DB_CONSOLE_EXECUTE` audit count). No hashes, passwords or secrets were printed or copied into this doc.
- **Not read:** contents of live `App_Data\dev-security-output.log` (contains one-time codes/links). IIS `applicationHost.config` / `appcmd` (needs elevation), so the app pool identity name is inferred from the SQL login `IIS AppPool\AssetManagement` and Integrated Security.
- **Not verified end-to-end:** the role-rename escalation in #8 (code-path analysis only); DB-side storage and encryption of Platform Settings SMTP credentials.
- **Phase B** (prepare-only, per Allan): #6 least-privilege login script plus backup and restore-test, and #7 `aspnet_regiis -pef` DPAPI steps. **Not started** in this audit.

---

## Phase B status (2026-10-05, SoT only — IIS hold)

Implemented / prepared on local SoT. Details: **`docs/PROD-SECURITY-PHASE-B-2026-10-05.md`**.

| Item | Phase B |
|------|---------|
| #2 Seeded force password change + remove credentials banner | Done in SoT (migration 083 + UI/message cleanup). MFA any-code banner kept ON. |
| #3 DB console audit + IP allowlist | Done in SoT (`DatabaseConsoleAllowedIps`, richer audit). IIS IP Restriction steps documented; not applied to live. |
| #8 Hardcoded roles | Partial: AuthorizationService / scope / RoleService hardened with `IsSystemRole`; leftovers listed in Phase B doc. |
| #9 Lockout alerting | Done in SoT (`AUTH_LOCKOUT` + soft-fail email). |
| #6 Least-privilege + backup | **Prepared only** under `database/scripts/ops/006_*` — do not run until approved. |
| #7 aspnet_regiis DPAPI | **Prepared only** under `database/scripts/ops/007_*` / `docs/PROD-SECURITY-ASPNET-REGIIS-DPAPI-2026-10-05.txt` — do not run on live. |

**IIS:** `C:\inetpub\AssetManagement` untouched. No publish.
