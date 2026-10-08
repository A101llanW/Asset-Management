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

                        // One shared, tested plan (OrganizationPurgeSql): children before parents,
                        // FK cycles and department->user links nulled first, OBJECT_ID/COL_LENGTH guards.
                        ExecuteDeletes(connection, transaction, organizationId, OrganizationPurgeSql.GetOrderedStatements());

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
