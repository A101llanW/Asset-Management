using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Helpers;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class ClassToRoomConversionHelperTests
    {
        [Test]
        public void BuildPlan_GroupsByGrade_CreatesContainerAndMutations()
        {
            var admin = Dept(1, "Academics", "ACAD", DepartmentKind.Administrative, null);
            var grade = Dept(2, "Grade 5", "G5", DepartmentKind.Grade, null);
            var classA = Dept(3, "5A", "5A", DepartmentKind.Class, 2);
            classA.ClassTeacherUserId = "teacher-1";
            var all = new List<Department> { admin, grade, classA };

            var plan = ClassToRoomConversionHelper.BuildPlan(all, 1, true);

            Assert.AreEqual(1, plan.NewDepartments.Count);
            Assert.AreEqual("grade-2", plan.NewDepartments[0].TempKey);
            Assert.AreEqual(1, plan.Mutations.Count);
            Assert.AreEqual(3, plan.Mutations[0].DepartmentId);
            Assert.AreEqual(DepartmentKind.Room, plan.Mutations[0].NewKind);
            Assert.AreEqual("grade-2", plan.Mutations[0].NewParentContainerTempKey);
            Assert.AreEqual("teacher-1", plan.Mutations[0].RoomCustodianUserId);
        }

        [Test]
        public void BuildPlan_ReusesExistingGradeContainer()
        {
            var admin = Dept(1, "Academics", "ACAD", DepartmentKind.Administrative, null);
            var grade = Dept(2, "Grade 5", "G5", DepartmentKind.Grade, null);
            var container = Dept(4, "Grade 5 homerooms", "ACAD-G5HR", DepartmentKind.SubDepartment, 1);
            var classA = Dept(3, "5A", "5A", DepartmentKind.Class, 2);
            var all = new List<Department> { admin, grade, classA, container };

            var plan = ClassToRoomConversionHelper.BuildPlan(all, 1, true);

            Assert.AreEqual(0, plan.NewDepartments.Count);
            Assert.AreEqual(4, plan.Mutations[0].NewParentDepartmentId);
        }

        [Test]
        public void BuildPlan_SkipsClassWithoutGradeParent()
        {
            var admin = Dept(1, "Academics", "ACAD", DepartmentKind.Administrative, null);
            var orphan = Dept(3, "Orphan", "ORP", DepartmentKind.Class, 1);
            var all = new List<Department> { admin, orphan };

            var plan = ClassToRoomConversionHelper.BuildPlan(all, 1, true);

            Assert.AreEqual(0, plan.Mutations.Count);
            Assert.IsTrue(plan.Warnings.Any(x => x.Contains("Orphan")));
        }

        [Test]
        public void BuildPlan_NoClasses_ReturnsEmptyPlan()
        {
            var admin = Dept(1, "Academics", "ACAD", DepartmentKind.Administrative, null);
            var plan = ClassToRoomConversionHelper.BuildPlan(new List<Department> { admin }, 1, true);
            Assert.AreEqual(0, plan.Mutations.Count);
            Assert.AreEqual(0, plan.NewDepartments.Count);
        }

        [Test]
        public void BuildPlan_InvalidHostDepartment_Throws()
        {
            var admin = Dept(1, "Academics", "ACAD", DepartmentKind.Administrative, null);
            var child = Dept(5, "Sub", "SUB", DepartmentKind.SubDepartment, 1);
            var all = new List<Department> { admin, child };
            Assert.That(() => ClassToRoomConversionHelper.BuildPlan(all, 5, true), Throws.Exception);
            Assert.That(() => ClassToRoomConversionHelper.BuildPlan(all, 999, true), Throws.Exception);
        }

        [Test]
        public void BuildPlan_FlatMode_CreatesSingleHomeroomsContainer()
        {
            var admin = Dept(1, "Academics", "ACAD", DepartmentKind.Administrative, null);
            var g5 = Dept(2, "Grade 5", "G5", DepartmentKind.Grade, null);
            var g6 = Dept(6, "Grade 6", "G6", DepartmentKind.Grade, null);
            var all = new List<Department> { admin, g5, g6,
                Dept(3, "5A", "5A", DepartmentKind.Class, 2), Dept(7, "6A", "6A", DepartmentKind.Class, 6) };

            var plan = ClassToRoomConversionHelper.BuildPlan(all, 1, false);

            Assert.AreEqual(1, plan.NewDepartments.Count);
            Assert.AreEqual(ClassToRoomConversionHelper.FlatContainerTempKey, plan.NewDepartments[0].TempKey);
            Assert.AreEqual(2, plan.Mutations.Count);
            Assert.IsTrue(plan.Mutations.All(m => m.NewParentContainerTempKey == ClassToRoomConversionHelper.FlatContainerTempKey));
        }

        [Test]
        public void BuildPlan_TwoClassesSameGrade_ShareOneContainer()
        {
            var admin = Dept(1, "Academics", "ACAD", DepartmentKind.Administrative, null);
            var grade = Dept(2, "Grade 5", "G5", DepartmentKind.Grade, null);
            var all = new List<Department> { admin, grade,
                Dept(3, "5A", "5A", DepartmentKind.Class, 2), Dept(4, "5B", "5B", DepartmentKind.Class, 2) };

            var plan = ClassToRoomConversionHelper.BuildPlan(all, 1, true);

            Assert.AreEqual(1, plan.NewDepartments.Count);
            Assert.AreEqual(2, plan.Mutations.Count);
        }

        [Test]
        public void BuildPlan_SkipsClassWithActiveChildren()
        {
            var admin = Dept(1, "Academics", "ACAD", DepartmentKind.Administrative, null);
            var grade = Dept(2, "Grade 5", "G5", DepartmentKind.Grade, null);
            var classA = Dept(3, "5A", "5A", DepartmentKind.Class, 2);
            var childOfClass = Dept(8, "Lab", "LAB", DepartmentKind.Room, 3);
            var plan = ClassToRoomConversionHelper.BuildPlan(new List<Department> { admin, grade, classA, childOfClass }, 1, true);

            Assert.AreEqual(0, plan.Mutations.Count);
            Assert.IsTrue(plan.Warnings.Any(x => x.Contains("5A")));
        }

        [Test]
        public void BuildPlan_BlankTeacher_GivesNullCustodian_AndIgnoresInactiveClasses()
        {
            var admin = Dept(1, "Academics", "ACAD", DepartmentKind.Administrative, null);
            var grade = Dept(2, "Grade 5", "G5", DepartmentKind.Grade, null);
            var classA = Dept(3, "5A", "5A", DepartmentKind.Class, 2);
            classA.ClassTeacherUserId = "   ";
            var inactive = Dept(4, "5Z", "5Z", DepartmentKind.Class, 2);
            inactive.IsActive = false;
            var plan = ClassToRoomConversionHelper.BuildPlan(new List<Department> { admin, grade, classA, inactive }, 1, true);

            Assert.AreEqual(1, plan.Mutations.Count);
            Assert.IsNull(plan.Mutations[0].RoomCustodianUserId);
        }

        [Test]
        public void BuildPlan_ContainerCode_IsUniqueAgainstExistingCodes()
        {
            var admin = Dept(1, "Academics", "ACAD", DepartmentKind.Administrative, null);
            var grade = Dept(2, "Grade 5", "G5", DepartmentKind.Grade, null);
            var first = ClassToRoomConversionHelper.BuildPlan(new List<Department> { admin, grade,
                Dept(3, "5A", "5A", DepartmentKind.Class, 2) }, 1, true).NewDepartments[0].Code;
            var clash = Dept(9, "Other", first, DepartmentKind.SubDepartment, 1);
            var second = ClassToRoomConversionHelper.BuildPlan(new List<Department> { admin, grade, clash,
                Dept(3, "5A", "5A", DepartmentKind.Class, 2) }, 1, true).NewDepartments[0].Code;

            Assert.AreNotEqual(first, second, "container code must not clash with an existing code");
        }

        private static Department Dept(int id, string name, string code, DepartmentKind kind, int? parentId)
        {
            return new Department
            {
                Id = id,
                Name = name,
                Code = code,
                DepartmentKind = kind,
                ParentDepartmentId = parentId,
                IsActive = true,
                IsRequisitionTarget = true
            };
        }
    }
}
