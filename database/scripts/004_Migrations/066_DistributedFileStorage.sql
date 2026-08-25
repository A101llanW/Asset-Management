IF OBJECT_ID(N'[StoredFile]', N'U') IS NULL
BEGIN
    CREATE TABLE [StoredFile] (
        [StorageKey] NVARCHAR(200) NOT NULL CONSTRAINT [PK_StoredFile] PRIMARY KEY,
        [OriginalFileName] NVARCHAR(260) NULL,
        [ContentType] NVARCHAR(200) NULL,
        [Content] VARBINARY(MAX) NOT NULL,
        [CreatedAtUtc] DATETIME NOT NULL CONSTRAINT [DF_StoredFile_CreatedAtUtc] DEFAULT(GETUTCDATE())
    );
END
