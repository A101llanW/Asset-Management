using System.Data.SqlClient;

namespace AssetManagement.Infrastructure.Persistence
{
    public interface ISqlConnectionFactory
    {
        SqlConnection CreateConnection();
    }
}
