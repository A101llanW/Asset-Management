-- Org purge performance + AuditLog platform (null-org) support for post-purge ORGANIZATION_DELETED audit.
-- Idempotent IF NOT EXISTS / IF COL style consistent with 008_PerformanceIndexes.sql / 076.

-- ===== Allow platform-scoped AuditLog rows (OrganizationId NULL) after org hard-delete =====
-- Entity + AuditWriter already treat OrganizationId as nullable; multitenancy backfill left the column NOT NULL.
IF OBJECT_ID(N'[AuditLog]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AuditLog]', N'OrganizationId') IS NOT NULL
   AND EXISTS (
       SELECT 1
       FROM sys.columns c
       WHERE c.object_id = OBJECT_ID(N'[AuditLog]')
         AND c.name = N'OrganizationId'
         AND c.is_nullable = 0
   )
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AuditLog_Organization' AND parent_object_id = OBJECT_ID(N'[AuditLog]'))
    BEGIN
        ALTER TABLE [AuditLog] DROP CONSTRAINT [FK_AuditLog_Organization];
    END

    ALTER TABLE [AuditLog] ALTER COLUMN [OrganizationId] INT NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AuditLog_Organization' AND parent_object_id = OBJECT_ID(N'[AuditLog]'))
    BEGIN
        ALTER TABLE [AuditLog] WITH CHECK ADD CONSTRAINT [FK_AuditLog_Organization]
            FOREIGN KEY ([OrganizationId]) REFERENCES [Organization]([Id]);
    END
END
GO

-- Org purge performance: FK/lookup indexes used when deleting parent rows and tenant-scoped DELETE WHERE OrganizationId = @OrgId.
-- Idempotent IF NOT EXISTS style (see 008_PerformanceIndexes.sql).

-- ===== Asset FK lookup indexes (parent deletes: Department / AssetType / Category / AssetSubType) =====
IF OBJECT_ID(N'[Asset]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[Asset]', N'DepartmentId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Asset_DepartmentId' AND object_id = OBJECT_ID(N'[Asset]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Asset_DepartmentId] ON [Asset]([DepartmentId]);
END
GO

IF OBJECT_ID(N'[Asset]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[Asset]', N'AssetTypeId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Asset_AssetTypeId' AND object_id = OBJECT_ID(N'[Asset]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Asset_AssetTypeId] ON [Asset]([AssetTypeId]);
END
GO

IF OBJECT_ID(N'[Asset]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[Asset]', N'CategoryId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Asset_CategoryId' AND object_id = OBJECT_ID(N'[Asset]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Asset_CategoryId] ON [Asset]([CategoryId]);
END
GO

IF OBJECT_ID(N'[Asset]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[Asset]', N'AssetSubTypeId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Asset_AssetSubTypeId' AND object_id = OBJECT_ID(N'[Asset]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Asset_AssetSubTypeId] ON [Asset]([AssetSubTypeId]);
END
GO

-- ===== Taxonomy / department hierarchy =====
IF OBJECT_ID(N'[AssetType]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AssetType]', N'AssetCategoryId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetType_AssetCategoryId' AND object_id = OBJECT_ID(N'[AssetType]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AssetType_AssetCategoryId] ON [AssetType]([AssetCategoryId]);
END
GO

IF OBJECT_ID(N'[AssetSubType]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AssetSubType]', N'AssetTypeId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetSubType_AssetTypeId' AND object_id = OBJECT_ID(N'[AssetSubType]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AssetSubType_AssetTypeId] ON [AssetSubType]([AssetTypeId]);
END
GO

IF OBJECT_ID(N'[Department]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[Department]', N'ParentDepartmentId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Department_ParentDepartmentId' AND object_id = OBJECT_ID(N'[Department]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Department_ParentDepartmentId] ON [Department]([ParentDepartmentId]);
END
GO

-- ===== Other common FKs to Asset / Department touched during purge =====
IF OBJECT_ID(N'[AssetAssignment]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AssetAssignment]', N'AssetId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetAssignment_AssetId' AND object_id = OBJECT_ID(N'[AssetAssignment]'))
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetAssignment_Asset_AssignedDate' AND object_id = OBJECT_ID(N'[AssetAssignment]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AssetAssignment_AssetId] ON [AssetAssignment]([AssetId]);
END
GO

IF OBJECT_ID(N'[AssetCustodyEvent]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AssetCustodyEvent]', N'AssetId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetCustodyEvent_AssetId' AND object_id = OBJECT_ID(N'[AssetCustodyEvent]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AssetCustodyEvent_AssetId] ON [AssetCustodyEvent]([AssetId]);
END
GO

IF OBJECT_ID(N'[AssetDocument]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AssetDocument]', N'AssetId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetDocument_AssetId' AND object_id = OBJECT_ID(N'[AssetDocument]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AssetDocument_AssetId] ON [AssetDocument]([AssetId]);
END
GO

IF OBJECT_ID(N'[AssetTransfer]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AssetTransfer]', N'AssetId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetTransfer_AssetId' AND object_id = OBJECT_ID(N'[AssetTransfer]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AssetTransfer_AssetId] ON [AssetTransfer]([AssetId]);
END
GO

IF OBJECT_ID(N'[DisposalRecord]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[DisposalRecord]', N'AssetId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DisposalRecord_AssetId' AND object_id = OBJECT_ID(N'[DisposalRecord]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DisposalRecord_AssetId] ON [DisposalRecord]([AssetId]);
END
GO

IF OBJECT_ID(N'[AssetMaintenanceRecord]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AssetMaintenanceRecord]', N'AssetId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetMaintenanceRecord_AssetId' AND object_id = OBJECT_ID(N'[AssetMaintenanceRecord]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AssetMaintenanceRecord_AssetId] ON [AssetMaintenanceRecord]([AssetId]);
END
GO

IF OBJECT_ID(N'[AssetIncident]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AssetIncident]', N'AssetId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetIncident_AssetId' AND object_id = OBJECT_ID(N'[AssetIncident]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AssetIncident_AssetId] ON [AssetIncident]([AssetId]);
END
GO

IF OBJECT_ID(N'[AssetReturn]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AssetReturn]', N'AssetId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetReturn_AssetId' AND object_id = OBJECT_ID(N'[AssetReturn]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AssetReturn_AssetId] ON [AssetReturn]([AssetId]);
END
GO

IF OBJECT_ID(N'[DepreciationRecord]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[DepreciationRecord]', N'AssetId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DepreciationRecord_AssetId' AND object_id = OBJECT_ID(N'[DepreciationRecord]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DepreciationRecord_AssetId] ON [DepreciationRecord]([AssetId]);
END
GO

IF OBJECT_ID(N'[Users]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[Users]', N'DepartmentId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_DepartmentId' AND object_id = OBJECT_ID(N'[Users]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Users_DepartmentId] ON [Users]([DepartmentId]);
END
GO

IF OBJECT_ID(N'[PurchaseRequest]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[PurchaseRequest]', N'DepartmentId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PurchaseRequest_DepartmentId' AND object_id = OBJECT_ID(N'[PurchaseRequest]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PurchaseRequest_DepartmentId] ON [PurchaseRequest]([DepartmentId]);
END
GO

-- ===== OrganizationId leading indexes for purge DELETE WHERE OrganizationId = @OrgId =====
-- Helper pattern: only create if table/column exist and no index already leads with OrganizationId.
IF OBJECT_ID(N'[ImpersonationRequest]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[ImpersonationRequest]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[ImpersonationRequest]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ImpersonationRequest_OrganizationId' AND object_id = OBJECT_ID(N'[ImpersonationRequest]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ImpersonationRequest_OrganizationId] ON [ImpersonationRequest]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[LoginAttempts]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[LoginAttempts]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[LoginAttempts]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LoginAttempts_OrganizationId' AND object_id = OBJECT_ID(N'[LoginAttempts]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_LoginAttempts_OrganizationId] ON [LoginAttempts]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[SecurityEvents]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[SecurityEvents]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[SecurityEvents]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SecurityEvents_OrganizationId' AND object_id = OBJECT_ID(N'[SecurityEvents]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SecurityEvents_OrganizationId] ON [SecurityEvents]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[Notification]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[Notification]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[Notification]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notification_OrganizationId' AND object_id = OBJECT_ID(N'[Notification]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Notification_OrganizationId] ON [Notification]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[OutboxMessage]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[OutboxMessage]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[OutboxMessage]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OutboxMessage_OrganizationId' AND object_id = OBJECT_ID(N'[OutboxMessage]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OutboxMessage_OrganizationId] ON [OutboxMessage]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[AuditLog]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AuditLog]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[AuditLog]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLog_OrganizationId' AND object_id = OBJECT_ID(N'[AuditLog]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditLog_OrganizationId] ON [AuditLog]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[WebhookDelivery]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[WebhookDelivery]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[WebhookDelivery]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WebhookDelivery_OrganizationId' AND object_id = OBJECT_ID(N'[WebhookDelivery]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_WebhookDelivery_OrganizationId] ON [WebhookDelivery]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[WebhookSubscription]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[WebhookSubscription]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[WebhookSubscription]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WebhookSubscription_OrganizationId' AND object_id = OBJECT_ID(N'[WebhookSubscription]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_WebhookSubscription_OrganizationId] ON [WebhookSubscription]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[OrganizationLicenseHistory]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[OrganizationLicenseHistory]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[OrganizationLicenseHistory]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OrganizationLicenseHistory_OrganizationId' AND object_id = OBJECT_ID(N'[OrganizationLicenseHistory]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationLicenseHistory_OrganizationId] ON [OrganizationLicenseHistory]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[OrganizationLicense]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[OrganizationLicense]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[OrganizationLicense]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OrganizationLicense_OrganizationId' AND object_id = OBJECT_ID(N'[OrganizationLicense]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationLicense_OrganizationId] ON [OrganizationLicense]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[RoleTemplate]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[RoleTemplate]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[RoleTemplate]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RoleTemplate_OrganizationId' AND object_id = OBJECT_ID(N'[RoleTemplate]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_RoleTemplate_OrganizationId] ON [RoleTemplate]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[AssetDocumentRequirement]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AssetDocumentRequirement]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[AssetDocumentRequirement]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetDocumentRequirement_OrganizationId' AND object_id = OBJECT_ID(N'[AssetDocumentRequirement]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AssetDocumentRequirement_OrganizationId] ON [AssetDocumentRequirement]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[AssetDocument]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AssetDocument]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[AssetDocument]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetDocument_OrganizationId' AND object_id = OBJECT_ID(N'[AssetDocument]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AssetDocument_OrganizationId] ON [AssetDocument]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[InsuranceClaim]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[InsuranceClaim]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[InsuranceClaim]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InsuranceClaim_OrganizationId' AND object_id = OBJECT_ID(N'[InsuranceClaim]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_InsuranceClaim_OrganizationId] ON [InsuranceClaim]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[InsurancePolicy]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[InsurancePolicy]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[InsurancePolicy]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InsurancePolicy_OrganizationId' AND object_id = OBJECT_ID(N'[InsurancePolicy]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_InsurancePolicy_OrganizationId] ON [InsurancePolicy]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[DepreciationRecord]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[DepreciationRecord]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[DepreciationRecord]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DepreciationRecord_OrganizationId' AND object_id = OBJECT_ID(N'[DepreciationRecord]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DepreciationRecord_OrganizationId] ON [DepreciationRecord]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[DisposalApprovalAction]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[DisposalApprovalAction]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[DisposalApprovalAction]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DisposalApprovalAction_OrganizationId' AND object_id = OBJECT_ID(N'[DisposalApprovalAction]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DisposalApprovalAction_OrganizationId] ON [DisposalApprovalAction]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[DisposalRecord]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[DisposalRecord]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[DisposalRecord]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DisposalRecord_OrganizationId' AND object_id = OBJECT_ID(N'[DisposalRecord]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DisposalRecord_OrganizationId] ON [DisposalRecord]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[TransferApprovalAction]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[TransferApprovalAction]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[TransferApprovalAction]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TransferApprovalAction_OrganizationId' AND object_id = OBJECT_ID(N'[TransferApprovalAction]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TransferApprovalAction_OrganizationId] ON [TransferApprovalAction]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[AssetTransfer]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AssetTransfer]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[AssetTransfer]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetTransfer_OrganizationId' AND object_id = OBJECT_ID(N'[AssetTransfer]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AssetTransfer_OrganizationId] ON [AssetTransfer]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[AssetReturn]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AssetReturn]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[AssetReturn]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetReturn_OrganizationId' AND object_id = OBJECT_ID(N'[AssetReturn]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AssetReturn_OrganizationId] ON [AssetReturn]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[AssetCustodyEvent]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AssetCustodyEvent]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[AssetCustodyEvent]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetCustodyEvent_OrganizationId' AND object_id = OBJECT_ID(N'[AssetCustodyEvent]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AssetCustodyEvent_OrganizationId] ON [AssetCustodyEvent]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[AssetMaintenanceRecord]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AssetMaintenanceRecord]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[AssetMaintenanceRecord]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetMaintenanceRecord_OrganizationId' AND object_id = OBJECT_ID(N'[AssetMaintenanceRecord]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AssetMaintenanceRecord_OrganizationId] ON [AssetMaintenanceRecord]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[AssetIncident]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AssetIncident]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[AssetIncident]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetIncident_OrganizationId' AND object_id = OBJECT_ID(N'[AssetIncident]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AssetIncident_OrganizationId] ON [AssetIncident]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[AssetAssignment]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AssetAssignment]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[AssetAssignment]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetAssignment_OrganizationId' AND object_id = OBJECT_ID(N'[AssetAssignment]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AssetAssignment_OrganizationId] ON [AssetAssignment]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[AssetReceiving]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AssetReceiving]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[AssetReceiving]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetReceiving_OrganizationId' AND object_id = OBJECT_ID(N'[AssetReceiving]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AssetReceiving_OrganizationId] ON [AssetReceiving]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[PurchaseRecord]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[PurchaseRecord]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[PurchaseRecord]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PurchaseRecord_OrganizationId' AND object_id = OBJECT_ID(N'[PurchaseRecord]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PurchaseRecord_OrganizationId] ON [PurchaseRecord]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[PurchaseApprovalAction]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[PurchaseApprovalAction]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[PurchaseApprovalAction]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PurchaseApprovalAction_OrganizationId' AND object_id = OBJECT_ID(N'[PurchaseApprovalAction]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PurchaseApprovalAction_OrganizationId] ON [PurchaseApprovalAction]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[PurchaseRequest]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[PurchaseRequest]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[PurchaseRequest]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PurchaseRequest_OrganizationId' AND object_id = OBJECT_ID(N'[PurchaseRequest]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PurchaseRequest_OrganizationId] ON [PurchaseRequest]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[AssetRequest]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AssetRequest]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[AssetRequest]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetRequest_OrganizationId' AND object_id = OBJECT_ID(N'[AssetRequest]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AssetRequest_OrganizationId] ON [AssetRequest]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[SupplierCatalogItem]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[SupplierCatalogItem]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[SupplierCatalogItem]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SupplierCatalogItem_OrganizationId' AND object_id = OBJECT_ID(N'[SupplierCatalogItem]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SupplierCatalogItem_OrganizationId] ON [SupplierCatalogItem]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[Asset]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[Asset]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[Asset]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Asset_OrganizationId' AND object_id = OBJECT_ID(N'[Asset]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Asset_OrganizationId] ON [Asset]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[AssetSubType]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AssetSubType]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[AssetSubType]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetSubType_OrganizationId' AND object_id = OBJECT_ID(N'[AssetSubType]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AssetSubType_OrganizationId] ON [AssetSubType]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[AssetType]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AssetType]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[AssetType]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetType_OrganizationId' AND object_id = OBJECT_ID(N'[AssetType]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AssetType_OrganizationId] ON [AssetType]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[AssetCategory]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AssetCategory]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[AssetCategory]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssetCategory_OrganizationId' AND object_id = OBJECT_ID(N'[AssetCategory]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AssetCategory_OrganizationId] ON [AssetCategory]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[Supplier]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[Supplier]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[Supplier]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Supplier_OrganizationId' AND object_id = OBJECT_ID(N'[Supplier]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Supplier_OrganizationId] ON [Supplier]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[Users]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[Users]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[Users]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_OrganizationId' AND object_id = OBJECT_ID(N'[Users]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Users_OrganizationId] ON [Users]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[RolePermission]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[RolePermission]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[RolePermission]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RolePermission_OrganizationId' AND object_id = OBJECT_ID(N'[RolePermission]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_RolePermission_OrganizationId] ON [RolePermission]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[Roles]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[Roles]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[Roles]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Roles_OrganizationId' AND object_id = OBJECT_ID(N'[Roles]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Roles_OrganizationId] ON [Roles]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[Department]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[Department]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[Department]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Department_OrganizationId' AND object_id = OBJECT_ID(N'[Department]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Department_OrganizationId] ON [Department]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[SystemSetting]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[SystemSetting]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[SystemSetting]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SystemSetting_OrganizationId' AND object_id = OBJECT_ID(N'[SystemSetting]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SystemSetting_OrganizationId] ON [SystemSetting]([OrganizationId]);
END
GO

IF OBJECT_ID(N'[UserInvitation]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[UserInvitation]', N'OrganizationId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[UserInvitation]')
         AND i.type > 0
         AND ic.key_ordinal = 1
         AND ic.is_included_column = 0
         AND c.name = N'OrganizationId'
   )
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserInvitation_OrganizationId' AND object_id = OBJECT_ID(N'[UserInvitation]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_UserInvitation_OrganizationId] ON [UserInvitation]([OrganizationId]);
END
GO
