namespace AssetManagement.Tests.Organizations
{
    /// <summary>
    /// Checked-in snapshot of every foreign key in AssetManagementModuleDb (taken 2026-10-08 from a
    /// GrokScratch copy). OrganizationPurgeSchemaTests fails when the live schema drifts from this list,
    /// and OrganizationPurgeForeignKeyTests checks the purge plan against it without a database.
    /// </summary>
    public static class PurgeForeignKeySnapshot
    {
        public sealed class ForeignKeyEdge
        {
            public ForeignKeyEdge(string name, string child, string column, string parent, string deleteAction)
            {
                Name = name;
                Child = child;
                Column = column;
                Parent = parent;
                DeleteAction = deleteAction;
            }

            public string Name { get; private set; }
            public string Child { get; private set; }
            public string Column { get; private set; }
            public string Parent { get; private set; }
            public string DeleteAction { get; private set; }
        }

        public static readonly ForeignKeyEdge[] Edges =
        {
            new ForeignKeyEdge("FK_Asset_AssetSubType", "Asset", "AssetSubTypeId", "AssetSubType", "NO_ACTION"),
            new ForeignKeyEdge("FK_Asset_AssetType", "Asset", "AssetTypeId", "AssetType", "NO_ACTION"),
            new ForeignKeyEdge("FK_Asset_Category", "Asset", "CategoryId", "AssetCategory", "NO_ACTION"),
            new ForeignKeyEdge("FK_Asset_Department", "Asset", "DepartmentId", "Department", "NO_ACTION"),
            new ForeignKeyEdge("FK_Asset_Organization", "Asset", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_Asset_Supplier", "Asset", "SupplierId", "Supplier", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetAssignment_Asset", "AssetAssignment", "AssetId", "Asset", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetAssignment_Organization", "AssetAssignment", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetAssignment_Department", "AssetAssignment", "ToDepartmentId", "Department", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetCategory_Organization", "AssetCategory", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetMovement_Asset", "AssetMovement", "AssetId", "Asset", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetMovement_Organization", "AssetMovement", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetMovement_FromDepartment", "AssetMovement", "FromDepartmentId", "Department", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetMovement_ToDepartment", "AssetMovement", "ToDepartmentId", "Department", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetCustodyEvent_Asset", "AssetCustodyEvent", "AssetId", "Asset", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetCustodyEvent_Organization", "AssetCustodyEvent", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetDocument_Asset", "AssetDocument", "AssetId", "Asset", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetDocument_Organization", "AssetDocument", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetDocument_Requirement", "AssetDocument", "RequirementId", "AssetDocumentRequirement", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetDocumentRequirement_Asset", "AssetDocumentRequirement", "AssetId", "Asset", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetDocumentRequirement_Document", "AssetDocumentRequirement", "DocumentId", "AssetDocument", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetIncident_Asset", "AssetIncident", "AssetId", "Asset", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetIncident_Organization", "AssetIncident", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetMaintenanceRecord_Asset", "AssetMaintenanceRecord", "AssetId", "Asset", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetMaintenanceRecord_Organization", "AssetMaintenanceRecord", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetReceiving_Asset", "AssetReceiving", "AssetId", "Asset", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetReceiving_Organization", "AssetReceiving", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetReceiving_PurchaseRecord", "AssetReceiving", "PurchaseRecordId", "PurchaseRecord", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetRequest_Category", "AssetRequest", "CategoryId", "AssetCategory", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetRequest_Department", "AssetRequest", "DepartmentId", "Department", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetRequest_FulfilledAsset", "AssetRequest", "FulfilledAssetId", "Asset", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetRequest_Organization", "AssetRequest", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetRequest_RequestedAsset", "AssetRequest", "RequestedAssetId", "Asset", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetReturn_Asset", "AssetReturn", "AssetId", "Asset", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetReturn_Organization", "AssetReturn", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetSubType_AssetType", "AssetSubType", "AssetTypeId", "AssetType", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetTransfer_Asset", "AssetTransfer", "AssetId", "Asset", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetTransfer_FromDepartment", "AssetTransfer", "FromDepartmentId", "Department", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetTransfer_Organization", "AssetTransfer", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetTransfer_ToDepartment", "AssetTransfer", "ToDepartmentId", "Department", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetType_AssetCategory", "AssetType", "AssetCategoryId", "AssetCategory", "NO_ACTION"),
            new ForeignKeyEdge("FK_AssetType_Organization", "AssetType", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_AuditLog_Organization", "AuditLog", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_Department_ClassTeacherUser", "Department", "ClassTeacherUserId", "Users", "NO_ACTION"),
            new ForeignKeyEdge("FK_Department_Organization", "Department", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_Department_ParentDepartment", "Department", "ParentDepartmentId", "Department", "NO_ACTION"),
            new ForeignKeyEdge("FK_Department_RoomCustodianUser", "Department", "RoomCustodianUserId", "Users", "NO_ACTION"),
            new ForeignKeyEdge("FK_DepartmentApprovalStage_Department", "DepartmentApprovalStage", "DepartmentId", "Department", "NO_ACTION"),
            new ForeignKeyEdge("FK_DepreciationRecord_Asset", "DepreciationRecord", "AssetId", "Asset", "NO_ACTION"),
            new ForeignKeyEdge("FK_DepreciationRecord_Organization", "DepreciationRecord", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_DisposalApprovalAction_DisposalRecord", "DisposalApprovalAction", "DisposalRecordId", "DisposalRecord", "NO_ACTION"),
            new ForeignKeyEdge("FK_DisposalApprovalAction_Organization", "DisposalApprovalAction", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_DisposalRecord_Asset", "DisposalRecord", "AssetId", "Asset", "NO_ACTION"),
            new ForeignKeyEdge("FK_DisposalRecord_Organization", "DisposalRecord", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_ImpersonationRequest_Organization", "ImpersonationRequest", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_InsuranceClaim_Asset", "InsuranceClaim", "AssetId", "Asset", "NO_ACTION"),
            new ForeignKeyEdge("FK_InsuranceClaim_Incident", "InsuranceClaim", "IncidentId", "AssetIncident", "NO_ACTION"),
            new ForeignKeyEdge("FK_InsuranceClaim_Organization", "InsuranceClaim", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_InsurancePolicy_Asset", "InsurancePolicy", "AssetId", "Asset", "NO_ACTION"),
            new ForeignKeyEdge("FK_InsurancePolicy_Organization", "InsurancePolicy", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_Notification_Organization", "Notification", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_OrganizationLicense_Organization", "OrganizationLicense", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_OrganizationLicenseHistory_Organization", "OrganizationLicenseHistory", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_OrganizationLicenseHistory_License", "OrganizationLicenseHistory", "OrganizationLicenseId", "OrganizationLicense", "NO_ACTION"),
            new ForeignKeyEdge("FK_PasswordResetToken_User", "PasswordResetToken", "UserId", "Users", "CASCADE"),
            new ForeignKeyEdge("FK_PurchaseApprovalAction_Organization", "PurchaseApprovalAction", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_PurchaseApprovalAction_PurchaseRequest", "PurchaseApprovalAction", "PurchaseRequestId", "PurchaseRequest", "NO_ACTION"),
            new ForeignKeyEdge("FK_PurchaseOrderLine_AssetCategory", "PurchaseOrderLine", "AssetCategoryId", "AssetCategory", "NO_ACTION"),
            new ForeignKeyEdge("FK_PurchaseOrderLine_AssetSubType", "PurchaseOrderLine", "AssetSubTypeId", "AssetSubType", "NO_ACTION"),
            new ForeignKeyEdge("FK_PurchaseOrderLine_AssetType", "PurchaseOrderLine", "AssetTypeId", "AssetType", "NO_ACTION"),
            new ForeignKeyEdge("FK_PurchaseOrderLine_PurchaseRecord", "PurchaseOrderLine", "PurchaseRecordId", "PurchaseRecord", "NO_ACTION"),
            new ForeignKeyEdge("FK_PurchaseOrderLine_PurchaseRequestLine", "PurchaseOrderLine", "PurchaseRequestLineId", "PurchaseRequestLine", "NO_ACTION"),
            new ForeignKeyEdge("FK_PurchaseOrderLineReceipt_Asset", "PurchaseOrderLineReceipt", "AssetId", "Asset", "NO_ACTION"),
            new ForeignKeyEdge("FK_PurchaseOrderLineReceipt_AssetReceiving", "PurchaseOrderLineReceipt", "AssetReceivingId", "AssetReceiving", "NO_ACTION"),
            new ForeignKeyEdge("FK_PurchaseOrderLineReceipt_Line", "PurchaseOrderLineReceipt", "PurchaseOrderLineId", "PurchaseOrderLine", "NO_ACTION"),
            new ForeignKeyEdge("FK_PurchaseOrderLineReceipt_PurchaseRecord", "PurchaseOrderLineReceipt", "PurchaseRecordId", "PurchaseRecord", "NO_ACTION"),
            new ForeignKeyEdge("FK_PurchaseRecord_Organization", "PurchaseRecord", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_PurchaseRecord_PurchaseRequest", "PurchaseRecord", "PurchaseRequestId", "PurchaseRequest", "NO_ACTION"),
            new ForeignKeyEdge("FK_PurchaseRecord_Supplier", "PurchaseRecord", "SupplierId", "Supplier", "NO_ACTION"),
            new ForeignKeyEdge("FK_PurchaseRequest_Department", "PurchaseRequest", "DepartmentId", "Department", "NO_ACTION"),
            new ForeignKeyEdge("FK_PurchaseRequest_Organization", "PurchaseRequest", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_PurchaseRequest_TargetAsset", "PurchaseRequest", "TargetAssetId", "Asset", "NO_ACTION"),
            new ForeignKeyEdge("FK_PurchaseRequestLine_PurchaseRequest", "PurchaseRequestLine", "PurchaseRequestId", "PurchaseRequest", "NO_ACTION"),
            new ForeignKeyEdge("FK_RolePermission_Organization", "RolePermission", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_RolePermission_Permission", "RolePermission", "PermissionId", "Permission", "CASCADE"),
            new ForeignKeyEdge("FK_RolePermission_Role", "RolePermission", "RoleId", "Roles", "CASCADE"),
            new ForeignKeyEdge("FK_Roles_Organization", "Roles", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_RoleTemplate_Organization", "RoleTemplate", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_RoleTemplate_SourceRole", "RoleTemplate", "SourceRoleId", "Roles", "NO_ACTION"),
            new ForeignKeyEdge("FK_Supplier_Organization", "Supplier", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_SupplierCatalogItem_AssetCategory", "SupplierCatalogItem", "AssetCategoryId", "AssetCategory", "NO_ACTION"),
            new ForeignKeyEdge("FK_SupplierCatalogItem_AssetType", "SupplierCatalogItem", "AssetTypeId", "AssetType", "NO_ACTION"),
            new ForeignKeyEdge("FK_SupplierCatalogItem_Supplier", "SupplierCatalogItem", "SupplierId", "Supplier", "NO_ACTION"),
            new ForeignKeyEdge("FK_SupplierCatalogItem_TaggedAsset", "SupplierCatalogItem", "TaggedAssetId", "Asset", "NO_ACTION"),
            new ForeignKeyEdge("FK_SystemSetting_Organization", "SystemSetting", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_OrganizationSmtpSettings_Organization", "OrganizationSmtpSettings", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_TransferApprovalAction_AssetTransfer", "TransferApprovalAction", "AssetTransferId", "AssetTransfer", "NO_ACTION"),
            new ForeignKeyEdge("FK_TransferApprovalAction_Organization", "TransferApprovalAction", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_UserInvitation_Department", "UserInvitation", "DepartmentId", "Department", "NO_ACTION"),
            new ForeignKeyEdge("FK_UserInvitation_Organization", "UserInvitation", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_UserInvitation_Role", "UserInvitation", "RoleId", "Roles", "NO_ACTION"),
            new ForeignKeyEdge("FK_Users_Organization", "Users", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_WebhookDelivery_Subscription", "WebhookDelivery", "WebhookSubscriptionId", "WebhookSubscription", "NO_ACTION"),
            new ForeignKeyEdge("FK_WebhookSubscription_Organization", "WebhookSubscription", "OrganizationId", "Organization", "NO_ACTION"),

            // rel1022a, migration 105_LabelPrintBatchLog.sql (checked on a scratch restore 2026-10-22).
            new ForeignKeyEdge("FK_LabelPrintBatch_Organization", "LabelPrintBatch", "OrganizationId", "Organization", "NO_ACTION"),
            new ForeignKeyEdge("FK_LabelPrintBatch_ReprintOf", "LabelPrintBatch", "ReprintOfBatchId", "LabelPrintBatch", "NO_ACTION"),
            new ForeignKeyEdge("FK_LabelPrintBatchItem_Batch", "LabelPrintBatchItem", "BatchId", "LabelPrintBatch", "NO_ACTION"),
            new ForeignKeyEdge("FK_LabelPrintBatchItem_Asset", "LabelPrintBatchItem", "AssetId", "Asset", "NO_ACTION"),
        };
    }
}
