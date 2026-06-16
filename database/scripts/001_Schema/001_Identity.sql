-- Identity and user profile tables
IF OBJECT_ID(N'[Users]', N'U') IS NULL
BEGIN
    CREATE TABLE [Users] (
        [Id] NVARCHAR(128) NOT NULL PRIMARY KEY,
        [Email] NVARCHAR(256) NULL,
        [EmailConfirmed] BIT NOT NULL CONSTRAINT DF_Users_EmailConfirmed DEFAULT(0),
        [PasswordHash] NVARCHAR(MAX) NULL,
        [SecurityStamp] NVARCHAR(MAX) NULL,
        [PhoneNumber] NVARCHAR(MAX) NULL,
        [PhoneNumberConfirmed] BIT NOT NULL CONSTRAINT DF_Users_PhoneNumberConfirmed DEFAULT(0),
        [TwoFactorEnabled] BIT NOT NULL CONSTRAINT DF_Users_TwoFactorEnabled DEFAULT(0),
        [LockoutEndDateUtc] DATETIME NULL,
        [LockoutEnabled] BIT NOT NULL CONSTRAINT DF_Users_LockoutEnabled DEFAULT(0),
        [AccessFailedCount] INT NOT NULL CONSTRAINT DF_Users_AccessFailedCount DEFAULT(0),
        [UserName] NVARCHAR(256) NULL,
        [EmployeeNumber] NVARCHAR(50) NULL,
        [FirstName] NVARCHAR(100) NULL,
        [LastName] NVARCHAR(100) NULL,
        [Phone] NVARCHAR(50) NULL,
        [DepartmentId] INT NULL,
        [PositionTitle] NVARCHAR(200) NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT(1),
        [RoleId] INT NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL
    );
END
GO

IF OBJECT_ID(N'[IdentityRoles]', N'U') IS NULL
BEGIN
    CREATE TABLE [IdentityRoles] (
        [Id] NVARCHAR(128) NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(256) NOT NULL
    );
END
GO

IF OBJECT_ID(N'[IdentityUserRoles]', N'U') IS NULL
BEGIN
    CREATE TABLE [IdentityUserRoles] (
        [UserId] NVARCHAR(128) NOT NULL,
        [RoleId] NVARCHAR(128) NOT NULL,
        CONSTRAINT PK_IdentityUserRoles PRIMARY KEY ([UserId], [RoleId])
    );
END
GO

IF OBJECT_ID(N'[IdentityUserLogins]', N'U') IS NULL
BEGIN
    CREATE TABLE [IdentityUserLogins] (
        [LoginProvider] NVARCHAR(128) NOT NULL,
        [ProviderKey] NVARCHAR(128) NOT NULL,
        [UserId] NVARCHAR(128) NOT NULL,
        CONSTRAINT PK_IdentityUserLogins PRIMARY KEY ([LoginProvider], [ProviderKey], [UserId])
    );
END
GO

IF OBJECT_ID(N'[IdentityUserClaims]', N'U') IS NULL
BEGIN
    CREATE TABLE [IdentityUserClaims] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [UserId] NVARCHAR(128) NOT NULL,
        [ClaimType] NVARCHAR(MAX) NULL,
        [ClaimValue] NVARCHAR(MAX) NULL
    );
END
GO
