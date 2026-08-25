IF COL_LENGTH(N'[Supplier]', N'TaxId') IS NULL
    ALTER TABLE [Supplier] ADD [TaxId] NVARCHAR(50) NULL;
GO

IF COL_LENGTH(N'[Supplier]', N'PaymentTerms') IS NULL
    ALTER TABLE [Supplier] ADD [PaymentTerms] NVARCHAR(100) NULL;
GO

IF COL_LENGTH(N'[Supplier]', N'DefaultLeadTimeDays') IS NULL
    ALTER TABLE [Supplier] ADD [DefaultLeadTimeDays] INT NULL;
GO

IF COL_LENGTH(N'[Supplier]', N'Website') IS NULL
    ALTER TABLE [Supplier] ADD [Website] NVARCHAR(300) NULL;
GO

IF COL_LENGTH(N'[Supplier]', N'IsPreferred') IS NULL
    ALTER TABLE [Supplier] ADD [IsPreferred] BIT NOT NULL CONSTRAINT DF_Supplier_IsPreferred DEFAULT(0);
GO

IF COL_LENGTH(N'[Supplier]', N'Country') IS NULL
    ALTER TABLE [Supplier] ADD [Country] NVARCHAR(100) NULL;
GO

IF COL_LENGTH(N'[Supplier]', N'PaymentInstructions') IS NULL
    ALTER TABLE [Supplier] ADD [PaymentInstructions] NVARCHAR(MAX) NULL;
GO
