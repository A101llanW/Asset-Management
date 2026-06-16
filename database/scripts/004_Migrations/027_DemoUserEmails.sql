-- Align demo login emails: platform superadmin@asset.local; tenant company admins default@ / demo@.

UPDATE [Users]
SET [Email] = N'demo@asset.local',
    [UserName] = N'demo@asset.local'
WHERE [Email] = N'companyadmin@demo-b.asset.local';
GO

UPDATE [Users]
SET [Email] = N'default@asset.local',
    [UserName] = N'default@asset.local'
WHERE [Email] = N'companyadmin@asset.local';
GO

UPDATE [Users]
SET [Email] = N'default@asset.local',
    [UserName] = N'default@asset.local'
WHERE [Email] = N'superadmin@asset.local'
  AND [OrganizationId] IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM [Users] WHERE [Email] = N'default@asset.local');
GO

IF EXISTS (SELECT 1 FROM [Users] WHERE [Email] = N'default@asset.local' AND [Id] = N'seed-user-001')
   AND EXISTS (SELECT 1 FROM [Users] WHERE [Email] = N'default@asset.local' AND [Id] = N'seed-user-companyadmin')
BEGIN
    DELETE FROM [Users] WHERE [Id] = N'seed-user-companyadmin';
END
GO

UPDATE [Users]
SET [Email] = N'superadmin@asset.local',
    [UserName] = N'superadmin@asset.local'
WHERE [Email] = N'platform@asset.local'
   OR ([Id] = N'seed-user-platform' AND [OrganizationId] IS NULL);
GO
