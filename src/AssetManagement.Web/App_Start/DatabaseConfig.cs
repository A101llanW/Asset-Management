using System.Data.Entity;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Persistence.Migrations;

namespace AssetManagement.Web.App_Start
{
    public static class DatabaseConfig
    {
        public static void Configure()
        {
            Database.SetInitializer(new MigrateDatabaseToLatestVersion<AssetManagementDbContext, Configuration>());
            using (var context = new AssetManagementDbContext())
            {
                context.Database.Initialize(false);
            }
        }
    }
}
