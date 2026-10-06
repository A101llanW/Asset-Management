# LIVE DEPLOY CHANGELOG

## Standing rule

**Local (SoT + localhost IIS + `.\SQLEXPRESS`) is NOT live/production.**  
Every change must be recorded here so it can be applied to live later. **One entry per change.**

**Order in this file:** newest first (newest at top).

**Required fields per entry:** Date/time EAT · What and why · Commit hash · Files changed · DB migration/script · Config/Web.config keys · IIS/server steps (local) · Live-deploy steps · Verify · Rollback

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
- **What and why:** QA against TEMP IIS sub-app `/NanoAssetSuit` → `Publish\NanoAssetSuitProve` (LocalDB E2E) found three issues. Fixed setup + E2E schema only; root site `C:\inetpub\AssetManagement` untouched.
- **Issue 1 (Content/Scripts HTTP 401 under sub-app):** Publish folder lacked `NT AUTHORITY\IUSR:(OI)(CI)(RX)` which the root site folder has. Anonymous static files under the sub-app therefore 401'd while root `/Content` and `/Scripts` returned 200. **Fix (TEMP only):** `icacls Publish\NanoAssetSuitProve /grant "NT AUTHORITY\IUSR:(OI)(CI)(RX)"`. Not a SoT/code/Web.config change; no live ACL step unless live prove folder was copied without IUSR.
- **Issue 2 (Index 500 / Invalid column UseCustomRequisitionApproval):** E2E DB `AssetManagementModuleDb_E2E` on shared LocalDB (`MSSQLLocalDB` / shared name `MSSQLLocalDBShared`) was missing department/requisition schema from pending SoT migrations. Applied **schema-only** (idempotent IF COL_LENGTH / IF OBJECT_ID; no data deletes; **083 seed RequirePasswordChange UPDATE skipped** so QA creds keep working):
  - `065_DepartmentRequisitionApproval.sql` (**adds `UseCustomRequisitionApproval`** + stage CSV columns) — **this was the Index 500**
  - `069_UserInvitations.sql`
  - `070_PurchaseRequestLineItems.sql`
  - `079_DepartmentRowVersion.sql`
  - `080_DepartmentApprovalStage.sql`
  - `081_DepartmentAcademicAndRoomInherit.sql`
  - `082_DepartmentClassTeacherRoomCustodian.sql`
  - Evidence log: `%LOCALAPPDATA%\Temp\nis-nanoassetsuit-vd-2026-10-05\e2e-migrate-*.log`
- **Issue 3 (rootpath login → Profile vs tenant login → Dashboard):** Compared SoT `7b3b171` diffs: `PostLoginRedirectHelper` / `LocalReturnUrlHelper` / `TenantUrlHelper` only gained app-base strip/prefix for vdir hosting; **destination selection (Dashboard vs Profile fallback) unchanged**. Smoke: `/NanoAssetSuit/nanosoft/Account/Login` → Dashboard; `/NanoAssetSuit/Account/Login` → Profile (same as QA). After Profile landing, `/Account/Landing` and `/Dashboard/Index` still resolve to tenant Dashboard. Root site `/Account/Login` smoke hits `ChangePassword` (SQLEXPRESS Phase B `RequirePasswordChange`) so Profile-vs-Dashboard is not comparable there with these creds. **Treated as pre-existing design for non-tenant login path — no code change.**
- **Commit hash:** `5e399a711ca8802ce6473da284e7554386743dda` (short `5e399a7`)
- **Files changed (SoT):** `docs/LIVE-DEPLOY-CHANGELOG.md` only.
- **DB migration/script:** E2E LocalDB only (above). **Live may need the same migrations** if live DB lacks `Department.UseCustomRequisitionApproval` (and related 069/070/079–082 objects). Deploy-order: run `065_DepartmentRequisitionApproval.sql` before publishing bits that SELECT that column; then 069, 070, 079–082 as needed. Do **not** blindly run `083` on live without an explicit password-change plan.
- **Config/Web.config keys:** None.
- **IIS/server steps (local):** TEMP prove folder ACL only (IUSR RX). Sub-app URL unchanged: `http://localhost:8080/NanoAssetSuit`.
- **Live-deploy steps:** (1) Confirm live `Department.UseCustomRequisitionApproval` exists; if missing, apply `065_DepartmentRequisitionApproval.sql` (then 069/070/079–082 as gap analysis requires). (2) If a fresh copy of site files is used for `/NanoAssetSuit`, grant IUSR RX on that folder (or ensure app-pool/IUSR can read `Content`/`Scripts`). (3) No code publish required for these three QA items beyond whatever already carries `7b3b171`.
- **Verify (TEMP prove):**
  - `GET http://localhost:8080/NanoAssetSuit/Content/bootstrap.min.css` → 200
  - `GET http://localhost:8080/NanoAssetSuit/Scripts/jquery-3.7.1.min.js` → 200
  - Authenticated `/NanoAssetSuit/nanosoft/Assets/Index` → 200 title Assets (not UseCustomRequisitionApproval)
- **Rollback:** Revert IUSR ACE on prove folder if undesired; E2E columns are additive (safe to leave). Changelog revert only for docs.

---
## 2026-10-05 — IIS sub-application support (/NanoAssetSuit) for all app URLs

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
- **Config/Web.config keys:** **None changed** (local `Web.config` and IIS untouched). **Live check:** `ExternalBaseUrl` (Platform > Email Settings, or Web.config fallback) should be `https://nanosoft.africa/NanoAssetSuit`, including the sub-app path, same as HireHub's `https://nanosoft.africa/Hirehub`. `forms loginUrl="~/Account/Login"` is already vdir-safe.
- **IIS/server steps (local):** TEMP prove sub-app `/NanoAssetSuit` and `Publish\NanoAssetSuitProve` were removed after QA OVERALL PASS (2026-10-05). Root `C:\inetpub\AssetManagement` untouched. GET `http://localhost:8080/Account/Login` → 200.
- **Live-deploy steps (in order):**
  1. **Migrations first (if live lacks them):** run `065_DepartmentRequisitionApproval.sql` (adds `UseCustomRequisitionApproval`), then `069`, `070`, `079`–`082` as gap analysis requires. Do **not** run `083` on live without an explicit password-change plan.
  2. Push/tag `7b3b171` when Allan approves (not pushed), or deploy from local SoT.
  3. Back up the live site folder.
  4. Publish Web/Application/Infrastructure/Domain DLLs plus the changed views/scripts (`Login.cshtml`, `Notifications/Index.cshtml`, `Settings/LabelDesigner.cshtml`, `Areas/Platform/Views/Shared/_PlatformLayout.cshtml`, `Scripts/app/label-designer.js`). Preserve live `Web.config` and secrets. Ensure IUSR (or app-pool identity) can read `Content`/`Scripts` on the live folder.
  5. Set `ExternalBaseUrl` = `https://nanosoft.africa/NanoAssetSuit`.
  6. Recycle the `/NanoAssetSuit` app pool.
- **Verify (on live under /NanoAssetSuit):**
  - `/NanoAssetSuit/{slug}/Account/Login` → 200, and the form `action` starts with `/NanoAssetSuit/`.
  - Login (+MFA) lands on `/NanoAssetSuit/<code>/Dashboard/Index` (or first permitted page under that tenant prefix). Platform admin lands on `/NanoAssetSuit/Platform/Organizations/Index`.
  - Forced password change redirects to `/NanoAssetSuit/{slug}/Account/ChangePassword`.
  - Sidebar, log-off form, global search, notification "Open", org logo, and QR label URL all include `/NanoAssetSuit` exactly once.
  - Password-reset and invite email links include `/NanoAssetSuit`.
  - Root-hosted local `http://localhost:8080/` still works after a local publish (paths unchanged at root).
- **Local verification done:** Release build of src projects OK. Path probe against the built DLLs: 25/25 PASS (sub-app vs root, no double prefix). `aspnet_compiler -v /NanoAssetSuit` view precompile OK on a temp copy. Pre-existing unrelated issues: test projects don't compile at `ea07d5b` (tests reference APIs missing from src), and `Views/Permissions/Index.cshtml(11)` has a Razor parse error under `aspnet_compiler`.
- **Rollback:** Redeploy the previous site backup / prior DLLs and views. No DB or config rollback needed.

---

## 2026-10-05 — Phase B production security (#2 / #3 / #8 / #9)

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
  - **Ran locally:** YES on `.\SQLEXPRESS` / `AssetManagementModuleDb` — **36 rows** `RequirePasswordChange=1`
  - **KEEP AS-IS locally** (Allan did not ask to clear)
  - Prepared revert (**DO NOT RUN** unless Allan asks): `database/scripts/ops/083_Revert_RequirePasswordChange_PREPARE_DO_NOT_RUN.sql`
- **Config/Web.config keys:** `DatabaseConsoleAllowedIps` = `127.0.0.1,::1` (SoT Web.config only)
- **IIS/server steps (local):** **NONE — IIS publish of Phase B DLLs is ON HOLD until Allan is ready.** `C:\inetpub\AssetManagement` was not modified by Phase B.
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

## 2026-10-05 — #6 / #7 prepare-only (least-privilege DB + aspnet_regiis DPAPI)

- **Date/time EAT:** Commit **2026-10-05 12:47:21 EAT** (prepare-only; scripts not executed; local commit only, not pushed)
- **What and why:** Stage operator scripts/docs for least-privilege app-pool DB login + backup/restore, and aspnet_regiis DPAPI encryption of connection strings / secrets — **without running them** until Allan approves.
- **Commit hash:** `56f3f29daa63fb143348303ea350c817c36da670` (short `56f3f29`)
- **Files changed:**
  - `database/scripts/ops/006_AppPool_LeastPrivilege.sql`
  - `database/scripts/ops/006_Backup_AssetManagementModuleDb.sql`
  - `database/scripts/ops/006_RestoreTest_ScratchDb.sql`
  - `database/scripts/ops/006_Register-NightlyBackupTask.ps1.txt`
  - `database/scripts/ops/007_aspnet_regiis_DPAPI_steps.txt`
  - `docs/PROD-SECURITY-ASPNET-REGIIS-DPAPI-2026-10-05.txt`
  - (also referenced from `docs/PROD-SECURITY-PHASE-B-2026-10-05.md`)
- **DB migration/script:** prepare-only paths above — **NOT run locally; NOT run on live**
- **Config/Web.config keys:** none applied (DPAPI steps document encrypting live `connectionStrings` / appSettings later)
- **IIS/server steps (local):** none (docs only)
- **Live-deploy steps (in order):** **HOLD — do not run until Allan approves.** When approved: (1) backup DB + site, (2) run `#6` least-privilege + backup scripts per Phase B doc, (3) maintenance window for `#7` aspnet_regiis on live site path, (4) rotate Gmail app password after encrypt/ACL, (5) verify app pool still connects and SMTP still sends.
- **Verify:** After future approval only — app pool login is not `db_owner`; backup file exists; restore-test scratch DB works; site still starts with encrypted config.
- **Rollback:** N/A until scripts are run. If run later: restore pre-change SQL logins/roles from backup; restore unencrypted Web.config from pre-encrypt bak; recycle pool.

---

## 2026-10-04 — SecurityLogs parser fix (missing closing brace)

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
- **Verify:** Open Platform → Security Logs; page renders (no Razor parse/YSOD); login + security event tabs load.
- **Rollback:** Restore previous `Index.cshtml` from IIS bak or prior git revision of the view.

---

## 2026-10-03 — DatabaseConsoleController include in csproj

- **Date/time EAT:** Commit **2026-10-03 23:11:42 EAT**; local IIS bak stamp `sysdb_controller_20261003_231106` (~23:11 EAT)
- **What and why:** Controller source and routes/views existed, but the type was missing from `AssetManagement.Web.dll` because it was not a `<Compile Include>` in the csproj — `/Sys/Database` could not resolve the controller type.
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

## 2026-10-03 — 7 FAILs UI fixes (Assignments null id + leftover dash mojibake)

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
  - `/…/Assignments` (no assetId) redirects instead of 500.
  - Login / requisition guide / purchase create / receive / supplier details show ASCII text (no mojibake dashes/quotes).
  - Supplier catalog line with equal min/max shows single price + currency.
- **Rollback:** Restore site from `a461_7fails_*` bak (local) or prior live bak; recycle pool.

---

## 2026-10-02 — Blank em-dash fix (empty cells instead of U+2014)

- **Date/time EAT:** Commit **2026-10-02 16:40:19 EAT**; IIS publish ~**16:41:35–16:41:47 EAT** (docs stamp); IIS stamps filled in commit `37bf03b` at 16:42:12 EAT
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
- **IIS/server steps (local):** Pool AssetManagement stopped → copy → Started; bak `C:\inetpub\AssetManagement\_bak\blank_emdash_20261002_164135`; SoT↔IIS DLL SHA256 match for Web/Application/Domain/Infrastructure.
- **Live-deploy steps (in order):**
  1. Build/publish commit `c7d2651` (or later containing it).
  2. Deploy listed DLLs, views, and JS; **do not overwrite** live secrets in Web.config.
  3. Recycle app pool.
- **Verify:** Spot-check Departments, AssetSubTypes, Suppliers, Assignments/BatchCreate, UserInvitations — empty cells blank (no `â€"` / mojibake); intentional separators still present where designed.
- **Rollback:** Restore from `blank_emdash_20261002_164135` bak (local) or prior live bak; recycle pool.

---

## Notes for operators

- Local SoT path: `C:\Users\allan\Documents\Examples\CodexAsset\`
- Local IIS site: `C:\inetpub\AssetManagement` (not production)
- Local DB: `.\SQLEXPRESS` / `AssetManagementModuleDb`
- Phase B / SecurityLogs / #6 / #7 are committed locally (`fd5ab58`, `5ba25c0`, `56f3f29`) but **not pushed** - confirm `git status` / `git log` before any live cutover.
- **Do not** clear `RequirePasswordChange` locally unless Allan explicitly asks.
- **Do not** run `083` revert, `#6`, or `#7` scripts without Allan’s approval.


