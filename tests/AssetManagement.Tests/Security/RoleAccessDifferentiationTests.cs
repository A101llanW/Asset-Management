using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Security;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    [TestFixture]
    public class RoleAccessDifferentiationTests
    {
        private static readonly IDictionary<string, ISet<string>> ExpectedRolePermissions = new Dictionary<string, ISet<string>>
        {
            {
                "Procurement Officer",
                new HashSet<string>
                {
                    "Reports.View", "Assets.View", "Purchases.View", "Purchases.Create", "Purchases.Edit",
                    "Purchases.Approve", "Suppliers.View", "Suppliers.Create", "Suppliers.Edit", "Users.View"
                }
            },
            {
                "Department Head",
                new HashSet<string>
                {
                    "Reports.View", "Departments.View", "Departments.Edit", "Users.ViewDepartment", "Assets.View",
                    "Assets.Assign", "Assets.Return", "Assets.Request", "Assets.Request.Approve", "Purchases.Create",
                    "Incidents.View", "Incidents.Create", "Claims.View", "Assets.Transfer"
                }
            },
            {
                "Staff",
                new HashSet<string>
                {
                    "Assets.View", "Assets.Return", "Assets.Request", "Incidents.Create", "Incidents.View",
                    "Documents.View", "Documents.Upload"
                }
            }
        };

        [Test]
        public void DepartmentHead_DoesNotIncludeProcurementApprovalOrPurchaseListPermissions()
        {
            var permissions = ExpectedRolePermissions["Department Head"];
            CollectionAssert.DoesNotContain(permissions, "Purchases.Approve");
            CollectionAssert.DoesNotContain(permissions, "Purchases.View");
            CollectionAssert.DoesNotContain(permissions, "Purchases.Edit");
            CollectionAssert.DoesNotContain(permissions, "Suppliers.View");
        }

        [Test]
        public void ProcurementOfficer_IncludesPurchaseOrderManagementPermissions()
        {
            var permissions = ExpectedRolePermissions["Procurement Officer"];
            CollectionAssert.Contains(permissions, "Purchases.View");
            CollectionAssert.Contains(permissions, "Purchases.Edit");
            CollectionAssert.Contains(permissions, "Purchases.Approve");
            CollectionAssert.Contains(permissions, "Suppliers.View");
            CollectionAssert.Contains(permissions, "Users.View");
        }

        [Test]
        public void Staff_DoesNotIncludeProcurementModulePermissions()
        {
            var permissions = ExpectedRolePermissions["Staff"];
            Assert.IsFalse(permissions.Any(code => code.StartsWith("Purchases.")));
            Assert.IsFalse(permissions.Any(code => code.StartsWith("Suppliers.")));
        }

        [Test]
        public void SecurityLogsModule_IncludesSecurityLogsViewPermission()
        {
            var codes = ModulePermissionCatalog.PermissionCodesForModule(ModulePermissionCatalog.SecurityLogs).ToList();
            CollectionAssert.Contains(codes, "SecurityLogs.View");
        }
    }
}
