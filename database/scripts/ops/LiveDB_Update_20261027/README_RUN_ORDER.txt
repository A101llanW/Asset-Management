=====================================================================
  LIVE DATABASE UPDATE 27 Oct 2026 - NOT RUN
  For: AssetManagementModuleDb on live.  To be run by Allan only.
=====================================================================
Status: NOTHING here has been run on live. All scripts were tested on a scratch restore of the
local database on alLAN (see "Tested" below).

HOW TO RUN: in SSMS or sqlcmd ONLY - NOT the web Sys/Database console. The guards rely on GO batches
and SET NOEXEC, which only SSMS/sqlcmd understand. Connect to the AssetManagementModuleDb database
first. Every script is idempotent (running it twice does no harm).

SAFETY GUARDS (review S5): 02, 03, 04 and 05 each refuse to run (and change nothing) unless
  - the current database is AssetManagementModuleDb, AND
  - msdb.dbo.backupset shows a COPY_ONLY full backup of THIS database (made by 00) that finished in
    the last 2 hours.
If more than 2 hours pass between 00 and 05, simply run 00 again (it makes a new timestamped file).

---------------------------------------------------------------------
RUN ORDER
---------------------------------------------------------------------
00_BACKUP_COPY_ONLY_LIVE_RUN_BY_ALLAN.sql
   COPY_ONLY backup + RESTORE VERIFYONLY. Replace <BACKUP_FOLDER> in @folder first (the same folder
   as AssetManagementModuleDb_pre_tagseg_20261021.bak). The file name is made automatically with the
   server time: AssetManagementModuleDb_pre_sections_yyyyMMdd_HHmmss.bak (review S2). It refuses while
   the placeholder is there, and refuses if that file already exists - it never overwrites.
   Expected: "BACKUP DATABASE successfully processed ...", "The backup set on file 1 is valid." and
   a line with the database name and the backup file path (write the path down).

01_PRECHECKS_READ_ONLY_LIVE_RUN_BY_ALLAN.sql   (only reads)
   Reports the history rows 103-107, whether the three 104 tag-segment columns exist, the 104 history
   Checksum, the AuditLog.EntityId width (the other "104"), the 105/106/107 objects, and:
     - the 11 Prep IDs (1614 and 1582-1591), each with its live name, kind, parent and a YES/NO match
       (a straight or curly apostrophe both match, e.g. 1587 'Prep Counsellor's Room');
     - prep_rows_106_would_match (expected 11) - write this number into @ExpectedPrep at the top of 09;
     - the units whose parent is 1614 (the Prep grade). Expected: NONE (children_of_1614 = 0).
   Last line = RESULT:
     READY                   104 already on live (expected after 21 Oct), 105-107 absent -> go on.
     READY_104_HISTORY_ONLY  the 3 104 columns exist but the history row does not -> go on; 02 only
                             adds the history row (Checksum 'LiveDB_20261027:history-only').
     READY_104_MISSING       the 3 104 columns do not exist -> go on; 02 adds them and the history row
                             (Checksum 'LiveDB_20261027:added-columns').
     ALREADY_APPLIED         everything already there -> just run 09.
     STOP_...                do not continue (see "If something fails").
   If any of the 11 Prep rows says NO, or children_of_1614 is not 0, stop and send the 01 output.

02_MIGRATION_104_AssetTagSegmentToggles_IF_MISSING_LIVE_RUN_BY_ALLAN.sql
   Columns absent: adds Department.AssetTagUse{Department,SubDepartment,Room}Segment (BIT NULL) + the
   history row marked 'LiveDB_20261027:added-columns'. Columns present without history row: adds only
   the history row ('LiveDB_20261027:history-only'). Both present (21 Oct run happened, history Id 107):
   changes nothing. Half-applied 104: refuses.
03_MIGRATION_105_LabelPrintBatchLog_LIVE_RUN_BY_ALLAN.sql      new tables LabelPrintBatch, LabelPrintBatchItem
04_MIGRATION_106_DepartmentSection_LIVE_RUN_BY_ALLAN.sql       Department.Section (0 = Senior, all existing rows) + SortOrder;
                                                               marks "Prep" (1614) and the ten Prep rooms (1582-1591) as Prep,
                                                               only where ID AND name match (apostrophe-tolerant, as in 01).
                                                               Children of 1614 are not touched (live has none).
05_MIGRATION_107_DepartmentSectionHeads_LIVE_RUN_BY_ALLAN.sql  Department.PrepHeadUserId / SeniorHeadUserId (all NULL). Needs 04.
   Each of 02-05 is one transaction: if it fails, nothing from that script stays.
   Expected: "Commands completed successfully." (plus the 02 / 106 / 107 messages).

09_VERIFY_READ_ONLY_LIVE_RUN_BY_ALLAN.sql   (only reads)
   First set @ExpectedPrep at the top to the prep_rows_106_would_match number from 01 (11 expected).
   Every row PASS and RESULT = ALL_PASS. It FAILs if the number of Prep rows (Section = 1) differs
   from @ExpectedPrep (review S4). Also lists the history rows and the Prep/Senior counts.

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
GOOD TO KNOW AFTER PUBLISHING (no action needed)
---------------------------------------------------------------------
- Section heads (review S10): if a unit's head is made inactive, or its Kind is changed, its Prep and
  Senior section heads are cleared. Set them again on Departments > Edit if needed.
- QR scan page (review S8): the serial number, and lookup by exact serial, are hidden from anyone who
  is not signed in or lacks Assets.View. To show the serial to the public again, add
  <add key="AssetScan:ShowSerialToPublic" value="true" /> to <appSettings> in the site's Web.config
  (Web.config is not in the publish folder; the live one is kept). Absent or anything else = hidden.

---------------------------------------------------------------------
IF SOMETHING FAILS
---------------------------------------------------------------------
- 00 fails or says NOT RUN: stop. No later script until a verified backup exists.
- 02-05 say "NOT RUN: no COPY_ONLY full backup ... in the last 2 hours": run 00 (again), then retry.
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
previous site build is back. 99b refuses while 107's columns exist. 99c drops the print-log tables
and their rows.
99d (review S1) only works if 02 ADDED the 104 columns in this run (history Checksum
'LiveDB_20261027:added-columns', i.e. 01 said READY_104_MISSING). In every other case (READY,
READY_104_HISTORY_ONLY, the 21 Oct run) it refuses and the columns stay. It never deletes the
AssetTag.Segment.* settings. The reference rollback for the 21 Oct 104 is in this repository:
database/scripts/005_Rollback/104_AssetTagSegmentToggles_rollback.sql (also guarded: does nothing
unless @ConfirmRollback is changed to N'YES'; that one does delete the AssetTag.Segment.* settings).

---------------------------------------------------------------------
TESTED
---------------------------------------------------------------------
alLAN, 27 Oct 2026 09:39 EAT. Scratch DB AssetManagementModuleDb_LiveScriptTest restored from
AssetManagementModuleDb_pre_mig104to107_20261027_082328.bak for every scenario, dropped afterwards.
Only the local .\SQLEXPRESS server was used. To look like live, the scratch DB got the 11 Prep IDs
with the live names (1587 written with a curly apostrophe). Full log: _TEST_RESULTS_scratch_restore_20261027.txt
A. 104 absent: 01 = READY_104_MISSING, 11 of 11 Prep rows YES, children_of_1614 = 0.
   02 on master refused; 02 before any 00 backup refused (nothing changed); 00 with placeholder
   refused; 00 refused when the timestamped file already existed; 00 real = backup + VERIFYONLY ok.
   02 added the columns with marker 'LiveDB_20261027:added-columns'; 03-05 ok; 106 marked 11 rows;
   09 (ExpectedPrep 11) = ALL_PASS. Re-run of 01-05 + 09: 01 = ALREADY_APPLIED, no-ops, ALL_PASS.
   99a / 99d with @Confirm = NO refused (09 still ALL_PASS); 99b before 99a refused;
   99a-99d confirmed: all removed; 01 back to READY_104_MISSING.
H. columns present, history row deleted: 01 = READY_104_HISTORY_ONLY; 02 added only the history row
   ('LiveDB_20261027:history-only'); 03-05 ok; 09 = ALL_PASS; 99a-99c confirmed ok, 99d confirmed
   REFUSED, the 3 columns stayed; 01 afterwards = READY.
B. 104 pre-applied with history (like live after 21 Oct): 01 = READY; 02 no-op; 03-05 ok;
   09 = ALL_PASS; 01 = ALREADY_APPLIED. 99d confirmed refused (no marker); 005_Rollback/104 with
   @ConfirmRollback = NO refused (columns stayed, 09 ALL_PASS); with YES it removed the columns.
C. 05 before 04: refused ("run 04 first").
D. local names (Prep IDs not matching): 01 shows NO per row, count 0; 106 marked 0;
   09 with ExpectedPrep 11 = SOME_FAIL on the Prep count (as designed).
