# LIVE DEPLOY CHANGELOG

## Standing rule

**Local (SoT + localhost IIS + `.\SQLEXPRESS`) is NOT live/production.**  
Every change must be recorded here so it can be applied to live later. **One entry per change.**

**Order in this file:** newest first (newest at top).

**Required fields per entry:** Date/time EAT Â· What and why Â· Commit hash Â· Files changed Â· DB migration/script Â· Config/Web.config keys Â· IIS/server steps (local) Â· Live-deploy steps Â· Verify Â· Rollback

## 2026-10-09 00:31 - Asset movement rules: classes are immovable; assets move room<->room, room<->sub-department, class->room only (`107fd27`, `a5292c1`) - BUILT, NOT YET PUBLISHED

- **Date/time EAT:** 2026-10-09 00:26 / 00:31 (commits). Ships in the rel1009 package (rebuilt; see `artifacts\rel1009-pkg-path.txt`). Not published (needs one UAC prompt).
- **What and why:** Allan (D), priority. A class acts as a room. It cannot be moved or re-parented; only the assets in it can be moved to a room (single or "Move group to room"). Allowed asset moves: room->room, room->sub-department, sub-department->room, class->room. Every other move is refused server-side with "This move is not allowed. Assets can only be moved room to room, between a room and a sub-department, or from a class to a room." and hidden in the UI.
  - Before: Move to room was offered from any source (Administrative, Grade, sub-department, class, custody); Move to class (single and group) went into any class; bulk "Move to department" took any kind except Grade; the asset Edit department change was unrestricted; a class could be re-parented to another grade (Edit, or child moves during a kind change). Create under a room was already refused (class parent must be a grade).
  - Now: `AssetMoveRules` (Application/Helpers) is enforced in RelocateToRoom / RelocateGroupToRoom, RelocateToClass / RelocateGroupToClass (always refused), asset Edit department change (also refuses department -> none) and bulk "Move to department" (refused assets are skipped with the message). `DepartmentService.Update` refuses a class re-parent, including ChildMoves. UI: Move to room / Move group to room only on assets in a room, sub-department or class (or with no department); Move to class hidden; bulk target list = rooms + sub-departments; Departments Edit for a class lists only its stored grade.
  - Not changed (flag for Allan): Transfer approval and Assignment flows still set the asset department as a side effect (TransferService ~L420, AssignmentService ~L248). Placing an asset that has no department is treated as not a move and stays allowed.
- **Commit hash:** `107fd27`, `a5292c1`
- **Files changed:** `src/AssetManagement.Application/Helpers/AssetMoveRules.cs` (new; SDK project, auto-included), `Services/Assets/AssetService.cs`, `Services/Assets/AssetBulkService.cs`, `Services/DepartmentService.cs`, `src/AssetManagement.Web/Controllers/AssetsController.cs`, `Views/Assets/Index.cshtml`, `Views/Departments/Edit.cshtml`.
- **DB migration/script:** None. Movements still write AssetMovement rows (093). **Config/Web.config keys:** None.
- **IIS/server steps (local):** ships with `artifacts\rel1009-launch-elevated.ps1` (not yet run).
- **Live-deploy steps:** ships with the web publish. Needs 093 on live for AssetMovement rows.
- **Verify (IIS Express :8092, Test-WF A461 only, wambua9912, 00:27-00:30 EAT):** UI: Administrative list has 0 Move to room buttons, room and class lists show them, no Move to class anywhere. Allowed: room->room 23001 Art room->Music Room (AssetMovement 48), sub->room 24989 Dining->Art room (49), class->room group "Audio mixer" Grade 6A->Art room, 1 moved (50, batch), room->sub bulk 23102 Art room->Dining (51, type BulkDepartment). Refused with the friendly message: bulk room->Administrative (skipped), Administrative->room, room->class. Class re-parent Grade 6A to Grade 2 (injected option) -> "A class acts as a room and cannot be moved or re-parented. Move its assets to a room instead."; the Edit page lists only Grade 6; an unchanged save still works. COPY_ONLY backup first: `AssetMgmt_pre_moverules_20261009_002709.bak`. Assets 23001/23102/24989/25554 and Department 1482 UpdatedAt restored by SQL; AssetMovement rows 48-51 kept as an audit trail. Output: `artifacts\moverules-2026-10-09\`.
- **Rollback:** `git revert a5292c1 107fd27`; after publish restore the `pre_rel1009` IIS backup.

---
## 2026-10-09 00:21 - /Sys/Database (DataConsole) audits every page view and refused attempt (`2525ac9`) - BUILT, NOT YET PUBLISHED

- **Date/time EAT:** 2026-10-09 00:19:57 (commit). In package `web-publish-oct8-merge-20261009_001958` (rel1009). Not published (needs one UAC prompt).
- **What and why:** Allan (C): earlier check found only `DB_CONSOLE_EXECUTE` was audited. Now every allowed page view writes `DB_CONSOLE_VIEW` and every refusal writes `DB_CONSOLE_DENIED` (reasons `unauthenticated`, `not_platform_admin`, `ip_not_allowlisted`), logged before the redirect to Login/Forbidden. NewValues: `allowed|refused reason=... user=<email|anonymous> ip=<direct connection address, as the allowlist uses> method=... path=...`; AuditLog.Timestamp (UTC). The gate now also runs in `Controller.OnAuthorization` (before `[Authorize]` and anti-forgery) so anonymous refusals are logged. Signed-in users go through `IAuditWriter` (same mechanism as DB_CONSOLE_EXECUTE); anonymous or no-org cases insert the AuditLog row directly with OrganizationId NULL. Logging failures are traced, never thrown.
- **Commit hash:** `2525ac9`
- **Files changed:** `src/AssetManagement.Web/Controllers/DatabaseConsoleController.cs`.
- **DB migration/script:** None (inserts into existing AuditLog only). **Config/Web.config keys:** None.
- **IIS/server steps (local):** ships with `artifacts\rel1009-launch-elevated.ps1` (not yet run).
- **Live-deploy steps:** ships with the web publish.
- **Verify (IIS Express :8092, SQLEXPRESS, 00:20-00:22 EAT; no SQL run in the console):** anonymous `/Sys/Database` and `/Sys/DataConsole` -> Login, rows 51142/51143 `DB_CONSOLE_DENIED reason=unauthenticated user=anonymous ip=::1`; Test-WF Company Admin `/Sys/Database` -> 403 in-app Forbidden, row 51149 `reason=not_platform_admin user=wambua9912@gmail.com` (org 13); SuperAdmin `/Sys/Database` and `/Sys/DataConsole` 200, rows 51152/51153 `DB_CONSOLE_VIEW allowed user=superadmin@asset.local ip=::1`; with the temp copy's allowlist set to `10.255.255.1` (temp IIS Express copy only, restored after), SuperAdmin -> 403, row 51157 `reason=ip_not_allowlisted`. Output: `artifacts\roleactive-2026-10-09\10_*.txt`, `11_*.txt`.
- **Rollback:** `git revert 2525ac9`; after publish restore the `pre_rel1009` IIS backup.

---
## 2026-10-09 00:16 - Role Active flag: users in an inactive role cannot sign in and are signed out (`b59ae9a`, `06762d5`) - BUILT, NOT YET PUBLISHED

- **Date/time EAT:** 2026-10-09 00:09:58 / 00:16:08 (commits). In package rel1009. Not published.
- **What and why:** Allan: a role can be marked inactive; nobody in it may use the app. `Roles.IsActive` (with Create/Edit checkbox, list badge and filter) already existed but was not enforced.
  - Login (password) refuses right after the password check, **before MFA** (no code sent), with the generic `Access denied.` (no role or reason shown); not counted as a failed password attempt. VerifyMfa GET, VerifyEmail POST and `IssueAuthCookieAndRedirect` (end of every login path incl. MFA setup/verify, email verification, legal consent) re-check. No SSO/demo path issues cookies separately.
  - Signed-in sessions: `Global.asax` PostAuthenticateRequest signs the user out on the next request and redirects to the tenant login with `?reason=access_denied` (generic message). No cache: one primary-key join per authenticated request, so a deactivation applies immediately.
  - Never applies to platform users or platform roles (Platform Admin, OrganizationId NULL). System roles (Platform Admin, Company Admin) cannot be deactivated (RoleService), and a user cannot deactivate their own role (RolesController). Edit shows a disabled checkbox for system roles.
  - Denials are written to `SecurityEvents` (`AUTH_DENIED_ROLE_INACTIVE`, `AUTH_SESSION_ENDED_ROLE_INACTIVE`), not shown in the UI.
  - `06762d5`: Create with Active unchecked was saved Active because the unit of work forces IsActive = true on insert; RoleService.Create now updates it after insert.
- **Commit hash:** `b59ae9a`, `06762d5`
- **Files changed:** `src/AssetManagement.Web/Security/RoleAccessGuard.cs` (new, in csproj), `Global.asax.cs`, `Controllers/AccountController.cs`, `Controllers/RolesController.cs`, `Views/Roles/Create.cshtml`, `Views/Roles/Edit.cshtml`, `AssetManagement.Web.csproj`; `src/AssetManagement.Infrastructure/Identity/UserAccountRepository.cs`; `src/AssetManagement.Application/Services/Security/RoleService.cs`, `ViewModels/Security/SecurityViewModels.cs` (label "Active").
- **DB migration/script:** **None; no 095 written.** `Roles.IsActive BIT NOT NULL CONSTRAINT DF_Roles_IsActive DEFAULT(1)` has existed since `001_Schema/002_Domain.sql` (seed scripts already set it); all 93 local roles are Active. Nothing to back up for a migration. A COPY_ONLY + CHECKSUM backup was taken before the test toggles: `C:\Program Files\Microsoft SQL Server\MSSQL15.SQLEXPRESS\MSSQL\Backup\AssetMgmt_pre_roleactive_test_<ts>.bak` (exact path in `artifacts\roleactive-2026-10-09\backup-path.txt`; VERIFYONLY valid).
- **Config/Web.config keys:** None.
- **IIS/server steps (local):** ships with `artifacts\rel1009-launch-elevated.ps1` (not yet run).
- **Live-deploy steps:** web publish only; check on live that no role is unexpectedly `IsActive = 0` before publishing (`SELECT Id, Name, OrganizationId FROM Roles WHERE IsActive = 0`), since those users will be locked out at once.
- **Verify (IIS Express :8092 on the rel1009 code, SQLEXPRESS, 00:11-00:18 EAT, Test-WF only):** Roles list shows Active/Inactive badges; Company Admin (system) Edit shows a disabled checkbox; disposable role `ZZ RoleActive Test 1009` created with Active unchecked -> Inactive badge, edited to Active -> Active badge. Staff role (64) toggled inactive via Edit: signed-in Staff session's next request -> `/a46138179/Account/Login?reason=access_denied` with "Access denied."; fresh Staff login -> stays on Login with only "Access denied." and no MFA step; Company Admin `wambua9912` unaffected (login with MFA OK). Staff re-activated -> Staff login OK. SecurityEvents rows 12-14. **Cleanup:** Staff back to Active with original UpdatedAt (2026-10-08 05:46:25.273) and 0 permissions; disposable roles 165/166 deleted; 93 roles, 0 inactive. Left in place: SecurityEvents rows 12-14 and the AuditLog rows for the test actions (Roles.Create/Edit, Permissions.Assign).
- **Rollback:** `git revert 06762d5 b59ae9a`; after publish restore the `pre_rel1009` IIS backup. No DB rollback (no schema change).

---
## 2026-10-09 00:03 - Revert the custodian self-service bypass from `472b707` (`9670153`) - BUILT, NOT YET PUBLISHED

- **Date/time EAT:** 2026-10-09 00:03:45 (commit). In package rel1009. Not published.
- **What and why:** Allan: Staff has 0 permissions by design (teachers/staff are not supposed to log in), so nobody gets access without role permissions. Removed `AllowCustodianSelfServiceAttribute`, its OnAuthorization exemptions in PermissionAuthorize / AnyPermissionAuthorize / ModuleAccess, and the custodian-only Details path; MyAssets and Details are back to `[PermissionAuthorize("Assets.View")]` (`AssetsController.cs` identical to before 472b707). **Kept:** ForbiddenResults (in-app Forbidden page, status 403, TrySkipIisCustomErrors), BaseController.OnActionExecuted, HomeController/Forbidden.cshtml, and ModuleAccess authenticated denial -> friendly 403 (anonymous still goes to login).
- **Commit hash:** `9670153`
- **Files changed:** `src/AssetManagement.Web/Filters/AllowCustodianSelfServiceAttribute.cs` (deleted), `Filters/PermissionAuthorizeAttribute.cs`, `Filters/AnyPermissionAuthorizeAttribute.cs`, `Filters/ModuleAccessAttribute.cs`, `Controllers/AssetsController.cs`, `AssetManagement.Web.csproj`.
- **DB migration/script:** None. **Config:** None.
- **IIS/server steps (local):** ships with `artifacts\rel1009-launch-elevated.ps1` (not yet run).
- **Live-deploy steps:** ships with the web publish.
- **Verify (IIS Express :8092, rel1009 package):** Staff `/Assets/MyAssets`, `/Assets/Details/32197`, `/Assets/Details/32193` -> 403 in-app Forbidden page with app chrome (title "Forbidden - Asset Management Module"), not the IIS 403.0 page.
- **Rollback:** `git revert 9670153` (restores the bypass; not wanted).

---
## 2026-10-08 23:59 - DB: migrations 093 AssetMovement (+ 093b backfill) and 094 OrganizationSmtpSettings on local `.\SQLEXPRESS`

- **Date/time EAT:** 2026-10-08 23:58:52 (backup) to 23:59:24 EAT (093 recorded 23:59:09, 094 recorded 23:59:23). Local `.\SQLEXPRESS` / `AssetManagementModuleDb` only (the DB behind localhost:8080). Not live.
- **What and why:** Allan explicitly approved (Oct 8 23:52, relayed by PM) running 093, 093b and 094 locally. They turn on the Movements page / asset Movement tab (093) and per-company SMTP settings at Settings > Email (094). Until now the app showed "not available yet". Scripts run as-is; no org-specific changes.
- **Commit hash:** scripts already in main (`d51fbbf` and earlier); this entry is in the commit after `86abb17`.
- **Files changed:** none (DB only). Output: `artifacts\db-093-094-2026-10-08\01_recheck.txt` ... `08_verify.txt`, `iis-mig-smoke.txt`, screenshots `iis-01..03`.
- **DB migration/script:**
  1. Re-check before running: `AssetMovement` and `OrganizationSmtpSettings` absent, 0 history rows, 0 `Assets.Movements.View` permission rows (RolePermission total 1622, Permission total 69).
  2. Backup: `BACKUP DATABASE ... WITH COPY_ONLY, INIT, CHECKSUM` + `RESTORE VERIFYONLY` OK (6,689 pages) -> **`C:\Program Files\Microsoft SQL Server\MSSQL15.SQLEXPRESS\MSSQL\Backup\AssetMgmt_pre_093_094_20261008_235852.bak`** (54,882,816 bytes).
  3. `004_Migrations/093_AssetMovement.sql`: table `dbo.AssetMovement` created (4 FKs, 3 indexes + PK); `Permission` `Assets.Movements.View` inserted (Id 70, 1 row); **49 RolePermission rows** inserted (1622 -> 1671), every one with `OrganizationId` = the role's org (49/49). Per role: Asset Manager 10, Auditor 10, Company Admin 10, Department Head 10, Facilities Manager 9. Orgs: 1, 9, 10, 11, 13, 18, 21, 23, 24, 25 (5 roles each except org 9 and 10: 4 each) and org 2 (Facilities Manager only). Staff and other roles are unchanged. History row Id 97 `093_AssetMovement.sql` (AppliedBy `ALLAN\allan`, AppliedAt default UTC).
  4. `ops/093b_Backfill_AssetMovement_FromCustody.sql` (all orgs, `@OrgId = NULL`): **47 rows** backfilled from `AssetCustodyEvent` (org 1 Nanosoft 6, org 13 Test-WF 36, org 23 Wambua Lifecycle Co 5; types Assign 19, Room 8, Class 7, Transfer 4, Dept 2, Return 2, Other 5). Ops script, not recorded in history (by design).
  5. `004_Migrations/094_OrganizationSmtpSettings.sql`: table `dbo.OrganizationSmtpSettings` created (1 FK, PK + 1 index, 0 rows). History row Id 98.
  No errors (sqlcmd `-b`, exit 0 for every step).
- **Config/Web.config keys:** None.
- **IIS/server steps (local):** None. No recycle needed: the running build (`d51fbbf`) detects the tables per request.
- **Live-deploy steps (later, when approved):** back up the live DB; run 093 (idempotent, additive; note it grants the new permission to Company Admin and every role holding Assets.Assign/Assets.Transfer/AuditLogs.View in every org, so review the per-role list on live first); optionally 093b (idempotent, keyed by `SourceRef = custody:<id>`); run 094; insert the two `SchemaMigrationHistory` rows (the scripts do not do it themselves).
- **Verify (local, 00:00 EAT, live :8080 as Test-WF Company Admin):** `/a46138179/AssetMovements/Index` 200, no "not available yet", 36 table rows (= the Test-WF backfill); `/a46138179/Settings/Email` 200, no "094 pending" text; `/a46138179/Assets/Details/32197` 200, no "not available yet". No YSOD and no IIS error page on any of them.
- **Rollback:** preferred: restore the `.bak` above (stop the `AssetManagement` pool first). Script-level rollback (from the script headers): `DROP TABLE dbo.OrganizationSmtpSettings`; `DELETE rp FROM RolePermission rp JOIN Permission p ON p.Id = rp.PermissionId WHERE p.Code = N'Assets.Movements.View'`; `DELETE FROM Permission WHERE Code = N'Assets.Movements.View'`; `DROP TABLE dbo.AssetMovement`; `DELETE FROM dbo.SchemaMigrationHistory WHERE ScriptName IN (N'093_AssetMovement.sql', N'094_OrganizationSmtpSettings.sql')`.

---
## 2026-10-08 23:55 - Staff 403 fix: custodians open My Assets and their own asset Details; denials show the in-app Forbidden page (`472b707`) - SUPERSEDED, NEVER PUBLISHED

- **SUPERSEDED (2026-10-09 00:03):** Allan changed the plan: Staff is meant to have 0 permissions (teachers/staff do not log in), so the custodian self-service bypass was reverted in `9670153` and this package was never published. The friendly in-app 403 part ships with the rel1009 publish (see the 00:21 entries above). `staff403-*.ps1` publish scripts renamed `*.SUPERSEDED`.
- **Date/time EAT:** 2026-10-08 23:55:20 (commit). Package built 23:55:31. **Not published to IIS yet** (the publish needs one UAC prompt; waiting for the PM to warn Allan).
- **What and why:** AM QA FAIL on `d51fbbf` (Test-WF A461): a Staff user (role without permissions) got 403 on `/Assets/MyAssets` and on Details of their own assigned asset, rendered as the bare "IIS 10.0 Detailed Error - 403.0" page. Root cause: AssetsController has class-level `[PermissionAuthorize("Assets.View")]` + `[ModuleAccess(Assets)]` (and MyAssets/Details had method-level Assets.View), so authorization failed before the action's custodian scoping ran. Denials returned a bodiless `HttpStatusCodeResult(403)` without `TrySkipIisCustomErrors`, so IIS replaced it. ModuleAccess denials returned 401 and bounced to login. Fix is generic (any org), no DB or permission change:
  - New `[AllowCustodianSelfService]` marker. PermissionAuthorize, AnyPermissionAuthorize and ModuleAccess let **signed-in** users through on marked actions only. Anonymous users still get 401 -> login.
  - MyAssets marked (rows already limited to CustodianUserId = self, current org). Details marked: without Assets.View + Assets module access, only the current custodian of an active asset in the current org may open it; otherwise in-app 403 "You can only view assets that are assigned to you."
  - `ForbiddenResults` / `ForbiddenViewResult`: renders `~/Views/Home/Forbidden.cshtml` with the app layout, StatusCode 403, `TrySkipIisCustomErrors = true`; plain 403 text for AJAX/JSON. Used by the Permission, AnyPermission, Tenant and ModuleAccess (authenticated) denials, and by `BaseController.OnActionExecuted` for any action returning `HttpStatusCodeResult(403)`. `Home/Forbidden` also sets TrySkipIisCustomErrors.
- **Commit hash:** `472b707` (+ `86abb17` untracks `Properties/PublishProfiles/FolderProfile1.pubxml`, a local VS profile swept into 472b707 by mistake; the file stays on disk).
- **Files changed:** `src/AssetManagement.Web/`: `Filters/ForbiddenResults.cs` (new), `Filters/AllowCustodianSelfServiceAttribute.cs` (new), `Filters/PermissionAuthorizeAttribute.cs`, `Filters/AnyPermissionAuthorizeAttribute.cs`, `Filters/ModuleAccessAttribute.cs`, `Filters/TenantAuthorizeAttribute.cs`, `Controllers/AssetsController.cs`, `Controllers/BaseController.cs`, `Controllers/HomeController.cs`, `Views/Home/Forbidden.cshtml`, `AssetManagement.Web.csproj` (2 Compile includes).
- **DB migration/script:** None. Staff role permissions untouched. **Config/Web.config keys:** None.
- **IIS/server steps (local):** `artifacts\oct8-merge-build.ps1` at HEAD 472b707: Release Rebuild, 0 errors, 184 warnings. Package `artifacts\web-publish-oct8-merge-20261008_235527`, `AssetManagement.Web.dll` SHA256 `03BFC43DF90130692568AED4DA327EE0F8C44368955270A00002C4C0E58B0D45`. Publish prepared, not run: `artifacts\staff403-launch-elevated.ps1` -> `staff403-iis-publish.ps1` (checks live Web.dll `1E7B6B8D...F353` and Web.config `BF64D20E...44EB`; backup to `C:\inetpub\_backups\AssetManagement_root_pre_staff403_<ts>`; stop pool; copy excluding Web.config and secrets; hash check; start pool; rollback on error).
- **Live-deploy steps:** ships with the next web publish; no config or DB step.
- **Verify (IIS Express :8091 on a temp copy of the package, 23:56 EAT):** Staff `staff.a46138179@asset.local`: My Assets 200 with app chrome, 2 assets (32195, 32197); Details/32197 200; Details/32193 **403 in-app Forbidden page** (layout, "You can only view assets that are assigned to you."), not the IIS page; Assets/Index and Users/Index 403 in-app page. Company Admin: Details/32193 200, My Assets 200, Departments/Index 200. To do after publish: same smoke against :8080 (`SB=http://127.0.0.1:8080 TAG=iis node staff403-smoke.mjs`).
- **Rollback:** `git revert 472b707`; after publish: restore the `pre_staff403` backup (Web.dll back to `1E7B6B8D3A36DDE5C9A15DC6EEC34CDCD7460FD93D2D17792FEEF777AC73F353`).

---
## 2026-10-08 23:41 - ROOT local IIS publish of main `d51fbbf` (all unpublished Oct 8 work + 3 merges)

- **Date/time EAT:** 2026-10-08 23:41:24 EAT (UAC accepted by Allan 23:40:54). Local only: not pushed, no PR, not live nanosoft.africa.
- **What and why:** Allan GO (relayed by PM 23:36): publish everything built since the keyrot publish (IIS was `682ce85`). HEAD `d51fbbf` contains every commit in the entries below dated 16:20-23:38, i.e. 49 non-merge commits not in `682ce85` (`git log --no-merges 682ce85..d51fbbf`).
- **Commit hash:** `d51fbbf` (final HEAD of local main at publish).
- **Files changed:** see the per-change entries below (code, views, CSS, JS, csproj, SQL scripts).
- **DB migration/script:** **None run tonight.** Pending, NOT applied on `.\SQLEXPRESS` (app tolerates absence and shows "not available yet"): `004_Migrations/093_AssetMovement.sql` (+ optional `ops/093b_Backfill_AssetMovement_FromCustody.sql`), `004_Migrations/094_OrganizationSmtpSettings.sql`. Not run because 093 also grants a new permission to roles in every org and both turn on new features; needs Allan's explicit go. `ops/IntegrityCheck_CrossOrgReferences.sql` is read-only; `proposals/PROPOSAL_UsersRoleId_SameOrg_FK_DO_NOT_RUN.sql` must not be run. Already-applied DB changes today are listed in their own entries (19:49, 20:53, 21:07, 21:09).
- **Config/Web.config keys:** None changed. Root `Web.config` (keyrot v2 `SystemEncryptionKey`) preserved: SHA256 `BF64D20EF695EED2753522021CEC33EE5A71AB43622499C82A4355E3703444EB` before and after. `smtp.secrets.config` unchanged (`2F729C3F...23EF`). Live IIS Web.config has no `DatabaseConsoleAllowedIps` key, so the console uses the code default (loopback only).
- **IIS/server steps (local):**
  1. `artifacts\oct8-merge-build.ps1`: Release Rebuild (0 errors, 184 warnings) + WebPublish to `artifacts\web-publish-oct8-merge-20261008_234024`; Web.config renamed out of the package.
  2. `artifacts\oct8-merge-launch-elevated.ps1` -> `oct8-merge-iis-publish.ps1` (one UAC): checks live Web.dll = `CFF546F9...` and Web.config hash; full backup to **`C:\inetpub\_backups\AssetManagement_root_pre_oct8_merge_20261008_234109`** (1,585 files, excludes old `_bak`); stop pool `AssetManagement`; copy 279 files (skip Web.config / secrets / view web.configs, which were identical); hash-check 279/279, 0 mismatches; start pool.
  3. Log: `artifacts\oct8-merge-iis-publish.log`.
- **DLLs (IIS bin):** `AssetManagement.Web.dll` SHA256 **`1E7B6B8D3A36DDE5C9A15DC6EEC34CDCD7460FD93D2D17792FEEF777AC73F353`** (built 23:40:31 EAT; was `CFF546F9...CCE03`). Application `E474D87C92617F8E7A755864FA2C526D244284296FC941538874E2B37A4F61C3`, Infrastructure `8E97867661EDAE82B06A27C04E595176779394404471259E99571A3F951B4A25`, Domain `98886E473C9604644047ADF7999022025305AE1CABD2B79C586D15A01EF23497`.
- **Live-deploy steps (later, when approved):** (1) back up live DB; (2) run 093 then 094 (both idempotent, additive) if the features are wanted, optionally 093b; (3) the 19:49 / 21:07 / 21:09 data fixes are local-DB specific: re-run their dry-run/check scripts against live first, do not blindly apply; (4) publish web from `d51fbbf` or later, preserving live Web.config (live needs its own v2 key, see 20:02 entry); (5) recycle pool.
- **Verify (local, 23:41-23:50):** `/Account/Login` and `/A46138179/Account/Login` 200, no YSOD. As Test-WF Company Admin: `/a46138179/Departments/Index` 200, `badge-kind` x63, `am-dept-indent-0/1/2` rows 12/36/15, computed padding-left 4px/36px/68px, kind-count strip "5 Admin 12 Sub 27 Room 5 Grade 14 Class"; `?view=list` shows Parent column. SuperAdmin: `/Platform/Usage` 200; `/Sys/Database` 200 from loopback; via LAN IP `192.168.30.122` -> 302 to `/Home/Forbidden` (403, "IP allowlist"), spoofed `X-Forwarded-For: 127.0.0.1` also 403. Read-only `03_check_roleperm.sql`: 0 mismatches / 9 platform rows / 0 duplicates.
- **Rollback:** elevated PowerShell: `Stop-WebAppPool AssetManagement`; `robocopy C:\inetpub\_backups\AssetManagement_root_pre_oct8_merge_20261008_234109 C:\inetpub\AssetManagement /E /XF Web.config smtp.secrets.config /XD _bak`; `Start-WebAppPool AssetManagement`; confirm Web.dll = `CFF546F9CDCF5D0DB859294B5398306E626407201A135CE40315DEB2999CCE03`. No DB rollback needed (no scripts run).

---
## 2026-10-08 23:38 - site.css: repair mojibake em dash in a comment (`d51fbbf`)

- **Date/time EAT:** 2026-10-08 23:38 (commit), published 23:41.
- **What and why:** Merge check found `U+00E2 U+20AC U+201D` (mojibake em dash) in the `RESPONSIVE SHELL` CSS comment, introduced by `57edbc3` on Oct 7. Replaced with a real em dash; file stays UTF-8 without BOM (the BOM was already dropped by `ui-qa2-1008`). Comment only, no rendering change.
- **Commit hash:** `d51fbbf`
- **Files changed:** `src/AssetManagement.Web/Content/css/site.css` (1 line).
- **DB migration/script:** None. **Config:** None.
- **IIS/server steps (local):** included in the 23:41 publish.
- **Live-deploy steps:** ships with the web publish.
- **Verify:** no `U+00E2 U+20AC` sequence in site.css; `.am-dept-indent-*` and `.badge-kind` rules unchanged.
- **Rollback:** `git revert d51fbbf` (harmless either way).

---
## 2026-10-08 23:38 - csproj: include `asset-label-zebra-loader.js` (`ffd5773`)

- **Date/time EAT:** 2026-10-08 23:38 (commit), published 23:41.
- **What and why:** `ui-qa2-1008` (`3237867`) added `Scripts/app/asset-label-zebra-loader.js` and referenced it from Assets/Details, Assets/PrintLabel and AssetScan/Lookup, but did not add it to `AssetManagement.Web.csproj`, so WebPublish would leave it out (same class of bug as the Oct 6 missing-view 500s). A scan of all tracked Web `.cs/.cshtml/.js/.css` files against the csproj found this as the only missing item.
- **Commit hash:** `ffd5773`
- **Files changed:** `src/AssetManagement.Web/AssetManagement.Web.csproj` (+1 `Content Include`).
- **DB migration/script:** None. **Config:** None.
- **IIS/server steps (local):** included in the 23:41 publish (file present in package and IIS, hash-matched).
- **Live-deploy steps:** ships with the web publish.
- **Verify:** `C:\inetpub\AssetManagement\Scripts\app\asset-label-zebra-loader.js` exists; Print/Tag pages load the loader without 404.
- **Rollback:** `git revert ffd5773` (would break Zebra lazy-load on publish).

---
## 2026-10-08 23:37-23:38 - Local main merges: roleperm-org-fix, qa2-fixes, ui-qa2-1008

- **Date/time EAT:** 2026-10-08 23:37-23:38 (local merges, `--no-ff`, no rebase/force), published 23:41.
- **What and why:** PM/Allan asked for these three branches on main before the publish.
  - `225afe6` Merge `roleperm-org-fix` (`1a60ac2`, incl. `19c0edb`): no conflicts.
  - `e2a7441` Merge `qa2-fixes` (`c1f51bb`): no conflicts.
  - `83071ba` Merge `ui-qa2-1008` (`3237867`): **1 conflict**, `src/AssetManagement.Application/Services/Assets/AssetService.cs` (custody timeline). Resolved keeping both intents: qa2 ordering (`CustodyTimelineOrdering.EffectiveActionDate` desc, then CreatedAt desc, then Id desc, in memory) **and** ui-qa2 user-name lookup (`BuildUserDisplayLookup` over From/To/ApprovedBy ids passed to `BuildTransferEntityLabel`).
  - Overlaps auto-merged and reviewed: `BaseController.cs` (qa2 `OnActionExecuting` OrganizationWide reset + `CanViewAssetInScope` + R-DEADMOVE class list; ui-qa2 `NotFoundPage`), `DepartmentsController.cs`, `AssetsController.cs`, `AssetViewModels.cs`.
- **Commit hash:** `225afe6`, `e2a7441`, `83071ba`.
- **Files changed:** union of the branch commits listed in the 21:44-21:55 entries and the 20:57/21:10 roleperm commits.
- **Kept intact (checked after merge):** Oct 7 kind badges (`DepartmentKindHtmlHelpers`, `.badge-kind`) and name indent `.am-dept-indent-N { padding-left: calc(0.25rem + N*2rem) }` (0/36/68px; 1.25rem step on narrow screens) in `site.css`, `Views/Departments/Index.cshtml`, `_DepartmentChildList.cshtml`. ui-qa2 adds a child count to depth-0 names and a Parent column in list view.
- **DB migration/script:** none added by qa2-fixes or ui-qa2-1008. roleperm-org-fix edits seed migrations `002_Phase3.sql` / `060_SchoolRolesAndPermissions.sql` (fresh installs only) and carries the 21:09 retag scripts (already applied, see 21:09 entry).
- **Config:** None.
- **IIS/server steps (local):** Release build + 23:41 publish.
- **Live-deploy steps:** ships with the web publish from `d51fbbf`.
- **Verify:** `git log --merges -3`; Release build 0 errors; Departments Index smoke (see 23:41 entry).
- **Rollback:** `git revert -m 1 83071ba` / `e2a7441` / `225afe6` (newest first) and republish; or restore the 23:41 IIS backup.

---
## 2026-10-08 21:44-21:55 - qa2-fixes branch (Asset Dev QA2/QA3), merged in `e2a7441`

- **Date/time EAT:** commits 21:44-21:55; merged 23:37; published 23:41.
- **What and why:** fixes for Asset Dev QA2/QA3 findings: custody timeline, search, scope 404s, custodian My Assets, dept room codes, OrganizationWide query-string ignore, dead Move.

| Commit | EAT | What | Why | Files |
|---|---|---|---|---|
| `bdb5908` | 21:44 | fix(custody): timeline sorts same-day assignments by real time (QA2 item 1) | See commit message. | `Application/Helpers/CustodyTimelineOrdering.cs`, `Application/Services/Assets/AssetService.cs`, `Application/Services/Assignments/AssignmentService.cs`, `Application/Services/Returns/ReturnService.cs`, `Tests/Assets/CustodyTimelineOrderingTests.cs` |
| `8d9276f` | 21:48 | fix(time): label Assignments history dates as EAT (QA2 item 2) | See commit message. | `Web/Views/Assignments/Index.cshtml`, `Web/Views/Assignments/List.cshtml` |
| `e30bf2b` | 21:48 | fix(assets): search matches the current custodian's name and email (QA2 item 5) | See commit message. | `Infrastructure/Queries/AssetQueryService.cs` |
| `cab4fb3` | 21:48 | fix(security): Transfers/Create GET returns 404 for assets outside the user's scope (QA2 item 6) | See commit message. | `Web/Controllers/BaseController.cs`, `Web/Controllers/TransfersController.cs` |
| `32f0003` | 21:48 | fix(security): Assignments/Create GET and asset assignment history are scope-checked (QA2 item 7) | See commit message. | `Web/Controllers/AssignmentsController.cs` |
| `6f9927d` | 21:48 | fix(assets): custodians see the assets they hold in My Assets and Details (QA2 item 8) | See commit message. | `Application/Services/Assets/AssetService.cs`, `Application/ViewModels/Assets/AssetViewModels.cs`, `Web/Controllers/AssetsController.cs` |
| `e0e06c8` | 21:50 | fix(security): Departments Details/Edit/KindChangeImpact enforce department scope (QA3-1) | See commit message. | `Application/Services/DepartmentService.cs`, `Web/Controllers/DepartmentsController.cs` |
| `59e3014` | 21:53 | fix(departments): room codes fit 20 chars and stay unique; unchanged long code no longer blocks Edit (QA3-2 a,c) | See commit message. | `Application/Helpers/SchoolDepartmentCodeHelper.cs`, `Application/Services/DepartmentService.cs`, `Web/Controllers/DepartmentsController.cs`, `Tests/Departments/DepartmentServiceCreateFlowTests.cs` |
| `952f644` | 21:54 | fix(assets): scoped Transfer users get class targets so the unit Move works (QA3 R-DEADMOVE, dev half) | See commit message. | `Web/Controllers/BaseController.cs` |
| `c1f51bb` | 21:54 | fix(security): ignore OrganizationWide from the query string on asset filters (found during QA2 item 5/8) | See commit message. | `Web/Controllers/BaseController.cs` |

- **DB migration/script:** None. **Config:** None.
- **IIS/server steps (local):** via merge + 23:41 publish.
- **Live-deploy steps:** ships with web publish.
- **Verify:** AM QA headed retest (custody, search, My Assets, Move, scope 404s) after 23:41.
- **Rollback:** `git revert -m 1 e2a7441` and republish.

---
## 2026-10-08 21:49 - ui-qa2-1008 branch (Asset UI Fixes pack), merged in `83071ba`

- **Date/time EAT:** commits 21:49; merged 23:38; published 23:41.
- **What and why:** Asset UI Fixes pack (see `ASSETUIFIXES.md`): Zebra lazy-load, disposal flash, Move buttons, dept kind counts + Parent column, friendly 404, serial search, custody names.

| Commit | EAT | What | Why | Files |
|---|---|---|---|---|
| `ea68e0a` | 21:49 | fix(assets): map custody timeline user ids to display names | See commit message. | `Application/Services/Assets/AssetService.cs`, `Application/ViewModels/Assets/AssetViewModels.cs`, `Web/Views/Assets/Details.cshtml` |
| `ac660db` | 21:49 | fix(assets): stop client table filter hiding server serial search results | See commit message. | `Web/Views/Assets/Index.cshtml` |
| `61086b1` | 21:49 | fix(web): friendly 404 page for out-of-scope assets (skip IIS errors) | See commit message. | `Web/Controllers/AssetsController.cs`, `Web/Controllers/BaseController.cs`, `Web/Views/Shared/NotFound.cshtml` |
| `d8a0c22` | 21:49 | feat(departments): Index kind counts, core child counts, and Parent column | See commit message. | `Web/Controllers/DepartmentsController.cs`, `Web/Views/Departments/Index.cshtml` |
| `80c8e76` | 21:49 | fix(assets): show Move group buttons on Grouped summary rows | See commit message. | `Web/Content/css/site.css` |
| `c16445c` | 21:49 | fix(departments): show model-state and field errors on Edit | See commit message. | `Web/Views/Departments/Edit.cshtml` |
| `eed9028` | 21:49 | fix(assets): hide class Move when relocate modal is not rendered | See commit message. | `Web/Scripts/app/assets-grouped.js`, `Web/Views/Assets/Index.cshtml` |
| `54d9d57` | 21:49 | fix(assets): show Not set for book value when acquisition cost is unset | See commit message. | `Web/Views/Assets/Details.cshtml` |
| `3af1e45` | 21:49 | fix(assets): disposal flash distinguishes immediate dispose vs pending approval | See commit message. | `Web/Controllers/AssetsController.cs` |
| `30be522` | 21:49 | fix(web): GroupedDepartmentDropDown honours explicit id attributes | See commit message. | `Web/Helpers/DepartmentSelectHtmlHelpers.cs` |
| `3237867` | 21:49 | fix(labels): lazy-load Zebra helper on Print/Tag; no BrowserPrint until click | See commit message. | `Web/Scripts/app/asset-label-zebra-loader.js`, `Web/Scripts/app/asset-scan-batch-print.js`, `Web/Views/AssetScan/Lookup.cshtml`, `Web/Views/Assets/Details.cshtml`, `Web/Views/Assets/PrintLabel.cshtml` |

- **DB migration/script:** None. **Config:** None.
- **IIS/server steps (local):** via merge + csproj fix `ffd5773` + 23:41 publish.
- **Live-deploy steps:** ships with web publish (needs `ffd5773`).
- **Verify:** Departments Index kind-count strip + Parent column (verified 23:4x); AM QA retest for Zebra/disposal/404/serial.
- **Rollback:** `git revert -m 1 83071ba` and republish.

---
## 2026-10-08 21:09 - DB: RolePermission OrganizationId retag (31 rows) - ALREADY APPLIED

- **Date/time EAT:** backup 20:56; retag committed **2026-10-08 21:09:32 EAT** on `.\SQLEXPRESS` / `AssetManagementModuleDb`.
- **What and why:** 31 RolePermission rows were stamped org 1 (Nanosoft) by the 005/006 catch-all but belong to roles of org 2 (13 rows, from 060), org 9 (9) and org 10 (9) (from 002_Phase3). Retagged to the role's org. Platform Admin rows (role 1, NULL org, 9 rows) intentionally left alone.
- **Commit hash:** `19c0edb` (seed fix + scripts), `1a60ac2` (live output); on main via `225afe6`.
- **Files changed:** `database/scripts/004_Migrations/002_Phase3.sql`, `060_SchoolRolesAndPermissions.sql`, `artifacts/roleperm-org-fix-2026-10-08/*`.
- **DB migration/script:** `artifacts/roleperm-org-fix-2026-10-08/01_backup.sql` (creates `dbo.Bak_RolePermOrg_20261008`, 31 rows; CSV copy), `02_retag.sql -v COMMIT=0` then `COMMIT=1`, `03_check_roleperm.sql` (read-only). Full COPY_ONLY backup: `C:\Program Files\Microsoft SQL Server\MSSQL15.SQLEXPRESS\MSSQL\Backup\RolePerm_preretag_20261008.bak`.
- **Config:** None.
- **Deploy order:** backup -> dry run -> commit -> check. Code (seed fix) is only relevant to fresh installs.
- **Live-deploy steps:** run `03_check_roleperm.sql` on live first; only if live shows mis-tagged rows, run 01 -> 02 dry run -> 02 commit with live-specific counts (the script asserts 31; adjust/verify before use).
- **Verify:** re-checked 2026-10-08 23:4x EAT: `tenant_role_mismatches=0`, `platform_role_rows_left_alone=9`, `duplicate_triples=0`, no mismatch rows. Not re-run.
- **Rollback:** `99_rollback.sql -v COMMIT=0` then `-v COMMIT=1` (restores OrganizationId by Id from the backup table).

---
## 2026-10-08 21:07 - DB: Roles.Description mojibake repair (10 rows) - ALREADY APPLIED

- **Date/time EAT:** **2026-10-08 21:07:22 EAT** (PM log: ~21:05). Post-verify 21:07:48.
- **What and why:** Staff role description stored `U+00E2 U+20AC U+201D` instead of an em dash in 10 orgs (1, 9, 10, 11, 13, 18, 21, 23, 24, 25). Root cause: the PowerShell seed runner read BOM-less SQL as Windows-1252 (source fixed in `ea2a2f6`). Data replaced with ASCII " - " text.
- **Commit hash:** source fix `ea2a2f6` (merged `499123a`); data repair has no code commit.
- **Files changed:** `tools/database/_Common.ps1`, `tools/database/Initialize-Database.ps1`, `database/scripts/002_Seed/001_ReferenceData.sql`, `004_Migrations/040_RequisitionWorkflowDefaults.sql`; scripts in `artifacts/mojibake-fix-2026-10-08/`.
- **DB migration/script:** `02_REPAIR_Roles_Description_DO_NOT_RUN_UNTIL_APPROVED.sql` (run once on approval; backs up to `dbo.Bak_Roles_Mojibake_20261008`, BackedUp=10 Updated=10, committed). Before-CSV: `Roles_mojibake_before_20261008.csv`.
- **Config:** None.
- **Deploy order:** dry run `01_DRYRUN_SELECT_ONLY.sql` -> repair -> `06` verify -> Roles page check.
- **Live-deploy steps:** run `01_DRYRUN_SELECT_ONLY.sql` on live; repair only rows it reports.
- **Verify:** `06_POST_REPAIR_verify.txt` (all 10 rows hex `...2D00...`); Test-WF Roles page shows no mojibake (`07_*`).
- **Rollback:** `03_ROLLBACK_Roles_Description_from_backup_PREPARED.sql` (restores from `dbo.Bak_Roles_Mojibake_20261008`).

---
## 2026-10-08 20:53 - Data (UI): NIS Information Technology moved under Support as Sub-department - ALREADY APPLIED

- **Date/time EAT:** **2026-10-08 20:53:34 EAT** (UpdatedAt 17:53:34 UTC).
- **What and why:** Allan asked for NIS (Org 21) Department 1494 "Information Technology" (code IT) to become a Sub-department under **Support (1495)**. Done in the UI (Departments/Edit), no SQL writes.
- **Commit hash:** None (data only, through the app).
- **Files changed:** None. Evidence: `artifacts/nis-it-to-sub-2026-10-08/` (RESULT.md, before/after text, screenshots).
- **DB change:** `Department` 1494: DepartmentKind 0 -> 3, ParentDepartmentId NULL -> 1495. 5 child rooms, 158 assets and users unchanged. Org 21 Administrative count 4 -> 3.
- **Config:** None.
- **Live-deploy steps:** if live NIS needs the same structure, repeat in the live UI (Departments/Edit 1494-equivalent), not by SQL.
- **Verify:** `/nis/Departments/Index` shows IT under Support with its 5 rooms.
- **Rollback:** Departments/Edit 1494: Kind = Department (Administrative), no parent.

---
## 2026-10-08 20:40-21:39 - main: SMTP per company, same-org guard, hide Excel import, mojibake seed, login chrome, template stage roles, Usage page, xorg scan, parent picker

- **Date/time EAT:** commits 20:40-21:39 on local main (HEAD was `1b253af`, built but unpublished); published 23:41.
- **What and why:** per commit below.

| Commit | EAT | What | Why | Files |
|---|---|---|---|---|
| `dccd22d` | 20:40 | feat(email): per-company SMTP settings with platform-default fallback | Companies need their own outgoing mail identity; fall back to platform SMTP when unset. | `database/scripts/004_Migrations/094_OrganizationSmtpSettings.sql`, `Application/Contracts/IEmailService.cs`, `Application/Contracts/IOrganizationSmtpSettingsStore.cs`, `Application/Contracts/ISecretProtector.cs`, `Application/Contracts/ISmtpSettingsResolver.cs`, `Application/Contracts/Security/IAccountSecurityService.cs`, `Application/Email/OrganizationSmtpSettingsRecord.cs`, `Application/Email/SmtpSettings.cs`, `Application/Email/SmtpSettingsAudit.cs`, `Application/Email/SmtpSettingsResolution.cs`, `Infrastructure/Persistence/OrganizationSmtpSettingsRepository.cs`, `Infrastructure/Services/AccountSecurityService.cs` +21 more (`git show --stat dccd22d`) |
| `8454e10` | 20:49 | feat(security): server-side same-org guard for org-scoped ids on write paths | Block cross-org ids posted to write paths (security audit finding). | `Application/Helpers/OrgReferenceGuard.cs`, `Application/Services/Approvals/AssetApprovalSettingsHelper.cs`, `Application/Services/Assets/AssetBulkService.cs`, `Application/Services/Assets/AssetImportService.cs`, `Application/Services/Assets/AssetService.cs`, `Application/Services/Common/OrgReferenceValidator.cs`, `Application/Services/DepartmentService.cs`, `Application/Services/Purchases/PurchaseRequestService.cs`, `Application/Services/Transfers/TransferService.cs`, `Application/ViewModels/Assets/AssetViewModels.cs`, `Web/Controllers/SettingsController.cs` |
| `3c3996c` | 20:50 | ui(assets): hide Excel import chrome behind FeatureFlags | Excel import UI hidden until the import is signed off (FeatureFlags). | `Web/AssetManagement.Web.csproj`, `Web/Controllers/AssetsController.cs`, `Web/Helpers/FeatureFlags.cs`, `Web/Views/Assets/Index.cshtml`, `Web/Views/Shared/_RequisitionWorkflowGuide.cshtml`, `Tests/Security/FeatureFlagsTests.cs` |
| `ea2a2f6` | 20:51 | fix(db): stop double-encoding seed text (Staff role mojibake, L8) | Staff role description was double-encoded (mojibake L8) by the PowerShell seed runner. | `database/scripts/002_Seed/001_ReferenceData.sql`, `database/scripts/004_Migrations/040_RequisitionWorkflowDefaults.sql`, `tools/database/Initialize-Database.ps1`, `tools/database/_Common.ps1` |
| `21fb89c` | 20:51 | ui(auth): remove demo-mode indicators from login chrome | Demo-mode chrome should not show on the real login page. | `Web/Views/Account/Login.cshtml`, `Web/Views/Account/OrganizationNotFound.cshtml` |
| `3053be8` | 20:51 | test(purge): prove OrganizationSmtpSettings delete is OBJECT_ID-guarded | Proves purge does not fail when 094 table is absent. | `Tests/Organizations/OrganizationPurgeTests.cs` |
| `ffabff3` | 20:53 | feat(orgs): map approval stage roles on template copy instead of skipping/raw ids | Template copy skipped/raw-copied approval stage role ids across orgs (xorg finding). | `Application/Helpers/OrgReferenceGuard.cs`, `Application/Services/Organizations/OrganizationService.cs` |
| `3770b28` | 20:59 | ops(db): read-only cross-org reference integrity check | Read-only scan to find cross-org references (no writes). | `database/scripts/ops/IntegrityCheck_CrossOrgReferences.sql` |
| `7fda297` | 20:59 | docs(db): PROPOSAL (not applied) same-org FK for Users.RoleId | Proposal only; NOT applied (DO_NOT_RUN script). | `database/scripts/proposals/PROPOSAL_UsersRoleId_SameOrg_FK_DO_NOT_RUN.sql` |
| `514a246` | 20:59 | docs(design): ApprovalStage child table to replace approval CSV id columns | Design note only. | `docs/design/approval-stage-child-table.md` |
| `c7e0cd7` | 21:01 | feat(platform): add Usage page for per-company metrics | Platform admin needs per-company usage metrics. | `Application/AssetManagement.Application.csproj`, `Application/Contracts/Platform/IPlatformUsageService.cs`, `Application/Contracts/Queries/IPlatformUsageQueryRepository.cs`, `Application/Helpers/PlatformUsageMetrics.cs`, `Application/Services/Platform/PlatformUsageService.cs`, `Application/ViewModels/Platform/PlatformUsagePageVm.cs`, `Infrastructure/Queries/PlatformUsageQueryRepository.cs`, `Web/App_Start/AutofacConfig.cs`, `Web/Areas/Platform/Controllers/UsageController.cs`, `Web/Areas/Platform/Views/Shared/_PlatformNav.cshtml`, `Web/Areas/Platform/Views/Usage/Index.cshtml`, `Web/AssetManagement.Web.csproj` +1 more (`git show --stat c7e0cd7`) |
| `ef0aae3` | 21:06 | fix(email): audit a password clear as Settings.Smtp.ClearPassword | Audit trail for clearing a stored SMTP password. | `Application/Email/SmtpSettingsAudit.cs`, `Tests/Email/SmtpSettingsResolutionTests.cs` |
| `fc75d47` | 21:08 | fix(departments): exclude self/descendants from Edit parent picker | Parent picker allowed choosing self/descendant (cycle). | `Application/Helpers/DepartmentHierarchyRules.cs`, `Application/Helpers/DepartmentKindChangeGuard.cs`, `Application/Helpers/DepartmentRoomParentCandidates.cs`, `Application/Helpers/DepartmentSubtreeHelper.cs`, `Web/Controllers/DepartmentsController.cs`, `Web/Views/Departments/Edit.cshtml`, `Tests/Departments/DepartmentSubtreeHelperTests.cs` |
| `1de3e44` | 21:12 | fix(email): SMTP save messages without an apostrophe | Message text fix. | `Web/Controllers/SettingsController.Email.cs`, `Web/ViewModels/CompanySmtpSettingsViewModel.cs` |
| `68abc29` | 21:14 | fix(security): same-org guard treats platform-level (NULL org) rows as not cross-org | Guard false-positive on platform-level rows with NULL org. | `Application/Services/Assets/AssetBulkService.cs`, `Application/Services/Assets/AssetService.cs`, `Application/Services/Common/OrgReferenceValidator.cs` |
| `60ce38b` | 21:14 | test(security): cross-org guard, validator, template copy mapping, RoleId literal scan | Regression tests for the guard / mapping / RoleId literals. | `Tests/Organizations/OrganizationTemplateCopyMappingTests.cs`, `Tests/Requisitions/PurchaseRequestDepartmentFlowTests.cs`, `Tests/Security/CrossOrgReferenceGuardTests.cs`, `Tests/Security/NoHardCodedRoleIdTests.cs`, `Tests/Security/OrgReferenceValidatorTests.cs`, `Tests/ServiceRuleTests.cs` |
| `7b0e1c0` | 21:34 | fix(assets): FeatureFlags.AssetExcelImportUiEnabled is static readonly, not const | Build/analyzer fix for the flag. | `Web/Helpers/FeatureFlags.cs` |
| `1b253af` | 21:39 | fix(platform): Usage storage query brackets the reserved RowCount alias | Usage storage query failed on reserved alias RowCount. | `Infrastructure/Queries/PlatformUsageQueryRepository.cs` |

- **DB migration/script:** `094_OrganizationSmtpSettings.sql` (from `dccd22d`) **pending, not applied**; app falls back to platform SMTP. `IntegrityCheck_CrossOrgReferences.sql` read-only (run 20:59, `artifacts/xorg-scan-2026-10-08`). PROPOSAL script must not run. Seed fixes in 001/040 (`ea2a2f6`) and 002/060 (`19c0edb`) affect fresh installs only.
- **Config:** None. `FeatureFlags.AssetExcelImportUiEnabled` is a code flag (false).
- **IIS/server steps (local):** 23:41 publish.
- **Live-deploy steps:** web publish; run 094 on live when per-company SMTP is wanted.
- **Verify:** `/Platform/Usage` 200 (SuperAdmin, 23:4x); login page has no demo chrome; Settings > Email says "not available yet (094 pending)" until 094 runs.
- **Rollback:** revert the commits (or the 23:41 IIS backup); 094 rollback in its header (DROP TABLE) if it was applied.

---
## 2026-10-08 20:02 - Keyrot publish: SystemEncryptionKey v2 (`682ce85`)

- **Date/time EAT:** **2026-10-08 20:02:52 EAT** publish; prove 20:07-20:09.
- **What and why:** Rotate `SystemEncryptionKey` off the public default to a random key with encrypt-then-MAC v2 format; old default kept as `SystemEncryptionKeyPrevious` for decrypting old values.
- **Commit hash:** `682ce85` = `72b5548` (integration-1008) + cherry-pick of `0404919`. (`0404919` itself reached main later.)
- **Files changed:** 7 files (see `git show --stat 682ce85`).
- **DB migration/script:** None (throwaway org 25 "ZZ KeyRotation Test 1008" created by the prove).
- **Config/Web.config keys:** IIS `Web.config` only: `SystemEncryptionKey` = new random (48 CSPRNG bytes base64, never logged; lives only in `C:\inetpub\AssetManagement\Web.config`); added `SystemEncryptionKeyPrevious` = old public default. Web.config SHA256 `3B664DD5...39F7` -> `BF64D20EF695EED2753522021CEC33EE5A71AB43622499C82A4355E3703444EB`. `smtp.secrets.config` unchanged.
- **IIS/server steps (local):** package `artifacts\web-publish-grok-keyrot-20261008_195526`; elevated `artifacts\grok-keyrot-iis-publish.ps1`; backup `C:\inetpub\AssetManagement\_bak\grok_keyrot_20261008_200239\` (incl. Web.config); 273 files hash-checked; Web.dll `4808C995...C32C` -> `CFF546F9CDCF5D0DB859294B5398306E626407201A135CE40315DEB2999CCE03`.
- **Live-deploy steps:** generate a NEW key for live (never reuse the local one); set live `SystemEncryptionKeyPrevious` to live's current key; publish code first or together.
- **Verify:** `artifacts/key-rotation-2026-10-08/REPORT.md` (logins all PASS; v2 credential package encrypt/decrypt proven).
- **Rollback:** restore Web.config and files from `_bak\grok_keyrot_20261008_200239`, recycle pool.

---
## 2026-10-08 19:49 - DB: Test-WF cross-org stage-role remap (2,644 assets) - ALREADY APPLIED

- **Date/time EAT:** approved by Allan 19:46; committed **2026-10-08 19:49:07 EAT** (16:49:07 UTC).
- **What and why:** 2,644 org-13 (Test-WF) assets copied from org 11 "Template 3" carried Template 3 stage role ids (Transfer 45 Department Head, Disposal 41 Company Admin). Remapped to Test-WF's own roles 63 / 59.
- **Commit hash:** None for the data; root cause fixed in code by `ffabff3` (template copy maps stage roles).
- **Files changed:** scripts in `artifacts/xorg-role-remap-2026-10-08/` (00_counts, 01_backup, 01b_backup_export_csv, 02_remap, 03_postcheck, 99_rollback).
- **DB migration/script:** backup table `dbo.Bak_XorgRoleRemap_20261008` (2,644 rows, PK AssetId) + CSV; `02_remap.sql -v COMMIT=0` then `COMMIT=1`. UpdatedAt untouched.
- **Config:** None.
- **Deploy order:** counts -> backup -> dry run -> commit -> counts -> postcheck.
- **Live-deploy steps:** run `00_counts.sql` / the xorg scan on live first; remap only if live has the same cross-org ids.
- **Verify:** `counts_after.txt` (only org 13 lines differ), `postcheck_output.txt`.
- **Rollback:** `99_rollback.sql -v COMMIT=0` then `-v COMMIT=1`.

---
## 2026-10-08 16:20-16:54 - main commits not in the keyrot build (first published 23:41)

- **Date/time EAT:** commits 16:20-16:54; NOT in `682ce85` (keyrot was built from `72b5548`); first reached IIS in the 23:41 publish.
- **What and why:** per commit below.

| Commit | EAT | What | Why | Files |
|---|---|---|---|---|
| `0404919` | 16:20 | fix(security): rotate SystemEncryptionKey to v2 encrypt-then-MAC | Original key-rotation commit (cherry-picked into 682ce85 for the 20:02 publish). | `Web/Areas/Platform/Controllers/OrganizationsController.cs`, `Web/AssetManagement.Web.csproj`, `Web/Controllers/AccountController.cs`, `Web/Helpers/EncryptionHelper.cs`, `Web/Properties/AssemblyInfo.cs`, `Web/Views/Account/CredentialPackageUnavailable.cshtml`, `Tests/Security/EncryptionHelperTests.cs` |
| `6713440` | 16:28 | fix(assets): Edit posts SerialNumber once | Edit posted SerialNumber twice. | `Web/Views/Assets/Edit.cshtml` |
| `73dd98a` | 16:40 | feat(assets): AssetMovement table, recorder, Movements page and Details tab | Allan wants a movement history of department/custodian changes. Needs migration 093 (NOT yet applied; app tolerates absence). | `database/scripts/004_Migrations/093_AssetMovement.sql`, `database/scripts/ops/093b_Backfill_AssetMovement_FromCustody.sql`, `Application/Contracts/IAssetMovementService.cs`, `Application/Contracts/Queries/IAssetMovementQueryRepository.cs`, `Application/Helpers/AssetMovementRecorder.cs`, `Application/Security/ModulePermissionCatalog.cs`, `Application/Services/AssetMovementService.cs`, `Application/Services/Assets/AssetBulkService.cs`, `Application/Services/Assets/AssetImportService.cs`, `Application/Services/Assets/AssetService.cs`, `Application/Services/Assignments/AssignmentService.cs`, `Application/Services/MaintenanceService.cs` +19 more (`git show --stat 73dd98a`) |
| `627fdb0` | 16:40 | test(purge): include AssetMovement FKs in the checked-in purge snapshot | Purge snapshot must know AssetMovement FKs. | `Tests/Organizations/PurgeForeignKeySnapshot.cs` |
| `b5cb68e` | 16:54 | fix(time): treat date-only inputs as Kenya calendar days (Part C3) | Date-only inputs shifted a day (UTC vs Kenya). | `Application/Helpers/KenyaTimeHelper.cs`, `Application/Services/Assignments/AssignmentService.cs`, `Application/Services/IncidentService.cs`, `Application/Services/Returns/ReturnService.cs`, `Web/Controllers/AssetsController.cs`, `Web/Controllers/ClaimsController.cs`, `Web/Views/Assignments/Index.cshtml`, `Web/Views/Assignments/List.cshtml`, `Web/Views/Incidents/Details.cshtml`, `Web/Views/Incidents/Index.cshtml`, `Web/Views/Shared/_AssetIncidentsTab.cshtml` |
| `a8a45a6` | 16:54 | fix(time): show stored UTC timestamps in Kenya time with an EAT label (Part C1) | Timestamps displayed in UTC; now EAT with label. | `Application/Services/Approvals/ApprovalWorkflowHelper.cs`, `Application/Services/Assets/AssetService.cs`, `Application/Services/ReportDocumentService.cs`, `Application/Services/ReportService.cs`, `Web/Controllers/PendingApprovalsController.cs`, `Web/Controllers/ReportsController.cs`, `Web/Views/AssetRequests/Details.cshtml`, `Web/Views/AssetRequests/Index.cshtml`, `Web/Views/Assets/Details.cshtml`, `Web/Views/Claims/Details.cshtml`, `Web/Views/Notifications/Index.cshtml`, `Web/Views/PendingApprovals/Index.cshtml` |

- **DB migration/script:** `093_AssetMovement.sql` (+ `ops/093b_Backfill_AssetMovement_FromCustody.sql`) from `73dd98a` **pending, not applied**: Movements page / Details tab show "not available yet", writes are skipped. 093 adds table + indexes + permission `Assets.Movements.View` granted to Company Admin and roles with Assign/Transfer/AuditLogs.View (all orgs).
- **Config:** None.
- **Live-deploy steps:** web publish; then 093 (and optionally 093b) on approval.
- **Verify:** Assets/Details movement tab shows the "not available yet" notice until 093 runs.
- **Rollback:** revert commits / 23:41 IIS backup; 093 rollback in its header.

---
## 2026-10-08 09:46-16:28 - Daytime work already live before 20:02 (summary backfill)

- **Date/time EAT:** commits 09:46-16:28; published to local IIS during the day by the assetdev / grok publishes (packages `artifacts\web-publish-assetdev-20261008_*` and `artifacts\web-publish-grok-*-20261008_*`, backups under `C:\inetpub\AssetManagement\_bak\`). All are ancestors of `682ce85`, so they are live as of the 20:02 publish.
- **What and why:** perf, scan, Zebra, reports, requisition, departments and QA fixes (see each commit message and the QA artifacts for that hour). Summary backfill, not per-change detail.
- **Commits (48):**
  - `e254956` 09:46 feat(departments): kind-change child guard, org structure breakdown, inherit source choice
  - `319aad6` 10:13 perf(dashboard): grant-free tenant Dashboard KPIs; stop full-org asset loads per hit
  - `0f15527` 10:15 fix(reports): Preview modal stuck under backdrop; loading-modal race
  - `35f9fd2` 10:30 perf(approvals): drop SQL ORDER BY from pending-approval sources (no memory grant)
  - `4cd0978` 10:59 feat(requisitions): per-stage approver person, "Purchase order raised by", flow redirect fix
  - `53c32b8` 11:13 fix(purchases): PO raiser has no Company Admin exemption
  - `bd56936` 11:20 perf(audit): grant-free, indexed AuditLog history; Assets/Details survives history failure
  - `ecd632d` 11:31 feat(assets): sub-type Details page (paged assets) + per-asset price; blank stays 'Not set', never copied from sub-type
  - `1e853e6` 11:42 fix(requisitions): keep posted flow choice on invalid re-render; show PO create errors
  - `9313ce0` 11:45 perf(reports,dashboard): depreciation refresh off the request path; grant-free custody count and Asset Register export; Requested By names
  - `8dee4cb` 11:51 feat(assets): "Group by room" view on Assets Index (lazy rooms > categories > assets)
  - `2432e1a` 12:22 perf(reports): covering-index Asset Register export, targeted Pending Approvals preview lookup; user names in previews and Pending Approvals
  - `7431bf2` 12:51 fix(requisitions): PR Details shows the approval path the request actually resolves to
  - `864285f` 12:51 fix(purchases): a typed PO unit cost is never overwritten by a catalog price
  - `a1c5abe` 12:51 fix(departments): Sub with child rooms and no parent shows its own parent message
  - `a35e9bf` 12:51 feat(departments): live requisition flow summary on Edit
  - `48d1912` 13:00 fix(assets): View/Status filter selects show their full text
  - `e7948fb` 13:00 fix(assets): 'Back to Assets' returns to the list the user came from
  - `b37077b` 13:12 fix(assets): 'Back to Assets' only returns within this tenant's Assets pages
  - `570ae98` 13:44 perf(layout): self-host Inter/Outfit fonts; no render-blocking fonts.googleapis.com
  - `6bd73ca` 13:48 fix(labels): connect to Zebra Browser Print only when the user prints
  - `6f936bf` 14:01 fix(departments): stage-count trims the LAST stage; grey stage summary follows removals
  - `f51231a` 14:01 fix(ui): select chevron no longer covers the last letter; Assets filter row gets the full width
  - `cab3da6` 14:06 perf(assets): Edit/Create stop reading every asset row of the org
  - `d7c5af8` 14:06 perf(auth): memoise user and role permission codes per request
  - `b32fb67` 14:06 perf(settings): memoise platform settings per service instance (30 s TTL)
  - `84c7cc1` 14:12 fix(scan): asset-scan.js quote mismatch broke AssetScan/Lookup search and camera
  - `3cbde6c` 14:18 perf(reports): preview shows at most 200 rows per table; PDF/CSV keep all rows
  - `17c1642` 14:18 perf(layout): no three.js or 403 pollers on ordinary signed-in pages
  - `c05da54` 14:18 fix(print): PrintLabel hides the printer status line until BrowserPrint answers
  - `6fb8c58` 14:34 perf(startup): compile the login-to-dashboard and asset-form views in the background after app start
  - `37ace9e` 14:34 perf(subtypes): sub-type list filters by type, active and search in SQL
  - `b514eae` 14:40 fix(permissions): Permissions/Index 500 - redundant @ before if inside the foreach
  - `8a0a138` 14:40 perf(mfa): send the MFA code email off the request path
  - `1f850e6` 14:40 fix(scan): specific camera errors; version the scan scripts
  - `b216686` 14:43 Revert "perf(startup): compile the login-to-dashboard and asset-form views in the background after app start"
  - `25c518e` 14:45 fix(subtypes): sub-type search 500 - keep the search filter in memory
  - `449692e` 14:54 fix(auth): root-portal login counts lockout in the matched tenant's bucket
  - `04e82d0` 15:00 fix(soft-delete): filter IsActive consistently on asset/policy lookups
  - `365ca8b` 15:05 fix(scan): camera errors no longer masked by html5-qrcode stop()
  - `3d1e248` 15:05 fix(mfa): VerifyMfa shows only the limit message when no code was sent
  - `4f4f14c` 15:21 fix(purge): one shared purge plan; add the tables that blocked the purge
  - `79e02db` 15:21 test(purge,mfa): plan drift and FK coverage tests; rate limiter tests
  - `1bafac2` 15:31 perf(reports): pending-approvals aging CSV reads only the assets it names
  - `c8d5123` 15:53 feat(assets): Move to room / Move group to room; grouped bulk department targets; Edit department guard
  - `8570ba4` 16:11 fix(impersonation): store and compare impersonation times in UTC
  - `9261b4b` 16:11 fix(assets): saving an asset no longer corrupts unset dates, condition on receipt or stage roles
  - `72b5548` 16:28 fix(assets): Edit posts SerialNumber once
- **DB migration/script:** applied today per `SchemaMigrationHistory`: 085 (06:45), 088 (08:00), 089 (08:19), 091 (08:33), 090 (08:44), 092 (09:24), 081 + 083 (14:49). Also the Test-WF 11-account password reset at 09:33:45 EAT (`artifacts/qa-testwf-reset-all-2026-10-08`, Allan-approved).
- **Config:** None known.
- **Verify / Rollback:** per the QA artifacts of each publish; IIS backups under `C:\inetpub\AssetManagement\_bak\`.

---
## 2026-10-07 - Departments tree: clearer name-column tab-stop indent (~2rem/level)

- **Date/time EAT:** 2026-10-07 ~12:30 EAT (local SoT commit + ROOT local IIS publish). Not pushed; no PR; no live publish; no DB changes.
- **What and why:** Allan said prior hierarchy indent was too subtle / names still felt left-aligned across levels. Bumped Name-cell padding to clear tab stops (~2rem / 32px per depth) so depth-0 / depth-1 / depth-2 names do not share the same left edge. Kind badges unchanged; left borders kept as nesting cues.
- **Commit hash:** `57edbc3` (57edbc373fd0e05cde35ac7ef9298381f88c23c6)
- **Indent levels (Name column `.am-dept-name.am-dept-indent-N`):**
  - depth 0: `padding-left: calc(0.25rem + 0 * 2rem)` = **0.25rem**
  - depth 1: `padding-left: calc(0.25rem + 1 * 2rem)` = **2.25rem (~36px)** + blue left border
  - depth 2: `padding-left: calc(0.25rem + 2 * 2rem)` = **4.25rem (~68px)** + amber left border
  - narrow screens (<=575.98px): 1.25rem step (1.5rem / 2.75rem)
- **Files changed (SoT):**
  - `src/AssetManagement.Web/Content/css/site.css` (`.am-dept-indent-*` tab-stop padding)
  - `docs/LIVE-DEPLOY-CHANGELOG.md`
- **DB migration/script:** None.
- **Config/Web.config keys:** None. Root `Web.config` / secrets preserved on publish.
- **IIS/server steps (local):** Release rebuild; WebPublish package under `artifacts\web-publish-dept-indent-*`; elevated copy to `C:\inetpub\AssetManagement` (skip root Web.config + secrets); backup under `C:\inetpub\_backups\AssetManagement_root_pre_dept_indent_*`; recycle app pool `AssetManagement`.
- **Live-deploy steps:** Publish web project (CSS) from a HEAD containing this commit. No DB, no config keys. Verify Departments tree Name column indent on live after `/NanoAssetSuite` publish.
- **Verify (local):** Open `/A46138179/Departments?view=tree`, expand hierarchy: Name text at depth 1 starts ~2rem further right than depth 0; depth 2 ~4rem further. Confirm `/Content/css/site.css` contains `calc(0.25rem + 1 * 2rem)`.
- **Rollback:** Revert the commit; restore IIS from the `AssetManagement_root_pre_dept_indent_*` backup (or republish prior HEAD).

---
## 2026-10-07 - Departments tree: colored kind badges + hierarchy indent

- **Date/time EAT:** 2026-10-07 ~12:05 EAT (local SoT commit + ROOT local IIS publish). Not pushed; no PR; no live publish; no DB changes.
- **What and why:** Allan asked for different colored status/kind badges for departments, sub-departments, and rooms on the Departments screen, plus clearer hierarchy indent (sub under department further right; room under sub further still). Reuses the existing `DepartmentKindBadge` helper and Active/Inactive `badge-status` pattern (same idea as Assets "assigned" / "under maintenance" badges).
- **Commit hash:** `06c2dd8` (06c2dd89950f5f5aa2d03f7cd46792fa09775899)
- **Badge map (kind -> Bootstrap class / label):**
  - Administrative -> `badge bg-primary badge-kind` / **Department** (blue)
  - SubDepartment -> `badge bg-info text-dark badge-kind` / **Sub-department** (teal)
  - Grade -> `badge bg-purple badge-kind` / **Grade** (purple; `.bg-purple` added in site.css)
  - Class -> `badge bg-info text-dark badge-kind` / **Class** (teal)
  - Room -> `badge bg-warning text-dark badge-kind` / **Room** (amber)
  - Active/Inactive status column unchanged: `badge bg-success|secondary badge-status`
- **Indent levels (tree Name column):**
  - depth 0 (core department/grade): `.am-dept-indent-0` = 0.25rem
  - depth 1 (sub-department / class / direct room): `.am-dept-indent-1` = 1.5rem + blue left border (0.85rem on narrow screens)
  - depth 2 (room under sub): `.am-dept-indent-2` = 3rem + amber left border (1.5rem on narrow screens)
- **Files changed (SoT):**
  - `src/AssetManagement.Web/Helpers/DepartmentKindHtmlHelpers.cs` (colors + Department/Sub-department/Room labels)
  - `src/AssetManagement.Web/Views/Departments/Index.cshtml` (kind badges in tree + flat list; indent classes)
  - `src/AssetManagement.Web/Views/Departments/_DepartmentChildList.cshtml` (kind badges)
  - `src/AssetManagement.Web/Content/css/site.css` (`.bg-purple`, `.badge-kind`, `.am-dept-indent-*`)
- **DB migration/script:** None.
- **Config/Web.config keys:** None. Root `Web.config` / secrets preserved on publish.
- **IIS/server steps (local):** Release rebuild 0 errors; WebPublish package `artifacts\web-publish-dept-badges-20261007_120257`; elevated copy to `C:\inetpub\AssetManagement` (skip root Web.config + secrets); backup under `C:\inetpub\_backups\AssetManagement_root_pre_dept_badges_*`; recycle app pool `AssetManagement`.
- **Live-deploy steps:** Publish web project from a HEAD containing this commit (views + CSS + rebuilt `AssetManagement.Web.dll`). No DB, no config keys. Verify Departments tree badges + indent on live after `/NanoAssetSuite` publish.
- **Verify (local):** Login as a tenant user (e.g. Test-WF `A46138179`), open `/A46138179/Departments?view=tree`, expand hierarchy: Kind column shows colored badges; Name column indents by depth; Status still Active/Inactive. Flat list also shows kind badges. `/Content/css/site.css` contains `am-dept-indent-2`.
- **Rollback:** Revert the commit; restore IIS from the `AssetManagement_root_pre_dept_badges_*` backup (or republish prior HEAD).

---
## 2026-10-06 - Local main merge check + ROOT local IIS publish (HEAD 22a2d37) + live vdir correction (/NanoAssetSuite)

- **Date/time EAT:** 2026-10-06 11:57 EAT (root publish); entry written ~12:10 EAT. Local only; not pushed; no PR; no live publish.
- **What and why:** Allan-approved: confirm all recent fix commits are on local `main`, then publish merged `main` to the ROOT local IIS site `C:\inetpub\AssetManagement` (localhost:8080, `.\SQLEXPRESS`).
- **Merge:** No merge needed. All of these are already ancestors of local `main` (`git merge-base --is-ancestor` = 0 for each): `7b3b171`, `e717f99`, `5e399a7`, `48c4469`, `9dc7a7e`, `058526c`, `7112a1f`, `66ab949`, `553bfaa`, `49ed562`, `e65f205`, `22a2d37`. No conflicts.
- **Published HEAD:** `22a2d37481853ecfd12043a7f7dfeea1a86a71bd` (code as of `e65f205`).
- **Build:** Web project Release Rebuild + WebPublish (FileSystem) 0 errors. Full solution: 3 pre-existing failures, not touched: `src/AssetManagement.Runner` (NETSDK1047 net40/win-x86 assets), `tests/AssetManagement.PerformanceTests` (CS0535 FixedOrganizationScopeService missing Set/ClearExecutionContext), `tests/AssetManagement.Tests`.
- **What was published (local root only):** package `artifacts\web-publish-root-merge-20261006_115556` (264 files: bin, Views, Areas, Content, Scripts, Global.asax, `*.config.example`) copied over the root. Root `Web.config` NOT copied. `Views\web.config` WAS updated from SoT (adds `System.Collections.Generic` + `AssetManagement.Web.ViewModels` Razor namespaces); `Areas\Platform\Views\Web.config` identical. App pool `AssetManagement` stopped/started (recycled). ACL kept: `NT AUTHORITY\IUSR` + `IIS APPPOOL\AssetManagement` `(OI)(CI)(RX)`.
- **Backup:** `C:\inetpub\_backups\AssetManagement_root_pre_merge_20261006_115719` (full root copy excl. `_bak`, 957 files).
- **Configs preserved (SHA256 equal before/after/backup):** `Web.config`, `smtp.secrets.config`, `Web.Debug.config`, `Web.Release.config`, `Web.config.e2e-backup`, `Web.config.iis-host-backup`, `packages.config`, `bin\AssetManagement.Web.dll.config`, `*.config.example`. (Root has no separate `connectionStrings.config`.)
- **DB migration/script:** **NONE run.** Read-only check of `SchemaMigrationHistory` on `.\SQLEXPRESS`/`AssetManagementModuleDb`: NOT recorded = `065_DepartmentRequisitionApproval.sql`, `081_DepartmentAcademicAndRoomInherit.sql`, `083_SeededAccountsRequirePasswordChange.sql`, `084_AssetProductGroupListIndex.sql`. Schema objects for 065 (3 Department columns), 081 (`RoomInheritTarget`, `AcademicFlowMode`) and 084 (`IX_Asset_Org_Active_ProductGroup`) are PRESENT (applied outside the history table). 083 is a data UPDATE: 34 active seeded accounts already have `RequirePasswordChange=1`, 3 still 0. PM to decide whether to record/run anything. `PrePublish_SuperAdmin_SchemaCheck_SELECTS_ONLY.sql` rollup: `PASS_SUPERADMIN_SCHEMA`, MissingCount 0.
- **Config/Web.config keys:** None changed locally.
- **LIVE CORRECTION (2026-10-06, Allan):** the live virtual directory is now **`/NanoAssetSuite`** (with an `e`). `https://nanosoft.africa/NanoAssetSuite/Account/Login` -> 200; `/NanoAssetSuit` -> 404. The deploy-order step must set **`ExternalBaseUrl` = `https://nanosoft.africa/NanoAssetSuite`** (supersedes the earlier `.../NanoAssetSuit` lines in the 7b3b171 entry). Verify on live: tenant login lands on `/NanoAssetSuite/<code>/Dashboard/Index`; SuperAdmin lands on `/NanoAssetSuite/Platform/Organizations/Index`. No code change: the app reads the vdir name at runtime. (The ops SQL check's printed checklist still mentions `/NanoAssetSuit`; treat as superseded.)
- **IIS/server steps (local):** TEMP sub-app `AssetManagement/NanoAssetSuit` deleted (appcmd), `Publish\NanoAssetSuitProve` and `_bak\NanoAssetSuitProve_prev_20261005_1626` deleted. Root publish as above.
- **Live-deploy steps:** Back up live, publish a HEAD containing `e65f205` (preserve live Web.config/secrets), set `ExternalBaseUrl` as above, recycle the live pool. Run the read-only schema check first; apply missing migrations only per PM decision (never `083` without a password-change plan).
- **Verify (local, done):** `/Account/Login` 200; `/` 302 -> `/Account/Login?ReturnUrl=%2f`; `/Content/css/site.css`, `/Content/bootstrap.min.css`, `/Scripts/app/label-designer.js`, `/Scripts/app/departments-tree.js` 200; `/Dashboard/Index`, `/Assets`, `/Departments`, `/Platform/Users` 302 -> login; `Areas\Platform\Views\Users\_PlatformUserList.cshtml` present; `/Home/Privacy`, `/Home/Terms` 200 with `&middot;` and no mojibake.
- **Rollback:** Stop pool, robocopy the backup folder back over `C:\inetpub\AssetManagement`, start pool.

---

## 2026-10-06 - Raw non-ASCII chars in views/JS -> HTML entities / escapes (mojibake) + DepartmentsController 'Bulk grades 1-6' label

- **Date/time EAT:** 2026-10-06 12:05 EAT (local commit only; not pushed; no live publish; no DB access).
- **What and why:** Web views are UTF-8 without BOM and ASP.NET reads them as ANSI, so raw em/en dashes, middle dots, ellipses, arrow, small right triangle and division sign rendered as mojibake (`a-euro...`). Continues c7d2651 / 553bfaa. Razor markup -> HTML entities (`&mdash;` `&ndash;` `&middot;` `&hellip;` `&rarr;` `&#9656;` `&divide;`); C# strings inside views (PageHeader Subtitle/Title, string.Format, ToString format) -> `\uXXXX` C# escapes (same glyph at runtime, no literal-entity risk); Scripts/app JS strings/regex -> `\uXXXX` escapes; one JS comment -> ASCII. `DepartmentsController` setup-mode label was stored double-garbled (`1` + mojibake + `6`, originally en dash) -> ASCII `Bulk grades 1-6`.
- **Commit hash:** `e65f205` (fix); this changelog entry is a separate follow-up docs commit.
- **Files changed (SoT, 35):** `Controllers/DepartmentsController.cs`; Views: Assets/Edit, Assets/Import, Assets/_AssetClassificationFields, AssetScan/QuickActions, AuditLogs/Index, Departments/Index, Departments/_RoomRequisitionApproval, Home/Privacy, Home/Terms, Maintenance/Complete, Maintenance/Details, Notifications/Index, Purchases/Create, Roles/Create, Shared/_AssetClassificationPickerModal, Shared/_AssetDisposalTab, Shared/_AssetLifecyclePanel, Shared/_PublicLayout, Shared/_TaxonomyDepreciationFields, Users/Index; Scripts/app: approval-approver-picker, asset-classification-picker, asset-documents-tab, asset-label-zebra, asset-scan-batch-print, asset-scan-camera, asset-scan, asset-subtype-picker, assets-grouped, departments-tree, label-designer, purchase-request-create, purchase-request-details, supplier-create.
- **Scope:** views + Scripts/app + one controller string only. Line endings preserved (CRLF), files stay UTF-8 without BOM.
- **DB migration/script:** None.
- **Config/Web.config keys:** None.
- **IIS/server steps (local):** None for this commit. (Separately, TEMP sub-app `/NanoAssetSuit` was removed from site AssetManagement and `Publish\NanoAssetSuitProve` + `_bak\NanoAssetSuitProve_prev_20261005_1626` deleted; root site/pool untouched.)
- **Live-deploy steps:** Takes effect on the next normal publish of the web project from a HEAD containing `e65f205` (needs the rebuilt `AssetManagement.Web.dll` for the controller label plus the changed views/scripts). No DB step, no config change.
- **Verify:** Pages above show proper dashes/dots/ellipses (no `a-euro`); Departments/Index row toggles show the small triangle; Departments setup mode dropdown shows `Bulk grades 1-6`.
- **Remaining raw non-ASCII (intentional/harmless):** BOM files (Assets/Details, Assets/Index, AssetSubTypes/CreateSelectType incl. its ellipsis, DatabaseConsole/Index, Departments/Create, Departments/Edit, Home/Forbidden, Purchases/Receive, Shared/_DepartmentDispositionPrompt) are decoded correctly; `Content/css/site.css` L3671 em dash in a CSS comment.
- **Rollback:** Revert `e65f205`.

---

## 2026-10-06 - Missing csproj Content items (Platform/Users 500) + Platform org banner dash mojibake

- **Date/time EAT:** 2026-10-06 11:42 EAT (local commit only; not pushed; no live publish; no DB access).
- **What and why:** TEMP sub-app `/NanoAssetSuit` returned HTTP 500 on `/Platform/Users` (partial view `_PlatformUserList` not found). The file is in SoT but `AssetManagement.Web.csproj` had no `<Content Include>` for it, so MSBuild Package/WebPublish never copied it. A full scan of the web project found 30 more tracked, referenced views/scripts missing from the csproj (live only has them because they were hand-copied). All 31 added. Also: Platform Organization Details red "Test only - permanent delete" banner rendered the raw em dash as `a-euro` mojibake (UTF-8 file without BOM read as ANSI); replaced with `&ndash;`.
- **Commit hash:** `553bfaa81e41f659de0d0978fd50bcabe6b19c8b` (short `553bfaa`); this changelog entry is a separate follow-up docs commit.
- **Files changed (SoT):**
  - `src/AssetManagement.Web/AssetManagement.Web.csproj` (+31 Content Include)
  - `src/AssetManagement.Web/Areas/Platform/Views/Organizations/OrganizationDetails.cshtml` (banner `&ndash;`)
  - `src/AssetManagement.Web/Areas/Platform/Views/Shared/_PlatformLayout.cshtml` (raw dash in a JS comment -> ASCII)
- **Files now packaged (must exist on live after any publish):** `Areas/Platform/Views/Users/_PlatformUserList.cshtml`; `Scripts/app/`: asset-classification-picker.js, asset-price-apply.js, asset-scan-batch-print.js, asset-scan.js, asset-subtype-default-cost.js, batch-assign.js, departments-tree.js, label-designer.js, receive-catalog-match.js; `Scripts/vendor/BrowserPrint-2.0.0.75.min.js`; `Views/`: Account/VerifyEmail, Assets/_AssetLabelCodeTypeSelector, AssetScan/_ScanBatchPrintPanel, Assignments/BatchCreate, DatabaseConsole/Index, Departments/RequisitionFlows, Departments/_DepartmentChildList, Departments/_RoomParentOtherModal, Departments/_RoomRequisitionApproval, Home/Forbidden, Roles/Create, Roles/_RoleTemplateSaveModal, Settings/LabelDesigner, Shared/FreshPortalRestriction, Shared/_AcquisitionCostApplyModal, Shared/_AssetClassificationPickerModal, Shared/_AssetDocumentUploadModals, Shared/_PicPlacementPrompt, Shared/_RequisitionWorkflowGuide, Shared/_WorkflowFormScripts (all `.cshtml`).
- **DB migration/script:** None.
- **Config/Web.config keys:** None.
- **IIS/server steps (local):** Republished TEMP `Publish\NanoAssetSuitProve` from the new HEAD (publish-copy Web.config + connectionStrings.config restored). Root `C:\inetpub\AssetManagement` untouched.
- **Live-deploy steps:** Publish the web project from a HEAD containing `553bfaa` (Package/WebPublish now carries the files above). If live is updated by hand-copying files instead, make sure the files above are present (live already has them today) and copy the updated `OrganizationDetails.cshtml`. No DB step, no app-pool setting change.
- **Verify:** `/Platform/Users` 200 with user list; `/Platform/Organizations/OrganizationDetails/{id}` banner shows a proper dash (no `a-euro`); other Platform pages 200.
- **Rollback:** Revert `553bfaa` (csproj additions are packaging-only; reverting the banner restores the mojibake).

---
## 2026-10-05 - TEMP /NanoAssetSuit prove QA fixes (IUSR ACL + E2E schema migrations)

- **Date/time EAT:** 2026-10-05 14:45:43 EAT (local commit only; not pushed; no live publish; no SQLEXPRESS writes).
- **What and why:** QA against TEMP IIS sub-app `/NanoAssetSuit` â†’ `Publish\NanoAssetSuitProve` (LocalDB E2E) found three issues. Fixed setup + E2E schema only; root site `C:\inetpub\AssetManagement` untouched.
- **Issue 1 (Content/Scripts HTTP 401 under sub-app):** Publish folder lacked `NT AUTHORITY\IUSR:(OI)(CI)(RX)` which the root site folder has. Anonymous static files under the sub-app therefore 401'd while root `/Content` and `/Scripts` returned 200. **Fix (TEMP only):** `icacls Publish\NanoAssetSuitProve /grant "NT AUTHORITY\IUSR:(OI)(CI)(RX)"`. Not a SoT/code/Web.config change; no live ACL step unless live prove folder was copied without IUSR.
- **Issue 2 (Index 500 / Invalid column UseCustomRequisitionApproval):** E2E DB `AssetManagementModuleDb_E2E` on shared LocalDB (`MSSQLLocalDB` / shared name `MSSQLLocalDBShared`) was missing department/requisition schema from pending SoT migrations. Applied **schema-only** (idempotent IF COL_LENGTH / IF OBJECT_ID; no data deletes; **083 seed RequirePasswordChange UPDATE skipped** so QA creds keep working):
  - `065_DepartmentRequisitionApproval.sql` (**adds `UseCustomRequisitionApproval`** + stage CSV columns) â€” **this was the Index 500**
  - `069_UserInvitations.sql`
  - `070_PurchaseRequestLineItems.sql`
  - `079_DepartmentRowVersion.sql`
  - `080_DepartmentApprovalStage.sql`
  - `081_DepartmentAcademicAndRoomInherit.sql`
  - `082_DepartmentClassTeacherRoomCustodian.sql`
  - Evidence log: `%LOCALAPPDATA%\Temp\nis-nanoassetsuit-vd-2026-10-05\e2e-migrate-*.log`
- **Issue 3 (rootpath login â†’ Profile vs tenant login â†’ Dashboard):** Compared SoT `7b3b171` diffs: `PostLoginRedirectHelper` / `LocalReturnUrlHelper` / `TenantUrlHelper` only gained app-base strip/prefix for vdir hosting; **destination selection (Dashboard vs Profile fallback) unchanged**. Smoke: `/NanoAssetSuit/nanosoft/Account/Login` â†’ Dashboard; `/NanoAssetSuit/Account/Login` â†’ Profile (same as QA). After Profile landing, `/Account/Landing` and `/Dashboard/Index` still resolve to tenant Dashboard. Root site `/Account/Login` smoke hits `ChangePassword` (SQLEXPRESS Phase B `RequirePasswordChange`) so Profile-vs-Dashboard is not comparable there with these creds. **Treated as pre-existing design for non-tenant login path â€” no code change.**
- **Commit hash:** `5e399a711ca8802ce6473da284e7554386743dda` (short `5e399a7`)
- **Files changed (SoT):** `docs/LIVE-DEPLOY-CHANGELOG.md` only.
- **DB migration/script:** E2E LocalDB only (above). **Live may need the same migrations** if live DB lacks `Department.UseCustomRequisitionApproval` (and related 069/070/079â€“082 objects). Deploy-order: run `065_DepartmentRequisitionApproval.sql` before publishing bits that SELECT that column; then 069, 070, 079â€“082 as needed. Do **not** blindly run `083` on live without an explicit password-change plan.
- **Config/Web.config keys:** None.
- **IIS/server steps (local):** TEMP prove folder ACL only (IUSR RX). Sub-app URL unchanged: `http://localhost:8080/NanoAssetSuit`.
- **Live-deploy steps:** (1) Confirm live `Department.UseCustomRequisitionApproval` exists; if missing, apply `065_DepartmentRequisitionApproval.sql` (then 069/070/079â€“082 as gap analysis requires). (2) If a fresh copy of site files is used for `/NanoAssetSuit`, grant IUSR RX on that folder (or ensure app-pool/IUSR can read `Content`/`Scripts`). (3) No code publish required for these three QA items beyond whatever already carries `7b3b171`.
- **Verify (TEMP prove):**
  - `GET http://localhost:8080/NanoAssetSuit/Content/bootstrap.min.css` â†’ 200
  - `GET http://localhost:8080/NanoAssetSuit/Scripts/jquery-3.7.1.min.js` â†’ 200
  - Authenticated `/NanoAssetSuit/nanosoft/Assets/Index` â†’ 200 title Assets (not UseCustomRequisitionApproval)
- **Rollback:** Revert IUSR ACE on prove folder if undesired; E2E columns are additive (safe to leave). Changelog revert only for docs.

---
## 2026-10-05 â€” IIS sub-application support (/NanoAssetSuit) for all app URLs

- **Date/time EAT:** Commit **2026-10-05 13:45:09 EAT** (local commit only; not pushed; not published).
- **What and why:** Live hosts CodexAsset as the IIS sub-app `/NanoAssetSuit`. Hardcoded root-relative URLs (`"/Account/Login"`, `"/" + slug + "/..."`, `/Platform/...`) sent users outside the app (login form POST, post-login/MFA redirects, forced password change, sidebar/platform links, notification links, logos, QR codes, reset/invite emails). Fixed by mirroring HireHub's working `/HireHub` pattern (`HR.Web\Helpers\TenantAuthRedirectHelper.cs`, `HR.Web\Helpers\ExternalUrlHelper.cs`): prefix app-absolute paths with `HttpRuntime.AppDomainAppVirtualPath`, and strip it before route-segment parsing. At site root the base path is empty, so `localhost:8080` behaviour is unchanged. Full scan: `docs/VIRTUAL-DIR-ROOT-RELATIVE-SCAN-2026-10-05.md`.
- **Commit hash:** `7b3b1719ce67abc30206d55c13ea5c4095212038` (short `7b3b171`)
- **Files changed (SoT):**
  - `src/AssetManagement.Web/Helpers/AppPathHelper.cs` **(new)**
  - `src/AssetManagement.Web/AssetManagement.Web.csproj` (Compile include)
  - `src/AssetManagement.Web/Helpers/TenantUrlHelper.cs` (BuildTenantPath prefix; `/Account/Login` fallbacks)
  - `src/AssetManagement.Web/Helpers/PlatformAdminHelper.cs` (`PlatformOrganizationsIndexUrl`)
  - `src/AssetManagement.Web/Helpers/ImpersonationSessionHelper.cs`
  - `src/AssetManagement.Web/Filters/ImpersonationExpiryFilterAttribute.cs`
  - `src/AssetManagement.Web/Helpers/LocalReturnUrlHelper.cs`
  - `src/AssetManagement.Web/Helpers/PostLoginRedirectHelper.cs`
  - `src/AssetManagement.Web/Helpers/AssetQrUrlHelper.cs`
  - `src/AssetManagement.Web/Helpers/OrganizationLogoHelper.cs`
  - `src/AssetManagement.Web/Controllers/BaseController.cs` (`ResolveApplicationBaseUrl`)
  - `src/AssetManagement.Web/Controllers/DatabaseConsoleController.cs`
  - `src/AssetManagement.Web/Views/Account/Login.cshtml`
  - `src/AssetManagement.Web/Views/Notifications/Index.cshtml`
  - `src/AssetManagement.Web/Views/Settings/LabelDesigner.cshtml`
  - `src/AssetManagement.Web/Areas/Platform/Views/Shared/_PlatformLayout.cshtml`
  - `src/AssetManagement.Web/Scripts/app/label-designer.js`
  - `src/AssetManagement.Application/Helpers/AssetScanUrlHelper.cs` (3-arg `ResolvePasswordResetBaseUrl`)
  - `src/AssetManagement.Infrastructure/Services/MembershipService.cs`
  - `src/AssetManagement.Infrastructure/Services/UserInvitationService.cs`
  - `tests/AssetManagement.Tests/Helpers/AssetScanUrlHelperTests.cs` (+3 cases)
  - `docs/VIRTUAL-DIR-ROOT-RELATIVE-SCAN-2026-10-05.md` **(new)**
- **DB migration/script:** **NONE.** Stored notification `LinkUrl` values (`/Assets/Details/{id}` etc.) are left as they are and resolved at render time.
- **Config/Web.config keys:** **None changed** (local `Web.config` and IIS untouched). **Live check:** `ExternalBaseUrl` (Platform > Email Settings, or Web.config fallback) should be `https://nanosoft.africa/NanoAssetSuit`, including the sub-app path, same as HireHub's `https://nanosoft.africa/Hirehub`. `forms loginUrl="~/Account/Login"` is already vdir-safe. **[SUPERSEDED 2026-10-06: live virtual directory is now `/NanoAssetSuite`; use `ExternalBaseUrl` = `https://nanosoft.africa/NanoAssetSuite`. See the 2026-10-06 local main merge + root IIS publish entry.]**
- **IIS/server steps (local):** TEMP prove sub-app `/NanoAssetSuit` and `Publish\NanoAssetSuitProve` were removed after QA OVERALL PASS (2026-10-05). Root `C:\inetpub\AssetManagement` untouched. GET `http://localhost:8080/Account/Login` â†’ 200.
- **Live-deploy steps (in order):**
  1. **Migrations first (if live lacks them):** run `065_DepartmentRequisitionApproval.sql` (adds `UseCustomRequisitionApproval`), then `069`, `070`, `079`â€“`082` as gap analysis requires. Do **not** run `083` on live without an explicit password-change plan.
  2. Push/tag `7b3b171` when Allan approves (not pushed), or deploy from local SoT.
  3. Back up the live site folder.
  4. Publish Web/Application/Infrastructure/Domain DLLs plus the changed views/scripts (`Login.cshtml`, `Notifications/Index.cshtml`, `Settings/LabelDesigner.cshtml`, `Areas/Platform/Views/Shared/_PlatformLayout.cshtml`, `Scripts/app/label-designer.js`). Preserve live `Web.config` and secrets. Ensure IUSR (or app-pool identity) can read `Content`/`Scripts` on the live folder.
  5. Set `ExternalBaseUrl` = `https://nanosoft.africa/NanoAssetSuit`. **[SUPERSEDED 2026-10-06: live virtual directory is now `/NanoAssetSuite`; use `ExternalBaseUrl` = `https://nanosoft.africa/NanoAssetSuite`. See the 2026-10-06 local main merge + root IIS publish entry.]**
  6. Recycle the `/NanoAssetSuit` app pool.
- **Verify (on live under /NanoAssetSuit):**
  - `/NanoAssetSuit/{slug}/Account/Login` â†’ 200, and the form `action` starts with `/NanoAssetSuit/`.
  - Login (+MFA) lands on `/NanoAssetSuit/<code>/Dashboard/Index` (or first permitted page under that tenant prefix). Platform admin lands on `/NanoAssetSuit/Platform/Organizations/Index`.
  - Forced password change redirects to `/NanoAssetSuit/{slug}/Account/ChangePassword`.
  - Sidebar, log-off form, global search, notification "Open", org logo, and QR label URL all include `/NanoAssetSuit` exactly once.
  - Password-reset and invite email links include `/NanoAssetSuit`.
  - Root-hosted local `http://localhost:8080/` still works after a local publish (paths unchanged at root).
- **Local verification done:** Release build of src projects OK. Path probe against the built DLLs: 25/25 PASS (sub-app vs root, no double prefix). `aspnet_compiler -v /NanoAssetSuit` view precompile OK on a temp copy. Pre-existing unrelated issues: test projects don't compile at `ea07d5b` (tests reference APIs missing from src), and `Views/Permissions/Index.cshtml(11)` has a Razor parse error under `aspnet_compiler`.
- **Rollback:** Redeploy the previous site backup / prior DLLs and views. No DB or config rollback needed.

---

## 2026-10-05 â€” Phase B production security (#2 / #3 / #8 / #9)

- **Date/time EAT:** Commit **2026-10-05 12:47:14 EAT** (local commit only; not pushed). IIS publish of Phase B still ON HOLD.
- **What and why:** Harden local SoT toward production security: force password change on seeded accounts; remove demo credentials banner (keep MFA any-code banner); richer `/Sys/Database` audit + IP allowlist setting; remove Company Admin name-only auth bypass / require `IsSystemRole`; lockout alerting (`AUTH_LOCKOUT` + soft-fail email to platform admins).
- **Commit hash:** `5ba25c0f36d73b384f31daadb670f7453e5563cb` (short `5ba25c0`)
- **Files changed (SoT):**
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
  - `database/scripts/004_Migrations/083_SeededAccountsRequirePasswordChange.sql` **(new)**
  - `database/scripts/ops/083_Revert_RequirePasswordChange_PREPARE_DO_NOT_RUN.sql` **(new; prepare-only, DO NOT RUN)**
  - `database/scripts/002_Seed/002_DemoData.sql`
  - `database/scripts/004_Migrations/005_Multitenancy.sql`
  - `database/scripts/004_Migrations/013_SecondDemoOrg.sql`
  - `database/scripts/004_Migrations/014_DemoLoginUsers.sql`
  - Docs: `docs/PROD-SECURITY-PHASE-B-2026-10-05.md`, `docs/PROD-SECURITY-AUDIT-2026-10-05.md`
- **DB migration/script:**
  - Path: `database/scripts/004_Migrations/083_SeededAccountsRequirePasswordChange.sql`
  - **Ran locally:** YES on `.\SQLEXPRESS` / `AssetManagementModuleDb` â€” **36 rows** `RequirePasswordChange=1`
  - **KEEP AS-IS locally** (Allan did not ask to clear)
  - Prepared revert (**DO NOT RUN** unless Allan asks): `database/scripts/ops/083_Revert_RequirePasswordChange_PREPARE_DO_NOT_RUN.sql`
- **Config/Web.config keys:** `DatabaseConsoleAllowedIps` = `127.0.0.1,::1` (SoT Web.config only)
- **IIS/server steps (local):** **NONE â€” IIS publish of Phase B DLLs is ON HOLD until Allan is ready.** `C:\inetpub\AssetManagement` was not modified by Phase B.
- **Live-deploy steps (in order):**
  1. Phase B committed locally as `5ba25c0` (not pushed); push/tag when Allan approves.
  2. Backup live DB and site.
  3. Run `083_SeededAccountsRequirePasswordChange.sql` on live DB (or rely on app `ApplyMigrations` if that is the live process).
  4. Publish Web/Application/Infrastructure/Domain DLLs + views + Web.config merge (preserve live secrets; add `DatabaseConsoleAllowedIps`).
  5. Optionally apply IIS IP Restrictions for `/Sys` (see Phase B doc) after publish approval.
  6. Recycle app pool; smoke login / change-password / Sys/Database / lockout path.
- **Verify:**
  - Seeded `@asset.local` (and known seed) accounts redirect to Change Password until changed.
  - Login page: no demo credentials banner; MFA any-code banner still present when `MfaAllowAnyCode` is true.
  - `/Sys/Database` denied from non-allowlisted IP; audit rows include SQL/rows/duration/IP.
  - Company Admin without `IsSystemRole` does not get name-only bypass.
  - Lockout creates `AUTH_LOCKOUT` SecurityEvents row.
- **Rollback:**
  - Restore previous IIS site bak / prior DLLs.
  - Revert Web.config `DatabaseConsoleAllowedIps` if needed.
  - Only if Allan asks: run `database/scripts/ops/083_Revert_RequirePasswordChange_PREPARE_DO_NOT_RUN.sql` (prepared; do not run by default).
  - Redeploy prior commit binaries.

---

## 2026-10-05 â€” #6 / #7 prepare-only (least-privilege DB + aspnet_regiis DPAPI)

- **Date/time EAT:** Commit **2026-10-05 12:47:21 EAT** (prepare-only; scripts not executed; local commit only, not pushed)
- **What and why:** Stage operator scripts/docs for least-privilege app-pool DB login + backup/restore, and aspnet_regiis DPAPI encryption of connection strings / secrets â€” **without running them** until Allan approves.
- **Commit hash:** `56f3f29daa63fb143348303ea350c817c36da670` (short `56f3f29`)
- **Files changed:**
  - `database/scripts/ops/006_AppPool_LeastPrivilege.sql`
  - `database/scripts/ops/006_Backup_AssetManagementModuleDb.sql`
  - `database/scripts/ops/006_RestoreTest_ScratchDb.sql`
  - `database/scripts/ops/006_Register-NightlyBackupTask.ps1.txt`
  - `database/scripts/ops/007_aspnet_regiis_DPAPI_steps.txt`
  - `docs/PROD-SECURITY-ASPNET-REGIIS-DPAPI-2026-10-05.txt`
  - (also referenced from `docs/PROD-SECURITY-PHASE-B-2026-10-05.md`)
- **DB migration/script:** prepare-only paths above â€” **NOT run locally; NOT run on live**
- **Config/Web.config keys:** none applied (DPAPI steps document encrypting live `connectionStrings` / appSettings later)
- **IIS/server steps (local):** none (docs only)
- **Live-deploy steps (in order):** **HOLD â€” do not run until Allan approves.** When approved: (1) backup DB + site, (2) run `#6` least-privilege + backup scripts per Phase B doc, (3) maintenance window for `#7` aspnet_regiis on live site path, (4) rotate Gmail app password after encrypt/ACL, (5) verify app pool still connects and SMTP still sends.
- **Verify:** After future approval only â€” app pool login is not `db_owner`; backup file exists; restore-test scratch DB works; site still starts with encrypted config.
- **Rollback:** N/A until scripts are run. If run later: restore pre-change SQL logins/roles from backup; restore unencrypted Web.config from pre-encrypt bak; recycle pool.

---

## 2026-10-04 â€” SecurityLogs parser fix (missing closing brace)

- **Date/time EAT:** Published view-only to local IIS **2026-10-04 ~00:06 EAT** (IIS file LastWriteTime `2026-10-04 00:06:35`)
- **What and why:** `Areas/Platform/Views/SecurityLogs/Index.cshtml` was missing a closing `}` after the Razor variable block, which broke the Security Logs page parser/render. Added the missing brace.
- **Commit hash:** `fd5ab5877f9f86d360ff7f4cf07505dea4d1fb0b` (short `fd5ab58`) - committed **2026-10-05 12:46:55 EAT** (after the 2026-10-04 view-only IIS copy; local commit only, not pushed)
- **Files changed:**
  - `src/AssetManagement.Web/Areas/Platform/Views/SecurityLogs/Index.cshtml` (+1 line: closing `}`)
- **DB migration/script:** none / not run
- **Config/Web.config keys:** none
- **IIS/server steps (local):** View-only copy to `C:\inetpub\AssetManagement\Areas\Platform\Views\SecurityLogs\Index.cshtml` (~00:06 EAT). No DLL publish required for this fix.
- **Live-deploy steps (in order):**
  1. Checkout commit `fd5ab58` (or later that contains it), or copy the exact view.
  2. Copy `Index.cshtml` to live `Areas/Platform/Views/SecurityLogs/` (or full publish that includes the view).
  3. Recycle app pool if views are precompiled/cached; otherwise soft recycle / touch as needed.
- **Verify:** Open Platform â†’ Security Logs; page renders (no Razor parse/YSOD); login + security event tabs load.
- **Rollback:** Restore previous `Index.cshtml` from IIS bak or prior git revision of the view.

---

## 2026-10-03 â€” DatabaseConsoleController include in csproj

- **Date/time EAT:** Commit **2026-10-03 23:11:42 EAT**; local IIS bak stamp `sysdb_controller_20261003_231106` (~23:11 EAT)
- **What and why:** Controller source and routes/views existed, but the type was missing from `AssetManagement.Web.dll` because it was not a `<Compile Include>` in the csproj â€” `/Sys/Database` could not resolve the controller type.
- **Commit hash:** `88548c88b3eff1f09b6646dae57b7f371fef76e1` (short `88548c8`)
- **Files changed:**
  - `src/AssetManagement.Web/AssetManagement.Web.csproj` (+1 Compile Include)
- **DB migration/script:** none / not run
- **Config/Web.config keys:** none for this commit
- **IIS/server steps (local):** Elevated publish of rebuilt Web DLL; bak `C:\inetpub\AssetManagement\_bak\sysdb_controller_20261003_231106`; app pool recycle as usual for DLL publish.
- **Live-deploy steps (in order):**
  1. Ensure live build includes `DatabaseConsoleController` in the Web project Compile list (this commit or equivalent).
  2. Publish `AssetManagement.Web.dll` (and any dependent views already on live).
  3. Recycle app pool.
- **Verify:** Authenticated Platform Admin can open `/Sys/Database` without missing-type / 404 controller errors; execute path still gated by existing auth.
- **Rollback:** Restore Web DLL from `sysdb_controller_*` bak (local) or prior live bak; recycle pool.

---

## 2026-10-03 â€” 7 FAILs UI fixes (Assignments null id + leftover dash mojibake)

- **Date/time EAT:** Commit **2026-10-03 18:02:14 EAT**; local IIS bak stamp `a461_7fails_20261003_221558` (~22:15:58 EAT publish)
- **What and why:** Stop Assignments/Index throwing when `assetId` is missing (redirect instead). Replace leftover em/en dashes, curly quotes, and baked-in mojibake on login demo banner, requisition workflow help, purchase date hint, and receive warning with ASCII. Show one price and one currency on supplier catalog line when min equals max.
- **Commit hash:** `e037f87f4485df019929f18ce96a4a1b6fffaeac` (short `e037f87`)
- **Files changed:**
  - `src/AssetManagement.Web/ViewModels/Approvals/ApprovalWorkflowViewModels.cs`
  - `src/AssetManagement.Web/Controllers/AssignmentsController.cs`
  - `src/AssetManagement.Web/Views/Account/Login.cshtml`
  - `src/AssetManagement.Web/Views/PurchaseRequests/Create.cshtml`
  - `src/AssetManagement.Web/Views/Purchases/Receive.cshtml`
  - `src/AssetManagement.Web/Views/Shared/_RequisitionWorkflowGuide.cshtml`
  - `src/AssetManagement.Web/Views/Suppliers/Details.cshtml`
- **DB migration/script:** none / not run
- **Config/Web.config keys:** none
- **IIS/server steps (local):** Elevated publish; bak `C:\inetpub\AssetManagement\_bak\a461_7fails_20261003_221558`; pool stop/copy/start as per elevated publish script.
- **Live-deploy steps (in order):**
  1. Checkout/build commit `e037f87` (or later that contains it).
  2. Publish Web DLL + the seven view/VM files above.
  3. Preserve live Web.config; recycle app pool.
- **Verify:**
  - `/â€¦/Assignments` (no assetId) redirects instead of 500.
  - Login / requisition guide / purchase create / receive / supplier details show ASCII text (no mojibake dashes/quotes).
  - Supplier catalog line with equal min/max shows single price + currency.
- **Rollback:** Restore site from `a461_7fails_*` bak (local) or prior live bak; recycle pool.

---

## 2026-10-02 â€” Blank em-dash fix (empty cells instead of U+2014)

- **Date/time EAT:** Commit **2026-10-02 16:40:19 EAT**; IIS publish ~**16:41:35â€“16:41:47 EAT** (docs stamp); IIS stamps filled in commit `37bf03b` at 16:42:12 EAT
- **What and why:** Empty-cell U+2014 em-dash placeholders were mojibaking under UTF-8/cp1252 misdecode. Replace with empty string across helpers, views, JS, and JSON blanks; keep intentional separators/prose.
- **Commit hash:** `c7d26515bc1b41afb96062582d2c2ad2678d4851` (short `c7d2651`)
- **Files changed:**
  - `docs/BLANK-EMDASH-FIX-2026-10-02.md`
  - `docs/BLANK-EMDASH-MOJIBAKE-DIG-2026-10-02.md`
  - `src/AssetManagement.Web/Helpers/DepartmentPicDisplay.cs`
  - `src/AssetManagement.Web/Helpers/ReportHtmlBuilder.cs`
  - `src/AssetManagement.Web/Controllers/PurchaseRequestsController.cs`
  - `src/AssetManagement.Web/Controllers/SuppliersController.cs`
  - `src/AssetManagement.Web/Scripts/app/assets-grouped.js`
  - `src/AssetManagement.Web/Scripts/app/purchase-create.js`
  - `src/AssetManagement.Web/Scripts/app/purchase-request-create.js`
  - `src/AssetManagement.Web/Scripts/app/receive-serial-wizard.js`
  - `src/AssetManagement.Web/Scripts/app/supplier-create.js`
  - `src/AssetManagement.Web/Views/AssetSubTypes/Index.cshtml`
  - `src/AssetManagement.Web/Views/Assignments/BatchCreate.cshtml`
  - `src/AssetManagement.Web/Views/Departments/Index.cshtml`
  - `src/AssetManagement.Web/Views/Roles/Index.cshtml`
  - `src/AssetManagement.Web/Views/Shared/_AssetAuditTab.cshtml`
  - `src/AssetManagement.Web/Views/Suppliers/Create.cshtml`
  - `src/AssetManagement.Web/Views/Suppliers/Details.cshtml`
  - `src/AssetManagement.Web/Views/UserInvitations/Index.cshtml`
- **DB migration/script:** none / not run
- **Config/Web.config keys:** none (Web.config preserved on publish; SHA256 unchanged per fix doc)
- **IIS/server steps (local):** Pool AssetManagement stopped â†’ copy â†’ Started; bak `C:\inetpub\AssetManagement\_bak\blank_emdash_20261002_164135`; SoTâ†”IIS DLL SHA256 match for Web/Application/Domain/Infrastructure.
- **Live-deploy steps (in order):**
  1. Build/publish commit `c7d2651` (or later containing it).
  2. Deploy listed DLLs, views, and JS; **do not overwrite** live secrets in Web.config.
  3. Recycle app pool.
- **Verify:** Spot-check Departments, AssetSubTypes, Suppliers, Assignments/BatchCreate, UserInvitations â€” empty cells blank (no `Ã¢â‚¬"` / mojibake); intentional separators still present where designed.
- **Rollback:** Restore from `blank_emdash_20261002_164135` bak (local) or prior live bak; recycle pool.

---

## Notes for operators

- Local SoT path: `C:\Users\allan\Documents\Examples\CodexAsset\`
- Local IIS site: `C:\inetpub\AssetManagement` (not production)
- Local DB: `.\SQLEXPRESS` / `AssetManagementModuleDb`
- Phase B / SecurityLogs / #6 / #7 are committed locally (`fd5ab58`, `5ba25c0`, `56f3f29`) but **not pushed** - confirm `git status` / `git log` before any live cutover.
- **Do not** clear `RequirePasswordChange` locally unless Allan explicitly asks.
- **Do not** run `083` revert, `#6`, or `#7` scripts without Allanâ€™s approval.


