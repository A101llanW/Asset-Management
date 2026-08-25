-- Clear login lockouts and repair demo/platform admin access.

IF OBJECT_ID(N'[LoginAttempts]', N'U') IS NOT NULL
BEGIN
    DELETE FROM [LoginAttempts];
END
GO

UPDATE [Roles]
SET [OrganizationId] = NULL
WHERE [Name] = N'Platform Admin';
GO

DECLARE @passwordHash NVARCHAR(MAX) = N'ALJwzw5r970vW+fpNg4Ivw5nutwiP9Omge0gCdgtDVM2h6NFmycZ2GwSH5fyBqDTaw==';
DECLARE @securityStamp NVARCHAR(64) = N'00000000000000000000000000000002';

UPDATE u
SET u.[Email] = e.[TargetEmail],
    u.[UserName] = e.[TargetEmail],
    u.[PasswordHash] = @passwordHash,
    u.[IsActive] = 1,
    u.[TwoFactorEnabled] = 0
FROM [Users] u
INNER JOIN [Organization] o ON o.[Id] = u.[OrganizationId]
CROSS APPLY (
    SELECT CASE
        WHEN LOWER(LTRIM(RTRIM(o.[Slug]))) = N'demo-b' THEN N'demo@asset.local'
        ELSE LOWER(LTRIM(RTRIM(o.[Slug]))) + N'@asset.local'
    END AS [TargetEmail]
) e
WHERE u.[OrganizationId] IS NOT NULL
  AND u.[Email] = N'superadmin@asset.local'
  AND LOWER(e.[TargetEmail]) <> LOWER(u.[Email])
  AND NOT EXISTS (
      SELECT 1
      FROM [Users] x
      WHERE x.[OrganizationId] = u.[OrganizationId]
        AND LOWER(LTRIM(RTRIM(x.[Email]))) = LOWER(e.[TargetEmail])
        AND x.[Id] <> u.[Id]
  );

IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Email] = N'superadmin@asset.local' AND [OrganizationId] IS NULL)
BEGIN
    IF EXISTS (SELECT 1 FROM [Users] WHERE [Id] = N'seed-user-platform')
    BEGIN
        UPDATE [Users]
        SET [Email] = N'superadmin@asset.local',
            [UserName] = N'superadmin@asset.local',
            [OrganizationId] = NULL,
            [RoleId] = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Platform Admin' AND [OrganizationId] IS NULL ORDER BY [Id]),
            [PasswordHash] = @passwordHash,
            [IsActive] = 1,
            [TwoFactorEnabled] = 0,
            [LockoutEndDateUtc] = NULL,
            [AccessFailedCount] = 0
        WHERE [Id] = N'seed-user-platform';
    END
    ELSE
    BEGIN
        DECLARE @platformRoleId INT = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Platform Admin' AND [OrganizationId] IS NULL ORDER BY [Id]);

        IF @platformRoleId IS NOT NULL
        BEGIN
            INSERT INTO [Users]
                ([Id],[Email],[EmailConfirmed],[PasswordHash],[SecurityStamp],[PhoneNumberConfirmed],[TwoFactorEnabled],[LockoutEnabled],[AccessFailedCount],[UserName],
                 [EmployeeNumber],[FirstName],[LastName],[IsActive],[RoleId],[OrganizationId],[CreatedAt])
            VALUES
                (N'seed-user-platform', N'superadmin@asset.local', 1, @passwordHash, @securityStamp, 0, 0, 0, 0, N'superadmin@asset.local',
                 N'EMP-PLATFORM', N'Platform', N'Admin', 1, @platformRoleId, NULL, GETUTCDATE());
        END
    END
END
ELSE
BEGIN
    UPDATE [Users]
    SET [OrganizationId] = NULL,
        [RoleId] = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Platform Admin' AND [OrganizationId] IS NULL ORDER BY [Id]),
        [PasswordHash] = @passwordHash,
        [IsActive] = 1,
        [TwoFactorEnabled] = 0,
        [LockoutEndDateUtc] = NULL,
        [AccessFailedCount] = 0
    WHERE [Email] = N'superadmin@asset.local'
      AND [OrganizationId] IS NULL;
END

UPDATE [Users]
SET [PasswordHash] = @passwordHash,
    [IsActive] = 1,
    [TwoFactorEnabled] = 0,
    [LockoutEndDateUtc] = NULL,
    [AccessFailedCount] = 0
WHERE [Email] LIKE N'%@asset.local';
GO
