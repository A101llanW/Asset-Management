=====================================================================
  LIVE DATABASE UPDATE 27 Oct 2026 - NOT RUN
  For: AssetManagementModuleDb on live.  To be run by Allan only.
=====================================================================
Status: NOTHING here has been run on live. All scripts were tested on a scratch restore of the
local database on alLAN (see "Tested" below).

Every script is pasteable into SSMS / the database console as-is, split into GO batches, and
idempotent (running it twice does no harm). Connect to the AssetManagementModuleDb database
first; scripts 02-05 refuse to run on any other database.

---------------------------------------------------------------------
RUN ORDER
---------------------------------------------------------------------
00_BACKUP_COPY_ONLY_LIVE_RUN_BY_ALLAN.sql
   COPY_ONLY backup + RESTORE VERIFYONLY. Replace <BACKUP_FOLDER> first (same folder as
   AssetManagementModuleDb_pre_tagseg_20261021.bak). Suggested file name:
   AssetManagementModuleDb_pre_sections_20261027.bak. It refuses to run while the placeholder is there.
   Expected: "BACKUP DATABASE successfully processed ..." and "The backup set on file 1 is valid."

01_PRECHECKS_READ_ONLY_LIVE_RUN_BY_ALLAN.sql   (only reads)
   Reports the history rows 103-107, whether the three 104 tag-segment columns exist, the
   AuditLog.EntityId width (the other "104"), the 105/106/107 objects, and how many Prep rows
   106 will mark. Last line = RESULT:
     READY              104 already on live (expected after 21 Oct), 105-107 absent -> go on.
     READY_104_MISSING  104 is not on live after all -> go on; 02 adds it.
     ALREADY_APPLIED    everything already there -> just run 09.
     STOP_...           do not continue (see "If something fails").

02_MIGRATION_104_AssetTagSegmentToggles_IF_MISSING_LIVE_RUN_BY_ALLAN.sql
   Adds Department.AssetTagUse{Department,SubDepartment,Room}Segment (BIT NULL) only if missing
   (COL_LENGTH guards) and the history row only if missing. If the 21 Oct console run happened
   (history row Id 107), this changes nothing.
03_MIGRATION_105_LabelPrintBatchLog_LIVE_RUN_BY_ALLAN.sql      new tables LabelPrintBatch, LabelPrintBatchItem
04_MIGRATION_106_DepartmentSection_LIVE_RUN_BY_ALLAN.sql       Department.Section (0 = Senior, all existing rows) + SortOrder;
                                                               marks "Prep" (1614) and the ten Prep rooms (1582-1591) as Prep,
                                                               only where ID AND exact name match. Message shows how many.
05_MIGRATION_107_DepartmentSectionHeads_LIVE_RUN_BY_ALLAN.sql  Department.PrepHeadUserId / SeniorHeadUserId (all NULL). Needs 04.
   Each of 02-05 is one transaction: if it fails, nothing from that script stays.
   Expected: "Commands completed successfully." (plus the 106 / 107 messages).

09_VERIFY_READ_ONLY_LIVE_RUN_BY_ALLAN.sql   (only reads)
   Every row PASS and RESULT = ALL_PASS. Also lists the history rows and the Prep/Senior counts.

Then copy Desktop\publish (STAGED - NOT PUBLISHED) to the site. Code BEFORE database is not safe:
the new code reads the new columns.

---------------------------------------------------------------------
ABOUT THE TWO "104" SCRIPTS
---------------------------------------------------------------------
Two different scripts carry the number 104:
  - 104_AssetTagSegmentToggles.sql   (tag segment switches; history name '104_AssetTagSegmentToggles.sql')
  - 104_widen_auditlog_entityid.sql  (AuditLog.EntityId -> NVARCHAR(450); writes NO history row)
Nothing has been renumbered (live history must keep matching). The history names are already
distinct, and all new scripts start at 105. 104_widen_auditlog_entityid is NOT part of this run;
01 only reports the column width (450 = widened, 100 = not).

---------------------------------------------------------------------
IF SOMETHING FAILS
---------------------------------------------------------------------
- 00 fails: stop. No later script until a verified backup exists.
- 01 says STOP_103_MISSING: live is older than expected. Stop and check what is deployed.
- 01 says STOP_104_PARTIAL or STOP_PARTIAL_105_107: something was half-applied before. Stop; send
  the 01 output for review. Do not run the rollbacks blindly.
- 02-05 error: the script rolled itself back. Copy the error, stop, do NOT publish the code. Live
  keeps working on the current code because nothing new is required by it.
- 09 shows FAIL: do not publish the code; send the 09 output.
- Site broken after publishing the code: put the previous site files back first. Only then, and
  only if decided, use the rollbacks (newest first). Last resort: restore the 00 backup.

---------------------------------------------------------------------
ROLLBACKS (DO_NOT_RUN)
---------------------------------------------------------------------
99a (107) -> 99b (106) -> 99c (105) -> 99d (104).
Each does nothing unless @Confirm at the top is changed from N'NO' to N'YES'. Run only after the
previous site build is back. 99b refuses while 107's columns exist. 99d is only for the case
where 01 said READY_104_MISSING (so 02 added 104 today); if 104 was on live since 21 Oct, keep it
(the 21 Oct rollback is /workspace/live-update/tagseg/99_rollback.sql). 99c drops the print-log
tables and their rows.

---------------------------------------------------------------------
TESTED (alLAN, 27 Oct 2026, scratch DB AssetManagementModuleDb_LiveScriptTest from
AssetManagementModuleDb_pre_mig104to107_20261027_082328.bak; scratch DB dropped afterwards)
---------------------------------------------------------------------
A. 104 absent: 01 = READY_104_MISSING; 02-05 ok; 09 = ALL_PASS. Re-run of 01-05 + 09: 01 =
   ALREADY_APPLIED, 02-05 no-ops, 09 = ALL_PASS, no duplicate history rows.
   99a with @Confirm = NO refused, nothing changed (09 still ALL_PASS). 99b before 99a refused.
   99a-99d confirmed: all removed; 01 back to READY_104_MISSING.
B. 104 already applied (like live after 21 Oct): 01 = READY; 02 no-op; 03-05 ok; 09 = ALL_PASS;
   01 afterwards = ALREADY_APPLIED.
C. 05 before 04: refused ("run 04 first").
00: refused with the placeholder; with a real path the COPY_ONLY backup and VERIFYONLY succeeded
   (test .bak deleted).
Note: on the local data the 106 Prep fix matched 0 rows (local IDs differ from live). On live,
01 shows the number to expect.
