using System;
using System.Data;
using System.Data.SqlClient;
using AssetManagement.Application.Contracts.Organizations;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Services
{
    /// <summary>
    /// Hard-deletes an organization and related tenant data. Test/demo helper — not for production workflows.
    /// </summary>
    public class OrganizationPurgeService : IOrganizationPurgeService
    {
        private readonly ISqlConnectionFactory _connectionFactory;

        public OrganizationPurgeService(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public OrganizationDeleteResult DeleteOrganizationAndData(int organizationId)
        {
            if (organizationId <= 0)
            {
                return new OrganizationDeleteResult { Succeeded = false, Message = "Invalid organization id." };
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        string organizationName;
                        using (var lookup = connection.CreateCommand())
                        {
                            lookup.Transaction = transaction;
                            lookup.CommandText = "SELECT [Name] FROM [Organization] WHERE [Id] = @OrgId";
                            AddOrgId(lookup, organizationId);
                            var result = lookup.ExecuteScalar();
                            if (result == null || result == DBNull.Value)
                            {
                                return new OrganizationDeleteResult
                                {
                                    Succeeded = false,
                                    Message = "Organization not found."
                                };
                            }

                            organizationName = Convert.ToString(result);
                        }

                        // Child tables first (order matters for FKs without CASCADE).
                        // IF OBJECT_ID guards tables that may be missing on older DBs.
                        ExecuteDeletes(connection, transaction, organizationId, new[]
                        {
                            SafeDelete("ImpersonationRequest"),
                            SafeDelete("LoginAttempts"),
                            SafeDelete("SecurityEvents"),
                            SafeDelete("Notification"),
                            SafeDelete("OutboxMessage"),
                            SafeDelete("AuditLog"),
                            SafeDelete("WebhookDelivery"),
                            SafeDelete("WebhookSubscription"),
                            SafeDelete("OrganizationLicenseHistory"),
                            SafeDelete("OrganizationLicense"),
                            SafeDelete("RoleTemplate"),
                            SafeDelete("AssetDocumentRequirement"),
                            SafeDelete("AssetDocument"),
                            SafeDelete("InsuranceClaim"),
                            SafeDelete("InsurancePolicy"),
                            SafeDelete("DepreciationRecord"),
                            SafeDelete("DisposalApprovalAction"),
                            SafeDelete("DisposalRecord"),
                            SafeDelete("TransferApprovalAction"),
                            SafeDelete("AssetTransfer"),
                            SafeDelete("AssetReturn"),
                            SafeDelete("AssetCustodyEvent"),
                            SafeDelete("AssetMaintenanceRecord"),
                            SafeDelete("AssetIncident"),
                            SafeDelete("AssetAssignment"),
                            SafeDelete("AssetReceiving"),
                            SafeDelete("PurchaseRecord"),
                            SafeDelete("PurchaseApprovalAction"),
                            SafeDelete("PurchaseRequest"),
                            SafeDelete("AssetRequest"),
                            "IF OBJECT_ID(N'[SupplierCatalogItem]', N'U') IS NOT NULL UPDATE [SupplierCatalogItem] SET [TaggedAssetId] = NULL WHERE [OrganizationId] = @OrgId",
                            SafeDelete("SupplierCatalogItem"),
                            SafeDelete("Asset"),
                            SafeDelete("AssetSubType"),
                            SafeDelete("AssetType"),
                            SafeDelete("AssetCategory"),
                            SafeDelete("Supplier"),
                            "IF OBJECT_ID(N'[Users]', N'U') IS NOT NULL UPDATE [Users] SET [DepartmentId] = NULL, [RoleId] = NULL WHERE [OrganizationId] = @OrgId",
                            SafeDelete("Users"),
                            SafeDelete("RolePermission"),
                            SafeDelete("Roles"),
                            SafeDelete("Department"),
                            SafeDelete("SystemSetting"),
                            "DELETE FROM [Organization] WHERE [Id] = @OrgId"
                        });

                        transaction.Commit();
                        return new OrganizationDeleteResult
                        {
                            Succeeded = true,
                            OrganizationName = organizationName,
                            Message = "Organization '" + organizationName + "' and related data were permanently deleted."
                        };
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            transaction.Rollback();
                        }
                        catch
                        {
                            // Ignore rollback failures.
                        }

                        return new OrganizationDeleteResult
                        {
                            Succeeded = false,
                            Message = "Delete failed: " + ex.Message
                        };
                    }
                }
            }
        }

        private static string SafeDelete(string tableName)
        {
            return "IF OBJECT_ID(N'[" + tableName + "]', N'U') IS NOT NULL DELETE FROM [" + tableName + "] WHERE [OrganizationId] = @OrgId";
        }

        private static void ExecuteDeletes(
            SqlConnection connection,
            SqlTransaction transaction,
            int organizationId,
            string[] statements)
        {
            foreach (var sql in statements)
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandType = CommandType.Text;
                    command.CommandText = sql;
                    AddOrgId(command, organizationId);
                    command.ExecuteNonQuery();
                }
            }
        }

        private static void AddOrgId(IDbCommand command, int organizationId)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@OrgId";
            parameter.Value = organizationId;
            command.Parameters.Add(parameter);
        }
    }
}
