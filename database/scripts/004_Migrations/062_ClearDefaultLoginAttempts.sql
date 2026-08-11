-- Clear failed login attempts after primary tenant rename (default@ -> nanosoft@).

IF OBJECT_ID(N'[LoginAttempts]', N'U') IS NOT NULL
BEGIN
    DELETE FROM [LoginAttempts]
    WHERE LOWER(LTRIM(RTRIM([Username]))) IN (N'default@asset.local', N'nanosoft@asset.local');
END
GO
