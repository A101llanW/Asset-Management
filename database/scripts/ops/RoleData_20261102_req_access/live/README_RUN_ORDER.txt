LIVE console kit  -  RoleData_20261102_req_access/live  -  PREPARED 2026-11-02, NOT RUN
=======================================================================================
Scope: role permissions only (INSERT into dbo.RolePermission). No schema change, no user or department change.
       Users' departments on live are NOT touched (the Test-WF department script 03 is local test data only).
Approval: Allan approved 01/02 including live on 2026-11-02. Each live run still needs his go for that run.
Org: set @OrgName at the top of 02, 03 and 04 (default N'Nairobi International School'); the id is looked up by
     name and every file refuses unless exactly one organisation has that name. Roles and permissions are resolved
     by name / code (live ids differ from alLAN's).

What it grants (each only if missing):
  Finance Officer     + Purchases.Edit, Assets.ViewAll, Users.View
  Procurement Manager + Purchases.Edit
  Procurement Officer + Purchases.Edit, Purchases.Create
  Department Head, Facilities Manager, Company Admin + Purchases.Create   (correction 2: raising needs it)

Run order (web Sys/Database console as superadmin; one file = one batch; stop at any other RESULT):
  1. 01_backup.sql    expected RESULT = BACKUP_OK         (record backup_file)
  2. 02_precheck.sql  expected RESULT = READY             (record to_add, e.g. 3 on a copy like alLAN's)
                      RESULT = ALREADY_APPLIED -> skip 03, run 04.
  3. 03_change.sql    set @ExpectedToAdd = to_add from 02. expected RESULT = GRANT_OK, added = to_add, still_missing = 0
  4. 04_verify.sql    expected RESULT = ALL_PASS
Then users sign out and in (or wait for the permission cache).

Console-safe: no less-than sign before a letter, no ampersand-hash, every file ends in one one-row RESULT, no GO.

Rollback:
  - Targeted: ..\99_rollback_DO_NOT_RUN.sql in SSMS (set @OrgId/@OrgName; removes the Finance Officer grants).
  - Full: restore the 01 backup with the site stopped. This undoes EVERYTHING written after the backup.
