using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Queries;
using NUnit.Framework;

namespace AssetManagement.Tests.Assets
{
    [TestFixture]
    public class AssetRoomGroupViewTests
    {
        private static AssetRoomGroupVm Unit(int? id, string name, DepartmentKind? kind, int count, int priced = -1, decimal total = 0m)
        {
            return new AssetRoomGroupVm
            {
                DepartmentId = id,
                Name = name,
                DepartmentKind = kind.HasValue ? (int?)(int)kind.Value : null,
                AssetCount = count,
                PricedCount = priced < 0 ? count : priced,
                PricedTotal = total
            };
        }

        [Test]
        public void IsRoomView_AcceptsRoomOnly()
        {
            Assert.IsTrue(AssetRoomGroupView.IsRoomView("room"));
            Assert.IsTrue(AssetRoomGroupView.IsRoomView(" ROOM "));
            Assert.IsFalse(AssetRoomGroupView.IsRoomView("grouped"));
            Assert.IsFalse(AssetRoomGroupView.IsRoomView("flat"));
            Assert.IsFalse(AssetRoomGroupView.IsRoomView(null));
        }

        [Test]
        public void SortAndPage_RoomsFirstByName_ThenOtherUnits_UnassignedLast()
        {
            var page = AssetRoomGroupView.SortAndPage(new[]
            {
                Unit(null, null, null, 3),
                Unit(10, "zeta class", DepartmentKind.Class, 5),
                Unit(11, "Music Room", DepartmentKind.Room, 7),
                Unit(12, "art room", DepartmentKind.Room, 2),
                Unit(13, "Administration", DepartmentKind.Administrative, 4)
            }, 1, 10);

            CollectionAssert.AreEqual(
                new[] { "art room", "Music Room", "Administration", "zeta class", AssetRoomGroupView.UnassignedLabel },
                page.Items.Select(x => x.DisplayName).ToArray());
            Assert.AreEqual(5, page.TotalCount);
            Assert.AreEqual(2, page.RoomCount);
            Assert.AreEqual(21, page.TotalAssetCount);
            Assert.AreEqual(AssetRoomGroupView.UnassignedRoomId, page.Items.Last().RoomId);
        }

        [Test]
        public void SortAndPage_PagesAfterSorting_AndClampsPage()
        {
            var units = Enumerable.Range(1, 23).Select(i => Unit(i, "Room " + i.ToString("00"), DepartmentKind.Room, i)).ToList();

            var second = AssetRoomGroupView.SortAndPage(units, 2, 10);
            Assert.AreEqual(2, second.Page);
            Assert.AreEqual(3, second.TotalPages);
            Assert.AreEqual("Room 11", second.Items.First().Name);
            Assert.AreEqual(10, second.Items.Count);

            var beyond = AssetRoomGroupView.SortAndPage(units, 99, 10);
            Assert.AreEqual(3, beyond.Page);
            Assert.AreEqual(3, beyond.Items.Count);

            var empty = AssetRoomGroupView.SortAndPage(new List<AssetRoomGroupVm>(), 5, 10);
            Assert.AreEqual(1, empty.Page);
            Assert.AreEqual(0, empty.TotalCount);
        }

        [Test]
        public void UnpricedAssets_AreCountedNotSummed()
        {
            var page = AssetRoomGroupView.SortAndPage(new[]
            {
                Unit(1, "A", DepartmentKind.Room, 5, 3, 300m),
                Unit(2, "B", DepartmentKind.Room, 2, 0, 0m)
            }, 1, 10);

            Assert.AreEqual(2, page.Items[0].UnpricedCount);
            Assert.AreEqual(2, page.Items[1].UnpricedCount);
            Assert.AreEqual(4, page.UnpricedCount);
            Assert.AreEqual(300m, page.PricedTotal);
        }

        [Test]
        public void RoomWhereClause_UsesCaseScope_SameFiltersAsIndex()
        {
            var filter = new AssetFilterVm { Search = "Chair", Status = AssetStatus.InStore, CategoryId = 4, UnassignedOnly = true };
            var indexWhere = AssetQueryService.BuildWhereClause(filter);
            var roomWhere = AssetRoomGroupQueryService.BuildRoomWhereClause(filter);

            StringAssert.Contains(SqlQueryHelper.FormatAssetDepartmentScopeSql("a"), indexWhere);
            StringAssert.DoesNotContain(SqlQueryHelper.FormatAssetDepartmentScopeSql("a"), roomWhere);
            StringAssert.Contains(AssetRoomGroupQueryService.CaseDepartmentScopeSql, roomWhere);
            Assert.AreEqual(
                indexWhere.Replace(SqlQueryHelper.FormatAssetDepartmentScopeSql("a"), string.Empty),
                roomWhere.Replace(AssetRoomGroupQueryService.CaseDepartmentScopeSql, string.Empty));
            StringAssert.Contains("a.[OrganizationId] = @OrganizationId", roomWhere);
            StringAssert.Contains("a.[IsActive] = 1", roomWhere);
            StringAssert.Contains("@SearchPrefix", roomWhere);
            StringAssert.Contains("a.[CurrentStatus] = @Status", roomWhere);
            StringAssert.Contains("a.[CategoryId] = @CategoryId", roomWhere);
        }

        [Test]
        public void FormatKind_LabelsUnits()
        {
            Assert.AreEqual("Room", AssetRoomGroupView.FormatKind((int)DepartmentKind.Room));
            Assert.AreEqual("Class", AssetRoomGroupView.FormatKind((int)DepartmentKind.Class));
            Assert.AreEqual("Sub-department", AssetRoomGroupView.FormatKind((int)DepartmentKind.SubDepartment));
            Assert.AreEqual("Department", AssetRoomGroupView.FormatKind((int)DepartmentKind.Administrative));
            Assert.IsNull(AssetRoomGroupView.FormatKind(null));
        }
    }
}
