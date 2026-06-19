-- Allow assets without a department or supplier (organization-level custody / unknown supplier).
IF COL_LENGTH(N'[Asset]', N'DepartmentId') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Asset_Department' AND parent_object_id = OBJECT_ID(N'[Asset]'))
    BEGIN
        ALTER TABLE [Asset] DROP CONSTRAINT [FK_Asset_Department];
    END

    ALTER TABLE [Asset] ALTER COLUMN [DepartmentId] INT NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Asset_Department' AND parent_object_id = OBJECT_ID(N'[Asset]'))
    BEGIN
        ALTER TABLE [Asset] ADD CONSTRAINT [FK_Asset_Department] FOREIGN KEY ([DepartmentId]) REFERENCES [Department]([Id]);
    END
END
GO

IF COL_LENGTH(N'[Asset]', N'SupplierId') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Asset_Supplier' AND parent_object_id = OBJECT_ID(N'[Asset]'))
    BEGIN
        ALTER TABLE [Asset] DROP CONSTRAINT [FK_Asset_Supplier];
    END

    ALTER TABLE [Asset] ALTER COLUMN [SupplierId] INT NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Asset_Supplier' AND parent_object_id = OBJECT_ID(N'[Asset]'))
    BEGIN
        ALTER TABLE [Asset] ADD CONSTRAINT [FK_Asset_Supplier] FOREIGN KEY ([SupplierId]) REFERENCES [Supplier]([Id]);
    END
END
GO
