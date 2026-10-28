LiveDB_Update_20261028_110  -  migration 110 (requisition target: room or person)
=====================================================================================
PREPARED ONLY. Nobody but Allan runs these, on live, in SSMS or sqlcmd (NOT the web Sys/Database console).
Nothing here has been run on live. It was applied (source script 004_Migrations/110_...) only to the LOCAL
.\SQLEXPRESS AssetManagementModuleDb dev copy, after a full local backup.

ORDER (stop at the first failure):
  00_BACKUP_COPY_ONLY_LIVE_RUN_BY_ALLAN.sql
      Replace <BACKUP_FOLDER> first. Writes AssetManagementModuleDb_pre110_yyyyMMdd_HHmmss.bak (COPY_ONLY, CHECKSUM,
      verified). Refuses to overwrite.
  01_PRECHECKS_READ_ONLY_LIVE_RUN_BY_ALLAN.sql
      SELECTs only. Continue only if RESULT = READY (ALREADY_APPLIED: skip to 09).
  02_MIGRATION_110_RequisitionTargetRoomOrPerson_LIVE_RUN_BY_ALLAN.sql
      Adds 4 NULLable columns to dbo.PurchaseRequest (RequisitionTargetKind, TargetUserId, TargetPlacement,
      TargetDepartmentId), 6 NULLable columns to dbo.AssetCategory / AssetType / AssetSubType (CreatedById,
      CreatedVia: who created one inline, and on which screen) and the history row. No existing row changes; no FK;
      no index. Idempotent.
      Refuses unless 108 is applied and a COPY_ONLY backup finished in the last 2 hours.
  09_VERIFY_READ_ONLY_LIVE_RUN_BY_ALLAN.sql
      SELECTs only. Expect RESULT = OK.
  THEN publish the matching build (needs Allan's UAC OK). 110 must be on the database BEFORE the code is published:
  the PurchaseRequest, AssetCategory, AssetType and AssetSubType entity maps read the new columns.

ROLLBACK (only if needed, and only AFTER the previous build is live again):
  99_ROLLBACK_110_RequisitionTargetRoomOrPerson_DO_NOT_RUN.sql
      sqlcmd -E -S <server> -i 99_ROLLBACK_110_RequisitionTargetRoomOrPerson_DO_NOT_RUN.sql -v DbName="<database>" ConfirmRollback="YES"
      Loses which requisitions were for a person / the placement, and who created a category / type / sub-type
      inline (the AuditLog rows stay). Received assets keep their room / custodian.

If something fails:
  00 fails      -> stop; nothing has changed.
  01 STOP_*     -> stop; send the 01 output to the developer. STOP_NO_BACKUP: run 00 again.
  02 fails      -> the transaction rolls back by itself; nothing has changed; send the error text.
  09 NOT_OK     -> do not publish; send the 09 output.

Old requisitions (saved before 110) keep RequisitionTargetKind = NULL and behave exactly as before: their department
is the target, approval routes by it and receiving defaults to it.
