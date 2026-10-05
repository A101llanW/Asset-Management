-- PREPARE ONLY — DO NOT RUN until Allan explicitly approves.
-- HOLD: no more .\SQLEXPRESS migrations/scripts until Allan decides.
-- Reverts migration 083 effect: RequirePasswordChange = 0 for the same seed/@asset.local filter 083 used.
-- Does NOT change PasswordHash, does NOT delete/create users, does NOT touch other columns.
-- Written 2026-10-05 after 083 was applied early on shared live DB.
--
-- Live snapshot at write time: 36 rows with RequirePasswordChange=1 matching this filter
-- (see docs note / PM email list). 083 reported ~35 rows updated (only rows that were 0).

SET NOCOUNT ON;

UPDATE [Users]
SET [RequirePasswordChange] = 0
WHERE [RequirePasswordChange] = 1
  AND (
        [Email] LIKE N'%@asset.local'
     OR [Email] LIKE N'%@%.asset.local'
     OR [Id] LIKE N'seed-user-%'
     OR [Email] IN (
            N'superadmin@asset.local',
            N'nanosoft@asset.local',
            N'nis@asset.local',
            N'default@asset.local',
            N'demo@asset.local',
            N'platform@asset.local',
            N'companyadmin@asset.local',
            N'companyadmin@demo-b.asset.local',
            N'assetmanager@asset.local',
            N'procurement@asset.local',
            N'finance@asset.local',
            N'staff@asset.local',
            N'auditor@asset.local',
            N'departmenthead@asset.local',
            N'itstaff@asset.local',
            N'opsstaff@asset.local',
            N'labtech@asset.local',
            N'staff@demo-b.asset.local'
        )
  );

PRINT N'083 revert: RequirePasswordChange=0 (rows affected: ' + CAST(@@ROWCOUNT AS NVARCHAR(20)) + N').';
GO
