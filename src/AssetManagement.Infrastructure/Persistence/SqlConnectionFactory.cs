using System.Configuration;
using System.Data.SqlClient;

namespace AssetManagement.Infrastructure.Persistence
{
    public class SqlConnectionFactory : ISqlConnectionFactory
    {
        private readonly string _connectionString;

        public SqlConnectionFactory()
            : this("AssetManagementConnection")
        {
        }

        public SqlConnectionFactory(string connectionStringName)
        {
            _connectionString = ConfigurationManager.ConnectionStrings[connectionStringName].ConnectionString;
        }

        public SqlConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}
