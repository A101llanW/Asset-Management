using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure.Annotations;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Linq;
using System.Web;
using AssetManagement.Domain.Common;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace AssetManagement.Infrastructure.Persistence
{
    public class AssetManagementDbContext : IdentityDbContext<ApplicationUser>
    {
        public AssetManagementDbContext() : base("AssetManagementConnection", throwIfV1Schema: false)
        {
            Configuration.LazyLoadingEnabled = true;
            Configuration.ProxyCreationEnabled = true;
        }

        public static AssetManagementDbContext Create()
        {
            return new AssetManagementDbContext();
        }

        public DbSet<Role> RolesCustom { get; set; }

        public DbSet<Permission> Permissions { get; set; }

        public DbSet<RolePermission> RolePermissions { get; set; }

        public DbSet<Department> Departments { get; set; }

        public DbSet<Supplier> Suppliers { get; set; }

        public DbSet<AssetCategory> AssetCategories { get; set; }

        public DbSet<AssetType> AssetTypes { get; set; }

        public DbSet<Asset> Assets { get; set; }

        public DbSet<AssetDocument> AssetDocuments { get; set; }

        public DbSet<PurchaseRequest> PurchaseRequests { get; set; }

        public DbSet<PurchaseRecord> PurchaseRecords { get; set; }

        public DbSet<AssetReceiving> AssetReceivings { get; set; }

        public DbSet<AssetAssignment> AssetAssignments { get; set; }

        public DbSet<AssetTransfer> AssetTransfers { get; set; }

        public DbSet<AssetReturn> AssetReturns { get; set; }

        public DbSet<AssetCustodyEvent> AssetCustodyEvents { get; set; }

        public DbSet<AssetMaintenanceRecord> AssetMaintenanceRecords { get; set; }

        public DbSet<AssetIncident> AssetIncidents { get; set; }

        public DbSet<InsurancePolicy> InsurancePolicies { get; set; }

        public DbSet<InsuranceClaim> InsuranceClaims { get; set; }

        public DbSet<DepreciationRecord> DepreciationRecords { get; set; }

        public DbSet<DisposalRecord> DisposalRecords { get; set; }

        public DbSet<Notification> Notifications { get; set; }

        public DbSet<AuditLog> AuditLogs { get; set; }

        public DbSet<Organization> Organizations { get; set; }

        public DbSet<SystemSetting> SystemSettings { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();

            modelBuilder.Entity<ApplicationUser>().ToTable("Users");
            modelBuilder.Entity<IdentityRole>().ToTable("IdentityRoles");
            modelBuilder.Entity<IdentityUserRole>().ToTable("IdentityUserRoles");
            modelBuilder.Entity<IdentityUserLogin>().ToTable("IdentityUserLogins");
            modelBuilder.Entity<IdentityUserClaim>().ToTable("IdentityUserClaims");
            modelBuilder.Entity<Role>().ToTable("Roles");

            modelBuilder.Entity<Asset>()
                .Property(x => x.AssetTag)
                .IsRequired()
                .HasMaxLength(60)
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new System.ComponentModel.DataAnnotations.Schema.IndexAttribute("IX_AssetTag") { IsUnique = true }));

            modelBuilder.Entity<Asset>()
                .Property(x => x.SerialNumber)
                .HasMaxLength(120)
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new System.ComponentModel.DataAnnotations.Schema.IndexAttribute("IX_SerialNumber")));

            modelBuilder.Entity<Asset>().Property(x => x.RowVersion).IsRowVersion();
            modelBuilder.Entity<PurchaseRecord>().Property(x => x.RowVersion).IsRowVersion();
            modelBuilder.Entity<AssetAssignment>().Property(x => x.RowVersion).IsRowVersion();
            modelBuilder.Entity<AssetTransfer>().Property(x => x.RowVersion).IsRowVersion();
            modelBuilder.Entity<AssetReturn>().Property(x => x.RowVersion).IsRowVersion();

            modelBuilder.Entity<RolePermission>()
                .HasRequired(x => x.Role)
                .WithMany(x => x.RolePermissions)
                .HasForeignKey(x => x.RoleId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<RolePermission>()
                .HasRequired(x => x.Permission)
                .WithMany(x => x.RolePermissions)
                .HasForeignKey(x => x.PermissionId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<RolePermission>()
                .Property(x => x.RoleId)
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new System.ComponentModel.DataAnnotations.Schema.IndexAttribute("IX_RolePermission_Role_Permission", 1) { IsUnique = true }));

            modelBuilder.Entity<RolePermission>()
                .Property(x => x.PermissionId)
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new System.ComponentModel.DataAnnotations.Schema.IndexAttribute("IX_RolePermission_Role_Permission", 2) { IsUnique = true }));

            modelBuilder.Entity<Permission>()
                .Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(120)
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new System.ComponentModel.DataAnnotations.Schema.IndexAttribute("IX_Permission_Code") { IsUnique = true }));

            modelBuilder.Entity<Department>()
                .Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(40)
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new System.ComponentModel.DataAnnotations.Schema.IndexAttribute("IX_Department_Code") { IsUnique = true }));

            modelBuilder.Entity<AssetType>()
                .HasRequired(x => x.AssetCategory)
                .WithMany(x => x.AssetTypes)
                .HasForeignKey(x => x.AssetCategoryId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Asset>()
                .HasRequired(x => x.Category)
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Asset>()
                .HasRequired(x => x.AssetType)
                .WithMany()
                .HasForeignKey(x => x.AssetTypeId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Asset>()
                .HasRequired(x => x.Supplier)
                .WithMany()
                .HasForeignKey(x => x.SupplierId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Asset>()
                .HasRequired(x => x.Department)
                .WithMany(x => x.Assets)
                .HasForeignKey(x => x.DepartmentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<InsuranceClaim>()
                .HasOptional(x => x.Incident)
                .WithMany()
                .HasForeignKey(x => x.IncidentId)
                .WillCascadeOnDelete(false);
        }

        public override int SaveChanges()
        {
            var utcNow = DateTime.UtcNow;
            var entries = ChangeTracker.Entries()
                .Where(x => x.Entity is AuditableEntity && (x.State == EntityState.Added || x.State == EntityState.Modified));

            foreach (var entry in entries)
            {
                var entity = (AuditableEntity)entry.Entity;
                if (entry.State == EntityState.Added)
                {
                    entity.CreatedAt = utcNow;
                    entity.IsActive = true;
                }

                if (entry.State == EntityState.Modified)
                {
                    entity.UpdatedAt = utcNow;
                }
            }

            var userEntries = ChangeTracker.Entries<ApplicationUser>()
                .Where(x => x.State == EntityState.Added || x.State == EntityState.Modified);
            foreach (var userEntry in userEntries)
            {
                if (userEntry.State == EntityState.Added)
                {
                    userEntry.Entity.CreatedAt = utcNow;
                    userEntry.Entity.IsActive = true;
                }
                else
                {
                    userEntry.Entity.UpdatedAt = utcNow;
                }
            }

            return base.SaveChanges();
        }
    }
}
