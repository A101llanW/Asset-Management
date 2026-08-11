using System.Collections.Generic;
using AssetManagement.Application.Helpers;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class SchoolDepartmentCodeHelperTests
    {
        [Test]
        public void BuildAdminDepartmentCode_UsesKnownAliases()
        {
            Assert.AreEqual("ADMIN", SchoolDepartmentCodeHelper.BuildAdminDepartmentCode("Administration"));
            Assert.AreEqual("IT", SchoolDepartmentCodeHelper.BuildAdminDepartmentCode("Information Technology"));
            Assert.AreEqual("IT", SchoolDepartmentCodeHelper.BuildAdminDepartmentCode("IT"));
        }

        [Test]
        public void BuildSubDepartmentCode_CombinesParentAndSubUnit()
        {
            Assert.AreEqual("IT-COMPLABSEN", SchoolDepartmentCodeHelper.BuildSubDepartmentCode("IT", "Comp Lab - Senior"));
            Assert.AreEqual("ADMIN-RECEPTION", SchoolDepartmentCodeHelper.BuildSubDepartmentCode("ADMIN", "Reception"));
        }

        [Test]
        public void ShouldResolveAsSubDepartment_RequiresNonClassroomValues()
        {
            Assert.IsTrue(SchoolDepartmentCodeHelper.ShouldResolveAsSubDepartment("Information Technology", "Comp Lab - Senior"));
            Assert.IsTrue(SchoolDepartmentCodeHelper.ShouldResolveAsSubDepartment("Administration", "Reception"));
            Assert.IsFalse(SchoolDepartmentCodeHelper.ShouldResolveAsSubDepartment("Classroom", "2A"));
            Assert.IsFalse(SchoolDepartmentCodeHelper.ShouldResolveAsSubDepartment("Administration", string.Empty));
        }
    }

    [TestFixture]
    public class SchoolImportProvisionerTests
    {
        [Test]
        public void ProvisionFromRows_CreatesAdminSubUnitsAndClassHierarchyFromTemplate()
        {
            var unitOfWork = new FakeUnitOfWork();
            var provisioner = new SchoolImportProvisioner(
                unitOfWork,
                new FakeOrganizationScopeService(),
                new FakeReferenceDataCache());

            var rows = new List<IDictionary<string, string>>
            {
                Row("Finance team laptop", "IT Equipment", "Laptop", "Information Technology", "Comp Lab - Senior"),
                Row("Reception desktop", "Office equipment", "Desktop", "Administration", string.Empty),
                Row("Class desk", "Furniture", "Desk", "Classroom", "3C")
            };

            var result = provisioner.ProvisionFromRows(rows, GetValue);

            Assert.AreEqual(5, result.DepartmentsCreated);
            var departments = unitOfWork.Repository<Department>().GetAll();
            var itParent = FindByCode(departments, "IT");
            var itSub = FindByCode(departments, "IT-COMPLABSEN");
            var adminParent = FindByCode(departments, "ADMIN");
            var classLeaf = FindByCode(departments, "G03C");

            Assert.IsNotNull(itParent);
            Assert.IsFalse(itParent.IsRequisitionTarget);
            Assert.AreEqual(DepartmentKind.Administrative, itParent.DepartmentKind);

            Assert.IsNotNull(itSub);
            Assert.AreEqual(itParent.Id, itSub.ParentDepartmentId);
            Assert.AreEqual(DepartmentKind.SubDepartment, itSub.DepartmentKind);
            Assert.IsTrue(itSub.IsRequisitionTarget);

            Assert.IsNotNull(adminParent);
            Assert.IsTrue(adminParent.IsRequisitionTarget);

            Assert.IsNotNull(classLeaf);
            Assert.AreEqual(DepartmentKind.Class, classLeaf.DepartmentKind);
        }

        [Test]
        public void ProvisionFromRows_UsesDistinctTemplateDepartmentsOnly()
        {
            var unitOfWork = new FakeUnitOfWork();
            var provisioner = new SchoolImportProvisioner(
                unitOfWork,
                new FakeOrganizationScopeService(),
                new FakeReferenceDataCache());

            var rows = new List<IDictionary<string, string>>
            {
                Row("Desk", "Furniture", "Desk", "Administration", string.Empty),
                Row("Chair", "Furniture", "Chair", "Administration", string.Empty),
                Row("Board", "Furniture", "Board", "Classroom", "2A")
            };

            provisioner.ProvisionFromRows(rows, GetValue);

            var adminCount = 0;
            foreach (var department in unitOfWork.Repository<Department>().GetAll())
            {
                if (department.DepartmentKind == DepartmentKind.Administrative)
                {
                    adminCount++;
                }
            }

            Assert.AreEqual(1, adminCount);
        }

        private static Dictionary<string, string> Row(
            string assetName,
            string category,
            string assetType,
            string department,
            string classValue)
        {
            return new Dictionary<string, string>
            {
                { "AssetName", assetName },
                { "AssetCategory", category },
                { "AssetType", assetType },
                { "Department", department },
                { "Class", classValue }
            };
        }

        private static string GetValue(IDictionary<string, string> row, string key)
        {
            string value;
            return row.TryGetValue(key, out value) ? value : null;
        }

        private static Department FindByCode(IEnumerable<Department> departments, string code)
        {
            foreach (var department in departments)
            {
                if (string.Equals(department.Code, code, System.StringComparison.OrdinalIgnoreCase))
                {
                    return department;
                }
            }

            return null;
        }
    }
}
