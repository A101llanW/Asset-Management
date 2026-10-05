-- 083_SeededAccountsRequirePasswordChange.sql
-- Phase B (#2): Force password change on next login for seeded @asset.local / known seed accounts.
-- Does NOT change PasswordHash, does NOT delete or create users.
-- Idempotent: safe to re-run.

SET NOCOUNT ON;

IF COL_LENGTH(N'dbo.Users', N'RequirePasswordChange') IS NOT NULL
BEGIN
    UPDATE [Users]
    SET [RequirePasswordChange] = 1
    WHERE [IsActive] = 1
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
      )
      AND ISNULL([RequirePasswordChange], 0) = 0;

    PRINT N'083: RequirePasswordChange=1 set for seeded accounts (rows affected: ' + CAST(@@ROWCOUNT AS NVARCHAR(20)) + N').';
END
ELSE
BEGIN
    PRINT N'083 skip: RequirePasswordChange column missing (run 046 first).';
END
GO
