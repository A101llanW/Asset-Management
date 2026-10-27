# LIVE DEPLOY CHANGELOG

## 2026-10-27 ~10:30 EAT - QA fixes (Q7.2, scan texts, S1.8, tenantless QR, S1.10, N8.1, W5.x, P3.6/X9.4 move rule) - STAGED, NOT PUBLISHED

- Commits (main, local only, not pushed): 936a968 (Q7.2 + scan texts), 7192b00 (S1.8 + tenantless QR), 49d1ea3 (S1.10), a475992 (N8.1), b3428ad (W5.1/W5.4/W5.9/W5.6 display wording; codes keep stored value), 0d5ca73 (P3.6/X9.4, Allan's decision: classroom -> classroom moves allowed as well as classroom -> room). Backup tag backup/main-pre-qa-fixes-20261027 (da8b0e1).
- DB migration/script: none. Config keys: none.
- localhost:8080 redeployed 10:14 and 10:21:47-10:22:00 EAT; IIS backups _merge20261027\inetpub_AssetManagement_backup_20261027_1014 and _1021.
- Local DB only (Allan approved): QA test leftovers hard-deleted after backup AssetManagementModuleDb_pre_qaleftovers_20261027_102102.bak and Bak_QALeftovers_20261027_* tables (3 assets, 5 departments, 2 tag counters, 259 audit rows, 2 login rows). 2678 active Test-WF assets and their tags unchanged. Live DB not touched.
- Desktop\publish rebuilt (old folder renamed publish_old_20261027_103044). Web.dll SHA256 998F3B48...D32D2B. Not copied to live.
- Unit tests 1140 / 1133 passed / 7 skipped / 0 failed.

## 2026-10-27 ~09:50 EAT - review fixes S1-S11 - STAGED, NOT PUBLISHED

- Commits (main, local only, not pushed): 49837a4 (S6-S9), b337d68 (S9 print candidates scoped), 4f9330e (S8 serial hidden; AssetScan:ShowSerialToPublic switch), 4e20d79 (S1-S5, S10, S11: LiveDB pack 20261027 + guarded 104 rollback).
- Backup tags: backup/main-pre-merge-20261027 (8f6bff4), backup/main-pre-review-fixes-20261027 (143cead). Hold branch hold/tag-random-code (cf4a3fc) untouched.
- LiveDB pack ops/LiveDB_Update_20261027 (00 timestamped COPY_ONLY backup, 01 pre-checks with Prep listing, 02 104 add-missing/history-only, 03 105, 04 106 apostrophe-tolerant, 05 107, 09 verify with 11 Prep rows, 99a-d rollbacks): tested on a scratch restore only. NOT RUN on live.
- localhost:8080 redeployed 09:42:05-09:42:27 EAT; IIS backup _merge20261027\inetpub_AssetManagement_backup_20261027_0942.
- Desktop\publish rebuilt (old folder renamed publish_old_20261027_094920). Web.dll SHA256 4A1A1941...BC1E10. Not copied to live.
- Unit tests 1121 / 1114 passed / 7 skipped / 0 failed.

## 2026-10-27 09:10 - main: print log (105) + Prep/Senior Section (106) + heads per section (107) + tag segment switches (104) + Year/Classroom labels + required sub-type + QR scan card (`488e78c`) - NOT PUBLISHED (built + tested locally only; staged, not deployed)

- **Status:** NOT PUBLISHED. Local main only, not pushed, no PR. localhost:8080 runs `488e78c`. Live untouched. No print job sent.
- **What and why:** merged feat/print-log-bulk (105), feat/section-prep (106), tag segment switches 5e3101a (104, WITHOUT the random-code commit cf4a3fc, which is parked on `hold/tag-random-code`), feature/grade-to-year-labels (display wording only), fix/subtype-required-receiving; added separate Prep / Senior heads per department, any active user whatever the role (107, Departments > Edit > Heads per section); QR scan page card (public; signed-out viewers see no names, e-mails, cost, financial, insurance or supplier data; custodian and "View full details" only when signed in; details above the lookup panel after a scan; QR URL unchanged).
- **Commits:** 4428bbb, 6dd912a, 5e3101a, 3abcce4, d8a1e51, 0ed1ac9, e31bad8, 217e5df, 756c90a, 488e78c. Backup tag `backup/main-pre-merge-20261027` (8f6bff4).
- **DB migration/script:** 104_AssetTagSegmentToggles (may already be on live since 2026-10-21, history row Id 107), 105_LabelPrintBatchLog, 106_DepartmentSection, 107_DepartmentSectionHeads. Live run pack: `database/scripts/ops/LiveDB_Update_20261027/` (= Desktop\LiveDB_Update_20261027_NOT_RUN): 00 COPY_ONLY backup, 01 pre-checks, 02 104 (only what is missing), 03-05 = 105-107, 09 verify, 99a-d rollbacks DO_NOT_RUN. Tested on a scratch restore (104 absent and 104 present), including a re-run. The two scripts numbered 104 keep their names; nothing renumbered.
- **Config/Web.config keys:** none (no config file changed since rel1019g).
- **IIS/server steps (local):** robocopy mirror to C:\inetpub\AssetManagement (elevated), IIS backup `_merge20261027\inetpub_AssetManagement_backup_20261027_0826`. Local DB backup before migrations: `AssetManagementModuleDb_pre_mig104to107_20261027_082328.bak`.
- **Live-deploy steps:** run the LiveDB pack in order (README_RUN_ORDER.txt), then copy Desktop\publish (STAGED_README.txt; configs excluded).
- **Verify:** 09 = ALL_PASS; login; Departments > Edit shows Heads per section; Assets list Section filter; scan a QR signed out (no names/cost) and signed in.
- **Rollback:** previous site files first, then 99a -> 99b -> 99c (-> 99d only if 02 added 104 that day); last resort the 00 backup.
## Standing rule

**Local (SoT + localhost IIS + `.\SQLEXPRESS`) is NOT live/production.**  
Every change must be recorded here so it can be applied to live later. **One entry per change.**

**Order in this file:** newest first (newest at top).

**Required fields per entry:** Date/time EAT Â· What and why Â· Commit hash Â· Files changed Â· DB migration/script Â· Config/Web.config keys Â· IIS/server steps (local) Â· Live-deploy steps Â· Verify Â· Rollback

## 2026-10-22 10:05 - Label print log + print-batch log, Asset Details print history, grouped register bulk Move + Send to printing (rel1022a, `f926e31`, branch `feat/print-log-bulk`) - NOT PUBLISHED (built + tested locally only; staged, not deployed)

- **Status:** NOT PUBLISHED. Allan approved building and testing locally only ("Yes, build all 5 locally", 2026-10-22 09:25 EAT). No publish, no UAC, no push, no PR. Live local IIS is unchanged (Web.dll C8F92589CD1AF0B50E56CCA1B509F9054BC6D35238DDEDFEC0D5D9F9A98497DD = rel1019g). Nothing was sent to the Zebra printer and Browser Print was not called.
- **What and why:**
  1. **Print logging:** every label print or reprint (Details label modal, Assets/PrintLabel, Bulk Label Print) writes one AuditLog row on the asset: `Assets.LabelPrint`, or `Assets.LabelReprint` when the asset was printed before, the batch is a reprint, or the user chose reprint. The row holds the user, the time (UTC) and details (Tag, First print/Reprint, Mode Zebra|BrowserDialog, Source Single|Bulk, Batch, Code, Template, Count). The Zebra path logs after Browser Print reports success; the browser dialog logs when the dialog is requested. The client print code is unchanged. The server never contacts a printer: each label passes through `ILabelPrintDispatcher` (production `ClientBrowserPrintDispatcher`, no I/O; tests `DryRunLabelPrintDispatcher`).
  2. **Asset Details:** new "Label prints" tab (last printed in EAT, times printed, reprint count, latest 50 entries). Print rows also show on the Audit Trail tab.
  3. **Asset register, grouped and by-room views:** whole-group checkboxes on group/room rows. Units are resolved through the same GroupMembers / RoomCategories + RoomCategoryAssets endpoints and list scope, at most 500 per action. The existing Bulk action form (Move to department, status, maintenance) works on whole groups and keeps the AssetBulkService move rules and Assets.Edit.
  4. **Send to printing (register):** posts the selection to `AssetLabels/QueueFromRegister`, which creates ONE LabelPrintBatch (who, when, group label, filter description, asset list) and opens Bulk Label Print with the batch queued. Printing starts only when the user prints.
  5. **Print-batch log:** every Bulk Label Print run is one batch (it reuses a register batch). Per-label logging and a Completed/Stopped status are recorded. A collapsed "Print batch log" drawer on Bulk Label Print loads only when opened and offers Reprint group (loads the assets as a reprint batch: a `LabelPrintBatch.Reprint` row plus per-asset reprint rows when printed) and Clear (soft-hide: IsCleared, ClearedById, ClearedAt; the per-asset audit is kept). New endpoints RecordPrint, CreateBatch, CompleteBatch, ClearBatch and QueueFromRegister are POST + anti-forgery + Assets.View. Batches/BatchItems are GET + Assets.View. Clear is allowed for the batch creator or users with Assets.Edit. Everything is tenant-scoped.
- **Commit hash:** `f926e31` on `feat/print-log-bulk` (from `04b5872` = rel1019g). Local only, not pushed.
- **Files changed:**
  - New: `database/scripts/004_Migrations/105_LabelPrintBatchLog.sql`; `database/scripts/ops/105_Rollback_LabelPrintBatchLog_PREPARE_DO_NOT_RUN.sql`
  - New in Application: `Contracts/ILabelPrintDispatcher.cs`, `ILabelPrintBatchStore.cs`, `ILabelPrintLogService.cs`; `Services/LabelPrintLogService.cs`; `Services/LabelPrint/ClientBrowserPrintDispatcher.cs`, `DryRunLabelPrintDispatcher.cs`; `Helpers/LabelPrintAuditActions.cs`; `ViewModels/Assets/LabelPrintLogViewModels.cs`
  - New in Infrastructure: `Queries/LabelPrintBatchStore.cs`
  - New in Web: `Scripts/app/label-print-log.js`, `label-print-batch-log.js`, `asset-group-selection.js`; `Views/Shared/_AssetPrintHistoryTab.cshtml`
  - Changed: `Application/Helpers/AuditDisplayLabelHelper.cs`, `AssetAuditTrailFilterHelper.cs`; `Infrastructure/Services/OrganizationPurgeSql.cs`; `Web/App_Start/AutofacConfig.cs`; `Web/AssetManagement.Web.csproj`; `Web/Controllers/AssetLabelsController.cs`, `AssetsController.cs`; `Web/ViewModels/AssetScanViewModels.cs`; `Web/Scripts/app/asset-label.js`, `asset-label-zebra.js`, `asset-scan-batch-print.js`, `assets-grouped.js`, `list-bulk.js`; `Web/Views/AssetLabels/BulkPrint.cshtml`, `_BulkPrintPanel.cshtml`; `Web/Views/Assets/Details.cshtml`, `Index.cshtml`, `PrintLabel.cshtml`, `_AssetQrLabelModal.cshtml`, `_AssetRoomGroups.cshtml`; `Web/Views/Shared/_ListBulkToolbar.cshtml`
  - Tests: new `Assets/LabelPrintLogServiceTests.cs`; updated `Organizations/OrganizationPurgePlanTests.cs`, `PurgeForeignKeySnapshot.cs`.
- **DB migration/script:** `105_LabelPrintBatchLog.sql`. 104 is claimed twice (untracked `104_widen_auditlog_entityid.sql` in the main checkout and `104_AssetTagSegmentToggles.sql` on CodexAsset-wt-taglevel), so 105 is the next free number. It is additive and idempotent, and runs in one transaction with XACT_ABORT:
  - **LabelPrintBatch:** Id; OrganizationId FK Organization; Source; GroupLabel nvarchar(400); FilterDescription nvarchar(2000); AssetCount; PrintedCount; Status; IsReprint; ReprintOfBatchId (FK self); TemplateId; CodeType; CreatedById; CreatedAt; LastPrintedAt; CompletedAt; IsCleared; ClearedById; ClearedAt. Index IX_LabelPrintBatch_Org.
  - **LabelPrintBatchItem:** Id; OrganizationId; BatchId FK; AssetId FK Asset; AssetTag; SortOrder; PrintCount; LastPrintedAt. UX_LabelPrintBatchItem_Batch_Asset and IX_LabelPrintBatchItem_Asset.
  - Adds a SchemaMigrationHistory row. No change to AuditLog: the existing IX_AuditLog_Org_Entity covers the history query.
  - Tested ONLY on the scratch DB `AssetManagementModuleDb_Scratch_printlog_20261022_093518`, restored from COPY_ONLY + CHECKSUM verified backup `MSSQL15.SQLEXPRESS\MSSQL\Backup\AssetManagementModuleDb_pre_printlog_scratch_20261022_093518.bak`. It was applied twice (one history row). The real AssetManagementModuleDb was NOT changed.
- **Config/Web.config keys:** none.
- **IIS/server steps (local):** none done. Staged package `Desktop\Staged_printlog_f926e31` (293 files, no Web.config, *.config or secrets). Web.dll 3A8D5BB6CC695583BB45629EF57C415F803F5A12599E5CD1CC5B920423440691; Application F8DAE6F2...76FF6; Infrastructure D0E25FBA...AA56; Domain B564C7FD...843A.
- **Live-deploy steps (when approved):** COPY_ONLY backup, then apply 105 to the DB BEFORE the site files, then deploy (keep live Web.config). Contains rel1019g.
- **Verify:**
  - Debug and Release builds pass. All Razor views precompile (aspnet_compiler).
  - Unit tests: 949 total, 943 passed, 0 failed, 6 skipped. That is 42 more than rel1019g's 907.
  - The SQL store passed a scratch-DB smoke (24 checks: tenant scope, batch create/print/complete, reprint link, clear, history), using the dry-run dispatcher.
  - The read-only purge schema tests run on scratch show only pre-existing drift (AssetTagAlias/AssetTagRenameBatch/AssetTagSequence from 102/103, plus older FKs). They show no gap for the 105 tables.
- **Rollback:** redeploy rel1019g (`C:\inetpub\_backups\AssetManagement_root_pre_rel1019g_20261019_175143` or later backup). Then, only if the tables must go, run `ops/105_Rollback_LabelPrintBatchLog_PREPARE_DO_NOT_RUN.sql` (batch history lost; per-asset audit rows kept).
## 2026-10-23 08:49 - Section (Prep / Senior school) on departments, grades, classes and rooms; Create Grade / Create Class; Section filter in the asset register and reports; section tag prefixes (rel1023a, `7729026`, branch `feat/section-prep`) - NOT PUBLISHED (staged only, nothing run on :8080 or the live DB)

- **Status:** NOT PUBLISHED. Built and tested locally only. Staged at `C:\Users\allan\Desktop\publish\` (no *.config files). Live DB, live server and :8080 were not touched. Waiting for Allan's go-ahead for the DB script and for the publish.
- **What and why:**
  - Item 1: new Section attribute (Prep / Senior school) on departments, grades, classes and rooms. Every existing row is Senior school. Prep rooms can now be separated from Senior school.
  - Item 2: the Departments tree and Grades & Streams are grouped by Section (Prep first, then Senior school). Grades sort by a sort number (Department.SortOrder, else the number in the G## code) instead of the hard-coded G01..G99 list. The grade named Prep (code G00) was hidden in both views and now shows.
  - Item 3: Section filter on the asset register (list, counts, room groups, redirects and relocate forms keep it) and on reports (Section select; the report footer shows ""Section: X""). Optional tag prefixes per section in Settings > Asset tag prefix (AssetTag.Prefix.Section.Prep / .Senior, blank = no change). Prep tags look like PP-GALA-001 and never mix with G2-G2A-001. Precedence: node (room / sub-department / department) override > section prefix > tenant default prefix > derived. Existing tags are never renamed by this change.
  - Item 4: Departments > Create grade and Create class (also on the grade Details page). Section select on Create and Edit; a grade's Section change moves its classes that were on the old Section; grade sort number on Create and Edit.
  - Item 5: data fix inside the migration script only (see DB): Section = Prep for Grade Prep (ID 1614, code G00) and the ten Prep rooms (IDs 1582-1591), each matched by ID AND exact name.
- **Commit hash:** `7729026` on `feat/section-prep` (based on `04b5872` = rel1019g, what is live on :8080). Local only, not pushed. The print-log work (`feat/print-log-bulk`) is NOT included.
- **Files changed:** new `Domain/Enums/DepartmentSection.cs`, `Application/Helpers/DepartmentSectionHelper.cs`, `database/scripts/004_Migrations/106_DepartmentSection.sql`, `database/scripts/ops/106_Rollback_DepartmentSection_PREPARE_DO_NOT_RUN.sql`, 5 new test files. Changed: Domain `Department.cs`; Application `DepartmentService`, `DepartmentTreeHelper`, `DepartmentSetupModes`, `DepartmentCreateUserMessages`, `AssetTagPrefixResolver` / `Generator` / `AssetTagRenamePlanner`, `ReportDocumentService`, `LookupViewModels`, `AssetViewModels`, `ReportViewModels`; Infrastructure `AssetQueryService`; Web `DepartmentsController`, `AssetsController`, `AssignmentsController`, `BaseController`, `SettingsController.AssetTags`, `ListRoleDefaultsHelper`, `AssetTagSettingsVm`, `assets-grouped.js`, `reports.js`, views Departments (Create, Edit, Index, Details), Assets (Index, _AssetRoomGroups), Reports/_ReportFilters, Settings/AssetTags. Staged kit: 289 files, 18 differ from live (the 4 app DLLs + their pdbs, 2 scripts, 8 views). Web.dll (AssetManagement.Web.dll) SHA256 0437888C4D5577C3ED3FE07DF7F2246A8B740D5D55355F99F7211ACCDC15795E. No config files in the kit.
- **DB migration/script:** `106_DepartmentSection.sql` (migration 106; 104 is claimed twice elsewhere and 105 is the print-log migration, which is not in this base). Additive and idempotent, one transaction: adds `Department.Section INT NOT NULL DEFAULT 0` (+ `CK_Department_Section` CHECK 0/1), `Department.SortOrder INT NULL`, the guarded data fix, and a SchemaMigrationHistory row. **It must run BEFORE the build is published** (the Department entity map reads both columns). Allan runs it himself: `C:\Users\allan\Desktop\Migration_106_DepartmentSection_LIVE_RUN_BY_ALLAN.sql` (takes the DB name as `-v DbName=...`; the live name may differ from AssetManagementModuleDb; COPY_ONLY backup WITH CHECKSUM + RESTORE VERIFYONLY first, aborts if the backup fails; verify step at the end). Tested only on a scratch copy.
- **Config/Web.config keys:** Config: no changes.
- **IIS/server steps (local):** none done. For the publish later: one UAC for the local-IIS publish only after Allan approves.
- **Live-deploy steps:** (1) Allan runs the migration script on the live DB (it takes its own backup). (2) Only then publish the staged `publish` folder (overlay; no config files in it). (3) Pool recycle, smoke checks (plain GET). (4) Optional: set the Prep / Senior section prefix in Settings > Asset tag prefix.
- **Verify:** the script's verify step lists the columns, constraint, history row and the Prep rows. In the app: Departments shows Prep first with the Prep grade and its classes; Assets > Section filter; a report with Section = Prep; Create grade / Create class; a new Prep tag uses the section prefix (only if one is set).
- **Rollback:** redeploy the previous build first (Web.dll `C8F92589...97DD`), then, only if needed, run `Rollback_106_DepartmentSection_DO_NOT_RUN.sql` (needs `ConfirmRollback=YES`). The rollback drops Section and SortOrder (their values are lost). Leaving the columns in place is harmless to the old build.
## 2026-10-19 17:52 - Inline asset type + first sub-type creation in the classification picker; CustomText default +1 mm; Receive/Departments encoding fixes (rel1019g, `ad0a240`, branch `rel1019e-labels`) - PUBLISHED (local IIS :8080 2026-10-19 17:52:04 EAT)

- **Published:** 2026-10-19 17:52:04 EAT. Allan approved in chat ("Yes, publish and test", 17:49 EAT). The UAC prompt was fired once (launch 17:51:18 EAT) and accepted (elevated script started 17:51:40). Before the launch, live was confirmed unchanged since rel1019f: Web.dll 6613C446EEFAC587D22C755FBFEE1667758B47C735B74CE9E4D52D389CDA8DBC. It is now C8F92589CD1AF0B50E56CCA1B509F9054BC6D35238DDEDFEC0D5D9F9A98497DD (Application A4216B9E...FB5F, Infrastructure 1224812F...7C79, Domain F4BDE91E...0FA7). site.css is unchanged (BF5A04E7...D1E0).
- **Note:** after the pool restarted and the first login smoke check passed (17:52:11), the elevated PowerShell window went into QuickEdit "Select" mode (someone clicked in it), which paused the script before its remaining smoke checks and final hash check. The files were already copied and hash-verified (289 files, 0 mismatches). The full smoke list was then run non-elevated at 18:06 EAT with the same checks: all passed.
- **What and why:**
  - Assets/Create and Edit, asset classification picker: when the needed asset TYPE is missing in the chosen category, the user can create it inline ("Create type and select"), together with its first sub-type (required). This is one server call in one transaction, so a type is never left without a sub-type. The new type and sub-type are then auto-selected, CategoryId/AssetTypeId/AssetSubTypeId are filled and the modal closes. Categories can NOT be created here.
  - New endpoint `POST /{tenant}/AssetTypes/CreateFromAsset` (categoryId, typeName, subTypeName + anti-forgery token). Requires Assets.Create (plus the controller's Assets.Edit, same as the sub-type endpoint). Org-scoped. Duplicate checks are trimmed and case-insensitive: type name within its category (including inactive types) and sub-type name within its type. Two audit rows (AssetTypes.CreateFromAsset, AssetSubTypes.CreateFromAsset) are written inside the transaction, and the category/type cache is invalidated.
  - QA rel1019f re-check: the CustomText default Y moved from margin + 28 to margin + 29 mm (C# `GetDefaultCustomTextY` and label-designer.js), so it no longer touches SerialNumber's box (it overlapped by 0.02 mm).
  - Encoding: Purchases/Receive showed `Â·` and Departments/Index showed a garbled em dash (UTF-8 without BOM). They now use `&middot;` (markup) and `"\u00B7"` / `"\u2014"` (C# strings).
- **Commit hash:** `ad0a240` = `870ecbf` + merge of `7de0d23` (feat/inline-type-create); the tree is identical to `7de0d23`. Contains `9dd70d0`. Local only, not pushed.
- **Files changed:** new `Application/Services/AssetClassificationCreateService.cs`, `Application/Contracts/IAssetClassificationCreateService.cs`, `Application/ViewModels/Assets/AssetTypeInlineCreateViewModels.cs`; `Web/App_Start/AutofacConfig.cs`, `Web/Controllers/AssetTypesController.cs`, `Web/Controllers/AssetsController.cs`, `Web/Views/Shared/_AssetClassificationPickerModal.cshtml`, `Web/Scripts/app/asset-classification-picker.js`, `Application/Helpers/LabelLayoutDesign.cs`, `Web/Scripts/app/label-designer.js`, `Web/Views/Purchases/Receive.cshtml`, `Web/Views/Departments/Index.cshtml`. Tests: new `Assets/AssetClassificationCreateServiceTests.cs`, updated `Helpers/LabelPrintFeatureTests.cs`.
- **DB migration/script:** none (no schema change; 099-103 present). COPY_ONLY backup `AssetManagementModuleDb_pre_rel1019g_publish_20261019_175143.bak`.
- **Config/Web.config keys:** none. Web.config unchanged (27426D6E...D4D3); smtp.secrets unchanged.
- **IIS/server steps (local):** `CodexAsset\artifacts\rel1019g-launch-elevated.ps1` -> `rel1019g-iis-publish.ps1`. Package `Desktop\Staged_inlinetype_7de0d23` (289 files + README; no Web.config, *.config or secrets). Add/overwrite only: 289 copied, 0 hash mismatches. The real changes vs live are the 4 dlls and their pdbs, asset-classification-picker.js, label-designer.js, `_AssetClassificationPickerModal.cshtml`, Receive.cshtml and Departments/Index.cshtml. The 137 live .cs files are unchanged. Site backup `C:\inetpub\_backups\AssetManagement_root_pre_rel1019g_20261019_175143` (1154 files).
- **Live-deploy steps:** deploy the site files (keep live Web.config). No SQL. Contains rel1019f and rel1019e.
- **Verify:** full solution build passes; unit tests 901/907 pass, 0 failures, 6 skipped (includes 22 new/updated). Smoke (plain GETs): 4 login pages 200 with no YSOD. The 12 anonymous pages (root and A46138179) all 302 to login. Assets/Create 302 to login. GET AssetTypes/CreateFromAsset and AssetSubTypes/CreateFromAsset return 404 (POST-only), with no 500. Nothing was sent to a printer.
- **Rollback:** stop pool AssetManagement; `robocopy C:\inetpub\_backups\AssetManagement_root_pre_rel1019g_20261019_175143 C:\inetpub\AssetManagement /E /XF Web.config smtp.secrets.config`; start pool. No DB change to undo.
## 2026-10-19 12:24 - Label preview positions fix, CustomText default position, asset classification picker styles (rel1019f, `9dd70d0`, branch `rel1019e-labels`) - PUBLISHED (local IIS :8080 2026-10-19 12:24:26 EAT)

- **Published:** 2026-10-19 12:24:26 EAT. Allan approved in chat ("Fix the category picker too, then publish both"; then "Yes, send the prompt again" 12:22 EAT). The first prompt (12:14:46) was never accepted: the launcher was cut off, nothing ran and live was unchanged. One new prompt was fired at 12:24:03 and accepted at 12:24:35. Live Web.dll was D730461EC8DCD1384156B3F1367269F40181593DD56D8A98802877AC7CBA21FA (rel1019e), now 6613C446EEFAC587D22C755FBFEE1667758B47C735B74CE9E4D52D389CDA8DBC (Application 091A377B...AF66, Infrastructure FE41A877...0952, Domain 2F3BFA89...72CF). site.css is now BF5A04E775FB34F03C4C08042AB03D23515E76E1FEADD9649ED3BCE6C6DCE1D0 (was A02094A8...2C2F).
- **What and why:**
  - QA FAIL on rel1019e: the single-asset label preview (Assets/PrintLabel, Details label modal) ignored designer positions; fields ran past the label edge. Cause: site.css had no `.am-asset-label--designed` / `.am-asset-label-field` rules. They existed only on 2b8211b (Sep 17); f2cbcbd (Oct 2) dropped them, and the rel lineage (label code from d5e1a1f) never had them. Added: absolute mm positioning, the CustomText block clipped to its ^FB box, CompanyName, QR/barcode sizing, a print rule and the designer template-list styles. asset-label.js sets the CustomText block width and height in mm.
  - The CustomText default position is now 2 mm below SerialNumber (margin + 28 mm, inside the bottom margin) in C# (`GetDefaultCustomTextY`) and JS; it used to overlap SerialNumber.
  - Asset classification picker (Assets/Create, Assets/Edit): the `.am-classification-*` styles, also only on 2b8211b, are restored. They match the current markup; the unused `-option-none` rule was dropped and the colours use the current tokens. PurchaseRequests/Create and Purchases/Receive only use un-styled hook classes and are unchanged.
- **Commit hash:** `9dd70d0` = `7be54d6` + `bef2cdc` + `8966459` (merge of `819cde8`) + merge of `62daa0f` (feat/label-print-templates). Local only, not pushed.
- **Files changed:** `Application/Helpers/LabelLayoutDesign.cs`, `Web/Content/css/site.css` (+273 lines, nothing removed), `Web/Scripts/app/asset-label.js`, `Web/Scripts/app/label-designer.js`; test `LabelPrintFeatureTests.cs` (CustomText default position on 4 label sizes).
- **DB migration/script:** none (099-103 present). COPY_ONLY backup `AssetManagementModuleDb_pre_rel1019f_publish_20261019_122409.bak`.
- **Config/Web.config keys:** none. Web.config unchanged (27426D6E...D4D3); smtp.secrets unchanged.
- **IIS/server steps (local):** `CodexAsset\artifacts\rel1019f-launch-elevated.ps1` -> `rel1019f-iis-publish.ps1`. Package `Desktop\Staged_labels_9dd70d0` (289 files; no Web.config, *.config or secrets). Add/overwrite only: 289 copied, 0 hash mismatches. The real changes vs live are the 4 dlls and their pdbs, site.css, asset-label.js and label-designer.js. The 137 live .cs files are unchanged. Site backup `C:\inetpub\_backups\AssetManagement_root_pre_rel1019f_20261019_122409` (919 files).
- **Live-deploy steps:** deploy the site files (keep live Web.config). No SQL. Contains rel1019e.
- **Verify:** full solution build passes; unit tests 883/889 pass, 0 failures, 6 skipped. Smoke: 4 login pages 200 with no YSOD. Anonymous requests to AssetTags, AssetTagRename, BulkPrint, ConvertClassesToRooms, LabelDesigner, PrintLabel/1, LabelPrintConfig/1, PrintCandidates, Assets/Create, Assets/Edit/1, PurchaseRequests/Create and Purchases/Receive/1 (root and A46138179) all 302 to login. The served site.css contains the new rules. Nothing was sent to a printer.
- **Rollback:** stop pool AssetManagement; `robocopy C:\inetpub\_backups\AssetManagement_root_pre_rel1019f_20261019_122409 C:\inetpub\AssetManagement /E /XF Web.config smtp.secrets.config`; start pool. No DB change to undo.
## 2026-10-19 10:21 - Label printing: print-time template picker, CustomText block, CompanyName element, Bulk Label Print category/type/sub-type filters (rel1019e, `7be54d6`, branch `rel1019e-labels`) - PUBLISHED (local IIS :8080 2026-10-19 10:21:26 EAT)

- **Published:** 2026-10-19 10:21:26 EAT. Allan approved (GO PUBLISH, relayed by the parent agent); launcher fired once, UAC accepted 10:21:34. Live Web.dll was C8B1A9641DC82121E0D27EA327B1FB6CB01799A0FBC28908783135F5FD0B3F93 (rel1019c/rel1019d this morning, built from the uncommitted CodexAsset main tree; those deploys are not logged in this file), now D730461EC8DCD1384156B3F1367269F40181593DD56D8A98802877AC7CBA21FA (Application 4F3FF063...2C15, Infrastructure F55011C0...3AA0, Domain 9FE9FC58...C4D8).
- **What and why:**
  - Label templates (Settings > Label designer) can be chosen at print time on Bulk Label Print and on single-asset print (Assets/PrintLabel and the Details label modal). One optional `templateId` is used by `Assets/LabelZpl`, `Assets/LabelPrintConfig`, the browser preview and Zebra, so the preview and the print match. The default is "Active layout (Label designer)". Users need Assets.View; template management still needs Settings.Manage.
  - New CustomText element: a checkbox and a textarea in the designer. Its text survives Normalize, the JS copy and templates. It is wrapped to a line limit derived from the label size and position, with a words/lines count and an overflow warning; overflow is cut with "...". Printed with ZPL `^FB` and `^FH_` escaping (max 1000 stored characters).
  - New CompanyName element that prints `Organization.Name` of the current organisation. Off by default.
  - Bulk Label Print: cascading org-scoped Category > Type > Sub-type filters, applied server-side and combined (AND) with the other filters. A new "Include sub-departments" option (on by default) widens the department filter to its descendants. The department filter used to be an exact match.
- **Commit hash:** `7be54d6` = `cdb9a57` (snapshot of the CodexAsset main working tree = live rel1019c/d, on e798780) + merge of `feat/label-print-templates` `78d7440`. Local only, not pushed.
- **Files changed:** `Application/Helpers/LabelLayoutDesign.cs`, `LabelLayoutTemplateHelper.cs`, `PrintCandidateQuery.cs`, `ZplLabelBuilder.cs`, `Application/ViewModels/LabelDesignerVm.cs`, `Web/Helpers/LabelPrintSupport.cs` (new), `Web/Controllers/AssetsController.cs`, `AssetLabelsController.cs`, `AssetScanController.cs`, `SettingsController.cs`, `Web/ViewModels/AssetLabelPrintVm.cs`, `AssetScanViewModels.cs`, `Views/Assets/_AssetLabelTemplateSelector.cshtml` (new), `PrintLabel.cshtml`, `_AssetQrLabelModal.cshtml`, `_AssetLabelContent.cshtml`, `Views/AssetLabels/_BulkPrintPanel.cshtml`, `Views/Settings/LabelDesigner.cshtml`, `Scripts/app/label-designer.js`, `asset-label.js`, `asset-label-zebra.js`, `asset-scan-batch-print.js`, `AssetManagement.Web.csproj`; tests `LabelLayoutDesignHelperTests.cs`, `LabelPrintFeatureTests.cs` (new).
- **DB migration/script:** none. Label settings stay JSON in `dbo.SystemSetting` (99-103 present). COPY_ONLY backup `AssetManagementModuleDb_pre_rel1019e_publish_20261019_102111.bak`.
- **Config/Web.config keys:** none. Web.config unchanged (27426D6E...D4D3); smtp.secrets unchanged.
- **IIS/server steps (local):** `CodexAsset\artifacts\rel1019e-launch-elevated.ps1` -> `rel1019e-iis-publish.ps1`. Package `Desktop\Staged_labels_7be54d6` (289 files; no Web.config, *.config or secrets). Add/overwrite only, no deletes: 289 copied, 0 hash mismatches. Delta vs live: 4 dlls and their pdbs, 4 JS files, 5 views, 1 new view. The 137 live .cs files are unchanged. Site backup `C:\inetpub\_backups\AssetManagement_root_pre_rel1019e_20261019_102111` (1125 files).
- **Live-deploy steps:** deploy the site files (keep live Web.config). No SQL. Live needs rel1019c/d (tag-level rename) first, or deploy this package, which contains them.
- **Verify:** full solution build passes; unit tests 879/885 pass, 0 failures, 6 skipped. Smoke: 4 login pages 200 with no YSOD. Anonymous requests to Settings/AssetTags, AssetTagRename, LabelDesigner, AssetLabels/BulkPrint, Departments/ConvertClassesToRooms, Assets/PrintLabel/1, Assets/LabelPrintConfig/1?templateId=x and AssetScan/PrintCandidates?categoryId=1 (root and A46138179) all 302 to login. The served JS contains the new code. No signed-in browser test was done.
- **Rollback:** stop pool AssetManagement; `robocopy C:\inetpub\_backups\AssetManagement_root_pre_rel1019e_20261019_102111 C:\inetpub\AssetManagement /E /XF Web.config smtp.secrets.config`; start pool. Remove the new `Views\Assets\_AssetLabelTemplateSelector.cshtml` (harmless if left). No DB change to undo. Saved designs that contain CustomText/CompanyName are ignored by the old build.
## 2026-10-17 08:06 - Classes-to-rooms merged onto rel1014a (rel1016a, `e798780`, branch `rel1016a-c2r`) - PUBLISHED (local IIS :8080 2026-10-17 08:06:24 EAT)

- **Published:** 2026-10-17 08:06:24 EAT. Allan approved in chat 08:03 EAT ("Yes, send the prompt now"); launcher fired once, UAC accepted 08:06:32. Live Web.dll was 560F9071...82DF (uncommitted Oct 15 13:25 rel1009i-based c2r build), now 6050F3F98E40DEFC10416AA342F502DB83C7620DDB66518FDD5A98B2D336B214 (Application 15A91B0A, Infrastructure E9114D67, Domain 65BB6745). Restores tag prefix (rel1013a), tag rename (rel1013b), bulk print (rel1014a), catalog pickers; adds classes-to-rooms.
- **Copy mode:** add/overwrite only, no deletes. 292 files copied, 0 hash mismatches. Skipped: every Web.config, secrets, `bin\AssetManagement.Web.dll.config`, STAGED_README.txt, Convert-ClassesToRooms.ps1, _Common.ps1. Web.config unchanged 27426D6E...D4D3; smtp.secrets unchanged. 488 live-only files kept (App_Data\AspNetTemp, App_Data\Attachments, Areas\Platform, Properties\PublishProfiles, 137 root .cs sources unchanged by hash). No live file was newer than 2026-10-15 13:25.
- **DB migration/script:** none (099-103 present). COPY_ONLY backup `AssetManagementModuleDb_pre_rel1016a_publish_20261017_080603.bak`.
- **Config/Web.config keys:** none.
- **IIS/server steps (local):** `artifacts\rel1016a-launch-elevated.ps1` -> `rel1016a-iis-publish.ps1`. Site backup `C:\inetpub\_backups\AssetManagement_root_pre_rel1016a_20261017_080603` (774 files).
- **Live-deploy steps:** deploy site files (keep live Web.config). No SQL. Run Convert-ClassesToRooms.ps1 only in PREVIEW first, per org, after a DB backup.
- **Verify:** 4 login pages 200, no YSOD. Anonymous BulkPrint, Settings/AssetTagRename, Settings/AssetTags, Departments/ConvertClassesToRooms (root and A46138179) all 302 to login, no 500.
- **Rollback:** stop pool AssetManagement; `robocopy C:\inetpub\_backups\AssetManagement_root_pre_rel1016a_20261017_080603 C:\inetpub\AssetManagement /E /XF Web.config smtp.secrets.config`; start pool. No DB change to undo.
## 2026-10-18 22:30 - Asset tags: per-level segment switches (dept / sub-dept / room) + auto-fill in Settings (rel1018b, branch `feat/tag-level-toggle`) - LOCAL ONLY, NOT PUBLISHED

- **Date/time EAT:** 2026-10-18. Built and unit-tested in worktree `CodexAsset-wt-taglevel` off `feat/tag-random-code` `cf4a3fc` (rel1018a). Not published; :8080 not touched; no live change.
- **What and why:** Allan (Oct 18): choose per department / sub-department / room whether derived tags include the department, sub-department and room codes (e.g. room A = DEPT-code, room B = DEPT-ROOM-code, a whole department = DEPT-code).
  - Settings > Asset tag prefix: three tenant defaults (Department code on, Sub-department code off, Room code on = rel1018a shape) and per-row inherit / yes / no selects for each level. Nearest node with a value wins, then the tenant default.
  - Precedence unchanged otherwise: a literal override box is still the whole prefix (switches do not apply to it), then the tenant-wide prefix, then the derived prefix from the switched-on segments. All segments off = department code.
  - Effective prefixes update live for unsaved changes (read-only `Settings/AssetTagEffective`, works on detached copies). Clicking a row selects it and fills the override boxes of every node below with their current effective prefix (as the box's grey value, so they stay "inherit" unless typed over).
  - Rename tool preview and apply, new-asset generation and the tag preview all use the same resolver options, so they honour the switches.
- **Files changed:** `Application/Helpers/AssetTagPrefixResolver.cs`, `AssetTagRenamePlanner.cs`, `AssetTagPrefixGenerator.cs`, `Domain/Entities/Department.cs`, `Web/Controllers/SettingsController.AssetTags.cs`, `SettingsController.AssetTagRename.cs`, `Web/ViewModels/AssetTagSettingsVm.cs`, `Web/Views/Settings/AssetTags.cshtml`; tests `AssetTagPrefixResolverTests.cs`, `AssetTagRenamePlannerTests.cs`.
- **DB migration/script:** `database/scripts/004_Migrations/104_AssetTagSegmentToggles.sql` (adds `dbo.Department.AssetTagUseDepartmentSegment`, `AssetTagUseSubDepartmentSegment`, `AssetTagUseRoomSegment`, all `BIT NULL`, no default; NULL = inherit). Must run before this code. Rollback `database/scripts/005_Rollback/104_AssetTagSegmentToggles_rollback.sql`.
- **Settings rows (SystemSetting, written by the page on save, not by SQL):** `AssetTag.Segment.Department` (True), `AssetTag.Segment.SubDepartment` (False), `AssetTag.Segment.Room` (True). Missing = these defaults.
- **Config/Web.config keys:** none.
- **Live-deploy steps:** run 104 on live (backup first), then deploy the site files (keep live Web.config). Allan runs it himself.
- **Verify:** see the branch commit message / Debugging bot report (build + unit tests).
- **Rollback:** redeploy rel1018a, then run the rollback script. Tags already generated or renamed stay as they are.
## 2026-10-14 12:20 - Bulk Label Print only: PO filter, Send to Bulk Label Print from receipts, day groups (rel1014a, `5d0d7f1`) - PUBLISHED (rel1014a, local IIS :8080 2026-10-14 15:27:33 EAT; branch `feat/bulk-print-separate-module`)

- **Published:** 2026-10-14 15:27:33 EAT to local IIS :8080 (Allan approved 15:25 EAT; pre-checks re-run and matched; launcher fired once, UAC accepted 15:27:43). Live Web.dll was 06AC4589...0B55 (07:46 deploy), now E6C7B033C980A9B2394DDE09DD556752F7AA5AFBDDE8D95BE403A9470B39DE69 (Application 7E1149D7...6C18, Infrastructure 8632D926...EFF8, Domain 1CB22F79...7A3D). 289 files copied, 0 hash mismatches. Web.config unchanged 27426D6E...D4D3; smtp.secrets unchanged. Removed only the 4 stray hotfix .cs files (140 -> 136 .cs; the other 136 unchanged by hash). Site backup C:\inetpub\_backups\AssetManagement_root_pre_rel1014a_20261014_152653 (808 files); hotfix files also in ..._hotfix0742_files. DB backup AssetManagementModuleDb_pre_rel1014a_publish_20261014_152653.bak; no migration (099-103 present). Smoke: 4 login pages 200, no YSOD. Scratch Test-WF Company Admin check 27/27 (DB backup AssetManagementModuleDb_pre_rel1014a_liveverify_20261014_152934.bak first): Bulk Label Print ?purchaseId=17 preselects PO 5676 and shows its 12 assets newest first with a day key; Send to Bulk Label Print button on Purchases/Details/17 opens it; asset list PO filter shows the 12 tags; Scan & Search has no print panel; rename tool page; Dashboard/AssetCategories/AssetTypes/AssetSubTypes/Reports/Purchases/Assets/AssetScan 200; purchase request create shows the grouped department select and catalog pickers and saved (throw-away PR 51 deleted after; audit rows kept); anonymous PrintCandidates 302 to login; Test-WF assets unchanged (hash). Headless Edge on the live page/data: 1 day header (2026-10-09, 12 rows), partial = indeterminate, full select 12, collapse/expand ok. Scratch user deleted.
- **Date/time EAT:** 2026-10-14 12:20. Built, unit-tested and proven end-to-end on IIS Express (:8095) against a throw-away COPY of the local DB (Test-WF A46138179). :8080 not touched.
- **What and why:** Allan's Oct 14 plan: label printing lives on Bulk Label Print only.
  - Scan & Search: the print panel and queue that `134b980` put back are removed (views and `asset-scan.js` back to `b0b34a5`; the scan page model and lookup JSON no longer carry print wiring). The page still links to Bulk Label Print.
  - Bulk Label Print keeps sort, date range (entry or assignment), max N (100) and Queue / Print selected. The default is date of entry, newest first.
  - Assets are grouped under collapsible registration-day headers (Kenya time). Each day has a select-all checkbox: checked when all of the day is selected, indeterminate when part of it is. The top checkbox works the same for all loaded assets.
  - New purchase order (PO) filter on Bulk Label Print and on the asset list (`PurchaseRecordId`). Options are the tenant's own purchases only. An id from another tenant is ignored, and the SQL (`EXISTS` on `AssetReceiving` with the asset's own organisation) returns nothing for it anyway.
  - Purchase receipt banner ("Received units ready to assign"): new **Send to Bulk Label Print** button (Assets.View). It opens `AssetLabels/BulkPrint?purchaseId={id}`, which preselects the PO and loads ALL assets received on it, assigned ones included. When every unit is already assigned, a small "Received units" banner still shows the button. No print-all page.
  - Also carries the Oct 14 07:42 hotfix (`06bf814`) and rel1013b (`11a1fe7`). Note: the 07:46 deploy (below) dropped the catalog requisition flows (`afcfa78`, live until then); this package brings them back.
- **Commit hash:** `5d0d7f1` (merges `109c393` rel1013b and `eff96b2` hotfix). Local only.
- **Files changed:** `Application/Helpers/PrintCandidateQuery.cs`, `Application/ViewModels/Assets/AssetViewModels.cs`, `Infrastructure/Queries/AssetQueryService.cs`, `Web/Controllers/AssetLabelsController.cs`, `AssetScanController.cs`, `AssetsController.cs`, `PurchasesController.cs`, `Web/Helpers/PurchaseFilterOptions.cs` (new), `Web/Scripts/app/asset-scan-batch-print.js`, `asset-scan.js`, `Web/ViewModels/AssetScanViewModels.cs`, `Views/AssetLabels/BulkPrint.cshtml`, `_BulkPrintPanel.cshtml`, `Views/AssetScan/Lookup.cshtml`, `_ScanLookupResult.cshtml`, `_ScanSearchResults.cshtml`, `Views/Assets/Index.cshtml`, `_AssetRoomGroups.cshtml`, `Views/Purchases/Details.cshtml`, `AssetManagement.Web.csproj`; tests `PurchaseFilterOptionsTests.cs` (new), `PrintCandidateQueryTests.cs`, test csproj (System.Web.Mvc reference).
- **DB migration/script:** none. 099-103 are already applied on live (103 since 2026-10-13 13:25 EAT). The publish script checks them and takes a COPY_ONLY backup.
- **Config/Web.config keys:** none. Web.config not overwritten.
- **IIS/server steps (local):** `CodexAsset\artifacts\rel1014a-launch-elevated.ps1` -> `rel1014a-iis-publish.ps1`. It pre-checks live Web.dll `06AC4589...0B55`, Web.config `27426D6E...D4D3`, package Web.dll `E6C7B033...DE69`, the 6 hotfix files (06bf814 hashes) and 140 live .cs files. Then: site backup; DB COPY_ONLY backup; the 6 hotfix files backed up again on their own; removes ONLY the 4 stray hotfix .cs files (`ViewModels\UiChromeViewModels.cs`, `Helpers\DepartmentSelectHtmlHelpers.cs`, `Controllers\BaseController.cs`, `Controllers\PurchaseRequestsController.cs`); copy (289 files, skipping Web.config/secrets); hash check; the other 136 .cs files hash-checked unchanged; login smoke; rollback on failure. `Create.cshtml` and `site.css` from the hotfix are overwritten by the package (site.css is identical). Package `CodexAsset-wt-bulk-print\artifacts\web-publish-rel1014a-20261014_120643` (292 files, no Web.config, no .cs). Expected hashes in `CodexAsset\artifacts\rel1014a-expected.txt`.
- **Live-deploy steps:** deploy the site files (keep live Web.config). No SQL.
- **Verify:** unit tests 833/865 pass on the full suite; the 26 failures are identical on HEAD before this change (pre-existing), the 6 new/updated tests pass (scoped run 28/28). E2E 43/43 on the copy: PO 5676 (purchase 17) = 12 received assets, all 12 returned (one forced to Assigned on the copy, still included), newest first, day key on every row; banner button -> `?purchaseId=17` preselects and auto-loads; other-tenant purchase (org 10) ignored on the page and in JSON; asset list PO filter shows exactly the 12 tags; Scan & Search has no print panel or queue, lookup still works; rename tool page and preview, AssetCategories/Types/SubTypes/Edit 141, Reports, Purchases 200; purchase request create shows the grouped department select (hotfix) and catalog pickers, and saves; anonymous PrintCandidates -> 302 to login. Headless Edge test of the day headers: 3 days, day select, indeterminate on partial, master tri-state, collapse/expand, queue selected. Copy DB dropped, test site removed, :8095 free. DB backups first: `AssetManagementModuleDb_pre_rel1014a_e2e_20261014_121055.bak` and `..._121436.bak`.
- **Rollback:** the script restores the site backup on failure. After a good publish: restore `C:\inetpub\_backups\AssetManagement_root_pre_rel1014a_<stamp>` (also brings back the 4 .cs files; they are in `..._hotfix0742_files` too). No DB change to undo.

## 2026-10-14 07:46 - Department select / purchase request hotfix deployed straight to live (UNLOGGED at the time; recorded 2026-10-14 by rel1014a)

- **Status:** deployed to local IIS :8080 on 2026-10-14 (6 files copied 07:42:18-07:42:26 EAT, Debug DLLs 07:46 EAT). Not logged here at the time; no site backup and no deploy log were made. Allan reviewed it on Oct 14 and decided to keep it.
- **What:** Debug build of the main checkout (`fix/role-access-scope`) went to live: Web.dll `06AC4589CBF2A3F73F8375DA102371549775BC998AE16F6315D735206F610B55`, Application `4F6D7C28...`, Infrastructure `88DE2250...`, Domain `0DF7BEEF...`. Six source/content files were also copied into the site root: `ViewModels\UiChromeViewModels.cs`, `Helpers\DepartmentSelectHtmlHelpers.cs`, `Controllers\BaseController.cs`, `Controllers\PurchaseRequestsController.cs`, `Views\PurchaseRequests\Create.cshtml`, `Content\css\site.css` (grouped department select with optgroup classes on purchase request create).
- **Side effects found:** it replaced rel1013b (Web.dll `9F64B995...`). The rename tool pages and the catalog requisition flows (`afcfa78`) disappeared from live; migration 103 tables stayed in the DB. Four of the copied files are .cs sources that do not belong in the site root.
- **Commit hash:** `06bf814` "Oct 14 07:42 hotfix copied to live (6 files)" on `fix/role-access-scope`, committed afterwards. All 6 files are hash-identical to the live copies.
- **DB migration/script:** none. **Config/Web.config keys:** none (Web.config `27426D6E...D4D3` unchanged).
- **Follow-up:** rel1014a (above) carries the hotfix on top of rel1013b, brings back the rename tool and catalog requisition flows, and removes the 4 stray .cs files.
## 2026-10-13 13:25 - Rename existing asset tags to the prefix format (rel1013b, `d7a0552`) - PUBLISHED (rel1013b, local IIS :8080 2026-10-13 13:25:51 EAT; branch `feature/asset-tag-rename-rel1013b` on rel1013a `4e0b4ba`)

- **Published:** 2026-10-13 13:25:51 EAT to local IIS :8080 (Allan approved in chat 13:24 EAT; pre-checks re-run and matched; launcher fired once, UAC accepted 13:25:58). Live Web.dll was 08417DAA...5D55 (rel1013a), now 9F64B995CF505A84C9C6E838EAD571FC61D9F2707AF331A6FA2BEBCB35BB5634 (Application 69AB9096...4701, Infrastructure 360958DB...3A3B, Domain 0166AE2B...0B50); 288 files copied, 0 hash mismatches; Web.config unchanged 27426D6E...D4D3; smtp.secrets unchanged. Site backup C:\inetpub\_backups\AssetManagement_root_pre_rel1013b_20261013_132525 (1175 files). DB backup AssetManagementModuleDb_pre_rel1013b_publish_20261013_132525.bak; 103 applied and verified (102 still present). Smoke: 4 login pages 200, no YSOD. Scratch Test-WF Company Admin check 23/23 (DB backup AssetManagementModuleDb_pre_rel1013b_liveverify_20261013_132747.bak first): Rename existing tags page + whole-tenant preview (13 tags would change; NOT applied, 0 batches written); apply/undo only on 2 throw-away assets in a throw-away department (ZZVLEG001/002 -> ZVD-ZVR1-001/002 and back, 1 apply + 1 undo audit row); scan JSON, scan page, search page, print-candidates and Assets/Index found the asset by old tag (200); Dashboard/Reports/PurchaseRequests/AssetCategories/Settings/Settings AssetTags/Assets/AssetScan Lookup 200; anonymous redirect to login; existing Test-WF tags unchanged (hash). Cleanup: scratch user, throw-away assets, aliases, batch, departments and ZVD sequence rows deleted (audit rows kept).
- **Date/time EAT:** 2026-10-13 13:25. Built, unit-tested and proven end-to-end on IIS Express (:8095) against a throw-away COPY of the local DB (Test-WF A46138179). Not on :8080, not on live. Publish not fired.
- **What and why:** Allan asked for an admin tool to move EXISTING asset tags to the rel1013a prefix format without breaking old labels.
  - Settings > Asset tag prefix > "Rename existing tags" (`/Settings/AssetTagRename`, Settings.Manage only). Scope: whole tenant, or one department / sub-department / room and everything below it.
  - Preview lists every old tag and its new tag. Tags that already match `PREFIX-digits` for their department are left alone. Preview changes nothing.
  - Apply renames the whole preview as one batch. If assets in scope changed since the preview (fingerprint), nothing is renamed and the preview is shown again.
  - The batch runs in one SQL transaction under a per-tenant `sp_getapplock`. Each asset must still have the previewed old tag. Numbers come from `AssetTagSequence` (rel1013a counter, row locked UPDLOCK/HOLDLOCK), never below the highest tag or alias for the prefix, and each new tag is checked against all tenant tags and active aliases. Any problem rolls back the whole batch.
  - Every old tag is kept in `AssetTagAlias`. QR/scan lookup (exact, case/space/hyphen-insensitive), global search and the asset list search still find the asset by its old tag. A direct tag match always ranks before an alias.
  - Uniqueness per tenant now covers tags AND active aliases: a manual tag cannot reuse an old tag, and the tag counter skips alias numbers.
  - Undo per batch restores the exact old tags and turns the aliases off. It refuses, and changes nothing, when an asset in the batch has a different tag since, or an old tag is now used by another asset or alias.
  - One audit row per batch: `Assets.TagRename.Apply` / `Assets.TagRename.Undo` (EntityType `AssetTagRenameBatch`, scope, count, sample old->new).
  - Tenant isolation: departments come from the tenant-scoped repository (other tenant ids give 404) and every store query filters by organisation id.
- **Commit hash:** `d7a0552` (local only; no push, no PR).
- **Files changed:**
  - `src/AssetManagement.Application/Contracts/IAssetTagRenameStore.cs` (new), `src/AssetManagement.Application/Helpers/AssetTagRenamePlanner.cs` (new, pure planner)
  - `src/AssetManagement.Infrastructure/Queries/AssetTagRenameStore.cs` (new: transactional apply/undo)
  - `src/AssetManagement.Infrastructure/Queries/AssetTagSequenceStore.cs` (counter scan includes active aliases)
  - `src/AssetManagement.Infrastructure/Repositories/AssetScanLookupRepository.cs`, `Queries/SearchQueryService.cs`, `Queries/AssetQueryService.cs` (alias fallback)
  - `src/AssetManagement.Application/Services/Assets/AssetService.cs` (manual tag cannot equal an active alias; optional ctor param)
  - `src/AssetManagement.Web/Controllers/SettingsController.AssetTagRename.cs`, `ViewModels/AssetTagRenameVm.cs`, `Views/Settings/AssetTagRename.cshtml` (new); `Views/Settings/AssetTags.cshtml`, `Views/Settings/Index.cshtml` (links); `App_Start/AutofacConfig.cs`; `AssetManagement.Web.csproj`
  - `tests/AssetManagement.Tests/AssetTagRenamePlannerTests.cs` (new, 8 tests)
- **DB migration/script:** `database/scripts/004_Migrations/103_AssetTagRename.sql` (sha256 5AA48BC7...E42D). Creates `AssetTagRenameBatch` and `AssetTagAlias` (unique filtered index `UX_AssetTagAlias_Org_Alias_Active` on OrganizationId + AliasTag where active). Additive and idempotent (2 runs proven). **Must run before the code** (scan/search queries read `AssetTagAlias`). Needs 102 first.
- **Config/Web.config keys:** none.
- **IIS/server steps (local):** `artifacts\rel1013b-launch-elevated.ps1` -> `rel1013b-iis-publish.ps1` (pre-checks live Web.dll `08417DAA...5D55` = rel1013a, Web.config `27426D6E...D4D3`, package Web.dll `9F64B995...5634`, 102 applied; site backup; DB COPY_ONLY backup; 103 + verify; copy skipping Web.config/secrets; hash check; login smoke; rollback on failure). Package `artifacts\web-publish-rel1013b-20261013_131228` (worktree, 291 files, no Web.config). Expected hashes in `artifacts\rel1013b-expected.txt` (SoT). Fired once 13:25 EAT (see Published).
- **Live-deploy steps:** run 103 in SSMS on the live DB (after 102), then deploy the site files (keep live Web.config). Nothing is renamed on deploy; renames only happen when an admin applies a batch.
- **Verify:** unit tests 64/65 (8 new planner tests pass; the 1 failure `AssignmentService_CustodyEventIncludesPreviousCustodian` is the known one, fails at HEAD too). E2E 38/38 on the copy: 103 idempotent; other-tenant scope 404; preview (Physics subtree) = `ZBLEGA001->ZBSCI-ZBLAB-006`, `ZBLEGA002->-007` (after a conforming `-005`), `ZBLEGP001->ZBSCI-ZBPHY-001`, parent dept and conforming tag untouched; stale preview refused; apply matched preview, 1 batch, 3 aliases, 1 audit row, counter 7; scan by old tag (exact and `zbleg-a001`), search by old-tag prefix, asset list search and scan by new tag all find the asset; manual reuse of an old tag rejected; next auto tag `-008`; 0 duplicates across tags + aliases; 2 simultaneous applies -> exactly 1 batch; undo refused for both conflict types with nothing changed; undo restored exact old tags, aliases off, 1 audit row; second undo refused; all other assets' tags unchanged (hash). Copy DB dropped, site removed, :8095 free. DB backups before the E2E: `AssetManagementModuleDb_pre_rel1013b_e2e_20261013_13*.bak` (4 runs).
- **Rollback:** undo any applied batches first on the settings page (restores old tags). Then redeploy the rel1013a site backup; optionally `DROP TABLE dbo.AssetTagAlias; DROP TABLE dbo.AssetTagRenameBatch;` and delete the 103 history row. If tables are dropped without undo, renamed assets keep their new tags and old tags stop resolving.
## 2026-10-13 11:52 - Configurable asset tag prefix per tenant (rel1013a, `e561335` + `df327b4`) - PUBLISHED (rel1013a, local IIS :8080 2026-10-13 12:53:11 EAT; branch `feature/asset-tag-prefix-rel1013a` on base `592da5b` integ/catreq-reports-multiselect)

- **Published:** 2026-10-13 12:53:11 EAT to local IIS :8080 (Allan said "resend" in chat 12:52 EAT; launcher fired once, UAC accepted 12:53:25). Base: `592da5b` (integ/catreq-reports-multiselect, previously live as Web.dll E7887D1E...477C since 11:09 EAT, not recorded as PUBLISHED on this branch before). Live Web.dll now 08417DAA84889247C23F5700222F125294A49C9DF14EC0C5B040A49351F85D55 (Application A18BB8EF...2344, Infrastructure 53578C61...CDBC, Domain 3C898DCA...CD23); 287 files copied, 0 hash mismatches; Web.config unchanged 27426D6E...D4D3; smtp.secrets unchanged. Site backup C:\inetpub\_backups\AssetManagement_root_pre_rel1013a_20261013_125237 (874 files). DB backup AssetManagementModuleDb_pre_rel1013a_publish_20261013_125237.bak; 102 applied and verified (100/101 still present). Smoke: 4 login pages 200, no YSOD. Scratch Test-WF Company Admin check 12/12: Asset tag prefix page + preview ZVD-ZVR1-001, throw-away asset got ZVD-ZVR1-001, Dashboard/Reports (multi-select)/PurchaseRequests Index+Create/AssetCategories/Settings/Assets 200, anonymous redirect to login, existing Test-WF tags unchanged; scratch user, throw-away asset, departments and sequence rows deleted (backup AssetManagementModuleDb_pre_rel1013a_liveverify_20261013_125448.bak).
- **Publish attempt:** 2026-10-13 12:36 EAT, Allan approved in chat; launcher fired once; UAC declined at 12:38:38 EAT (1223). Nothing changed: live Web.dll still E7887D1E...477C (base 592da5b), Web.config 27426D6E...D4D3, 102 not applied. Still STAGED; not re-fired.
- **Date/time EAT:** 2026-10-13 11:52. Built, unit-tested and proven end-to-end on IIS Express (:8095) against a throw-away COPY of the local DB (Test-WF A46138179). Not on :8080, not on live.
- **What and why:** Allan asked that admins can choose how new asset tags start.
  - New tags are `PREFIX-NNN` (3+ digits, same `-NNN` suffix as the old DEPT-TYPE-NNN generator). The old random 10-character tag stays available (untick "Generate structured tags").
  - Default prefix (no override): top department code (max 6) + `-` + room code (max 8), e.g. `SCI-LAB1-001`. Asset not in a room: top department + its own sub-unit (`SCI-PHY-001`), or just the department (`SCI-001`). No code: acronym of the name. No department: `GEN-001`.
  - Overrides (`Department.AssetTagPrefix`) on any department, sub-department or room. Most specific wins: walk up from the asset's department/room; the first node with an override wins (room, then nearest sub-department, then department), then the optional tenant-wide prefix, then the derived default.
  - Existing tags are never changed. Manual tags are kept as typed.
  - Uniqueness per tenant: new table `AssetTagSequence` (OrganizationId, Prefix) hands out numbers atomically on its own short connection (UPDLOCK/HOLDLOCK), never below the highest existing tag for that prefix; every candidate is checked against all tenant assets; the unique index `IX_Asset_Org_AssetTag` is the last guard.
  - Applies to manual Create, receiving (it goes through Create) and Excel import.
  - Settings > Asset tag prefix (`/Settings/AssetTags`, Settings.Manage only): enable/disable, tenant prefix, override per department/room in a tree with the effective prefix and its source, and a "Preview the next tag" button (read-only, does not reserve a number). Only the current tenant's departments are listed or saved; preview refuses other tenants' ids. Saves write an audit row `Settings.AssetTagPrefix`.
- **Commit hash:** `e561335` (feature) + `df327b4` (migration renumbered 100 -> 102; 100/101 are catalog-requisition-flows). Same changes on `fix/role-access-scope` as `69dbab6` + `ba6fa43`.
- **Files changed:**
  - `src/AssetManagement.Application/Helpers/AssetTagPrefixResolver.cs` (new: precedence, default format, normalisation)
  - `src/AssetManagement.Application/Helpers/AssetTagPrefixGenerator.cs` (new: shared generator)
  - `src/AssetManagement.Application/Contracts/IAssetTagSequenceStore.cs` (new), `src/AssetManagement.Infrastructure/Queries/AssetTagSequenceStore.cs` (new: SQL counter)
  - `src/AssetManagement.Application/Services/Assets/AssetService.cs`, `AssetImportService.cs` (use the generator; optional ctor param)
  - `src/AssetManagement.Domain/Entities/Department.cs` (`AssetTagPrefix`)
  - `src/AssetManagement.Web/Controllers/SettingsController.AssetTags.cs`, `ViewModels/AssetTagSettingsVm.cs`, `Views/Settings/AssetTags.cshtml` (new), `Views/Settings/Index.cshtml` (link), `App_Start/AutofacConfig.cs`, `AssetManagement.Web.csproj`
  - `tests/AssetManagement.Tests/AssetTagPrefixResolverTests.cs` (new, 8 tests), `tests/AssetManagement.Tests/ServiceRuleTests.cs` (create-tag test now checks IT-001, IT-002, and legacy random when disabled)
- **DB migration/script:** `database/scripts/004_Migrations/102_AssetTagPrefix.sql` - adds `Department.AssetTagPrefix NVARCHAR(20) NULL`, creates `AssetTagSequence`, ensures `IX_Asset_Org_AssetTag` exists (only if no duplicates). Idempotent (re-run = no-op, proven). **Must run before the code** (the Department entity map reads the new column). Needs 100/101 applied first on this line.
- **Config/Web.config keys:** none. Tenant settings are SystemSetting rows `AssetTag.Prefix.Enabled` (missing = true) and `AssetTag.Prefix.TenantDefault` (blank = derived), written by the settings page.
- **IIS/server steps (local):** `artifacts\rel1013a-launch-elevated.ps1` -> `rel1013a-iis-publish.ps1` (pre-checks live Web.dll `E7887D1E...477C`, Web.config `27426D6E...D4D3`, package Web.dll `08417DAA...5D55`; site backup; DB COPY_ONLY backup; 102 + verify; copy skipping Web.config/secrets; hash check; login smoke; rollback on failure). Expected hashes in `artifacts\rel1013a-expected.txt`. **Not fired.**
- **Live-deploy steps:** run 102 in SSMS on the live DB (after 100/101), then deploy the site files (keep live Web.config). Behaviour changes for every tenant on deploy: new assets get structured tags by default; a tenant can switch back to random tags on the settings page.
- **Verify:** unit tests 56/57 (8 new resolver tests + updated create-tag test pass; the 1 failure `AssignmentService_CustodyEventIncludesPreviousCustodian` fails identically at HEAD before this change). E2E 19/19 on the copy: default `ZSCI-ZLAB1-001/002`, manual `-003` kept and skipped (`-004` next), sub-dept default `ZSCI-ZPHY-001`, dept `ZSCI-001`, dept override `ZSC-001`, sub-dept override `ZPH-001`, room override `ZL1-001`, preview matches each time, other-tenant preview refused, invalid prefix rejected, 0 duplicate tags, existing org-13 tags hash unchanged. Concurrency: 60 parallel allocations, 0 errors, no repeats. Duplicate insert refused by the unique index (2601).
- **Rollback:** redeploy the previous site backup; then optionally `DROP TABLE dbo.AssetTagSequence; ALTER TABLE dbo.Department DROP COLUMN AssetTagPrefix;` and delete the 102 history row (the column must stay while the rel1013a code is live). Tags already issued stay valid.
## 2026-10-13 08:58 - Customize Reports: multi-select Departments and Categories (`cc1119d`) - STAGED (branch `feature/reports-multiselect-dept-category`, GitHub PR; not published to IIS)

- **Date/time EAT:** 2026-10-13 08:58 (commit `cc1119d`). Not published to local IIS; not on live.
- **What and why:** Allan asked that Customize Reports let users pick several departments and several categories instead of one.
  - The Department and Category dropdowns on each report's filter panel are now scrollable checkbox lists with "Select all" and "Clear" links. Nothing ticked = all (same as the old "All departments"/"All categories").
  - Preview, CSV and PDF/HTML export filter by every ticked id (asset register, custody movement, department summary, pending approvals, general ledger, warranty expiry, insurance coverage, depreciation). The filter summary lists all chosen names.
  - Department scope security is unchanged: the list only shows departments the user may see, and every builder still applies `IDepartmentScope` on top of the chosen ids. Child departments are NOT auto-included (matches previous single-select exact-match behaviour).
  - Backward compatible: the legacy single `DepartmentId`/`CategoryId` request fields are still accepted and folded into the lists (`NormalizeMultiSelectFilters`); saved templates with the old single value still load.
- **Commit hash:** `cc1119d`
- **Files changed:**
  - `src/AssetManagement.Web/Views/Reports/_ReportFilters.cshtml` (checkbox lists)
  - `src/AssetManagement.Web/Scripts/app/reports.js` (collect/apply arrays, repeated-key POST, Select all/Clear)
  - `src/AssetManagement.Application/ViewModels/Reports/ReportViewModels.cs` (`DepartmentIds`, `CategoryIds`)
  - `src/AssetManagement.Application/ViewModels/Assets/AssetViewModels.cs` (`AssetFilterVm.DepartmentIds/CategoryIds`)
  - `src/AssetManagement.Infrastructure/Queries/AssetQueryService.cs` (parameterised `IN (...)` clauses)
  - `src/AssetManagement.Application/Services/ReportDocumentService.cs` (all builders + filter summary)
- **DB migration/script:** none.
- **Config/Web.config keys:** none.
- **IIS/server steps (local):** normal elevated publish when Allan approves (not done).
- **Live-deploy steps:** copy the published site (Web.dll, Application/Infrastructure DLLs, `Views/Reports/_ReportFilters.cshtml`, `Scripts/app/reports.js`) over live; keep live Web.config. No DB step. Hard-refresh browsers (JS changed).
- **Verify:** Reports > any report > Configure: tick 2 departments + 2 categories > Preview shows only assets in those; summary line reads "Departments: A, B"; CSV matches. Clear all > Preview = all in scope. A department-scoped user sees only their departments in the list.
- **Rollback:** redeploy the previous Web.dll/Application/Infrastructure DLLs and the previous `_ReportFilters.cshtml` + `reports.js`; or `git revert cc1119d`.

## 2026-10-12 21:10 - catalog-requisition-flows: owner decisions: identical paths from different catalog nodes stay separate; catalog paths always apply even when org approvals are off (supersede PM D1/D2) - STAGED (not published, not pushed)

- **What and why:** owner decision; RequisitionFlowResolver fingerprint includes the winning source node again, so different catalog nodes never merge even with identical stages; the org approval switch no longer suppresses catalog paths. Supersedes D1 and D2 (see D1a/D2a).
- **Commit hash:** see branch `feature/catalog-requisition-flows` (commit after `d84e7d6`).
- **Files changed:** Application/Helpers/RequisitionFlowResolver.cs; tests/Requisitions/CatalogRequisitionUiTests.cs; docs.
- **DB migration/script:** none. **Config keys:** none. **IIS steps:** none.
- **Live-deploy steps:** ships with stages (c)/(d).
- **Verify:** full unit test suite; only the 26 baseline failures.
- **Rollback:** revert the commit.

## 2026-10-12 22:30 - catalog-requisition-flows stages (c) + (d): Asset Categories approval setup, line pickers, batch page; sub-type required at receiving (`afcfa78`, `c103684`, branch `feature/catalog-requisition-flows`) - STAGED (not published, not pushed)

- **Date/time EAT:** 2026-10-12. Local worktree `CodexAsset-catalog-req` only. Nothing published, pushed or PR'd.
- **What and why:** (c) approval paths are set up on the existing Organization > Asset Categories / Types / Sub-types Edit pages; Settings page holds ClassificationMode / MixedFlowPolicy; requisition Create has cascading line pickers and a live path preview; split submits open a Batch summary. (d) every new receipt needs a sub-type (server + DB trigger); legacy NULLs untouched. PM decisions: identical paths merge; catalog paths obey the org approval switch; default policy rows seeded.
- **Commit hash:** `afcfa78` (c), `c103684` (d + PM decisions).
- **Files changed:** see DECISIONS-requisition.md C1-D6; main: CatalogRequisitionFlowService, PurchaseRequestService, RequisitionFlowResolver, ReceivingService, BaseController.CatalogRequisition.cs, AssetCategories/AssetTypes/AssetSubTypes/Settings/PurchaseRequests controllers, Create/Details/Batch/Receive/Settings Index views, purchase-request-classification.js, tests.
- **DB migration/script:** `004_Migrations/101_ReceivingSubTypeRequired.sql` (trigger `TR_AssetReceiving_RequireSubType`, seeds settings rows, SchemaMigrationHistory). NOT applied to local main DB; verified only on throwaway `AssetManagementModuleDb_catreq_test`. Rollbacks (prepared, not run): `ops/101_Rollback_ReceivingSubTypeRequired_PREPARE_DO_NOT_RUN.sql`, `ops/100_Rollback_CatalogRequisitionFlow_PREPARE_DO_NOT_RUN.sql` (run 101 rollback first).
- **Config/Web.config keys:** none.
- **IIS/server steps (local):** none. Not published.
- **Live-deploy steps:** backup DB; run 100 (if not yet) then 101; deploy Web. Run 101 only together with the new ReceivingService/Receive view (old receive UI could not pick a sub-type).
- **Verify:** 770 passed, 26 failed (same 26 as baseline), 6 skipped. aspnet_compiler Razor build OK. Trigger check on throwaway DB: NULL sub-type receipt rejected, with sub-type accepted, 12 legacy rows still updatable. No browser/UI tests (held by PM).
- **Rollback:** run 101 rollback script, revert `c103684` and `afcfa78`.

## 2026-10-12 20:06 - catalog-requisition-flows stage (b): group-and-split on submit, ClassificationMode / MixedFlowPolicy org policies (`264a225`, branch `feature/catalog-requisition-flows`) - STAGED (not published, not pushed)

- **Date/time EAT:** 2026-10-12 20:00-20:06. Local worktree `CodexAsset-catalog-req` only. Nothing published, nothing pushed, no PR.
- **What and why:** one requisition form whose lines need different approval paths can no longer silently use one path.
  1. `PurchaseRequestService.SubmitBatch` resolves each line's path (stage a resolver), groups lines by fingerprint and creates one requisition per path. One path = one requisition exactly as before. Several paths: `AutoSplit` (default) creates a `PurchaseRequestGroup` (`BATCH-000nnn`) and N requisitions with their own PR numbers and approval snapshots, sharing department, justification, notes and the attachment; `Block` rejects the form. All in one transaction; every path is validated first.
  2. Org settings `Purchases.Requisition.ClassificationMode` (`Optional` default, `RequiredPerLine`, `RequiredHeader`) and `Purchases.Requisition.MixedFlowPolicy` (`AutoSplit` default, `Block`), enforced server-side. Not seeded: missing = today's behaviour.
  3. Lines store Category/Type/Sub-type (normalized, org-checked). A tagged asset pre-fills line 1 (best effort).
  4. `Submit` keeps returning an int (first id). The Create POST uses `SubmitBatch`; a split shows "Submitted N requisitions (PR-..., BATCH-...) because the items use different approval paths." and opens the first one. No view changed (form pickers and batch summary are stage c).
- **Commit hash:** `264a225`.
- **Files changed:** Application: Contracts/Purchases/IPurchaseRequestService.cs, Services/Purchases/PurchaseRequestService.cs, ViewModels/Purchases/PurchaseRequestViewModels.cs. Web: Controllers/PurchaseRequestsController.cs. Tests: Requisitions/PurchaseRequestSplitSubmitTests.cs (15 tests).
- **DB migration/script:** none beyond 100 (stage a entry below).
- **Config/Web.config keys:** none.
- **IIS/server steps (local):** none. Not published.
- **Live-deploy steps:** deploy together with stage (a) after migration 100. No settings to add (defaults keep current behaviour).
- **Verify:** `dotnet test tests/AssetManagement.Tests` (see stage a for the build flag): 739 passed, 26 failed (all 26 fail identically on the base commit), 6 skipped; new split tests 15/15. No UI/browser test in this dispatch.
- **Rollback:** revert `264a225` (stage a can stay).

## 2026-10-12 20:00 - catalog-requisition-flows stage (a): catalog requisition flow resolver + approval fingerprint, migration 100, stale test fix (`474c715`, `d341621`, branch `feature/catalog-requisition-flows`) - STAGED (not published, not pushed)

- **Date/time EAT:** 2026-10-12 19:47-20:00. Worktree `C:\Users\allan\Documents\Examples\CodexAsset-catalog-req`, branch off `fix/role-access-scope` `9a908b8`. Nothing published, nothing pushed, no PR.
- **What and why:** catalog-level approval overrides (plan `catalog_requisition_flows_c041f2a2`, owner decisions D1-D4 in `docs/DECISIONS-requisition.md`).
  1. `CatalogRequisitionFlowResolver` + `RequisitionFlowResolver.ResolveForLine`: per line, sub-type > type > category override, else the unchanged department inherit chain, else org matrix. The PO raiser comes from the winning source.
  2. Approval fingerprint (source key + ordered stages + users + auto flag + PO raiser) used to group lines on submit.
  3. `PurchaseOrderRaiserRules.ResolveForRequest` resolves through the requisition's classified line, so a catalog-routed requisition's PO raiser is the catalog node's. Unclassified requisitions are unchanged.
  4. New entities `CatalogRequisitionFlow`, `PurchaseRequestGroup`; `PurchaseRequest.PurchaseRequestGroupId`, `ApprovalFlowSource`; `PurchaseRequestLine.AssetCategoryId/AssetTypeId/AssetSubTypeId`. Registered in `EntityMapRegistry`; added to `OrganizationPurgeSql`.
  5. `474c715`: the test project did not build (43 errors, tests using removed APIs). Those tests are `#if`-excluded, 3 ported; no production change.
- **Commit hash:** `474c715` (tests build fix), `d341621` (stage a).
- **Files changed:** Domain: Entities/CatalogRequisitionFlow.cs (new), Entities/PurchaseRequestGroup.cs (new), Enums/CatalogFlowScope.cs (new), Enums/RequisitionPolicies.cs (new), Entities/PurchaseRequest.cs, Entities/PurchaseRequestLine.cs. Application: Helpers/CatalogRequisitionFlowResolver.cs, RequisitionFlowResolver.cs, RequisitionLineClassification.cs, RequisitionPolicySettings.cs (all new), Services/Approvals/PurchaseOrderRaiserRules.cs. Infrastructure: Persistence/EntityMapRegistry.cs, Services/OrganizationPurgeSql.cs. DB: database/scripts/004_Migrations/100_CatalogRequisitionFlow.sql. Tests: Requisitions/CatalogRequisitionFlowResolverTests.cs (20 tests), Helpers/ApprovalWorkflowSelfApproveTests.cs, DepartmentHierarchyRulesTests.cs, DepartmentRequisitionFlowResolverTests.cs.
- **DB migration/script:** `database\scripts\004_Migrations\100_CatalogRequisitionFlow.sql` (additive, idempotent, rollback in header, records SchemaMigrationHistory). **Applied on local .\SQLEXPRESS AssetManagementModuleDb 2026-10-12 19:59:28 EAT** (history Id 104), run twice (second run no-op). Verify row: all objects 1, HistoryRows 1; 0 existing PurchaseRequest rows changed (45 PRs, 19 lines untouched, new columns NULL). Pre-change backup (COPY_ONLY, CHECKSUM, RESTORE VERIFYONLY valid): `C:\Program Files\Microsoft SQL Server\MSSQL15.SQLEXPRESS\MSSQL\Backup\AssetManagementModuleDb_pre100_catreq_20261012.bak` (58,683,904 bytes, SHA256 `119878ABFC3EDB8C65CF7496FF058C061268128CF8C43153D82B3BAD8A51B27A`).
- **Config/Web.config keys:** none.
- **IIS/server steps (local):** none. Local IIS :8080 still runs the role-access-scope build, which does not map the new columns; the additive schema is harmless to it.
- **Live-deploy steps:** back up, run 100 on the live DB **before** deploying a build that contains `d341621` (the ORM writes every mapped column, so PurchaseRequest/PurchaseRequestLine inserts fail without it). No catalog rows or settings needed: behaviour is unchanged until an admin configures a catalog flow (UI in stage c).
- **Verify:** test project build flag from this worktree: `-p:NuGetPackageRoot=C:\Users\allan\Documents\Examples\CodexAsset\.nuget\packages\`. After stage a: 724 passed, 26 failed (same 26 as base), 6 skipped; resolver tests 20/20.
- **Rollback:** revert `d341621` (and `474c715` if wanted), then run the rollback lines in the 100 header (or restore the backup above).

## 2026-10-12 19:20 - role-access-scope: Assets.ViewAll org-wide capability, custom roles need a department, dashboard/redirect alignment, Depreciation in role editor, single tenant-admin helper (`5857956`, branch `fix/role-access-scope`) - PUBLISHED (local IIS :8080 2026-10-12 19:45:22 EAT; not pushed)

- **Date/time EAT:** 2026-10-12 18:59-19:20. Owner (Wambua) approved in chat 18:58 EAT. Local branch `fix/role-access-scope` (off `main` `8f6bff4`). No package built, nothing published, nothing pushed.
- **What and why:** symptom was "custom role (Facilities Manager) can't reach modules, editing the role doesn't help, the user sees no assets while Company Admin does".
  1. Department scope bypass was the hardcoded role name "Company Admin". It is now the permission **Assets.ViewAll** (`DepartmentScopeService.BypassesDepartmentScope`, `BaseController.HasOrgWideScope` / `IsCurrentUserSuperAdmin`, `MetricsService`). Company Admin keeps it (AuthorizationService bypass plus the 099 seed); support impersonation keeps its bypass.
  2. Asset list default filter: non-approvers were silently filtered to "assets I hold" (`ListRoleDefaultsHelper`), so a Facilities Manager with a department saw an empty register. Now holders of Assets.Assign / Transfer / Edit (or Request.Approve) default to their department (`AssetsController.CanManageDepartmentAssets`).
  3. Custom (non-system) roles must have a department when the org has active departments: `CustomRoleDepartmentRule` (MembershipService.CreateUser, invitation create; invitation accept registers without the role if its department vanished) and `UserService.AssignRole` / `AssignDepartment`.
  4. Dashboard and Notifications gate on any working tenant right (`DashboardAccessAttribute`, `ModulePermissionCatalog.DashboardAccessPermissionCodes`), not only Reports.View; sidebar Dashboard link matches. PostLoginRedirectHelper uses any-of lists matching the controllers (Dashboard, Notifications, PendingApprovals, PurchaseRequests, Users, Documents, UserInvitations). Users.Invite added to the Users module; Insurance.View to Incidents & Claims; Assets.ViewAll to Assets.
  5. Depreciation module no longer hidden from the role matrix (`PermissionService`), so Depreciation.View/Manage can be granted and saved.
  6. `OrgCapabilities` (Application.Security) is the single helper for tenant-admin identity and org-wide scope; used by TenantFilterAttribute (removed the always-false `IsInRole("Company Admin")`), BaseController, UserLifecycleRepository, UserAccountQueryRepository, OrganizationsController, PlatformUserViewModels, OrganizationService.
- **Commit hash:** `5857956`.
- **Files changed:** Application: Security/OrgCapabilities.cs (new), Security/ModulePermissionCatalog.cs, Services/Security/DepartmentScopeService.cs, Services/Security/PermissionService.cs, Services/MetricsService.cs, Services/Organizations/OrganizationService.cs, ViewModels/Platform/PlatformUserViewModels.cs. Infrastructure: Security/CustomRoleDepartmentRule.cs (new), Services/MembershipService.cs, Services/UserInvitationService.cs, Services/UserService.cs, Identity/UserLifecycleRepository.cs, Identity/UserAccountQueryRepository.cs. Web: Filters/DashboardAccessAttribute.cs (new), Filters/TenantFilterAttribute.cs, AssetManagement.Web.csproj, Controllers/BaseController.cs, AssetsController.cs, DashboardController.cs, NotificationsController.cs, PendingApprovalsController.cs, Helpers/PostLoginRedirectHelper.cs, Views/Shared/_SidebarNav.cshtml, Areas/Platform/Controllers/OrganizationsController.cs. Test/proof: e2e/_qa_roleaccess_headed.js, e2e/_qa_roleaccess_after2.js, artifacts/roleaccess/zz_fixtures*.sql.
- **DB migration/script:** `database\scripts\004_Migrations\099_AssetsViewAllPermission.sql` (additive, idempotent, transactional, rollback in header, records SchemaMigrationHistory). **Applied on local .\SQLEXPRESS AssetManagementModuleDb 2026-10-12 19:11:17 EAT** (history Id 103; re-run = no-op). Result: Permission Assets.ViewAll Id 71; granted to all 10 system Company Admin roles; 0 other roles. Pre-change backup (COPY_ONLY, CHECKSUM, RESTORE VERIFYONLY valid): `C:\Program Files\Microsoft SQL Server\MSSQL15.SQLEXPRESS\MSSQL\Backup\AssetManagementModuleDb_pre_roleaccess_20261012_185940.bak`.
- **Config/Web.config keys:** none. Web.config not touched.
- **IIS/server steps (local):** **PUBLISHED 2026-10-12 19:45:22 EAT.** Owner approved in chat 19:44 and accepted the UAC prompt (launcher fired once 19:44:32, elevated exit 0 at 19:45:29). Pre-checks passed (live Web.dll `9EDFCD0F...93C3`, Web.config `27426D6E...D4D3`, 099 verified=1). IIS backup `C:\inetpub\_backups\AssetManagement_root_pre_roleaccess_20261012_194437` (876 files); DB backup `AssetManagementModuleDb_pre_roleaccess_publish_20261012_194437.bak`. 283 files copied, 283 hash-checked, 0 mismatches; Web.config, Views web.configs and smtp.secrets.config skipped (Web.config and secrets unchanged). Live DLLs: Web `0BE0611B0E535ACEBD6B9DCF2381CC4BD2B5DC5A2604890EB5782A536499001D`, Application `F833AE9B...8F6F`, Infrastructure `CE51D5DA...40E9`, Domain `698D19C6...8B5B`. Login pages 200, no YSOD. Headed smoke on :8080 (scratch users, removed after; AuditLog kept): Company Admin 415 assets, all pages 200; FM with dept 39 dept asset rows, Dashboard/Notifications 200, Roles/Invitations 403; FM without dept empty by design (`artifacts\roleaccess\after-iis`). Earlier attempt: 2026-10-12 19:23:50 EAT: package `artifacts\web-publish-roleaccess-20261012_192314` (built at `87de8b8`, Web.dll `0BE0611B0E535ACEBD6B9DCF2381CC4BD2B5DC5A2604890EB5782A536499001D`), scripts `artifacts\roleaccess-launch-elevated.ps1` -> `artifacts\roleaccess-iis-publish.ps1`. UAC prompt fired once and was cancelled/not accepted at 19:25:54 (launcher exit 1223); elevated script never ran, so the site, Web.config and DB are unchanged. Not re-fired. Earlier note: Local IIS :8080 still runs rel1009i (old build ignores Assets.ViewAll; additive DB change is harmless to it). Publishing needs an elevated (UAC) step: not done, awaiting owner approval.
- **Live-deploy steps:** run 099 on live DB (after backup), deploy build containing `5857956`. After deploy, give Assets.ViewAll to any custom role that must see every department; give users of custom roles a department.
- **Verify:** headed Playwright, scratch users in Test-WF (org 13). Before = local IIS :8080 (rel1009i); after = IIS Express :8091 serving the branch build against the same .\SQLEXPRESS DB. Company Admin identical before/after (Assets 415 grouped rows, same nav, all pages 200). Custom Facilities Manager with department: before Assets empty, Dashboard/Notifications 403; after Assets list shows its department, Dashboard/Notifications 200. Without department: still empty (by design) and the rule now blocks creating/assigning that state ("A department is required for users with a custom role..."). Role editor shows Depreciation.View and Assets.ViewAll and saves them; after ticking Assets.ViewAll both FM users see all 415. Scratch users/role removed afterwards (AuditLog rows kept). Screenshots/results: `artifacts\roleaccess\before`, `after`, `after2`.
- **Rollback:** revert `5857956` (or redeploy previous build) and run the rollback lines in the 099 header (or restore the backup above).
## 2026-10-09 15:59 - rel1009i: QR magnification cap, deactivated-user login message, platform admin bootstrap, POs from approved requisitions (098), receiving per PO line, /Sys/Database allowlist wildcard (`9b3a9b4`, `5633f6b`, `f722a89`, `32044a3`, `ce42016`, `e565d21`, `6438394`, `83f39a6`, `d1708ec`) - PUBLISHED (rel1009i, local IIS :8080 2026-10-12 07:56:23 EAT; DatabaseConsoleAllowedIps=* set on local IIS)

- **Date/time EAT:** commits 14:0x-15:58. Package 15:59:11 (`artifacts\web-publish-rel1009i-20261009_155911`, no Web.config), built at HEAD `d1708ec` on top of rel1009h (`5b71697`). It replaces the 14:54 package (`..._145440`, Web.dll `2AD8B33E...5B2A`, deleted). **PUBLISHED_AT 2026-10-12 07:56:23 EAT** (local IIS :8080; see IIS/server steps).
  - Package DLLs: Web.dll `9EDFCD0F13E0C97AD311806CB48FD97FA1DFB4865CC8C04F531B8CE7D04E93C3`, Application `69E40FDA...0EF9`, Infrastructure `3163E78F...452F`, Domain `AC771CB6...3FB5`. Full list in `artifacts\rel1009i-expected.txt`.
  - This entry's commit is docs only, so the package was not rebuilt. The rel1009g and rel1009h files and packages are untouched.
- **What and why:**
  1. **Label Designer QR cap.** The magnification was only clamped to 1..10, so a large value printed a QR wider than the label.
     - New `LabelQrSizing.MaxMagnificationFor` (`LabelQrSizing.cs:76`): maxMag = floor((min(w,h) - 2x2 mm margin) x dots/mm / modules), clamped to 1..10.
     - modules = 17 + 4 x version, where the version comes from the worst-case scan-URL payload (byte mode, ECC M, 40-character tag).
     - At 203 dpi (8 dots/mm) with a payload up to 106 bytes (version 6, 41 modules): 50x25 caps at **x4** (164 dots = 20.5 mm) and 20x10 at **x1** (41 dots, about 5.1 mm). A longer public URL (version 7, 45 modules) caps 50x25 at x3.
     - Applied in the settings save (stored value clamped, positions re-clamped), the designer UI (input max, hint, preview) and `ZplLabelBuilder.CapForLabel` (`ZplLabelBuilder.cs:345`, used at :55 and :128), so preview and print agree.
  2. **Deactivated-user login.** Inactive users got the generic "invalid" message and were counted as failures.
     - Now, only after the password verifies, they see "This account is deactivated. Contact your administrator.". There is no failure count or lockout. A SecurityEvent and AuditLog `LOGIN_DENIED_INACTIVE` are written (entity type ApplicationUser, so the root portal is covered: fix `e565d21`).
     - A wrong password still gives the generic message and counts.
     - Tenant and root multi-portal login: `AccountController.cs:135` `TryDenyDeactivatedLogin`, plus `InactiveAccountLoginRules`.
  3. **Platform admin bootstrap.** Recovery when the platform admin cannot sign in (the email changed in the DB, MFA lost).
     - `PlatformAdminBootstrap` runs from `Global.asax.cs:33` (`RunPlatformAdminBootstrap`, defined at :98). Keys: `Bootstrap:PlatformAdminEmail`, `Bootstrap:PlatformAdminPassword`, `Bootstrap:ClearMfaEnrollment`, `Bootstrap:RunId`.
     - It finds the single platform admin by role and syncs the email (and UserName), reactivates and unlocks, and clears failed attempts and MFA rate limits. It optionally clears MFA enrolment.
     - A password is optional. When one is set, it uses the policy and hasher, sets `RequirePasswordChange=1` and LastPasswordChange, and rotates the AccessToken.
     - It writes AuditLog `PLATFORM_ADMIN_BOOTSTRAP` (no secrets), which also serves as the run-once marker (SHA256 of the key set). The run is skipped if the admin changed the password afterwards or the hash already matches.
     - The password is never logged. Everything is in one transaction wrapped in try/catch, so start-up is never blocked. The log is `App_Data\platform-admin-bootstrap.log`, and a banner shows on Platform pages while any key is present.
  4. **POs must come from an approved requisition.**
     - Before: a PO was one quantity, optionally linked to a requisition, with no limit (one PO per requisition, any quantity) and a standalone path.
     - Now Create has a requisition picker: Approved, same org, scoped to the user's department subtree and raiser. Lines are prefilled from the requisition lines (`PurchaseService.cs:112` Create, :399 BuildFromRequisition).
     - Cumulative over-ordering per requisition line is blocked on the server (`PurchaseOrderQuantityRules`, `PurchaseOrderLedger`). Forged posts are refused, and so are standalone creates.
     - The link shows on PO Details and requisition Details. Legacy POs show "Not linked" and stay viewable and receivable.
  5. **Receiving per PO line.** Old problems:
     - single-quantity POs with no lines;
     - a "Category #id" type selector with a raw subtype id box, where a posted subtype could override the context;
     - a catalog modal that reloaded the page;
     - a hidden hard-coded CompanyCustody;
     - one condition for every unit;
     - a one-at-a-time serial wizard;
     - PO status never updated;
     - no cache-busting;
     - dead code.

     Now `ReceivingService.GetReceiveOrder` (:283) and `ReceiveOrder` (:346) work per PO line:
     - category, type and subtype come from the line or requisition, read-only (receiver override only when unknown; posted values are otherwise ignored);
     - each line takes a quantity, bulk serials (count and duplicates checked), generated or entered tags, a condition (written to Asset.Condition: fix `6438394`) and a destination (store, or any department/room through AssetMoveRules);
     - partial receipts are allowed and cumulative over-receipt is blocked;
     - receipts are recorded in `PurchaseOrderLineReceipt` and audited `Purchases.Receive`, the PO gets ReceivedDate and "Received" when complete, the requisition gets "Received", and the requester is notified.

     Depends on item 4 (098 and the ledger). The rel1009h asset-request "Receive into store" is unchanged. `receive-serial-wizard.js`, `receive-catalog-match.js` and `_ReceiveSerialWizardModal` are now unused but left in place.
  6. **/Sys/Database IP allowlist wildcard** (Allan: "remove the ip restriction"). `DatabaseConsoleAllowedIps` = `*` or `any` (case-insensitive, alone or as one list entry) skips the IP check.
     - The logic is in the new pure `DatabaseConsoleIpAllowlist` (in `DatabaseConsoleController.cs`). A blank or missing value still means loopback only, and explicit lists are still enforced.
     - The Platform Admin gate and [Authorize] login/MFA are unchanged.
     - DB_CONSOLE_VIEW/DENIED/EXECUTE audit rows keep `ip=` and add `allowlist=disabled`, and the console shows a red warning banner (`DatabaseConsoleViewModel.AllowlistDisabled`, `Views/DatabaseConsole/Index.cshtml`).
- **Commit hash:**
  - `9b3a9b4` item 1, `5633f6b` item 2, `f722a89` item 3, `32044a3` item 4, `ce42016` item 5 (also covers the purge steps and FK snapshot for the new tables, and `Rel1009iTests`).
  - Fixes: `e565d21` (item 2), `6438394` (item 5), `83f39a6` (item 3). `d1708ec` item 6 (`DatabaseConsoleIpAllowlistTests`, 14 cases).
  - Tests: full suite 677 = 658 pass, the same 13 pre-existing failures as baseline `5b71697` (645 = 626/13/6), 6 skipped.
  - Each item is in its own commit, so any one can be held back; item 5 needs item 4. Local only, not pushed.
- **Files changed:** see `git diff --stat 5b71697 83f39a6`.
  - New: `098_PurchaseOrderLines.sql`; `LabelQrSizing.cs`, `PurchaseOrderQuantityRules.cs`, `PurchaseOrderLedger.cs`, `PurchaseOrderLineViewModels.cs`, `ReceiveOrderViewModels.cs` (Application); `PurchaseOrderLine.cs`, `PurchaseOrderLineReceipt.cs` (Domain); `InactiveAccountLoginRules.cs`, `PlatformAdminBootstrap.cs` (Infrastructure/Security); `Scripts/app/purchase-order-lines.js`, `Scripts/app/receive-order.js`; `tests/.../Purchases/Rel1009iTests.cs`.
- **DB migration/script:** **NEW `098_PurchaseOrderLines.sql`** (sha256 `BC9C1FA93BB7744F45404523CAE9FF8F3807CE01E5F0E0526FCE2CD11EBB2BDA`).
  - Adds the `PurchaseOrderLine` and `PurchaseOrderLineReceipt` tables. Additive and idempotent; the rollback is in its header.
  - Run after 097 and after the backup. Applied locally 14:33 EAT (SchemaMigrationHistory Id 102); a re-run was a no-op.
- **Config/Web.config keys:** item 6 uses `<add key="DatabaseConsoleAllowedIps" value="*" />` to turn the allowlist off (it replaces the existing key; apply it after the publish because it changes the Web.config hash). `C:\Users\allan\Desktop\Web.config.UPDATED` updated to `*`. No other keys are needed. The optional, temporary `Bootstrap:*` keys are covered in `artifacts\rel1009i\DEPLOY-SHEET.md` section 3, with exact lines, a plain-text-password security note and removal after the first sign-in. They change the Web.config hash while present.
- **IIS/server steps (local):** **PUBLISHED 2026-10-12.** Allan approved in chat 07:54 EAT (publish + DB console any IP). One UAC session: `artifacts\rel1009i-dbconsole-launch-elevated.ps1` -> `artifacts\rel1009i-dbconsole-wrapper.ps1`, which ran `artifacts\rel1009i-iis-publish.ps1` and then, only on success, the Web.config key change. Launcher fired once 07:56:00, UAC accepted by Allan, elevated exit 0 at 07:56:35. **PUBLISHED_AT 2026-10-12 07:56:23 EAT.** Pre-checks passed (live Web.dll `C50083D3...FA43`, Web.config `BF64D20E...44EB`, package `9EDFCD0F...93C3`). IIS backup `C:\inetpub\_backups\AssetManagement_root_pre_rel1009i_20261012_075602` (1,079 files); DB backup `AssetManagementModuleDb_pre_rel1009i_publish_20261012_075602.bak`; 097 no-op and 098 verified=1. 283 files copied, 283 hash-checked, 0 mismatches; Web.config, Views web.configs and smtp.secrets.config skipped. Live DLLs after: Web `9EDFCD0F13E0C97AD311806CB48FD97FA1DFB4865CC8C04F531B8CE7D04E93C3`, Application `69E40FDA...0EF9`, Infrastructure `3163E78F...452F`, Domain `AC771CB6...3FB5` (all = expected). **Web.config change (local IIS):** backup `C:\inetpub\_backups\Web.config.pre_dbconsole_any_20261012_075630` (`BF64D20E...44EB`); added `<add key="DatabaseConsoleAllowedIps" value="*" />` before `</appSettings>` (key was absent; 1 line added, 0 removed; XML valid; SystemEncryptionKey line unchanged); new Web.config sha256 `27426D6E91024DEC055C07EE1D54ACB35A97317BE49F7A281B4D2DA39CF7D4D3`. `smtp.secrets.config` has no DatabaseConsoleAllowedIps key (unchanged, `2F729C3F...23EF`). Smoke (no real logins): login/forgot-password pages 200 on localhost and 192.168.30.122; protected pages 302 to Login. Console proof via http://192.168.30.122:8080 with scratch users `zzi-dbc-*@rel1009i.invalid` (DB backup `AssetManagementModuleDb_pre_rel1009i_dbconsole_proof_20261012_075805.bak`; one sign-in each; MfaAllowAnyCode=true locally): anonymous -> 302 Login (AuditLog 54479 `refused reason=unauthenticated ... ip=192.168.30.122 allowlist=disabled`); scratch Platform Admin -> console 200 with the "IP allowlist is disabled" banner (AuditLog 54480 `DB_CONSOLE_VIEW allowed ... ip=192.168.30.122 allowlist=disabled`); scratch A461 Staff -> /Home/Forbidden 403 "The database console is restricted to Platform Admin (SuperAdmin)." (AuditLog 54484/54493 `refused reason=not_platform_admin ... allowlist=disabled`). Scratch users deleted (platform admins back to 1; AuditLog rows kept). Staged originally as: `artifacts\rel1009i-launch-elevated.ps1` -> `artifacts\rel1009i-iis-publish.ps1`.
  - Pre-checks: live Web.dll `C50083D34DE540EFB036E38FAA64F057C2C8452E218719374764975FB313FA43` (rel1009h), Web.config `BF64D20E...44EB`, package Web.dll `9EDFCD0F...93C3`.
  - Backs up the site to `C:\inetpub\_backups\AssetManagement_root_pre_rel1009i_<stamp>` and takes a DB COPY_ONLY backup `_pre_rel1009i_publish_<stamp>.bak`.
  - Runs and verifies 097 (no-op), then runs and verifies 098.
  - Stops the pool, copies the package (skipping Web.config and secrets), hash-verifies, starts the pool, then runs a read-only smoke test. Rollback restores the site backup.
- **Live-deploy steps:** see `artifacts\rel1009i\DEPLOY-SHEET.md`. Copy the package after rel1009h and run 098 after 097. Use the bootstrap only if the platform admin is locked out, and remove its keys right after.
- **Verify:** proven on IIS Express :8091 (package plus proof Web.config, MfaAllowAnyCode) with A461 scratch data (backups `..._pre-rel1009i-098-proof_20261009_143307.bak`, `..._pre-rel1009i-bootstrap-proof_20261009_144441.bak`, `..._pre-rel1009i-cleanup_20261009_145749.bak`):
  - QR: 50x25 with mag 10 was saved as 4 (Y re-clamped); 20x10 was saved as 1.
  - Deactivated login: the message and audit appear on tenant and root; a wrong password counts.
  - Bootstrap: applied once, re-start skipped, no secret in the log or audit. The real seed-user-platform row was backed up and restored exactly. The banner is not UI-proven.
  - POs: PO from a requisition, over-order blocked, picker hides fully-ordered requisitions, standalone refused, links shown, legacy "Not linked".
  - Receiving: partial receipts, over-receipt blocked, serials, tags, conditions and destinations stored, PO and requisition "Received".
  - rel1009h receive-into-store still works.
  - Item 6 (scratch Platform Admin and tenant admin, IIS Express :8091, backup `..._pre-rel1009i-dbconsole-proof_20261009_155955.bak`):
    - `192.0.2.1` -> 403 "restricted by IP allowlist";
    - `*` -> console 200 with banner, Execute works, audit `allowlist=disabled`; anonymous -> Login; tenant admin -> 403 Platform Admin;
    - `ANY;192.0.2.1` -> 200 with banner;
    - key removed -> loopback 200, no banner, no `allowlist=disabled`.
    - Scratch users, 14 audit rows, 5 LoginAttempts and 2 rate-limit rows removed (zero `zzj` references).
  - Scratch data removed (zero `zzi` references).
- **Rollback:** the publish script's Rollback restores the site backup. 098 is additive and the old code ignores its tables; drop script in the 098 header if needed. Remove any `Bootstrap:*` keys.

## 2026-10-09 13:41 - rel1009h: receive asset requests into store, batch pool rows, up-front Move/Keep, assign to top-level departments (RULE CHANGE), room-only returns (`2998cb7`, `a1c0581`, `1c62958`, `d08c93b`, `4af258c`, `03af7bb`) - PUBLISHED (rel1009h, local IIS :8080 2026-10-09 13:56:08 EAT)

- **Date/time EAT:** commits 13:0x-13:40; package 13:41 (`artifacts\web-publish-rel1009h-20261009_134127`, no Web.config), built at HEAD `03af7bb` on top of rel1009g (`936b0e9`). Package Web.dll `C50083D34DE540EFB036E38FAA64F057C2C8452E218719374764975FB313FA43`, Application `AEE333A5...22A1`, Infrastructure `B541FD12...5625`, Domain `3E1C85F6...66BB` (full list in `artifacts\rel1009h-expected.txt`). Later commits (rel1009g PUBLISHED changelog `774b69a`, this entry) are docs only, so the package was not rebuilt. rel1009g files and package are untouched. **PUBLISHED_AT 2026-10-09 13:56:08 EAT** (local IIS :8080; see IIS/server steps).
- **What and why:**
  1. **Receive into store (asset requests).** An approved request could only be fulfilled by assigning the asset. Now the fulfil form has "How to fulfil": Assign (as before) or Receive into store, for user and room targets. `AssetRequestService.ReceiveIntoStore` (`AssetRequestService.cs:346`) requires Approved status, segregation of duties (not the requester), and an active InStore asset with no custodian inside the request department's subtree; it marks the request Fulfilled with FulfilledAssetId, notifies, and audits `AssetRequests.ReceiveIntoStore`. No assignment is created; the asset stays in store.
  2. **Batch assign custodian.** Every row needed a custodian. Now a blank-custodian row becomes a DepartmentPool assignment (no custodian) when a room/department is chosen (`AssignmentService.cs:347`); a custodian is required only when no department is chosen.
  3. **Move/Keep before submit.** The choice used to appear only after a failed submit. Assign and Transfer forms now carry a per-department config (`BaseController.SetDepartmentDispositionConfig`, `BaseController.cs:278`) and `Scripts/app/department-disposition.js` shows the Move/Keep prompt as soon as a cross-department target is picked (Move disabled with the reason when the rule refuses it). The server-side prompt stays as a fallback.
  4. **RULE CHANGE - assignment to top-level departments.** Assigning into a top-level department (and from a top-level/store department into any department) was refused by `AssetMoveRules.IsAllowed` (rel1009g refused Admin -> Room). New `AssetMoveRules.IsAllowedAssignmentTarget` (`AssetMoveRules.cs:55`) = `IsAllowed` OR target is top-level OR source is top-level; used for assignments AND transfers (`AssignmentService.cs:467`, `TransferService.cs:619`, `AssignmentsController.cs:663`, `TransfersController.cs:284`). New refusal text `AssetMoveRules.AssignmentRefusedMessage`. Physical moves (Move to room, bulk move, asset edit) still use `IsAllowed` (unchanged). A department-pool assignment into another (sub-)department now asks Move/Keep instead of silently moving (`CrossDepartmentDisposition.IsCrossDepartmentPool/NeedsChoice`); Keep records the selected department on the assignment and leaves the asset department unchanged; Room/Class pools still always move.
  5. **Room-only returns.** "Returned By" was required and limited to the custodian's department. With no custodian it is now optional and lists any active user; a supplied value must be an active user (`ReturnService.cs`, `ReturnsController.cs:101,142,145`, `Returns/Create.cshtml:54`, `Returns/Wizard.cshtml:51`).
- **Commit hash:** `2998cb7` (item 5), `a1c0581` (item 4), `1c62958` (item 3), `d08c93b` (item 2), `4af258c` (item 1), `03af7bb` (tests: `Rel1009hTests`, 15 pass; full suite 645 = 626 pass, the same 13 pre-existing failures, 6 skipped). Local only, not pushed.
- **Files changed:** see `git show --stat` of the six commits. New: `Scripts/app/department-disposition.js` (csproj Content), `tests/AssetManagement.Tests/Assignments/Rel1009hTests.cs`.
- **DB migration/script:** none new. The publish script re-runs `097_AssetRequestTarget.sql` (sha `50B81AEE...99A4`) as an idempotent no-op after a COPY_ONLY backup.
- **Config/Web.config keys:** none.
- **IIS/server steps (local):** **PUBLISHED.** Allan approved; launcher fired once 13:55:48, UAC accepted by Allan, elevated exit 0 at 13:56:14. **PUBLISHED_AT 2026-10-09 13:56:08 EAT.** IIS backup `C:\inetpub\_backups\AssetManagement_root_pre_rel1009h_20261009_135551` (1,096 files; fewer than the rel1009g backup only because `App_Data\AspNetTemp` compile cache was smaller); DB backup `AssetManagementModuleDb_pre_rel1009h_publish_20261009_135551.bak`; 097 no-op, verified=1. 281 files copied, 281 hash-checked, 0 mismatches; Web.config, Views web.configs and smtp.secrets.config skipped and unchanged (Web.config still `BF64D20E...44EB`). Live DLLs after: Web `C50083D34DE540EFB036E38FAA64F057C2C8452E218719374764975FB313FA43`, Application `AEE333A5...22A1`, Infrastructure `B541FD12...5625`, Domain `3E1C85F6...66BB` (all = expected). Read-only smoke (no logins): 5 login/forgot-password pages 200, no YSOD; Dashboard, Assignments Create/BatchCreate, Transfers/Create, Returns/Create, AssetRequests, Users and Platform/Organizations 302 to Login. Staged as: `artifacts\rel1009h-launch-elevated.ps1` -> `artifacts\rel1009h-iis-publish.ps1`. Pre-checks: live Web.dll `C7636F842F467F545AF38DC123189C25A2303DD92A0FDA4161ABCABAC62177F1` (rel1009g), Web.config `BF64D20E...44EB`, package Web.dll `C50083D3...FA43`. Backs up the site to `C:\inetpub\_backups\AssetManagement_root_pre_rel1009h_<stamp>`, takes a DB COPY_ONLY backup `AssetManagementModuleDb_pre_rel1009h_publish_<stamp>.bak`, re-runs/verifies 097, stops the pool, copies the package skipping Web.config and secrets, hash-verifies every file, starts the pool, read-only login smoke on :8080; Rollback restores the site backup.
- **Live-deploy steps:** copy the package (never Web.config or secrets) after rel1009g; no SQL beyond 097 (already required by rel1009g).
- **Verify:** proven on IIS Express :8091 with Test-WF A461 scratch data (backup `AssetManagementModuleDb_pre-rel1009h-proof_20261009_134202.bak`): receive into store for a user-target and a room-target request (status Fulfilled, asset unchanged InStore, no assignment; requester refused; out-of-subtree asset refused); batch of 2 with room and no custodians -> 2 DepartmentPool rows; Move/Keep prompt shown before submit (jsdom); top-level TEST and Admin -> Meet Room assignments succeed; sub-department pool Keep leaves the department unchanged; room-only return with blank or any-user Returned By. Scratch data removed (zero `zzh` references; all 9 touched assets restored exactly; cleanup backup taken first).
- **Rollback:** the publish script's Rollback (restore `AssetManagement_root_pre_rel1009h_<stamp>`); no DB rollback needed (no schema change).

## 2026-10-09 13:05 - rel1009g: audit logs, label designer, room-targeted asset requests (097), Users create role dropdown, user deactivate/delete, ExternalBaseUrl URL builders (`cd9be86`, `c7cdb2a`, `1db44de`) - PUBLISHED (rel1009g, local IIS :8080 2026-10-09 13:43:18 EAT; supersedes rel1009f)

- **Date/time EAT:** commits 12:52, 12:55 and 13:04; package 13:05 (`artifacts\web-publish-rel1009g-20261009_130524`, no Web.config). Package Web.dll `C7636F842F467F545AF38DC123189C25A2303DD92A0FDA4161ABCABAC62177F1`, Application `C1CF25FF...3F3F`, Infrastructure `651004EC...A3CB`, Domain `50D751AC...598B` (all values in `artifacts\rel1009g-expected.txt`). Includes rel1009f (`c902bc7`); the rel1009f build, publish, launcher and expected files were renamed `*.SUPERSEDED`. **PUBLISHED_AT 2026-10-09 13:43:18 EAT** (local IIS :8080; see IIS/server steps).
- **What and why:**
  1. **Audit logs empty.** Root cause: MVC binds the route value `{action}` ("Index"/"Export") into `AuditLogFilterVm.Action` (`DashboardViewModels.cs:83`), so every query filtered on `Action = 'Index'`. Fix: the filter uses `ActionCode` (or a legacy `?Action=` query value) via `AuditLogFilterHelper.ResolveActionFilter`. SQL paging (50 per page, 10-200), filters, actor names, and sign-in rows (`Account.LoginSucceeded/LoginFailed`). Audit writes are synchronous unless `AuditWritesViaOutbox=true`.
  2. **Label designer.** QR size and position, drag bounds and resize used the scaled rectangle and size-blind clamps. Fix: `LabelLayoutDesignHelper` uses the real QR footprint from QrMagnification (`GetElementFootprintMm`) with NaN-safe clamps; layouts are normalised on load and save; CSS/JS fixes; the script is cache-busted. Stored layouts were already in bounds, so no data SQL.
  3. **Asset requests.** Deliver to me or to a room/class (`TargetKind`/`TargetDepartmentId`, migration 097), limited to the request department's subtree. A room target is fulfilled like Assignments: to the person in charge (class teacher / room custodian) or the room pool. Full-depth "Parent > Child" department picker (Asset Requests and the requisition list). InStore assets of descendant departments are offered, labelled 'tag - serial - name (Dept)'. The category cache resets on change/import. The Move/Keep rules are unchanged (b21ac7f). The fulfil form defaults to Keep, and Move/Keep is hidden for a room pool (a pool always moves the asset into the room, so AssetMoveRules applies: Administrative -> Room is refused, as on Assignments).
  4. **Users > Create.** "Position Title" was a separate free-text job title (`Users.PositionTitle`, distinct from `RoleId`); the screen had no role or department. Now Department + "Role / position" dropdowns: the org's active, grantable roles with the Department -> Role cascade, validated server-side with the same rules as Invite (shared `GrantableRoleResolver`). PositionTitle is set from the role name; existing values are still shown. Invite and Platform create already used validated role dropdowns (unchanged). Field validation messages were added (EmployeeNumber/Email errors used to be invisible).
  5. **Users deactivate/reactivate/delete** (Users.Edit / Users.Delete; no new permission). Deactivate sets IsActive=0 and rotates AccessToken, which ends existing sessions (`UserAccountRepository.ValidateAccessToken`, `TenantFilterAttribute`). Login already refused inactive users (`MembershipService`). Inactive named stage approvers fall back to the role, and the outbox skips inactive recipients. Hard delete only without linked records (schema-discovered *UserId/*ById/*CustodianId columns, audit log, sign-ins); otherwise blocked with counts and a "deactivate instead" message. No self-action; never the last active Company Admin; only Company Admins act on a Company Admin. Every action is audited.
  6. **URL builders.** Every absolute URL uses ExternalBaseUrl (+ app path when host-only), or request + app path when the configured base is loopback and the request is not (`AssetScanUrlHelper.ResolveAbsoluteBase`). This covers QR/scan, invite, reset, the portal URL on Platform Organizations, security file links, BaseController and the label designer sample (the hard-coded `/nanosoft` fallback is removed). `http://localhost` fallbacks log a TraceWarning. Platform settings save warns on a localhost URL, a wrong or missing app path, or an invalid URL (`ExternalBaseUrlValidator`). PrePublish SQL spelling: `/NanoAssetSuite`.
- **Commit hash:** `cd9be86` (main), `c7cdb2a` (Asset Requests tree picker), `1db44de` (Users/Create field messages; fulfil Keep default and pool hint). Local only, not pushed.
- **Files changed:** 65 files in `cd9be86` (see `git show --stat cd9be86`). New: `Application/Helpers/{AuditLogFilterHelper,DepartmentTreeHelper,ExternalBaseUrlValidator,UserLifecycleRules}.cs`, `Application/Contracts/IUserLifecycleStore.cs`, `Infrastructure/Identity/UserLifecycleRepository.cs`, `Infrastructure/Queries/AssetRequestTargetRepository.cs`, `Web/Helpers/GrantableRoleResolver.cs`, `tests/.../Helpers/Rel1009gTests.cs`, migration 097.
- **DB migration/script:** `database/scripts/004_Migrations/097_AssetRequestTarget.sql` (additive and idempotent; rollback in the header). **Applied locally** 2026-10-09 12:36 after a COPY_ONLY backup (`AssetManagementModuleDb_pre_097_rel1009g_20261009_123553.bak`); `SchemaMigrationHistory` Id 101; the 9 existing requests are TargetKind 0. The code tolerates the columns being absent.
- **Config/Web.config keys:** none required. Optional `AuditWritesViaOutbox=true` only where an outbox worker runs.
- **IIS/server steps (local):** **PUBLISHED.** Allan approved; launcher `artifacts\rel1009g-launch-elevated.ps1` fired once 13:42:57, UAC accepted by Allan, elevated exit 0 at 13:43:26. **PUBLISHED_AT 2026-10-09 13:43:18 EAT.** Pre-checks passed (live Web.dll `E15FBEDF...50FC`, Web.config `BF64D20E...44EB`, package `C7636F84...77F1`). IIS backup `C:\inetpub\_backups\AssetManagement_root_pre_rel1009g_20261009_134300` (1,668 files); DB backup `AssetManagementModuleDb_pre_rel1009g_publish_20261009_134300.bak`; 097 re-run as a no-op, verified=1. 280 files copied, 280 hash-checked, 0 mismatches; Web.config, Views web.configs and smtp.secrets.config skipped and unchanged. Live Web.dll after: `C7636F842F467F545AF38DC123189C25A2303DD92A0FDA4161ABCABAC62177F1` (Application `C1CF25FF...3F3F`, Infrastructure `651004EC...A3CB`, Domain `50D751AC...598B`); Web.config still `BF64D20E...44EB`. Read-only smoke (no logins): `/Account/Login`, `/A46138179/Account/Login`, `/L35160674/Account/Login`, `/Account/ForgotPassword` 200 with no YSOD; Dashboard, AssetRequests Index/Create, AuditLogs, Settings/LabelDesigner, Users Index/Create and Platform/Organizations 302 to Login. The script:
  - pre-checks live Web.dll `E15FBEDF...50FC` (rel1009e), Web.config `BF64D20E...44EB` and package Web.dll `C7636F84...77F1`;
  - backs up the site to `C:\inetpub\_backups\AssetManagement_root_pre_rel1009g_<stamp>`;
  - takes a DB COPY_ONLY backup, re-runs 097 (no-op here) and verifies it;
  - copies the package, skipping Web.config and secrets, and hash-verifies every file;
  - starts the pool, runs a login smoke test, and rolls back automatically on failure.
- **Live-deploy steps:** back up the DB, run 097, then deploy the web files (never Web.config). Check ExternalBaseUrl in Platform settings (the new save warning flags localhost or a missing `/NanoAssetSuite`).
- **Verify:** 46 new unit tests (URL helpers: subfolder app, org code, localhost config + public request, host-only + app path, validator; requisition tree; lifecycle rules + UserService; audit filters; label clamp). Scratch run of the test project (the stale files that don't compile were excluded, same set at HEAD): 611 pass / 13 fail; the identical 13 fail at `77d6090`.
  - **Proof on IIS Express 8091** (MfaAllowAnyCode proof config, A461 only, scratch users `zzg-*@rel1009g.invalid`):
    - audit 3,330 rows, 50 per page, page 2, sign-in filter;
    - label designer sample URL;
    - Users/Create dropdowns: an ungrantable role is refused; PositionTitle = "Staff";
    - deactivate: the same session is sent to Login and a fresh login is refused; reactivate: login OK;
    - delete is blocked with "2 sign-in records"; a clean user is deleted; self-deactivate/delete are blocked; all actions audited;
    - room request: room outside the subtree refused; pool fulfil room->room (assignment type 3, asset moved); PIC fulfil + Keep (custodian set, department kept).
  - All scratch data was removed and the inventory is clean; Department 270 custodian is back to NULL. DB backup before the proof: `AssetManagementModuleDb_pre_proof_rel1009g_20261009_130009.bak`.
- **Rollback:** `git revert 1db44de c7cdb2a cd9be86`; after publish, restore the `pre_rel1009g` IIS backup. 097 rollback is in its header (only needed if the columns must go; the code works without them).

---
## 2026-10-09 08:25 - Approver picker no longer auto-selects the only role holder (`c902bc7`) - STAGED (rel1009f, not published)

- **Date/time EAT:** 2026-10-09 08:23 (commit), package 08:25 (`artifacts\web-publish-rel1009f-20261009_082459`). Package Web.dll `2D424F39D8F1D588D1F648C02D70472C7462F13EB546E638A79E6522DF72ECA5` (Application `929787DF...274D`), `Scripts\app\approval-stage-person.js` `075574C1...98D1`. **Not published:** the UAC launcher was not fired.
- **What and why:** QA after rel1009e: when a stage's role is Department Head (or any role), choosing the role auto-selected the named approver if exactly one active user held the role (e.g. "Demo DeptHead"). Saving then stored a named person, and a named person overrides the department-scoped HOD routing from rel1009e. The auto-select was client-side only (`approval-stage-person.js`, added in `4cd0978` on 2026-10-08); the server never filled a person. Fix: a role change always resets the person to "Any user in role" (stages and "Purchase order raised by"); only an explicit pick saves a person. Saved persons still load as before. `StageApproverRules.ResolveAutoSelectUserId` (the C# mirror) now always returns null.
- **Existing data (READ-ONLY check, all orgs incl. NIS, no changes):** `DepartmentApprovalStage`: 219 active rows, **0** with a named UserId. Named persons are only stored in the Department CSV columns, on one department: org 13 Test-WF, `ZZ-QA1-ROOM` (Id 1618, parent 1616), `CustomStageUserIds` = `RequisitionApprovalStageUserIds` = `a461-role-depthead` (Demo DeptHead), `a461-role-facilities`, `a461-role-finance`, `a461-role-procman`, plus `PurchaseOrderRaiserUserId` `a461-role-procoff`. **Each is the sole active holder of its role** in org 13. Created 2026-10-08 21:07 and edited 21:10 and 21:28 by `e2e-admin-A46138179` (QA automation), after the auto-select shipped. The audit rows (`Departments.Create`/`Departments.Edit`) record only the name, so they neither prove nor rule out auto-fill. No org `Approval.Process.*.StageUserIds` setting has a person. No data changed.
- **Commit hash:** `c902bc7`.
- **Files changed:** `Web/Scripts/app/approval-stage-person.js`, `Application/Services/Approvals/StageApproverRules.cs`, `tests/.../Requisitions/StageApproverAndPurchaseOrderRaiserTests.cs` (auto-select test now expects none; new test: blank stays blank, explicit pick saved).
- **DB migration/script:** None. **Config/Web.config keys:** None.
- **IIS/server steps (local):** STAGED, not fired: `artifacts\rel1009f-launch-elevated.ps1` -> `artifacts\rel1009f-iis-publish.ps1`. Pre-check live Web.dll `E15FBEDF...50FC` (rel1009e) and Web.config `BF64D20E...44EB`, package Web.dll `2D424F39...ECA5`; backup `C:\inetpub\_backups\AssetManagement_root_pre_rel1009f_<stamp>`; skips Web.config/secrets; hash-verifies; recycles; login smoke; auto-rollback. Expected values in `artifacts\rel1009f-expected.txt`.
- **Live-deploy steps:** web files only. Optionally, with approval and a backup, clear named persons that were auto-filled (check with the SELECT in this entry).
- **Verify:** unit tests 47/48 in `artifacts\rel1009e\tests` (only the known `AllowsChildTarget_UnderHomeDepartmentBranch` failure). Browser check of the old vs new script with the picker markup (`artifacts\rel1009e\f1-js-proof.txt`): old, choosing Department Head with one holder -> "Demo DeptHead"; new -> "Any user in role". Explicit pick and Edit load of a saved person are unchanged. After publish: Departments > Edit > requisition stage: choose Department Head; the person stays "Any user in role".
- **Rollback:** `git revert c902bc7`; after publish, restore the `pre_rel1009f` IIS backup.

---
## 2026-10-09 07:45 - Several Department Heads per department; stage approvers and pending-approval notifications scoped to the request's department (`80864d1`, tests `4ec86ef`) - PUBLISHED (local IIS, rel1009e, 2026-10-09 07:52:03 EAT)

- **Date/time EAT:** 2026-10-09 07:45 (commit `80864d1`), package 07:48 (`artifacts\web-publish-rel1009e-20261009_074745`). UAC pre-approved by Allan; launcher fired once 07:49:40, UAC accepted by Allan 07:54:25; **PUBLISHED_AT 2026-10-09 07:52:03 EAT**. Live Web.dll after `E15FBEDFD1D093A2B1E5E8FCB0ADA36ABA98F262EC9C08870D2A99B3D88450FC` (Application.dll `D821129F...3D9B`); Web.config `BF64D20E...44EB` and smtp.secrets.config unchanged; 280 files copied and hash-verified (0 mismatches); backup `C:\inetpub\_backups\AssetManagement_root_pre_rel1009e_20261009_074945` (871 files). Log `artifacts\rel1009e-iis-publish.log`.
- **What and why:** Allan: allow more than one Head of Department (HOD) per department, and send HOD notifications and approvals to the request's department only. (1) The one-HOD-per-department guard was removed (`UserService.EnsureDepartmentHeadAssignment` and its 2 callers); the Users/Details hint now says a department can have more than one Department Head. (2) New shared `StageApproverScopeResolver` (`Application/Services/Approvals/StageApproverScopeResolver.cs`), used by both the notifications and the approve/reject eligibility check. **Rule:** named person on the stage (unchanged) > active holders of the stage role in the request's department > nearest ancestor department (ParentDepartmentId chain, depth 32, cycle-safe) that has holders > holders with **no department** (org-wide approvers such as Procurement or Company Admin) > all holders in the organization. A request with no department keeps the old org-wide routing. Department used: requisition/PO = PurchaseRequest.DepartmentId; transfer = FromDepartmentId, else the asset's department; disposal = the asset's department; asset request = AssetRequest.DepartmentId. "First decision wins" is unchanged: conditional update plus the unique stage index; the second approver gets "no longer pending".
- **Included (scoped):** requisition submit and next-stage notifications and approve/reject (`PurchaseRequestService`); "Ready for purchase order" notifications and PO-raise permission (`PurchaseOrderRaiserRules.ResolveRaiserScope`, `PurchaseService`, `PurchaseRequestsController`, `PurchasesController`); transfer submit and next-stage notifications and approve/reject (`TransferService`); disposal request and next-stage notifications and approve/reject (`AssetService`); asset-request "pending" notification to Asset Managers (`AssetRequestService`); approval buttons on Asset Details (`AssetsController`); pending inbox (`PendingApprovalQueryService` adds ancestor/no-department rows the user can act on; the transfer row department is `COALESCE(FromDepartmentId, asset department)`); requisition Details access for a resolved approver from a parent department (`IsCurrentStageApprover`).
- **Still org-wide / not scoped:** asset-request **approval** is still permission-based (Assets.Request.Approve), not stage-scoped. `CountGlobalPending` (the global badge count) is not extended to ancestor-fallback items. An HOD with **no department** is the no-department fallback for HOD stages in every department whose chain has no HOD: Test-WF has one (`a461-role-depthead`, depthead.a46138179@asset.local, DepartmentId NULL), so it gets HOD stages for departments with no HOD in their chain. A role holder assigned to a department only gets that department chain's requests while a no-department holder of the same role exists.
- **Commit hash:** `80864d1` (code), `4ec86ef` (tests).
- **Files changed:** Application: `Services/Approvals/StageApproverScopeResolver.cs` (new), `ApprovalWorkflowHelper.cs`, `ApprovalWorkflowEngine.cs`, `PurchaseOrderRaiserRules.cs`, `Contracts/Approvals/IApprovalWorkflowEngine.cs`, `Services/Common/NotificationHelper.cs`, `Services/Assets/AssetService.cs`, `Services/Assets/AssetRequestService.cs`, `Services/Purchases/PurchaseRequestService.cs`, `Services/Purchases/PurchaseService.cs`, `Services/Transfers/TransferService.cs`, `Services/PendingApprovalQueryService.cs`; Infrastructure: `Queries/PendingApprovalQueryRepository.cs`, `Services/UserService.cs`; Web: `Controllers/AssetsController.cs`, `PurchaseRequestsController.cs`, `PurchasesController.cs`, `Views/Users/Details.cshtml`; tests: `tests/AssetManagement.Tests/Approvals/StageApproverScopeTests.cs` (new, 13 tests).
- **DB migration/script:** None. **Config/Web.config keys:** None.
- **IIS/server steps (local):** `artifacts\rel1009e-launch-elevated.ps1` -> `artifacts\rel1009e-iis-publish.ps1`: pre-check live Web.dll `9E5D79F8...F2E5` (rel1009d) and Web.config `BF64D20E...44EB`, package Web.dll `E15FBEDF...50FC`; backup; skips Web.config/secrets; hash-verifies; recycles; login smoke; auto-rollback. Expected values in `artifacts\rel1009e-expected.txt`.
- **Live-deploy steps:** web files only (`LiveKit_80864d1`). After deploy, check each organization for HODs with no department: they become the HOD fallback for departments with no HOD. Give them a department, or leave them as the intended org-wide fallback.
- **Verify:** unit tests: 46 of 47 pass in the scratch runner `artifacts\rel1009e\tests` (StageApproverScopeTests 13/13, plus the existing StageApproverAndPurchaseOrderRaiser, PurchaseRequestDepartmentFlow, DepartmentHeadAssignmentRules and ConcurrencyWorkflow tests). **Known test failures (not caused by this change):** `PurchaseRequestDepartmentFlowTests.PurchaseRequestService_Submit_AllowsChildTarget_UnderHomeDepartmentBranch` fails the same way at `afa596d` (test fake `StrictDepartmentScopeService` refuses child departments). `ApprovalWorkflowSelfApproveTests` does not compile at HEAD (it calls `AllowsEligibleSelfApproval*`, which is not in src). Test-WF A461 DB-backed proof on IIS Express :8091 (rel1009e package, MfaAllowAnyCode proof config, scratch `.invalid` users, no real emails): requisition in a two-HOD department notified exactly the two HODs; the other-department HOD got "You are not configured to approve this stage..."; the first HOD approved and the second got "This purchase request is no longer pending approval."; a requisition in a child unit with no HOD notified the parent's two HODs, the parent HOD could open and approve it, and the other HOD could not. Transfers: same results, plus the other-department HOD got "This asset belongs to another department." Scratch data was removed after a copy-only backup `AssetManagementModuleDb_pre_rel1009e_proof_20261009_080410.bak`. :8080 smoke: 3 login URLs 200, no error page; Requisitions, Approvals and Users redirect to login (no login, no MFA attempts).
- **Rollback:** `git revert 4ec86ef 80864d1`; after publish, stop pool AssetManagement, robocopy `C:\inetpub\_backups\AssetManagement_root_pre_rel1009e_20261009_074945` back excluding Web.config and smtp.secrets.config, and start the pool (live Web.dll returns to `9E5D79F8...F2E5`).

---
## 2026-10-09 05:40 - Roles: saving the permission matrix no longer wipes permissions it does not show (`9126a2d`) - PUBLISHED (local IIS, rel1009d, 2026-10-09 07:21:40 EAT)

- **Date/time EAT:** 2026-10-09 05:27 (commit), package 05:30. Allan approved 07:20; launcher fired 07:21:19, UAC accepted by Allan 07:21:48; **PUBLISHED_AT 2026-10-09 07:21:40 EAT**. Live Web.dll after `9E5D79F8516C039B77F7686AC6A0A181FB8C60EECEFE1D10D865661D8D58F2E5` (Application.dll `4A4328B3...3DF2`); Web.config `BF64D20E...44EB` and smtp.secrets.config unchanged; 280 files copied and hash-verified (0 mismatches); backup `C:\inetpub\_backups\AssetManagement_root_pre_rel1009d_20261009_072122` (1277 files); script smoke: 3 login URLs 200, no error page. :8080 role-save UI smoke: login page 200; the scripted role save was not run (test-login password not available to the agent in this session), so the role-save behaviour on :8080 is proven by the identical package on IIS Express :8092 only. Log `artifacts\rel1009d-iis-publish.log`.
- **What and why:** QA (rel1009c, Test-WF A461): saving Roles > Edit for Auditor (role 65) dropped 3 `Api.Assets.Read` and 61 `Depreciation.View`. Root cause: `RoleService.SetPermissions` (`Application/Services/Security/RoleService.cs`, old lines 255-270) deleted **every** RolePermission of the role and re-inserted only the ticked boxes, but the matrix never renders the Api and Depreciation modules (`PermissionService.GetAll` hides them), so those rows were always lost. Existed since the first commit (not a rel1009c regression). Fix: the save computes the rendered matrix set server-side (`RoleService.GetMatrixPermissionIds`, the same ModulePermissionService/PermissionService list the view renders, never a client list), adds/removes only inside it, keeps every RolePermission outside it, and ignores posted ids outside it. The "can't grant what you don't hold" check is unchanged. Audit `Permissions.Assign` now records `added=`, `removed=`, `keptOutsideMatrix=` and `ignoredOutsideMatrix=`.
- **Other role-save paths checked:** Role Create (same `SetPermissions`, fixed by the same change; new role, nothing to wipe), Role Active toggle and Department scope (go through Edit / a single-column update, no RolePermission replace), Save-as-template and Create-from-template/copy (read-only copy into checkboxes; hidden permissions of the source are not carried to the new role, nothing deleted), organization provisioning (`OrganizationService` template copy and Company Admin, insert-only), org purge (deletes the whole org by design), roleperm retag scripts (update OrganizationId only). No API or bulk role/permission screen writes RolePermission. Only `SetPermissions` replaced the set.
- **Commit hash:** `9126a2d`.
- **Files changed:** `Application/Services/Security/RoleService.cs`, `Application/Contracts/Security/IRoleService.cs`, `tests/AssetManagement.Tests/Security/RolePermissionMatrixSaveTests.cs` (new, 6 tests, pass via a scratch runner; the full test project has pre-existing compile errors in 3 unrelated files).
- **DB migration/script:** None. No data fix (damage report only: Test-WF Auditor 65 lost 3 and 61 at 05:19:26 EAT; see `artifacts\roleperm-damage-2026-10-09\`). **Config/Web.config keys:** None.
- **IIS/server steps (local):** `artifacts\rel1009d-launch-elevated.ps1` -> `artifacts\rel1009d-iis-publish.ps1`: pre-check live Web.dll `5BAFFC3B...336C` (rel1009c) and Web.config `BF64D20E...44EB`, package Web.dll `9E5D79F8516C039B77F7686AC6A0A181FB8C60EECEFE1D10D865661D8D58F2E5`; backup `C:\inetpub\_backups\AssetManagement_root_pre_rel1009d_<stamp>`; skips Web.config/secrets; hash-verifies every file; recycles; login smoke; auto-rollback. Expected values in `artifacts\rel1009d-expected.txt`.
- **Live-deploy steps:** web files only. After deploy, re-grant any lost hidden permissions only where the role is meant to have them (e.g. Depreciation.View for Auditor), by SQL with a backup, since the UI cannot show them.
- **Verify:** IIS Express :8092 (Release package, Test-WF A461), scratch role "ZZ RolePerm Proof 1009" with 39,65 + hidden 3,61: Edit renders 66 boxes, none of 3/4/5/61/62; save with no change -> 3,39,61,65 kept; tick 67 + untick 65 -> 3,39,61,67; forged POST adding 62 + 4 -> 302, not granted (`ignoredOutsideMatrix=4,62`). Scratch role removed. Output `artifacts\roleperm-proof-2026-10-09\`.
- **Rollback:** `git revert 9126a2d`; after publish restore the `pre_rel1009d` IIS backup.

---
## 2026-10-09 05:07 - ROOT local IIS publish rel1009c (HEAD `7ef01fb`): Requisitions Order-by removal + department Move/Keep prompt + invite role + invite department / role departments

- **Date/time EAT:** Allan said "Send" at 05:06; launcher fired 05:06:56; UAC accepted by Allan; **PUBLISHED_AT 2026-10-09 05:07:25 EAT**. Local only: not pushed, no PR, not live.
- **What and why:** ships the entries below (`59ef566`, `b21ac7f`, `058b62a`, `ba95dd0`, `214b7cd`, `d3e21a1`).
- **Commit hash:** package built from `7ef01fb`.
- **Files changed:** 280 package files copied (Web.config, view web.configs and secrets skipped); all 280 hash-verified, 0 mismatches.
- **DB migration/script:** `096_RoleDepartmentScope.sql` was already applied locally at 01:05 (history Id 100); 065 history row Id 99. Nothing run during the publish.
- **Config/Web.config keys:** None. Web.config `BF64D20EF695EED2753522021CEC33EE5A71AB43622499C82A4355E3703444EB` before and after; smtp.secrets.config unchanged.
- **IIS/server steps (local):** pre-checks passed (live Web.dll `FF9B1237...D585`, package `5BAFFC3B...336C`, Web.config `BF64D20E...44EB`); backup `C:\inetpub\_backups\AssetManagement_root_pre_rel1009c_20261009_050708` (897 files); pool AssetManagement stopped, copied, started. Live Web.dll after: `5BAFFC3BE83B283281B2B47DCE82D814468A3E5F9FE6D372403ECCE6B539336C`. Logs `artifacts\rel1009c-iis-publish.log`, `artifacts\rel1009c-iis-launcher.log`.
- **Live-deploy steps:** see the new live kit `C:\Users\allan\Desktop\NanoAssetSuite_LiveKit_20261009_7ef01fb\`.
- **Verify:** script smoke: 3 login URLs 200, no error page. Read-only Playwright smoke on :8080 (`artifacts\rel1009c-published\`): Staff gets the friendly 403 on My Assets; Requisitions list and New requisition 200 with no "Order by"; Invite user 200, Department before Role, role list reloads on department change (no department: Company Admin only; Science / Counselling: 9 roles); Roles list 200 with the Department column; Batch assign 200 with Keep/Move; Transfer 200; Departments 200. No test data created.
- **Rollback:** stop pool AssetManagement, robocopy `C:\inetpub\_backups\AssetManagement_root_pre_rel1009c_20261009_050708` back over `C:\inetpub\AssetManagement` excluding Web.config and smtp.secrets.config, start the pool (live Web.dll returns to `FF9B1237...D585`). 096 can stay (code before it ignores the column) or be rolled back per its header.

---
## 2026-10-09 01:20 - Invite: cascading Department -> Role picker + role departments (`214b7cd`, `d3e21a1`, migration 096) - PUBLISHED (local IIS, rel1009c, 2026-10-09 05:07:25 EAT)

- **Date/time EAT:** 2026-10-09 01:20 (commit). **PUBLISHED 2026-10-09 05:07:25 EAT** to local IIS via `artifacts\rel1009c-launch-elevated.ps1` (UAC accepted by Allan), live Web.dll `5BAFFC3BE83B283281B2B47DCE82D814468A3E5F9FE6D372403ECCE6B539336C`. Expected package Web.dll SHA256 in `artifacts\rel1009c-expected.txt`.
- **What and why:** Allan: pick the invitee's **Department** next to the role (e.g. Department Head + Science), department first, then a Role list that shows only roles that apply in that department. Roles had no department link, so roles get an optional department: `Roles.DepartmentId` NULL = organization-wide (offered for every department), a value = offered only for that department. Every existing role stays NULL, so nothing changes until an admin ties a role to a department (new **Department** dropdown on Role Create/Edit, default "All departments (organization-wide)"; a **Department** badge column on the Roles list; system roles always stay organization-wide).
- **Invite rules (server-side on POST and on the AJAX endpoint `UserInvitations/RolesForDepartment`):** the department must be an active department of this organization **within the inviter's existing department scope** (Company Admin: all; department-scoped users: their own department, plus class departments if they hold Assets.Transfer, same as the rest of the app); the role must be active, grantable (inviter already holds all its permissions) and apply in the chosen department. Out-of-scope / other-organization departments and mismatched role+department combinations are refused.
- **Required vs optional (decision):** Department is **required** on new invites when the organization has departments, **except for Company Admin** (the only role the app does not limit to a department: every other role sees nothing without a department). With no department picked, only Company Admin is offered. Organizations with no departments yet can invite without one.
- **Registration:** the stored department is applied (posted values ignored, like the role). If it no longer exists / is inactive / is another organization's, the account is created with no department and `Users.InviteDepartmentFallback` is audited. Old invitations without a department keep the old default (no department).
- **Commit hash:** `214b7cd` (feature), `d3e21a1` (Role Create/Edit view fix).
- **Files changed:** `database/scripts/004_Migrations/096_RoleDepartmentScope.sql` (new), `Infrastructure/Identity/RoleDepartmentScopeRepository.cs` (new), `Infrastructure/Services/UserInvitationService.cs`, `Application/ViewModels/Security/SecurityViewModels.cs`, `Web/Controllers/UserInvitationsController.cs`, `Web/Controllers/RolesController.cs`, `Web/Views/UserInvitations/Create.cshtml`, `Web/Views/Roles/Create.cshtml`, `Web/Views/Roles/Edit.cshtml`, `Web/Views/Roles/Index.cshtml`, `Web/Scripts/app/invite-create.js` (new), `Web/AssetManagement.Web.csproj`.
- **DB migration/script:** `096_RoleDepartmentScope.sql` (additive: nullable `Roles.DepartmentId` + `FK_Roles_Department`; idempotent; rollback section in the header). The code tolerates the column being absent (all roles org-wide, picker hidden on Roles), so order is free. 095 is intentionally unused. Local: COPY_ONLY + CHECKSUM backup (VERIFYONLY valid) `C:\Program Files\Microsoft SQL Server\MSSQL15.SQLEXPRESS\MSSQL\Backup\AssetMgmt_pre_096_roledept_<stamp>.bak` (exact path in `artifacts\invitedept-2026-10-09\backup-path.txt`), applied twice (idempotent), `SchemaMigrationHistory` Id 100. `UserInvitation.DepartmentId` already existed (069), no change. **Config/Web.config keys:** None.
- **IIS/server steps (local):** `artifacts\rel1009c-launch-elevated.ps1` (pre-checks live Web.dll `FF9B1237...D585` and Web.config `BF64D20E...44EB`; backup `C:\inetpub\_backups\AssetManagement_root_pre_rel1009c_<stamp>`; skips Web.config/secrets; smoke + auto-rollback); run 2026-10-09 05:07 EAT, exit 0. Ships everything since rel1009 (Order-by removal, dept Move/Keep prompt, invite role, invite department).
- **Live-deploy steps:** run 096 on live (any order), then the web files; live kit to be refreshed after the local publish.
- **Verify:** IIS Express :8092, Test-WF A461: role "ZZ Science Role" tied to Science shows the Science badge, org-wide role shows "All departments"; picking Science lists it ("this department only") plus every org-wide role, picking Counselling drops it (AJAX reload); no department lists only Company Admin; other-org department -> 403/refused; Counselling + Science role -> refused; no department + Staff -> refused; registration (tampered RoleId=59 + DepartmentId posted) -> stored role + Science; invalid stored department -> no department + `Users.InviteDepartmentFallback`; old no-department invite -> no department; department-scoped inviter sees only Science and is refused for Counselling. Output `artifacts\invitedept-2026-10-09\` (proof.txt, proof-i7.txt, 01_db_after_proofs.txt, 02_cleanup.txt, screenshots). Test roles/users/invites removed.
- **Rollback:** `git revert d3e21a1 214b7cd`; after publish restore the `pre_rel1009c` IIS backup; DB: rollback section of 096 (or restore the backup above).

---
## 2026-10-09 01:00 - Department Move/Keep prompt (`b21ac7f`) + invite role locked in (`058b62a`, `ba95dd0`) + 065 history row - PUBLISHED (local IIS, rel1009c, 2026-10-09 05:07:25 EAT)

- **Date/time EAT:** 2026-10-09 01:00 (commit). **PUBLISHED 2026-10-09 05:07:25 EAT** to local IIS via `artifacts\rel1009c-launch-elevated.ps1` (UAC accepted by Allan), live Web.dll `5BAFFC3BE83B283281B2B47DCE82D814468A3E5F9FE6D372403ECCE6B539336C`. Expected package Web.dll SHA256 is recorded in `artifacts\rel1009c-expected.txt` (the rel1009b package is superseded).
- **What and why (F, department prompt):** Allan: when an asset is transferred/assigned to a user in a different department, ask **Move (to B)** or **Keep (A)**; no prompt for the same department. Server side: only an explicit **Move** changes `Asset.DepartmentId`; Keep or no choice keeps it (the old "choice required" throw is removed). Covers single assign, transfer request and approval (choice captured at request, re-checked and honoured at approval) and bulk assign (new Keep (default) / Move radio). A Move writes the AssetMovement row + custody event + audit. If B is not an allowed destination under `AssetMoveRules` (e.g. class asset to a non-room), the prompt shows Move disabled with the rule message and the server refuses a forced Move. There is no bulk transfer screen in the app.
- **What and why (H, invite role):** the role is now required on Invite user, chosen from the org's **active tenant roles** only (never Platform roles, never hardcoded) and limited to roles whose permissions the inviter already holds (escalation guard in the dropdown and on POST). The `RoleId` is stored on the invitation; registration always uses the stored role and ignores any posted role; if the stored role is no longer active/in the org the user gets no role (the old default) and `Users.InviteRoleFallback` is audited. Old invitations without a role keep the old default (no role).
- **What and why (G):** recorded `065_DepartmentRequisitionApproval.sql` in `SchemaMigrationHistory` (Id 99) so history-checking tooling sees it as applied (the schema was already present).
- **Commit hash:** `b21ac7f` (F), `058b62a` + `ba95dd0` (H).
- **Files changed:** F: `Application/Helpers/CrossDepartmentDisposition.cs`, `Application/Services/Assignments/AssignmentService.cs`, `Application/Services/Transfers/TransferService.cs`, `Application/ViewModels/Assignments/BatchAssignViewModels.cs`, `Web/Controllers/AssignmentsController.cs`, `Web/Controllers/TransfersController.cs`, `Web/Views/Assignments/BatchCreate.cshtml`, `Web/Views/Shared/_DepartmentDispositionPrompt.cshtml`. H: `Infrastructure/Identity/UserInvitationRepository.cs`, `Infrastructure/Services/UserInvitationService.cs`, `Web/Controllers/UserInvitationsController.cs`, `Web/Views/UserInvitations/Create.cshtml`.
- **DB migration/script:** None for F or H (`UserInvitation.RoleId` exists since 069). G: local DB only, one `SchemaMigrationHistory` row; COPY_ONLY backup first: `C:\Program Files\Microsoft SQL Server\MSSQL15.SQLEXPRESS\MSSQL\Backup\AssetMgmt_pre_065hist_deptprompt_20261009_004258.bak` (RESTORE VERIFYONLY: valid). **Config/Web.config keys:** None.
- **IIS/server steps (local):** run `artifacts\rel1009c-launch-elevated.ps1` (pre-checks live Web.dll `FF9B1237...D585` and Web.config `BF64D20E...44EB`, backs up to `C:\inetpub\_backups\AssetManagement_root_pre_rel1009c_<stamp>`, skips Web.config/secrets, smoke + auto-rollback); run 2026-10-09 05:07 EAT, exit 0.
- **Live-deploy steps:** ships with the web publish; live kit to be refreshed after the local publish.
- **Verify:** IIS Express :8092, Test-WF A461 (obscure test assets 25260/25230/25782/25208/25193/25554, restored afterwards): no choice -> prompt, nothing saved; Move -> dept 263->298 + AssetMovement 56 + custody 69 + audit; Keep -> stays 263; same dept -> no prompt; class asset -> Move disabled + rule message, forced Move refused; bulk Move -> 298, bulk without choice -> stays 263. Invite with role X -> user gets X even with a tampered posted RoleId; non-admin inviter sees only Staff + own role and a forced Company Admin POST is refused; old no-role invite -> user with no role. Output `artifacts\deptprompt-2026-10-09\` (proof.txt, proof2.txt, 01_db_after_proofs.txt, 02_cleanup.txt, screenshots).
- **Rollback:** `git revert ba95dd0 058b62a b21ac7f`; after publish restore the `pre_rel1009c` IIS backup. G: `DELETE FROM SchemaMigrationHistory WHERE Id=99` (or restore the backup above).

---
## 2026-10-09 00:40 - Requisitions: remove the "Order by" field (`59ef566`) - PUBLISHED (local IIS, rel1009c, 2026-10-09 05:07:25 EAT)

- **Date/time EAT:** 2026-10-09 00:40 (commit). **PUBLISHED 2026-10-09 05:07:25 EAT** to local IIS via `artifacts\rel1009c-launch-elevated.ps1`, live Web.dll `5BAFFC3BE83B283281B2B47DCE82D814468A3E5F9FE6D372403ECCE6B539336C`.
- **What and why:** Allan 00:34: "Remove the order by on requisition page". The only "order by" on the requisition pages was the **"Order by (optional)"** dropdown (teacher / class contact) on New requisition (shown to users with `Purchases.CreateForAnyDepartment`), echoed as an "Order by" row on requisition Details and the printed requisition. There was no sort control: the Requisitions list has no sortable headers or sort querystring (fixed newest-first order, unchanged). Removed the field from Create, Details and the printout; the controller now ignores any posted `OrderByUserId`; the workflow guide no longer mentions it.
- **Commit hash:** `59ef566`
- **Files changed:** `src/AssetManagement.Web/Controllers/PurchaseRequestsController.cs`, `Views/PurchaseRequests/Create.cshtml`, `Views/PurchaseRequests/Details.cshtml`, `Views/Shared/_RequisitionWorkflowGuide.cshtml`, `src/AssetManagement.Application/Helpers/ReportHtmlBuilder.cs`.
- **DB migration/script:** None. `PurchaseRequest.OrderByUserId` and existing values are kept (just no longer shown or set). **Config/Web.config keys:** None.
- **IIS/server steps (local):** `artifacts\rel1009c-launch-elevated.ps1` (pre-check live Web.dll `FF9B1237...D585`); run 2026-10-09 05:07 EAT, exit 0.
- **Live-deploy steps:** ships with the web publish (see live kit delta note).
- **Verify:** IIS Express :8092, Test-WF A461: Requisitions list and New requisition 200 with no "Order by"; output `artifacts\deptprompt-2026-10-09\` (proof.txt, R screenshot).
- **Rollback:** `git revert 59ef566`; after publish restore the `pre_rel1009c` IIS backup.

---
## 2026-10-09 00:32 - ROOT local IIS publish rel1009 (HEAD `7a7570c`): bypass revert + Role Active + DB console audit + asset movement rules

- **Date/time EAT:** UAC accepted by Allan; elevated run started 2026-10-09 00:32:23; **PUBLISHED_AT 00:32:42 EAT**. Local only: not pushed, no PR, not live nanosoft.africa.
- **What and why:** publishes `9670153`, `b59ae9a`, `06762d5`, `2525ac9`, `107fd27`, `a5292c1` (entries below) to `C:\inetpub\AssetManagement` (localhost:8080).
- **Commit hash:** package built from `7a7570c` (`artifacts\web-publish-oct8-merge-20261009_003014`).
- **Files changed:** 279 package files copied and hash-checked (0 mismatches); Web.config, `Views\web.config`, `Areas\Platform\Views\Web.config` and secrets skipped.
- **DB migration/script:** None (no 095: `Roles.IsActive` already exists).
- **Config/Web.config keys:** None. Web.config `BF64D20E...44EB` and secrets unchanged before/after.
- **IIS/server steps (local):** `artifacts\rel1009-launch-elevated.ps1` -> `rel1009-iis-publish.ps1` (log `artifacts\rel1009-iis-publish.log`, exit 0). Backup `C:\inetpub\_backups\AssetManagement_root_pre_rel1009_20261009_003223` (1,185 files). Pool AssetManagement stopped/started. Web.dll `1E7B6B8D...F353` -> **`FF9B1237DC960070D88E29A201F70988C6A1A06199FE46ABACEDBE19C001D585`** (Application `FDAFE3E0...`, Infrastructure `D5609F6D...`, Domain `F36E97BA...`).
- **Live-deploy steps:** see the live kit on the Desktop (`NanoAssetSuite_LiveKit_20261009_7a7570c`).
- **Verify (localhost:8080, 00:33-00:34 EAT):** login pages 200 (incl. `?reason=access_denied`); Staff `/a46138179/Assets/MyAssets` and `Details/32197` -> 403 in-app Forbidden page with app chrome (no IIS page); Company Admin `Departments/Index` 200; SuperAdmin `/Sys/Database` 200, AuditLog 51253 `DB_CONSOLE_VIEW allowed user=superadmin@asset.local ip=::1`. Output `artifacts\rel1009-published\`.
- **Rollback:** stop pool, robocopy `C:\inetpub\_backups\AssetManagement_root_pre_rel1009_20261009_003223` back over `C:\inetpub\AssetManagement`, start pool.

---
## 2026-10-09 00:31 - Asset movement rules: classes are immovable; assets move room<->room, room<->sub-department, class->room only (`107fd27`, `a5292c1`) - PUBLISHED to local IIS 2026-10-09 00:32:42 EAT (rel1009, Web.dll `FF9B1237...D585`)

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
## 2026-10-09 00:21 - /Sys/Database (DataConsole) audits every page view and refused attempt (`2525ac9`) - PUBLISHED to local IIS 2026-10-09 00:32:42 EAT (rel1009, Web.dll `FF9B1237...D585`)

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
## 2026-10-09 00:16 - Role Active flag: users in an inactive role cannot sign in and are signed out (`b59ae9a`, `06762d5`) - PUBLISHED to local IIS 2026-10-09 00:32:42 EAT (rel1009, Web.dll `FF9B1237...D585`)

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
## 2026-10-09 00:03 - Revert the custodian self-service bypass from `472b707` (`9670153`) - PUBLISHED to local IIS 2026-10-09 00:32:42 EAT (rel1009, Web.dll `FF9B1237...D585`)

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



## rel1018a - PUBLISHED to local IIS (localhost:8080) 2026-10-18 18:38 EAT
- **Build:** tagcode cf4a3fc (rel1016a + random 6-char tag code replacing the 001/002 suffix). Approved by Allan in chat Oct 18 18:37 EAT. Local IIS only; NOT on hosted live.
- **Web.dll:** 99585CF5FD244E419AB5448A0897A240332F1098D8EB51FD6B02163BE99BA5D7 (was 6050F3F9). Web.config unchanged 27426D6E.
- **Copy:** 287 files add/overwrite, no deletes, no migrations, no config/secrets copied.
- **Backups:** C:\inetpub\_backups\AssetManagement_root_pre_rel1018a_20261018_183825; DB AssetManagementModuleDb_pre_rel1018a_publish_20261018_183825.bak (COPY_ONLY, VERIFYONLY ok).
- **Smoke:** 4 login pages 200; 8 protected pages 302 to Login (root and /A46138179).
- **Note:** earlier attempt 18:18 EAT auto-rolled back due to a smoke-harness bug (not an app failure).

