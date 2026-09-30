IF OBJECT_ID(N'[PurchaseRequestLine]', N'U') IS NULL
BEGIN
    CREATE TABLE [PurchaseRequestLine] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [OrganizationId] INT NULL,
        [PurchaseRequestId] INT NOT NULL,
        [LineNumber] INT NOT NULL,
        [Description] NVARCHAR(2000) NOT NULL,
        [Quantity] INT NOT NULL,
        [CreatedAt] DATETIME NOT NULL CONSTRAINT [DF_PurchaseRequestLine_CreatedAt] DEFAULT (GETUTCDATE()),
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_PurchaseRequestLine_IsActive] DEFAULT (1),
        CONSTRAINT [FK_PurchaseRequestLine_PurchaseRequest] FOREIGN KEY ([PurchaseRequestId]) REFERENCES [PurchaseRequest]([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_PurchaseRequestLine_PurchaseRequestId]
        ON [PurchaseRequestLine] ([PurchaseRequestId], [LineNumber]);
END
GO
