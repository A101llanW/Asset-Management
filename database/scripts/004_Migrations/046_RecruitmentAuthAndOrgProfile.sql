-- Recruitment auth hardening: session tokens, email verification, org branding

IF COL_LENGTH(N'dbo.Users', N'AccessToken') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD AccessToken NVARCHAR(64) NULL;
END
GO

IF COL_LENGTH(N'dbo.Users', N'RequirePasswordChange') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD RequirePasswordChange BIT NOT NULL CONSTRAINT DF_Users_RequirePasswordChange DEFAULT(0);
END
GO

IF COL_LENGTH(N'dbo.Users', N'LastPasswordChange') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD LastPasswordChange DATETIME NULL;
END
GO

IF COL_LENGTH(N'dbo.Users', N'IsEmailVerified') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD IsEmailVerified BIT NOT NULL CONSTRAINT DF_Users_IsEmailVerified DEFAULT(0);
END
GO

IF COL_LENGTH(N'dbo.Users', N'EmailVerificationCode') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD EmailVerificationCode NVARCHAR(10) NULL;
END
GO

IF COL_LENGTH(N'dbo.Users', N'EmailVerificationExpiryUtc') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD EmailVerificationExpiryUtc DATETIME NULL;
END
GO

IF COL_LENGTH(N'dbo.Organization', N'AccessToken') IS NULL
BEGIN
    ALTER TABLE dbo.Organization ADD AccessToken NVARCHAR(64) NULL;
END
GO

IF COL_LENGTH(N'dbo.Organization', N'LogoPath') IS NULL
BEGIN
    ALTER TABLE dbo.Organization ADD LogoPath NVARCHAR(500) NULL;
END
GO

-- Backfill user access tokens
UPDATE u
SET u.AccessToken = LOWER(REPLACE(CONVERT(NVARCHAR(36), NEWID()), N'-', N''))
FROM dbo.Users u
WHERE u.AccessToken IS NULL OR LTRIM(RTRIM(u.AccessToken)) = N'';
GO

-- Backfill organization access tokens
UPDATE o
SET o.AccessToken = UPPER(SUBSTRING(REPLACE(CONVERT(NVARCHAR(36), NEWID()), N'-', N''), 1, 8))
FROM dbo.Organization o
WHERE o.AccessToken IS NULL OR LTRIM(RTRIM(o.AccessToken)) = N'';
GO

-- Existing active users are treated as email-verified
UPDATE dbo.Users
SET IsEmailVerified = 1
WHERE IsActive = 1 AND IsEmailVerified = 0;
GO
