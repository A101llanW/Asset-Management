using System;

namespace AssetManagement.Infrastructure.Services
{
    /// <summary>
    /// Ordered hard-delete SQL for tenant wipe. Pure string builders — no DB I/O.
    /// Child tables first (FK order); OBJECT_ID guards tolerate older schemas.
    /// </summary>
    public static class OrganizationPurgeSql
    {
        public static string BuildSafeDelete(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("Table name is required.", "tableName");
            }

            return "IF OBJECT_ID(N'[" + tableName + "]', N'U') IS NOT NULL DELETE FROM [" + tableName + "] WHERE [OrganizationId] = @OrgId";
        }

        /// <summary>
        /// Canonical delete/update statement order used by OrganizationPurgeService.
        /// </summary>
        public static string[] GetOrderedStatements()
        {
            return new[]
            {
                BuildSafeDelete("ImpersonationRequest"),
                BuildSafeDelete("LoginAttempts"),
                BuildSafeDelete("SecurityEvents"),
                BuildSafeDelete("Notification"),
                BuildSafeDelete("OutboxMessage"),
                BuildSafeDelete("AuditLog"),
                BuildSafeDelete("WebhookDelivery"),
                BuildSafeDelete("WebhookSubscription"),
                BuildSafeDelete("OrganizationLicenseHistory"),
                BuildSafeDelete("OrganizationLicense"),
                BuildSafeDelete("RoleTemplate"),
                BuildSafeDelete("AssetDocumentRequirement"),
                BuildSafeDelete("AssetDocument"),
                BuildSafeDelete("InsuranceClaim"),
                BuildSafeDelete("InsurancePolicy"),
                BuildSafeDelete("DepreciationRecord"),
                BuildSafeDelete("DisposalApprovalAction"),
                BuildSafeDelete("DisposalRecord"),
                BuildSafeDelete("TransferApprovalAction"),
                BuildSafeDelete("AssetTransfer"),
                BuildSafeDelete("AssetReturn"),
                BuildSafeDelete("AssetCustodyEvent"),
                BuildSafeDelete("AssetMaintenanceRecord"),
                BuildSafeDelete("AssetIncident"),
                BuildSafeDelete("AssetAssignment"),
                BuildSafeDelete("AssetReceiving"),
                BuildSafeDelete("PurchaseRecord"),
                BuildSafeDelete("PurchaseApprovalAction"),
                BuildSafeDelete("PurchaseRequest"),
                BuildSafeDelete("AssetRequest"),
                "IF OBJECT_ID(N'[SupplierCatalogItem]', N'U') IS NOT NULL UPDATE [SupplierCatalogItem] SET [TaggedAssetId] = NULL WHERE [OrganizationId] = @OrgId",
                BuildSafeDelete("SupplierCatalogItem"),
                BuildSafeDelete("Asset"),
                BuildSafeDelete("AssetSubType"),
                BuildSafeDelete("AssetType"),
                BuildSafeDelete("AssetCategory"),
                BuildSafeDelete("Supplier"),
                "IF OBJECT_ID(N'[Users]', N'U') IS NOT NULL UPDATE [Users] SET [DepartmentId] = NULL, [RoleId] = NULL WHERE [OrganizationId] = @OrgId",
                BuildSafeDelete("Users"),
                BuildSafeDelete("RolePermission"),
                BuildSafeDelete("Roles"),
                BuildSafeDelete("Department"),
                BuildSafeDelete("SystemSetting"),
                "DELETE FROM [Organization] WHERE [Id] = @OrgId"
            };
        }

        public static int StatementCount
        {
            get { return GetOrderedStatements().Length; }
        }

        /// <summary>
        /// Index of the final Organization row delete (must be last).
        /// </summary>
        public static int OrganizationDeleteIndex
        {
            get { return StatementCount - 1; }
        }
    }
}
