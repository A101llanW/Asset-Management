-- Backfill UpdatedAt on tables whose entities inherit AuditableEntity but were created without the column.
IF COL_LENGTH(N'[OutboxMessage]', N'UpdatedAt') IS NULL
BEGIN
    ALTER TABLE [OutboxMessage] ADD [UpdatedAt] DATETIME NULL;
END
GO

IF COL_LENGTH(N'[WebhookDelivery]', N'UpdatedAt') IS NULL
BEGIN
    ALTER TABLE [WebhookDelivery] ADD [UpdatedAt] DATETIME NULL;
END
GO

IF COL_LENGTH(N'[OrganizationLicense]', N'UpdatedAt') IS NULL
BEGIN
    ALTER TABLE [OrganizationLicense] ADD [UpdatedAt] DATETIME NULL;
END
GO
