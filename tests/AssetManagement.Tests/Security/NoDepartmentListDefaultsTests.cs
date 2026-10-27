using AssetManagement.Application.ViewModels;
using AssetManagement.Web.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    /// <summary>
    /// 2026-10-27 (QA follow-up to the no-department fix): a no-department user has organization-wide scope, so the
    /// default Assets and Assignments lists open on the whole organization instead of "in my custody" (which showed
    /// 'No assets yet'). Users in a department keep their current default; Assets.ViewAll users skip defaults.
    /// </summary>
    [TestFixture]
    public class NoDepartmentListDefaultsTests
    {
        private static UserVm NoDepartment()
        {
            return new UserVm { Id = "u-nodept", DepartmentId = null };
        }

        private static UserVm InDepartment()
        {
            return new UserVm { Id = "u-dept", DepartmentId = 9 };
        }

        [Test]
        public void HasOrganizationWideDefault_FollowsTheDepartmentScopeRule()
        {
            Assert.IsTrue(ListRoleDefaultsHelper.HasOrganizationWideDefault(NoDepartment()));
            Assert.IsTrue(ListRoleDefaultsHelper.HasOrganizationWideDefault(new UserVm { Id = "u0", DepartmentId = 0 }));
            Assert.IsFalse(ListRoleDefaultsHelper.HasOrganizationWideDefault(InDepartment()));
            Assert.IsFalse(ListRoleDefaultsHelper.HasOrganizationWideDefault(null), "no profile stays fail-closed");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AssetList_NoDepartment_NoFilter_OpensOrganizationWide(bool canManage)
        {
            var filter = ListRoleDefaultsHelper.ApplyAssetListDefaults(new AssetFilterVm(), NoDepartment(), canManage, false);

            Assert.IsNull(filter.CustodianUserId, "no 'in my custody' fallback");
            Assert.IsFalse(filter.DepartmentId.HasValue);
            Assert.IsFalse(filter.UnassignedOnly);
        }

        [Test]
        public void AssetList_NullFilter_NoDepartment_ReturnsEmptyFilter()
        {
            var filter = ListRoleDefaultsHelper.ApplyAssetListDefaults(null, NoDepartment(), true, false);

            Assert.IsNotNull(filter);
            Assert.IsNull(filter.CustodianUserId);
            Assert.IsFalse(filter.DepartmentId.HasValue);
        }

        [Test]
        public void AssetList_InDepartment_Manager_KeepsDepartmentDefault()
        {
            var filter = ListRoleDefaultsHelper.ApplyAssetListDefaults(new AssetFilterVm(), InDepartment(), true, false);

            Assert.AreEqual(9, filter.DepartmentId);
            Assert.IsNull(filter.CustodianUserId);
        }

        [Test]
        public void AssetList_InDepartment_NonManager_KeepsCustodyDefault()
        {
            var filter = ListRoleDefaultsHelper.ApplyAssetListDefaults(new AssetFilterVm(), InDepartment(), false, false);

            Assert.AreEqual("u-dept", filter.CustodianUserId);
            Assert.IsFalse(filter.DepartmentId.HasValue);
        }

        [Test]
        public void AssetList_ViewAll_SkipsDefaults_EvenWithADepartment()
        {
            var filter = ListRoleDefaultsHelper.ApplyAssetListDefaults(new AssetFilterVm(), InDepartment(), true, true);

            Assert.IsFalse(filter.DepartmentId.HasValue);
            Assert.IsNull(filter.CustodianUserId);
        }

        [Test]
        public void AssetList_NoDepartment_ExplicitFilterIsKept()
        {
            var filter = ListRoleDefaultsHelper.ApplyAssetListDefaults(
                new AssetFilterVm { DepartmentId = 20, Search = "laptop" }, NoDepartment(), false, false);

            Assert.AreEqual(20, filter.DepartmentId);
            Assert.AreEqual("laptop", filter.Search);
            Assert.IsNull(filter.CustodianUserId);
        }

        [Test]
        public void AssetList_NoDepartment_BatchAssignFromFilter_IsNotForcedToOwnCustody()
        {
            // AssignmentsController.LoadBatchAssignRows applies the defaults, then sets UnassignedOnly. With the old
            // custody fallback that was "held by me AND unassigned", which is always empty.
            var filter = ListRoleDefaultsHelper.ApplyAssetListDefaults(new AssetFilterVm(), NoDepartment(), true, false);
            filter.UnassignedOnly = true;

            Assert.IsNull(filter.CustodianUserId);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AssignmentList_NoDepartment_NoFilter_OpensOrganizationWide(bool canAssign)
        {
            var filter = ListRoleDefaultsHelper.ApplyAssignmentListDefaults(new AssignmentFilterVm(), NoDepartment(), canAssign, false);

            Assert.IsNull(filter.CustodianUserId);
            Assert.IsFalse(filter.DepartmentId.HasValue);
            Assert.IsFalse(filter.ActiveOnly.HasValue);
        }

        [Test]
        public void AssignmentList_InDepartment_KeepsCurrentDefaults()
        {
            var manager = ListRoleDefaultsHelper.ApplyAssignmentListDefaults(new AssignmentFilterVm(), InDepartment(), true, false);
            Assert.AreEqual(9, manager.DepartmentId);
            Assert.IsNull(manager.CustodianUserId);

            var custodian = ListRoleDefaultsHelper.ApplyAssignmentListDefaults(new AssignmentFilterVm(), InDepartment(), false, false);
            Assert.AreEqual("u-dept", custodian.CustodianUserId);
            Assert.AreEqual(true, custodian.ActiveOnly);
        }

        [Test]
        public void AssetRequestList_DefaultIsByRequester_NotByDepartment_AndIsUnchanged()
        {
            // "My requests" for non-approvers applies to every user, with or without a department, so it is not the
            // department fallback pattern and stays as it was.
            var noDept = ListRoleDefaultsHelper.ApplyAssetRequestListDefaults(new AssetRequestFilterVm(), NoDepartment(), false, false);
            Assert.AreEqual("u-nodept", noDept.RequestedById);

            var approver = ListRoleDefaultsHelper.ApplyAssetRequestListDefaults(new AssetRequestFilterVm(), NoDepartment(), true, false);
            Assert.IsTrue(string.IsNullOrWhiteSpace(approver.RequestedById));
        }
    }
}