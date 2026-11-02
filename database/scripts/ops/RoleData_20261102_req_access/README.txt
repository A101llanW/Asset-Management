RoleData_20261102_req_access  -  2026-11-02 (corrected 14:34 EAT, branch feature/req-role-access-3)
=====================================================================================================

Context: Allan's answers of 2026-11-02 (requisition role access) and his corrections:
  1. A member with no department is NOT blocked: organisation-wide for rooms, people and assets, still bounded by
     role permissions.
  2. Raising a requisition (own department subtree, or organisation-wide) needs Purchases.Create, in the UI and on
     the server.
  3. Department model: a procurement officer belongs under Procurement; a department head heads one department and a
     department can have several heads (role-based: every active "Department Head" holder whose department is the
     unit; no schema change needed - see tests Approvals/DepartmentHeadModelTests and StageApproverScopeTests).
Recording a PO needs Purchases.View + Purchases.Edit. Finance also reads the whole asset register (Assets.ViewAll)
and member details (Users.View).

Files (SSMS / sqlcmd; every one guarded and idempotent; @Confirm = 0 by default = report only)
--------------------------------------------------------------------------------------------
01_TestWF_org13_grant.sql        Test-WF (org 13). Finance Officer + Purchases.Edit, Assets.ViewAll, Users.View;
                                 Procurement Manager / Officer + Purchases.Edit; Department Head, Procurement Officer,
                                 Facilities Manager, Company Admin + Purchases.Create. INSERT only, one transaction.
02_NIS_org21_grant.sql           Same for "Nairobi International School" (org 21).
03_TestWF_org13_departments.sql  Test-WF ONLY (Allan 2026-11-02 15:03 EAT): creates a top-level active 'Procurement'
                                 department (Code PROCUREMENT, administrative, requisition target, plus the app's
                                 Departments.Create audit row) if org 13 has none; depthead -> Academics (1456);
                                 procofficer -> Procurement. Only moves a user whose department is NULL. NIS gets nothing.
99_rollback_DO_NOT_RUN.sql       Removes the Finance Officer grants for one organisation; for org 13 also clears the
                                 03 departments (only while still on them) and deletes the Procurement department 03
                                 created, only if nothing else references it (else IN_USE, left in place).
live/                            Console kit for the live database (01_backup, 02_precheck, 03_change, 04_verify,
                                 README_RUN_ORDER.txt). Grants only; org set by @OrgName; names resolved by name.

Safety
------
- Every script refuses unless the database is AssetManagementModuleDb* and the organisation matches.
- Roles that do NOT get Purchases.Create here: Staff, Auditor, Asset Manager, Finance Officer, Procurement Manager,
  Accounts clerk. Adding it to Staff needs Allan's decision.
- Users pick up new permissions after signing out/in (or after the permission cache refresh).

What the grants give
--------------------
- Purchases.Create: New requisition button, Create GET/POST, approval-path preview; the server refuses without it.
- Purchases.Edit: only Purchases/Create (record a purchase order) and the Purchase orders menu entry.
- Assets.ViewAll: organisation-wide read only (no edit/assign/transfer/dispose; never unlocks raising).
- Users.View: Users list and details only.

State on alLAN's local copy before the run (2026-11-02)
-------------------------------------------------------
Finance Officer 62 (Test-WF) and 125 (NIS): Assets.View, Purchases.View, Purchases.Approve.
Purchases.Create already held by Department Head 63/126, Procurement Officer 61/124, Facilities Manager 57/120,
Company Admin 59/122. No department named like "Procurement" existed in org 13; 03 (15:08 EAT) created Procurement
(Department 3696) and put procofficer.a46138179 in it (backup AssetManagementModuleDb_pre_procdept_20261102_1507.bak).
