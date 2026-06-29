using System.Configuration;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Web.App_Start
{
    public static class DatabaseConfig
    {
        public static void Configure()
        {
            var autoInitialize = ConfigurationManager.AppSettings["AutoInitializeDatabase"];
            if (autoInitialize != null && autoInitialize.Equals("false", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            SqlDatabaseInitializer.Initialize("AssetManagementConnection");
        }

        public static void ApplyMigrations()
        {
            SqlDatabaseInitializer.ApplyMigrations("AssetManagementConnection");
        }
    }
}
