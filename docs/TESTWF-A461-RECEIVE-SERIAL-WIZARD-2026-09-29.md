# TESTWF-A461 Receive/4 Enter serial numbers wizard invisible (2026-09-29)

## Symptom
Purchases Receive/4: clicking **Enter serial numbers (optional)** left `#receiveSerialWizardModal` at `display:none` / `opacity:0` / no `.show` / no backdrop.
Sub-type **Assign** modal (`#assetSubTypePickerModal`) PASS.
Evidence: `C:\Users\allan\AppData\Local\Temp\nis-a461-receive4-invisible-2026-09-29\` (11-after-assign PASS, 12-after-serial FAIL).

## Root cause
`Scripts/app/receive-serial-wizard.js` open-click handler (~L168-173) returned early when `#ConditionOnReceipt` was empty (placeholder `-- Select condition --`), so `bootstrap.Modal.show()` never ran. Silent UX: invalid class + focus only; no modal.

Condition is required for Receive **submit**, not for capturing optional serials.

## Fix
Removed the condition gate from the wizard open path. Always prepare step UI and call `modal.show()` (with a minimal show fallback if Bootstrap Modal is missing). Condition validation remains on form submit / server.

## Files
- `src/AssetManagement.Web/Scripts/app/receive-serial-wizard.js`

## IIS publish (2026-09-29 ~15:18 EAT)
- Targeted copy of JS only (no DLL rebuild).
- Backup: `C:\inetpub\AssetManagement\_bak\receive-serial-20260929-151852\`
- Web.config preserved.
- Pool recycled; login `http://127.0.0.1:8080/nanosoft/Account/Login` → 200.
- IIS JS: len 11103, mtime 2026-09-29 15:17:34 EAT; old gate absent.
- `AssetManagement.Web.dll` unchanged (mtime 2026-09-29 14:15:13 EAT, 570368 bytes).

## Product note
After Receive, custody Assign remains optional (assign or keep In Store) — separate from this sub-type Assign CTA. Not changed here.

## GitHub
No PR / no push. Local SoT edit only.

## READY_FOR_QA
yes — re-click Enter serial numbers on Receive/4 without selecting condition first; modal should show with backdrop.
