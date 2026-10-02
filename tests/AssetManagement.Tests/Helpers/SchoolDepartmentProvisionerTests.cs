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

        [Test]
        public void TryResolveInformationTechnologySubUnit_MapsIctAndSharedScopeToItIctSubDepartment()
        {
            string parentName;
            string subUnitName;

            Assert.IsTrue(SchoolDepartmentCodeHelper.TryResolveInformationTechnologySubUnit(
                "ICT", "ALL", out parentName, out subUnitName));
            Assert.AreEqual("Information Technology", parentName);
            Assert.AreEqual("ICT", subUnitName);

            Assert.IsTrue(SchoolDepartmentCodeHelper.TryResolveInformationTechnologySubUnit(
                "Information Technology", "All", out parentName, out subUnitName));
            Assert.AreEqual("Information Technology", parentName);
            Assert.AreEqual("ICT", subUnitName);

            Assert.IsTrue(SchoolDepartmentCodeHelper.TryResolveInformationTechnologySubUnit(
                "ICT", string.Empty, out parentName, out subUnitName));
            Assert.AreEqual("ICT", subUnitName);

            Assert.IsTrue(SchoolDepartmentCodeHelper.TryResolveInformationTechnologySubUnit(
                "Information Technology", "Comp Lab - Senior", out parentName, out subUnitName));
            Assert.AreEqual("Comp Lab - Senior", subUnitName);
        }

        [Test]
        public void BuildSubDepartmentCode_UsesIctInsteadOfAllForSharedScope()
        {
            Assert.AreEqual("IT-ICT", SchoolDepartmentCodeHelper.BuildSubDepartmentCode("IT", "ICT"));
        }

        [Test]
        public void IsRoomLikeDepartmentName_DetectsRoomsAndLabs()
        {
            Assert.IsTrue(SchoolDepartmentCodeHelper.IsRoomLikeDepartmentName("Art room"));
            Assert.IsTrue(SchoolDepartmentCodeHelper.IsRoomLikeDepartmentName("Music Room"));
            Assert.IsTrue(SchoolDepartmentCodeHelper.IsRoomLikeDepartmentName("Biology Lab"));
            Assert.IsTrue(SchoolDepartmentCodeHelper.IsRoomLikeDepartmentName("Meet Room"));
            Assert.IsFalse(SchoolDepartmentCodeHelper.IsRoomLikeDepartmentName("Administration"));
            Assert.IsFalse(SchoolDepartmentCodeHelper.IsRoomLikeDepartmentName("Classroom"));
            Assert.IsFalse(SchoolDepartmentCodeHelper.IsRoomLikeDepartmentName("Academics"));
        }

        [Test]
        public void IsAdministrativeDepartmentName_ExcludesRoomLikeNames()
        {
            Assert.IsTrue(SchoolDepartmentCodeHelper.IsAdministrativeDepartmentName("Administration"));
            Assert.IsTrue(SchoolDepartmentCodeHelper.IsAdministrativeDepartmentName("Examinations"));
            Assert.IsFalse(SchoolDepartmentCodeHelper.IsAdministrativeDepartmentName("Art room"));
            Assert.IsFalse(SchoolDepartmentCodeHelper.IsAdministrativeDepartmentName("Biology Lab"));
        }

        [Test]
        public void BuildRoomCode_CombinesParentAndRoomName()
        {
            Assert.AreEqual("ITCOMPLABS-LAB1", SchoolDepartmentCodeHelper.BuildRoomCode("IT-COMPLABSE", "Lab 1"));
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
        public void ProvisionFromRows_CreatesItIctSubDepartmentForSharedScopeRows()
        {
            var unitOfWork = new FakeUnitOfWork();
            var provisioner = new SchoolImportProvisioner(
                unitOfWork,
                new FakeOrganizationScopeService(),
                new FakeReferenceDataCache());

            var rows = new List<IDictionary<string, string>>
            {
                Row("Plastic stool", "Furniture", "Stools", "ICT", "ALL"),
                Row("Desktop table", "Furniture", "Desks", "Information Technology", "All")
            };

            provisioner.ProvisionFromRows(rows, GetValue);

            var departments = unitOfWork.Repository<Department>().GetAll();
            Assert.IsNotNull(FindByCode(departments, "IT"));
            Assert.IsNotNull(FindByCode(departments, "IT-ICT"));
            Assert.IsNull(FindByCode(departments, "ICT"));
            Assert.IsNull(FindByCode(departments, "ICT-ALL"));
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


        [Test]
        public void ProvisionFromRows_CreatesRoomKindForRoomLikeDepartmentNames()
        {
            var unitOfWork = new FakeUnitOfWork();
            var provisioner = new SchoolImportProvisioner(
                unitOfWork,
                new FakeOrganizationScopeService(),
                new FakeReferenceDataCache());

            var rows = new List<IDictionary<string, string>>
            {
                Row("Easel", "Art Supplies", "Easel", "Art room", string.Empty),
                Row("Keyboard", "Music Equipment", "Keyboard", "Entertainment", "Music Room")
            };

            provisioner.ProvisionFromRows(rows, GetValue);

            var departments = unitOfWork.Repository<Department>().GetAll();
            Department art = null;
            Department music = null;
            foreach (var department in departments)
            {
                if (department.DepartmentKind == DepartmentKind.Room
                    && department.Name.IndexOf("Art", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    art = department;
                }

                if (department.DepartmentKind == DepartmentKind.Room
                    && department.Name.IndexOf("Music", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    music = department;
                }
            }

            Assert.IsNotNull(art);
            Assert.AreEqual(DepartmentKind.Room, art.DepartmentKind);
            Assert.IsNotNull(music);
            Assert.AreEqual(DepartmentKind.Room, music.DepartmentKind);
            Assert.IsFalse(art.DepartmentKind == DepartmentKind.Administrative);
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
