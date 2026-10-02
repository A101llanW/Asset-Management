using System.Linq;
using AssetManagement.Application.Helpers;
using AssetManagement.Domain.Enums;
using NUnit.Framework;

namespace AssetManagement.Tests.Departments
{
    [TestFixture]
    public class DepartmentKindSelectListBuilderTests
    {
        [Test]
        public void BuildItems_MarksOnlySelectedKind_UsingIntStringValues()
        {
            var items = DepartmentKindSelectListBuilder.BuildItems(DepartmentKind.Room);

            Assert.AreEqual(5, items.Count);
            Assert.AreEqual("0", items.Single(i => i.Text == "Administrative").Value);
            Assert.AreEqual("4", items.Single(i => i.Text == "Room").Value);

            Assert.IsTrue(items.Single(i => i.Value == "4").Selected, "Room must be selected");
            Assert.IsFalse(items.Single(i => i.Value == "0").Selected, "Administrative must not win by default");
            Assert.AreEqual(1, items.Count(i => i.Selected));
        }

        [Test]
        public void BuildItems_SelectsSubDepartment_AndGrade_AndAdmin()
        {
            Assert.IsTrue(DepartmentKindSelectListBuilder.BuildItems(DepartmentKind.SubDepartment).Single(i => i.Value == "3").Selected);
            Assert.IsTrue(DepartmentKindSelectListBuilder.BuildItems(DepartmentKind.Grade).Single(i => i.Value == "1").Selected);
            Assert.IsTrue(DepartmentKindSelectListBuilder.BuildItems(DepartmentKind.Administrative).Single(i => i.Value == "0").Selected);
            Assert.AreEqual("3", DepartmentKindSelectListBuilder.ToOptionValue(DepartmentKind.SubDepartment));
        }
    }
}
