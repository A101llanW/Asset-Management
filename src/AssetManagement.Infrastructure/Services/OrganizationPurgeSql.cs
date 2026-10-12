using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetManagement.Infrastructure.Services
{
    /// <summary>
    /// The single ordered purge plan for a tenant wipe, used by OrganizationPurgeService and by the
    /// tests. Pure string builders, no DB I/O. Children are deleted before parents (FKs have no
    /// CASCADE); FK columns that point "backwards" (cycles, department -> user) are nulled first.
    /// OBJECT_ID / COL_LENGTH guards tolerate older schemas.
    /// </summary>
    public static class OrganizationPurgeSql
    {
        public enum StepKind
        {
            DeleteByOrganization,
            NullColumnByOrganization,
            DeleteOrganizationRow
        }

        public sealed class PurgeStep
        {
            public StepKind Kind { get; private set; }
            public string Table { get; private set; }
            public string Column { get; private set; }

            internal PurgeStep(StepKind kind, string table, string column)
            {
                Kind = kind;
                Table = table;
                Column = column;
            }

            public string ToSql()
            {
                switch (Kind)
                {
                    case StepKind.DeleteByOrganization:
                        return BuildSafeDelete(Table);
                    case StepKind.NullColumnByOrganization:
                        return BuildSafeNullColumn(Table, Column);
                    default:
                        return "DELETE FROM [Organization] WHERE [Id] = @OrgId";
                }
            }
        }

        /// <summary>
        /// Tables that are deliberately not deleted by OrganizationId: Organization (deleted last by Id),
        /// global or non-tenant tables, rows removed by ON DELETE CASCADE, and ad-hoc backup tables (Bak_*).
        /// </summary>
        public static readonly string[] ExcludedTables =
        {
            "Organization",
            "Permission",
            "TemporaryCredentials",
            "PasswordResetToken",
            "SchemaMigrationHistory"
        };

        public static bool IsExcludedTable(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                return false;
            }

            return tableName.StartsWith("Bak_", StringComparison.OrdinalIgnoreCase)
                || ExcludedTables.Any(x => string.Equals(x, tableName, StringComparison.OrdinalIgnoreCase));
        }

        private static readonly PurgeStep[] Steps =
        {
            Delete("ImpersonationRequest"),
            Delete("LoginAttempts"),
            Delete("SecurityEvents"),
            Delete("Notification"),
            Delete("OutboxMessage"),
            Delete("AuditLog"),
            Delete("WebhookDelivery"),
            Delete("WebhookSubscription"),
            Delete("OrganizationLicenseHistory"),
            Delete("OrganizationLicense"),
            Delete("RoleTemplate"),
            // UserInvitation -> Roles, Department.
            Delete("UserInvitation"),
            // AssetDocument.RequirementId <-> AssetDocumentRequirement.DocumentId is a cycle.
            NullColumn("AssetDocument", "RequirementId"),
            NullColumn("AssetDocumentRequirement", "DocumentId"),
            Delete("AssetDocumentRequirement"),
            Delete("AssetDocument"),
            Delete("InsuranceClaim"),
            Delete("InsurancePolicy"),
            Delete("DepreciationRecord"),
            Delete("DisposalApprovalAction"),
            Delete("DisposalRecord"),
            Delete("TransferApprovalAction"),
            Delete("AssetTransfer"),
            Delete("AssetReturn"),
            Delete("AssetMovement"),
            Delete("AssetCustodyEvent"),
            Delete("AssetMaintenanceRecord"),
            Delete("AssetIncident"),
            Delete("AssetAssignment"),
            // rel1009i (098): receipt rows -> AssetReceiving/Asset/PurchaseOrderLine; lines -> PurchaseRecord/PurchaseRequestLine/AssetType.
            Delete("PurchaseOrderLineReceipt"),
            Delete("PurchaseOrderLine"),
            Delete("AssetReceiving"),
            Delete("PurchaseRecord"),
            Delete("PurchaseApprovalAction"),
            Delete("PurchaseRequestLine"),
            Delete("PurchaseRequest"),
            Delete("PurchaseRequestGroup"), // migration 100: after PurchaseRequest (FK)
            Delete("CatalogRequisitionFlow"), // migration 100: no FK, catalog overrides
            Delete("AssetRequest"),
            NullColumn("SupplierCatalogItem", "TaggedAssetId"),
            Delete("SupplierCatalogItem"),
            Delete("Asset"),
            Delete("AssetSubType"),
            Delete("AssetType"),
            Delete("AssetCategory"),
            Delete("Supplier"),
            NullColumn("Users", "DepartmentId"),
            NullColumn("Users", "RoleId"),
            // Department -> Users (class teacher, room custodian) must be cleared before Users go.
            NullColumn("Department", "ClassTeacherUserId"),
            NullColumn("Department", "RoomCustodianUserId"),
            Delete("Users"),
            Delete("RolePermission"),
            Delete("Roles"),
            Delete("DepartmentApprovalStage"),
            Delete("Department"),
            Delete("SystemSetting"),
            Delete("OrganizationSmtpSettings"),
            new PurgeStep(StepKind.DeleteOrganizationRow, "Organization", null)
        };

        private static PurgeStep Delete(string table)
        {
            return new PurgeStep(StepKind.DeleteByOrganization, table, null);
        }

        private static PurgeStep NullColumn(string table, string column)
        {
            return new PurgeStep(StepKind.NullColumnByOrganization, table, column);
        }

        public static IList<PurgeStep> GetSteps()
        {
            return Steps.ToList();
        }

        /// <summary>Tables deleted by [OrganizationId] = @OrgId, in execution order.</summary>
        public static string[] GetDeletedTableNames()
        {
            return Steps.Where(x => x.Kind == StepKind.DeleteByOrganization).Select(x => x.Table).ToArray();
        }

        public static string BuildSafeDelete(string tableName)
        {
            EnsureIdentifier(tableName, "tableName");
            return "IF OBJECT_ID(N'[" + tableName + "]', N'U') IS NOT NULL DELETE FROM [" + tableName + "] WHERE [OrganizationId] = @OrgId";
        }

        /// <summary>
        /// UPDATE ... SET [column] = NULL for the org. Dynamic SQL so an older schema without the column
        /// does not fail the batch at compile time.
        /// </summary>
        public static string BuildSafeNullColumn(string tableName, string columnName)
        {
            EnsureIdentifier(tableName, "tableName");
            EnsureIdentifier(columnName, "columnName");
            return "IF COL_LENGTH(N'" + tableName + "', N'" + columnName + "') IS NOT NULL EXEC sp_executesql N'UPDATE ["
                + tableName + "] SET [" + columnName + "] = NULL WHERE [OrganizationId] = @OrgId AND ["
                + columnName + "] IS NOT NULL', N'@OrgId int', @OrgId = @OrgId";
        }

        /// <summary>
        /// Canonical statement order executed by OrganizationPurgeService.
        /// </summary>
        public static string[] GetOrderedStatements()
        {
            return Steps.Select(x => x.ToSql()).ToArray();
        }

        public static int StatementCount
        {
            get { return Steps.Length; }
        }

        /// <summary>
        /// Index of the final Organization row delete (must be last).
        /// </summary>
        public static int OrganizationDeleteIndex
        {
            get { return StatementCount - 1; }
        }

        private static void EnsureIdentifier(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Identifier is required.", parameterName);
            }

            foreach (var ch in value)
            {
                if (!(char.IsLetterOrDigit(ch) || ch == '_'))
                {
                    throw new ArgumentException("Identifier contains an invalid character: " + value, parameterName);
                }
            }
        }
    }
}