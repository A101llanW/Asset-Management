IF OBJECT_ID(N'[dbo].[UserInvitation]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UserInvitation] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [TokenHash] NVARCHAR(128) NOT NULL,
        [OrganizationId] INT NOT NULL,
        [InvitedByUserId] NVARCHAR(128) NOT NULL,
        [Email] NVARCHAR(256) NULL,
        [RoleId] INT NULL,
        [DepartmentId] INT NULL,
        [ExpiresAtUtc] DATETIME NOT NULL,
        [UsedAtUtc] DATETIME NULL,
        [UsedByUserId] NVARCHAR(128) NULL,
        [CreatedAtUtc] DATETIME NOT NULL CONSTRAINT [DF_UserInvitation_CreatedAtUtc] DEFAULT (GETUTCDATE()),
        CONSTRAINT [FK_UserInvitation_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [Organization]([Id]),
        CONSTRAINT [FK_UserInvitation_Role] FOREIGN KEY ([RoleId]) REFERENCES [Roles]([Id]),
        CONSTRAINT [FK_UserInvitation_Department] FOREIGN KEY ([DepartmentId]) REFERENCES [Department]([Id])
    );

    CREATE UNIQUE INDEX [IX_UserInvitation_TokenHash] ON [dbo].[UserInvitation]([TokenHash]);
    CREATE INDEX [IX_UserInvitation_OrganizationId_CreatedAtUtc] ON [dbo].[UserInvitation]([OrganizationId], [CreatedAtUtc] DESC);
END
GO

DECLARE @now DATETIME = GETUTCDATE();

IF NOT EXISTS (SELECT 1 FROM [Permission] WHERE [Code] = N'Users.Invite')
BEGIN
    INSERT INTO [Permission] ([Name],[Code],[Module],[Description],[CreatedAt],[IsActive])
    VALUES (N'Invite Users', N'Users.Invite', N'Users', N'Can invite users to register via email link', @now, 1);
END
GO

DECLARE @permUsersInvite INT = (SELECT TOP 1 [Id] FROM [Permission] WHERE [Code] = N'Users.Invite' ORDER BY [Id]);

IF @permUsersInvite IS NOT NULL
BEGIN
    INSERT INTO [RolePermission] ([RoleId], [PermissionId], [OrganizationId])
    SELECT r.[Id], @permUsersInvite, r.[OrganizationId]
    FROM [Roles] r
    WHERE r.[Name] = N'Company Admin'
      AND NOT EXISTS (
          SELECT 1 FROM [RolePermission] rp
          WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = @permUsersInvite
            AND ((rp.[OrganizationId] IS NULL AND r.[OrganizationId] IS NULL) OR rp.[OrganizationId] = r.[OrganizationId]));
END
GO
