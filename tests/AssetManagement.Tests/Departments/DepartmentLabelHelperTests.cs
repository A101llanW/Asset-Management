using AssetManagement.Application.Helpers;
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
    }
}
