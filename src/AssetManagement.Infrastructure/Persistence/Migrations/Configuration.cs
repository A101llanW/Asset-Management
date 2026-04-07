using System;
using System.Collections.Generic;
using System.Data.Entity.Migrations;
using System.Linq;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.AspNet.Identity;

namespace AssetManagement.Infrastructure.Persistence.Migrations
{
    public sealed class Configuration : DbMigrationsConfiguration<AssetManagementDbContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = true;
            AutomaticMigrationDataLossAllowed = true;
        }

        protected override void Seed(AssetManagementDbContext context)
        {
            SeedPermissions(context);
            SeedRoles(context);
            SeedDepartments(context);
            SeedSuppliersAndTypes(context);
            SeedUsers(context);
            SeedAssetsAndHistory(context);
        }

        private static void SeedPermissions(AssetManagementDbContext context)
        {
            var definitions = new[]
            {
                new { Code = "Users.View", Module = "Users", Name = "View Users", Description = "Can view users" },
                new { Code = "Users.Create", Module = "Users", Name = "Create Users", Description = "Can create users" },
                new { Code = "Users.Edit", Module = "Users", Name = "Edit Users", Description = "Can edit users" },
                new { Code = "Users.Delete", Module = "Users", Name = "Delete Users", Description = "Can delete users" },
                new { Code = "Roles.View", Module = "Roles", Name = "View Roles", Description = "Can view roles" },
                new { Code = "Roles.Create", Module = "Roles", Name = "Create Roles", Description = "Can create roles" },
                new { Code = "Roles.Edit", Module = "Roles", Name = "Edit Roles", Description = "Can edit roles" },
                new { Code = "Roles.Delete", Module = "Roles", Name = "Delete Roles", Description = "Can delete roles" },
                new { Code = "Permissions.Assign", Module = "Roles", Name = "Assign Permissions", Description = "Can assign permissions" },
                new { Code = "Departments.View", Module = "Departments", Name = "View Departments", Description = "Can view departments" },
                new { Code = "Departments.Create", Module = "Departments", Name = "Create Departments", Description = "Can create departments" },
                new { Code = "Departments.Edit", Module = "Departments", Name = "Edit Departments", Description = "Can edit departments" },
                new { Code = "Departments.Delete", Module = "Departments", Name = "Delete Departments", Description = "Can delete departments" },
                new { Code = "Suppliers.View", Module = "Suppliers", Name = "View Suppliers", Description = "Can view suppliers" },
                new { Code = "Suppliers.Create", Module = "Suppliers", Name = "Create Suppliers", Description = "Can create suppliers" },
                new { Code = "Suppliers.Edit", Module = "Suppliers", Name = "Edit Suppliers", Description = "Can edit suppliers" },
                new { Code = "Suppliers.Delete", Module = "Suppliers", Name = "Delete Suppliers", Description = "Can delete suppliers" },
                new { Code = "Assets.View", Module = "Assets", Name = "View Assets", Description = "Can view assets" },
                new { Code = "Assets.Create", Module = "Assets", Name = "Create Assets", Description = "Can create assets" },
                new { Code = "Assets.Edit", Module = "Assets", Name = "Edit Assets", Description = "Can edit assets" },
                new { Code = "Assets.Delete", Module = "Assets", Name = "Delete Assets", Description = "Can delete assets" },
                new { Code = "Assets.Assign", Module = "Assets", Name = "Assign Assets", Description = "Can assign assets" },
                new { Code = "Assets.Transfer", Module = "Assets", Name = "Transfer Assets", Description = "Can transfer assets" },
                new { Code = "Assets.Return", Module = "Assets", Name = "Return Assets", Description = "Can return assets" },
                new { Code = "Assets.Receive", Module = "Assets", Name = "Receive Assets", Description = "Can receive assets" },
                new { Code = "Assets.Dispose", Module = "Assets", Name = "Dispose Assets", Description = "Can dispose assets" },
                new { Code = "Assets.ApproveDisposal", Module = "Assets", Name = "Approve Disposal", Description = "Can approve disposal" },
                new { Code = "Purchases.View", Module = "Purchases", Name = "View Purchases", Description = "Can view purchases" },
                new { Code = "Purchases.Create", Module = "Purchases", Name = "Create Purchases", Description = "Can create purchases" },
                new { Code = "Purchases.Edit", Module = "Purchases", Name = "Edit Purchases", Description = "Can edit purchases" },
                new { Code = "Purchases.Approve", Module = "Purchases", Name = "Approve Purchases", Description = "Can approve purchases" },
                new { Code = "Incidents.View", Module = "Incidents", Name = "View Incidents", Description = "Can view incidents" },
                new { Code = "Incidents.Create", Module = "Incidents", Name = "Create Incidents", Description = "Can create incidents" },
                new { Code = "Incidents.Edit", Module = "Incidents", Name = "Edit Incidents", Description = "Can edit incidents" },
                new { Code = "Claims.View", Module = "Claims", Name = "View Claims", Description = "Can view claims" },
                new { Code = "Claims.Create", Module = "Claims", Name = "Create Claims", Description = "Can create claims" },
                new { Code = "Claims.Edit", Module = "Claims", Name = "Edit Claims", Description = "Can edit claims" },
                new { Code = "Financials.View", Module = "Financials", Name = "View Financials", Description = "Can view financial data" },
                new { Code = "Financials.Edit", Module = "Financials", Name = "Edit Financials", Description = "Can edit financial data" },
                new { Code = "Depreciation.View", Module = "Depreciation", Name = "View Depreciation", Description = "Can view depreciation" },
                new { Code = "Depreciation.Manage", Module = "Depreciation", Name = "Manage Depreciation", Description = "Can run/manage depreciation" },
                new { Code = "Documents.Upload", Module = "Documents", Name = "Upload Documents", Description = "Can upload documents" },
                new { Code = "Documents.Delete", Module = "Documents", Name = "Delete Documents", Description = "Can delete documents" },
                new { Code = "Reports.View", Module = "Reports", Name = "View Reports", Description = "Can view reports" },
                new { Code = "Reports.Export", Module = "Reports", Name = "Export Reports", Description = "Can export reports" },
                new { Code = "AuditLogs.View", Module = "Audit", Name = "View Audit Logs", Description = "Can view audit logs" },
                new { Code = "Settings.Manage", Module = "Settings", Name = "Manage Settings", Description = "Can manage settings" }
            };

            foreach (var item in definitions)
            {
                context.Permissions.AddOrUpdate(x => x.Code, new Permission
                {
                    Name = item.Name,
                    Code = item.Code,
                    Module = item.Module,
                    Description = item.Description,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            context.SaveChanges();
        }

        private static void SeedRoles(AssetManagementDbContext context)
        {
            var roleNames = new[]
            {
                new { Name = "Super Admin", IsSystem = true },
                new { Name = "Asset Manager", IsSystem = false },
                new { Name = "Procurement Officer", IsSystem = false },
                new { Name = "Finance Officer", IsSystem = false },
                new { Name = "Department Head", IsSystem = false },
                new { Name = "Staff", IsSystem = false },
                new { Name = "Auditor", IsSystem = false }
            };

            foreach (var role in roleNames)
            {
                context.RolesCustom.AddOrUpdate(x => x.Name, new Role
                {
                    Name = role.Name,
                    Description = role.Name + " seeded demo role",
                    IsSystemRole = role.IsSystem,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            context.SaveChanges();

            var allPermissions = context.Permissions.ToList();
            var superAdminRole = context.RolesCustom.Single(x => x.Name == "Super Admin");
            foreach (var permission in allPermissions)
            {
                if (!context.RolePermissions.Any(x => x.RoleId == superAdminRole.Id && x.PermissionId == permission.Id))
                {
                    context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = superAdminRole.Id,
                        PermissionId = permission.Id
                    });
                }
            }

            var rolePermissionMap = new Dictionary<string, string[]>
            {
                { "Asset Manager", new[] { "Reports.View", "Assets.View", "Assets.Create", "Assets.Edit", "Assets.Assign", "Assets.Transfer", "Assets.Return", "Assets.Receive", "Assets.Dispose", "Assets.ApproveDisposal", "Departments.View", "Suppliers.View", "Incidents.View", "Incidents.Create", "Claims.View", "Claims.Create", "Documents.Upload" } },
                { "Procurement Officer", new[] { "Reports.View", "Assets.View", "Purchases.View", "Purchases.Create", "Purchases.Edit", "Purchases.Approve", "Suppliers.View", "Suppliers.Create", "Suppliers.Edit" } },
                { "Finance Officer", new[] { "Reports.View", "Assets.View", "Financials.View", "Financials.Edit", "Depreciation.View", "Depreciation.Manage", "Claims.View", "Claims.Edit" } },
                { "Department Head", new[] { "Reports.View", "Departments.View", "Departments.Edit", "Assets.View", "Assets.Assign", "Assets.Return", "Incidents.View", "Incidents.Create", "Claims.View" } },
                { "Staff", new[] { "Assets.View", "Assets.Return", "Incidents.Create", "Incidents.View", "Documents.Upload" } },
                { "Auditor", new[] { "Reports.View", "AuditLogs.View", "Assets.View", "Incidents.View", "Claims.View", "Depreciation.View", "Financials.View" } }
            };

            foreach (var rolePermission in rolePermissionMap)
            {
                var role = context.RolesCustom.Single(x => x.Name == rolePermission.Key);
                var allowedPermissions = allPermissions.Where(x => rolePermission.Value.Contains(x.Code)).ToList();

                foreach (var permission in allowedPermissions)
                {
                    if (!context.RolePermissions.Any(x => x.RoleId == role.Id && x.PermissionId == permission.Id))
                    {
                        context.RolePermissions.Add(new RolePermission
                        {
                            RoleId = role.Id,
                            PermissionId = permission.Id
                        });
                    }
                }
            }

            context.SaveChanges();
        }

        private static void SeedDepartments(AssetManagementDbContext context)
        {
            var departments = new[]
            {
                new Department { Name = "Information Technology", Code = "IT", Description = "IT department" },
                new Department { Name = "Finance", Code = "FIN", Description = "Finance department" },
                new Department { Name = "Human Resources", Code = "HR", Description = "HR department" },
                new Department { Name = "Operations", Code = "OPS", Description = "Operations department" },
                new Department { Name = "Administration", Code = "ADMIN", Description = "Administration department" }
            };

            foreach (var department in departments)
            {
                context.Departments.AddOrUpdate(x => x.Code, new Department
                {
                    Name = department.Name,
                    Code = department.Code,
                    Description = department.Description,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            context.SaveChanges();
        }

        private static void SeedSuppliersAndTypes(AssetManagementDbContext context)
        {
            context.Suppliers.AddOrUpdate(x => x.SupplierName,
                new Supplier
                {
                    SupplierName = "Tech Source Ltd",
                    ContactPerson = "Mary Wanjiku",
                    Email = "sales@techsource.example",
                    Phone = "+254700000001",
                    Address = "Nairobi",
                    RegistrationNumber = "TSL-001",
                    Notes = "Primary IT supplier",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Supplier
                {
                    SupplierName = "Office Works Hub",
                    ContactPerson = "David Mwangi",
                    Email = "contact@officeworks.example",
                    Phone = "+254700000002",
                    Address = "Mombasa",
                    RegistrationNumber = "OWH-003",
                    Notes = "Furniture and office equipment",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Supplier
                {
                    SupplierName = "MedEquip Africa",
                    ContactPerson = "Anne Njeri",
                    Email = "support@medequip.example",
                    Phone = "+254700000003",
                    Address = "Kisumu",
                    RegistrationNumber = "MEA-018",
                    Notes = "Medical and lab equipment",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });

            context.AssetCategories.AddOrUpdate(x => x.Name,
                new AssetCategory { Name = "IT Equipment", Description = "Computing and peripheral assets", IsActive = true, CreatedAt = DateTime.UtcNow },
                new AssetCategory { Name = "Furniture", Description = "Office furniture assets", IsActive = true, CreatedAt = DateTime.UtcNow },
                new AssetCategory { Name = "Networking", Description = "Network and communication assets", IsActive = true, CreatedAt = DateTime.UtcNow },
                new AssetCategory { Name = "Medical/Lab Equipment", Description = "Healthcare and laboratory assets", IsActive = true, CreatedAt = DateTime.UtcNow });

            context.SaveChanges();

            var categoryMap = context.AssetCategories.ToDictionary(x => x.Name, x => x.Id);
            context.AssetTypes.AddOrUpdate(x => x.Name,
                new AssetType { AssetCategoryId = categoryMap["IT Equipment"], Name = "Laptop", Description = "Portable computer", IsActive = true, CreatedAt = DateTime.UtcNow },
                new AssetType { AssetCategoryId = categoryMap["IT Equipment"], Name = "Desktop", Description = "Desktop computer", IsActive = true, CreatedAt = DateTime.UtcNow },
                new AssetType { AssetCategoryId = categoryMap["Networking"], Name = "Router", Description = "Router and gateway", IsActive = true, CreatedAt = DateTime.UtcNow },
                new AssetType { AssetCategoryId = categoryMap["Furniture"], Name = "Office Chair", Description = "Ergonomic chair", IsActive = true, CreatedAt = DateTime.UtcNow },
                new AssetType { AssetCategoryId = categoryMap["Medical/Lab Equipment"], Name = "Lab Microscope", Description = "Microscope device", IsActive = true, CreatedAt = DateTime.UtcNow });

            context.SaveChanges();
        }

        private static void SeedUsers(AssetManagementDbContext context)
        {
            var departments = context.Departments.ToDictionary(x => x.Code, x => x.Id);
            var roles = context.RolesCustom.ToDictionary(x => x.Name, x => x.Id);

            var users = new List<ApplicationUser>
            {
                new ApplicationUser { UserName = "superadmin@asset.local", Email = "superadmin@asset.local", EmployeeNumber = "EMP-0001", FirstName = "System", LastName = "Admin", Phone = "+254700001001", DepartmentId = departments["IT"], PositionTitle = "Super Administrator", IsActive = true, RoleId = roles["Super Admin"], EmailConfirmed = true, LockoutEnabled = false, LockoutEndDateUtc = null, AccessFailedCount = 0, TwoFactorEnabled = false, PhoneNumberConfirmed = false },
                new ApplicationUser { UserName = "assetmanager@asset.local", Email = "assetmanager@asset.local", EmployeeNumber = "EMP-0002", FirstName = "Peter", LastName = "Asset", Phone = "+254700001002", DepartmentId = departments["IT"], PositionTitle = "Asset Manager", IsActive = true, RoleId = roles["Asset Manager"], EmailConfirmed = true, LockoutEnabled = false, LockoutEndDateUtc = null, AccessFailedCount = 0, TwoFactorEnabled = false, PhoneNumberConfirmed = false },
                new ApplicationUser { UserName = "procurement@asset.local", Email = "procurement@asset.local", EmployeeNumber = "EMP-0003", FirstName = "Ruth", LastName = "Procure", Phone = "+254700001003", DepartmentId = departments["OPS"], PositionTitle = "Procurement Officer", IsActive = true, RoleId = roles["Procurement Officer"], EmailConfirmed = true, LockoutEnabled = false, LockoutEndDateUtc = null, AccessFailedCount = 0, TwoFactorEnabled = false, PhoneNumberConfirmed = false },
                new ApplicationUser { UserName = "finance@asset.local", Email = "finance@asset.local", EmployeeNumber = "EMP-0004", FirstName = "James", LastName = "Finance", Phone = "+254700001004", DepartmentId = departments["FIN"], PositionTitle = "Finance Officer", IsActive = true, RoleId = roles["Finance Officer"], EmailConfirmed = true, LockoutEnabled = false, LockoutEndDateUtc = null, AccessFailedCount = 0, TwoFactorEnabled = false, PhoneNumberConfirmed = false },
                new ApplicationUser { UserName = "staff@asset.local", Email = "staff@asset.local", EmployeeNumber = "EMP-0005", FirstName = "Lucy", LastName = "Staff", Phone = "+254700001005", DepartmentId = departments["ADMIN"], PositionTitle = "Staff", IsActive = true, RoleId = roles["Staff"], EmailConfirmed = true, LockoutEnabled = false, LockoutEndDateUtc = null, AccessFailedCount = 0, TwoFactorEnabled = false, PhoneNumberConfirmed = false },
                new ApplicationUser { UserName = "auditor@asset.local", Email = "auditor@asset.local", EmployeeNumber = "EMP-0006", FirstName = "Ian", LastName = "Audit", Phone = "+254700001006", DepartmentId = departments["FIN"], PositionTitle = "Auditor", IsActive = true, RoleId = roles["Auditor"], EmailConfirmed = true, LockoutEnabled = false, LockoutEndDateUtc = null, AccessFailedCount = 0, TwoFactorEnabled = false, PhoneNumberConfirmed = false },
                new ApplicationUser { UserName = "departmenthead@asset.local", Email = "departmenthead@asset.local", EmployeeNumber = "EMP-0007", FirstName = "Grace", LastName = "Head", Phone = "+254700001007", DepartmentId = departments["HR"], PositionTitle = "Department Head", IsActive = true, RoleId = roles["Department Head"], EmailConfirmed = true, LockoutEnabled = false, LockoutEndDateUtc = null, AccessFailedCount = 0, TwoFactorEnabled = false, PhoneNumberConfirmed = false }
            };

            var hasher = new PasswordHasher();
            foreach (var user in users)
            {
                if (context.Users.Any(x => x.UserName == user.UserName))
                {
                    continue;
                }

                user.PasswordHash = hasher.HashPassword("P@ssw0rd!");
                user.SecurityStamp = Guid.NewGuid().ToString("N");
                user.CreatedAt = DateTime.UtcNow;
                context.Users.Add(user);
            }

            context.SaveChanges();
        }

        private static void SeedAssetsAndHistory(AssetManagementDbContext context)
        {
            if (context.Assets.Any())
            {
                return;
            }

            var categoryMap = context.AssetCategories.ToDictionary(x => x.Name, x => x.Id);
            var typeMap = context.AssetTypes.ToDictionary(x => x.Name, x => x.Id);
            var supplierMap = context.Suppliers.ToDictionary(x => x.SupplierName, x => x.Id);
            var departments = context.Departments.ToDictionary(x => x.Code, x => x.Id);
            var sampleCustodian = context.Users.FirstOrDefault(x => x.UserName == "staff@asset.local");

            for (var i = 1; i <= 10; i++)
            {
                var asset = new Asset
                {
                    AssetName = i % 2 == 0 ? "Dell Latitude " + (5000 + i) : "HP EliteBook " + (700 + i),
                    AssetTag = "AST-2026-" + i.ToString("000"),
                    CategoryId = categoryMap["IT Equipment"],
                    AssetTypeId = typeMap["Laptop"],
                    Brand = i % 2 == 0 ? "Dell" : "HP",
                    Model = i % 2 == 0 ? "Latitude" : "EliteBook",
                    SerialNumber = "SN-2026-" + i.ToString("0000"),
                    BarcodeOrQRCode = "BC-2026-" + i.ToString("0000"),
                    Specifications = "Core i7, 16GB RAM, 512GB SSD",
                    Condition = AssetCondition.New,
                    CurrentStatus = i <= 6 ? AssetStatus.Assigned : AssetStatus.InStore,
                    Description = "Seeded sample enterprise laptop",
                    PurchaseDate = DateTime.UtcNow.AddMonths(-i),
                    AcquisitionCost = 1400 + i * 50,
                    TaxAmount = 220,
                    Currency = "USD",
                    SupplierId = supplierMap["Tech Source Ltd"],
                    DepartmentId = departments["IT"],
                    CurrentCustodianId = i <= 6 ? sampleCustodian?.Id : null,
                    ConditionOnReceipt = "New",
                    UsefulLifeMonths = 48,
                    SalvageValue = 100,
                    DepreciationMethod = DepreciationMethod.StraightLine,
                    DepreciationStartDate = DateTime.UtcNow.AddMonths(-i),
                    CurrentBookValue = 1300 + i * 10,
                    AccumulatedDepreciation = 100 + i * 15,
                    ReplacementValue = 1600 + i * 45,
                    IsInsured = true,
                    InsuredValue = 1500 + i * 40,
                    PolicyReference = "POL-IT-2026-" + i.ToString("000"),
                    WarrantyStartDate = DateTime.UtcNow.AddMonths(-i),
                    WarrantyEndDate = DateTime.UtcNow.AddMonths(12 - i),
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                context.Assets.Add(asset);
                context.SaveChanges();

                context.AssetCustodyEvents.Add(new AssetCustodyEvent
                {
                    AssetId = asset.Id,
                    ActionType = CustodyActionType.Assigned,
                    ActionDate = DateTime.UtcNow.AddMonths(-i + 1),
                    ToUserId = asset.CurrentCustodianId,
                    ToDepartmentId = asset.DepartmentId,
                    ConditionBefore = "New",
                    ConditionAfter = "Good",
                    Reason = "Initial handover",
                    Notes = "Seed custody event",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                });

                context.DepreciationRecords.Add(new DepreciationRecord
                {
                    AssetId = asset.Id,
                    PeriodStartDate = DateTime.UtcNow.AddMonths(-1),
                    PeriodEndDate = DateTime.UtcNow,
                    Method = DepreciationMethod.StraightLine,
                    OpeningBookValue = asset.CurrentBookValue + 25,
                    DepreciationAmount = 25,
                    ClosingBookValue = asset.CurrentBookValue,
                    AccumulatedDepreciation = asset.AccumulatedDepreciation,
                    IsPosted = true,
                    PostedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                });

                context.InsurancePolicies.Add(new InsurancePolicy
                {
                    AssetId = asset.Id,
                    InsurerName = "Global Insurance Plc",
                    PolicyNumber = "GIP-" + i.ToString("00000"),
                    PolicyStartDate = DateTime.UtcNow.AddMonths(-2),
                    PolicyEndDate = DateTime.UtcNow.AddMonths(10),
                    InsuredValue = asset.InsuredValue ?? asset.AcquisitionCost,
                    ReplacementValue = asset.ReplacementValue,
                    ValuationDate = DateTime.UtcNow.AddDays(-15),
                    ClaimEligibility = true,
                    DeductibleAmount = 100,
                    ClaimNotes = "Standard asset policy",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                });
            }

            var firstAsset = context.Assets.OrderBy(x => x.Id).First();
            var incident = new AssetIncident
            {
                AssetId = firstAsset.Id,
                IncidentNumber = "INC-0001",
                IncidentType = IncidentType.Damaged,
                IncidentDate = DateTime.UtcNow.AddDays(-20),
                Description = "Screen crack during transport",
                Severity = IncidentSeverity.Medium,
                ResolutionStatus = "Under Review",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            context.AssetIncidents.Add(incident);
            context.SaveChanges();

            context.InsuranceClaims.Add(new InsuranceClaim
            {
                ClaimNumber = "CLM-0001",
                AssetId = firstAsset.Id,
                IncidentId = incident.Id,
                ClaimDate = DateTime.UtcNow.AddDays(-15),
                ClaimType = "Damage",
                Insurer = "Global Insurance Plc",
                Assessor = "Field Assessor A",
                DocumentsSubmitted = "Invoice, photos, claim form",
                ClaimStatus = ClaimStatus.UnderReview,
                ApprovedAmount = 0,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });

            context.AuditLogs.Add(new AuditLog
            {
                ActorUserId = context.Users.First().Id,
                Action = "Seed.InitialLoad",
                EntityType = "System",
                EntityId = "Seed",
                OldValues = null,
                NewValues = "Initial seed data loaded",
                Timestamp = DateTime.UtcNow,
                IPAddress = "127.0.0.1",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });

            context.SaveChanges();
        }
    }
}
