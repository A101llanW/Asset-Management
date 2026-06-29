IF OBJECT_ID(N'[RoleTemplate]', N'U') IS NULL
BEGIN
    CREATE TABLE [RoleTemplate] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [OrganizationId] INT NULL,
        [Name] NVARCHAR(120) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [PermissionIds] NVARCHAR(2000) NULL,
        [SourceRoleId] INT NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT DF_RoleTemplate_CreatedAt DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_RoleTemplate_IsActive DEFAULT (1),
        CONSTRAINT FK_RoleTemplate_Organization FOREIGN KEY ([OrganizationId]) REFERENCES [Organization]([Id]),
        CONSTRAINT FK_RoleTemplate_SourceRole FOREIGN KEY ([SourceRoleId]) REFERENCES [Roles]([Id])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RoleTemplate_Organization_Name' AND object_id = OBJECT_ID('RoleTemplate'))
BEGIN
    CREATE UNIQUE INDEX IX_RoleTemplate_Organization_Name ON [RoleTemplate]([OrganizationId], [Name]) WHERE [IsActive] = 1;
END
GO
