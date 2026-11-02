RoleData_20261102_req_access  -  PREPARED 2026-11-02, NOT RUN ANYWHERE
=====================================================================

Context: Allan's answers of 2026-11-02 (requisition role access, phase 2, branch feature/req-role-access-2).
The code no longer hard-codes Finance/Procurement for purchase orders: recording a PO needs Purchases.View +
Purchases.Edit. Finance should also read the whole asset register (Assets.ViewAll) and member details (Users.View).
These scripts give the roles those permissions. Each one needs Allan's individual approval. Agents never run them.

Files
-----
01_TestWF_org13_grant.sql   Test-WF (org 13). Finance Officer + Purchases.Edit, Assets.ViewAll, Users.View;
                            Procurement Manager / Procurement Officer + Purchases.Edit (already present on
                            alLAN's copy -> no-op). INSERT only, guarded, idempotent, one transaction.
02_NIS_org21_grant.sql      Same for NIS, "Nairobi International School" (org 21).
99_rollback_DO_NOT_RUN.sql  Removes the Finance Officer grants for one organisation (set @OrgId/@OrgName).
optional_assign_department_depthead_procofficer_PREPARE.sql
                            Optional. Gives Test-WF depthead / procofficer a department so they can raise again.
                            The department is Allan's choice; placeholders are NULL and it refuses until they are set.

Safety
------
- Every script refuses unless the database is AssetManagementModuleDb* and the organisation id + name match.
- Every script only reports what it would do while @Confirm = 0 (the default). Set @Confirm = 1 to write.
- Run in SSMS/sqlcmd, not the web Sys/Database console. Take the usual COPY_ONLY backup first on live.
- Users pick up new permissions after signing out/in (or after the permission cache refresh).

What the grants give (checked in the code on feature/req-role-access-2)
-----------------------------------------------------------------------
- Purchases.Edit: only Purchases/Create (record a purchase order) and the Purchase orders menu entry.
- Assets.ViewAll: organisation-wide read of the asset register and lists. It gives no edit, assign, transfer or
  dispose: those buttons and actions still need Assets.Edit / Assets.Assign / Assets.Transfer / Assets.Dispose,
  which Finance Officer does not have. Since 2026-11-02 it no longer unlocks "request for others" or inline
  classification create either, and it never unlocks raising requisitions.
- Users.View: Users list and details only. Create, edit, role/department assignment, activate, unlock and delete
  need Users.Create / Users.Edit / Users.Delete.

State on alLAN's local copy (read-only SELECTs, 2026-11-02)
-----------------------------------------------------------
Finance Officer 62 (Test-WF) and 125 (NIS): Assets.View, Purchases.View only.
Procurement Manager 58/121 and Procurement Officer 61/124: already have Purchases.Edit.
Test-WF users depthead.a46138179 and procofficer.a46138179 have no department and no CreateForAny, so after
answer 1 they cannot raise until a department is assigned (see the optional script, or Users > Assign department).
Re-check on the target database before running: the plan result set shows each row as "to add" or "no-op".
