-- Process-linked document requirements (e.g. incident damage photos)
IF OBJECT_ID(N'[AssetDocumentRequirement]', N'U') IS NULL
BEGIN
    CREATE TABLE [AssetDocumentRequirement] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [OrganizationId] INT NULL,
        [AssetId] INT NOT NULL,
        [ProcessType] NVARCHAR(50) NOT NULL,
        [ProcessId] INT NOT NULL,
        [DocumentType] NVARCHAR(100) NOT NULL,
        [Label] NVARCHAR(200) NULL,
        [DocumentId] INT NULL,
        [CreatedAt] DATETIME NOT NULL,
        [FulfilledAt] DATETIME NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_AssetDocumentRequirement_IsActive DEFAULT(1),
        CONSTRAINT FK_AssetDocumentRequirement_Asset FOREIGN KEY ([AssetId]) REFERENCES [Asset]([Id]),
        CONSTRAINT FK_AssetDocumentRequirement_Document FOREIGN KEY ([DocumentId]) REFERENCES [AssetDocument]([Id])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetDocumentRequirement_Asset_Process' AND object_id = OBJECT_ID(N'[AssetDocumentRequirement]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_AssetDocumentRequirement_Asset_Process
        ON [AssetDocumentRequirement]([AssetId], [ProcessType], [ProcessId])
        WHERE [IsActive] = 1;
END
GO

IF COL_LENGTH(N'[AssetDocument]', N'ProcessType') IS NULL
    ALTER TABLE [AssetDocument] ADD [ProcessType] NVARCHAR(50) NULL;
GO

IF COL_LENGTH(N'[AssetDocument]', N'ProcessId') IS NULL
    ALTER TABLE [AssetDocument] ADD [ProcessId] INT NULL;
GO

IF COL_LENGTH(N'[AssetDocument]', N'RequirementId') IS NULL
    ALTER TABLE [AssetDocument] ADD [RequirementId] INT NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE [name] = N'FK_AssetDocument_Requirement'
      AND [parent_object_id] = OBJECT_ID(N'[AssetDocument]'))
BEGIN
    ALTER TABLE [AssetDocument]
        ADD CONSTRAINT FK_AssetDocument_Requirement
        FOREIGN KEY ([RequirementId]) REFERENCES [AssetDocumentRequirement]([Id]);
END
GO
