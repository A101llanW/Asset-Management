-- Restore demo login access after email migration and failed-attempt lockouts.

IF OBJECT_ID(N'[LoginAttempts]', N'U') IS NOT NULL
BEGIN
    DELETE FROM [LoginAttempts];
END
GO

DECLARE @passwordHash NVARCHAR(MAX) = N'ALJwzw5r970vW+fpNg4Ivw5nutwiP9Omge0gCdgtDVM2h6NFmycZ2GwSH5fyBqDTaw==';
DECLARE @securityStamp NVARCHAR(64) = N'00000000000000000000000000000002';

UPDATE [Users]
SET [PasswordHash] = @passwordHash,
    [IsActive] = 1,
    [EmailConfirmed] = 1,
    [LockoutEndDateUtc] = NULL,
    [AccessFailedCount] = 0,
    [TwoFactorEnabled] = 0,
    [TwoFactorCode] = NULL,
    [TwoFactorExpiryUtc] = NULL
WHERE [Email] LIKE N'%@asset.local';
GO

-- Ensure platform admin exists and is not tied to a tenant.
IF EXISTS (
    SELECT 1
    FROM [Users]
    WHERE [Email] = N'superadmin@asset.local'
      AND [OrganizationId] IS NOT NULL
)
BEGIN
    UPDATE u
    SET u.[Email] = e.[TargetEmail],
        u.[UserName] = e.[TargetEmail]
    FROM [Users] u
    INNER JOIN [Organization] o ON o.[Id] = u.[OrganizationId]
    CROSS APPLY (
        SELECT CASE
            WHEN LOWER(LTRIM(RTRIM(o.[Slug]))) = N'demo-b' THEN N'demo@asset.local'
            ELSE LOWER(LTRIM(RTRIM(o.[Slug]))) + N'@asset.local'
        END AS [TargetEmail]
    ) e
    WHERE u.[Email] = N'superadmin@asset.local'
      AND u.[OrganizationId] IS NOT NULL
      AND NOT EXISTS (
          SELECT 1
          FROM [Users] x
          WHERE x.[OrganizationId] = u.[OrganizationId]
            AND LOWER(LTRIM(RTRIM(x.[Email]))) = LOWER(e.[TargetEmail])
            AND x.[Id] <> u.[Id]
      );
END
GO

IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Email] = N'superadmin@asset.local' AND [OrganizationId] IS NULL)
BEGIN
    DECLARE @passwordHash2 NVARCHAR(MAX) = N'ALJwzw5r970vW+fpNg4Ivw5nutwiP9Omge0gCdgtDVM2h6NFmycZ2GwSH5fyBqDTaw==';
    DECLARE @securityStamp2 NVARCHAR(64) = N'00000000000000000000000000000002';
    DECLARE @platformRoleId INT = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Platform Admin' AND [OrganizationId] IS NULL ORDER BY [Id]);

    IF @platformRoleId IS NOT NULL
    BEGIN
        INSERT INTO [Users]
            ([Id],[Email],[EmailConfirmed],[PasswordHash],[SecurityStamp],[PhoneNumberConfirmed],[TwoFactorEnabled],[LockoutEnabled],[AccessFailedCount],[UserName],
             [EmployeeNumber],[FirstName],[LastName],[IsActive],[RoleId],[OrganizationId],[CreatedAt])
        VALUES
            (N'seed-user-platform', N'superadmin@asset.local', 1, @passwordHash2, @securityStamp2, 0, 0, 0, 0, N'superadmin@asset.local',
             N'EMP-PLATFORM', N'Platform', N'Admin', 1, @platformRoleId, NULL, GETUTCDATE());
    END
END
ELSE
BEGIN
    UPDATE [Users]
    SET [OrganizationId] = NULL,
        [RoleId] = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Platform Admin' AND [OrganizationId] IS NULL ORDER BY [Id]),
        [IsActive] = 1,
        [PasswordHash] = N'ALJwzw5r970vW+fpNg4Ivw5nutwiP9Omge0gCdgtDVM2h6NFmycZ2GwSH5fyBqDTaw=='
    WHERE [Email] = N'superadmin@asset.local'
      AND [OrganizationId] IS NULL;
END
GO
