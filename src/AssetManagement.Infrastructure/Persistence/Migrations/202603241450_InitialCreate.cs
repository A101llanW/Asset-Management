using System.Data.Entity.Migrations;

namespace AssetManagement.Infrastructure.Persistence.Migrations
{
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            Sql(@"
IF OBJECT_ID('Asset', 'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Asset_SerialNumber_NotNull' AND object_id = OBJECT_ID('Asset'))
BEGIN
    CREATE UNIQUE INDEX IX_Asset_SerialNumber_NotNull ON Asset(SerialNumber) WHERE SerialNumber IS NOT NULL;
END");
        }

        public override void Down()
        {
            Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Asset_SerialNumber_NotNull' AND object_id = OBJECT_ID('Asset'))
BEGIN
    DROP INDEX IX_Asset_SerialNumber_NotNull ON Asset;
END");
        }
    }
}
