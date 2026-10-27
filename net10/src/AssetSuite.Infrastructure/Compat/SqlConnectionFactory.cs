using System;
using System.Data.SqlClient;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;

namespace AssetManagement.Infrastructure.Persistence
{
    public class SqlConnectionFactory : ISqlConnectionFactory
    {
        private readonly string _connectionString;

        public SqlConnectionFactory(IConfiguration configuration)
            : this(configuration, "AssetManagementConnection")
        {
        }

        public SqlConnectionFactory(IConfiguration configuration, string connectionStringName)
        {
            _connectionString = configuration.GetConnectionString(connectionStringName)
                ?? configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException(
                    "Connection string '" + connectionStringName + "' was not found in configuration.");
            }
        }

        public SqlConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}
