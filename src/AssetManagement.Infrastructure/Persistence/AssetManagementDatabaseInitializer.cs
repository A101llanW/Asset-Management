using System.Data.Entity;
using AssetManagement.Infrastructure.Persistence.Migrations;

namespace AssetManagement.Infrastructure.Persistence
{
    public class AssetManagementDatabaseInitializer : MigrateDatabaseToLatestVersion<AssetManagementDbContext, Configuration>
    {
    }
}
