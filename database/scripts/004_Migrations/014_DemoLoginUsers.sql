-- Ensure demo login accounts exist with the documented password (P@ssw0rd!).
DECLARE @passwordHash NVARCHAR(MAX) = N'ALJwzw5r970vW+fpNg4Ivw5nutwiP9Omge0gCdgtDVM2h6NFmycZ2GwSH5fyBqDTaw==';
DECLARE @defaultOrgId INT = (SELECT TOP 1 [Id] FROM [Organization] WHERE LOWER(LTRIM(RTRIM([Slug]))) = N'default' ORDER BY [Id]);

IF @defaultOrgId IS NOT NULL
BEGIN
    UPDATE [Users]
    SET [PasswordHash] = @passwordHash,
        [IsActive] = 1,
        [EmailConfirmed] = 1
    WHERE [Email] IN (
        N'superadmin@asset.local',
        N'nanosoft@asset.local',
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
        N'departmenthead@asset.local'
    );
END
GO
