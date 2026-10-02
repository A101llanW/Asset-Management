using AssetManagement.Application.Helpers;
using AssetManagement.Domain.Enums;
using NUnit.Framework;

namespace AssetManagement.Tests.Departments
{
    [TestFixture]
    public class DepartmentLabelHelperTests
    {
        [Test]
        public void FormatCodeName_UsesAsciiHyphenSeparator()
        {
            Assert.AreEqual("IT - Information Technology", DepartmentLabelHelper.FormatCodeName("IT", "Information Technology"));
            Assert.IsFalse(DepartmentLabelHelper.FormatCodeName("IT", "Information Technology").Contains("\u2013"));
            Assert.IsFalse(DepartmentLabelHelper.FormatCodeName("IT", "Information Technology").Contains("\u2014"));
        }

        [Test]
        public void GetEditActionLabel_UsesGlossaryPerKind()
        {
            Assert.AreEqual("Edit room", DepartmentLabelHelper.GetEditActionLabel(DepartmentKind.Room, false));
            Assert.AreEqual("Edit sub-department", DepartmentLabelHelper.GetEditActionLabel(DepartmentKind.SubDepartment, false));
            Assert.AreEqual(
                "Edit administrative department",
                DepartmentLabelHelper.GetEditActionLabel(DepartmentKind.Administrative, true));
        }
    }
}
