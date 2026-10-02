-- 082: Optional ClassTeacher / RoomCustodian on Department (Class/Room PIC).
-- Soft additive only. No wipe. Never required for Create/Edit Class/Room.

IF COL_LENGTH(N'[Department]', N'ClassTeacherUserId') IS NULL
BEGIN
    ALTER TABLE [Department]
        ADD [ClassTeacherUserId] NVARCHAR(128) NULL;
END
GO

IF COL_LENGTH(N'[Department]', N'RoomCustodianUserId') IS NULL
BEGIN
    ALTER TABLE [Department]
        ADD [RoomCustodianUserId] NVARCHAR(128) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Department_ClassTeacherUser'
)
BEGIN
    ALTER TABLE [Department] WITH NOCHECK
        ADD CONSTRAINT [FK_Department_ClassTeacherUser]
        FOREIGN KEY ([ClassTeacherUserId]) REFERENCES [Users]([Id]);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Department_RoomCustodianUser'
)
BEGIN
    ALTER TABLE [Department] WITH NOCHECK
        ADD CONSTRAINT [FK_Department_RoomCustodianUser]
        FOREIGN KEY ([RoomCustodianUserId]) REFERENCES [Users]([Id]);
END
GO
