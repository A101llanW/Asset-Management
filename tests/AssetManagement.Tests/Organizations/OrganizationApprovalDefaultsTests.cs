using System;
using System.Collections.Generic;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using NUnit.Framework;

namespace AssetManagement.Tests.Organizations
{
    [TestFixture]
    public class OrganizationApprovalDefaultsTests
    {
        [Test]
        public void GetDefaultApproverRoleName_UsesDepartmentHeadForTransfer()
        {
            Assert.AreEqual(OrganizationApprovalDefaults.DepartmentHeadRoleName,
                OrganizationApprovalDefaults.GetDefaultApproverRoleName(ApprovalProcessCodes.Transfer));
        }

        [Test]
        public void GetDefaultApproverRoleName_UsesCompanyAdminForDisposal()
        {
            Assert.AreEqual(OrganizationApprovalDefaults.CompanyAdminRoleName,
                OrganizationApprovalDefaults.GetDefaultApproverRoleName(ApprovalProcessCodes.Disposal));
        }

        [Test]
        public void GetDefaultApproverRoleName_UsesProcurementManagerForPurchase()
        {
            Assert.AreEqual(OrganizationApprovalDefaults.ProcurementManagerRoleName,
                OrganizationApprovalDefaults.GetDefaultApproverRoleName(ApprovalProcessCodes.Purchase));
        }

        [Test]
        public void EnsureApprovalSettings_WritesTenantRoleIdsNotTemplateIds()
        {
            var orgId = 99;
            var now = DateTime.UtcNow;
            var roles = new List<Role>
            {
                new Role { Id = 501, Name = OrganizationApprovalDefaults.CompanyAdminRoleName, IsActive = true },
                new Role { Id = 502, Name = OrganizationApprovalDefaults.DepartmentHeadRoleName, IsActive = true },
                new Role { Id = 503, Name = OrganizationApprovalDefaults.ProcurementManagerRoleName, IsActive = true }
            };

            var added = new List<SystemSetting>();
            OrganizationApprovalDefaults.EnsureApprovalSettings(
                new List<SystemSetting>(),
                roles,
                orgId,
                now,
                added.Add,
                null,
                refreshInvalidStageRoleIds: true);

            var transferSetting = added.Find(x => x.SettingKey == ApprovalProcessCodes.GetStageRoleIdsSettingKey(ApprovalProcessCodes.Transfer));
            var disposalSetting = added.Find(x => x.SettingKey == ApprovalProcessCodes.GetStageRoleIdsSettingKey(ApprovalProcessCodes.Disposal));
            var purchaseSetting = added.Find(x => x.SettingKey == ApprovalProcessCodes.GetStageRoleIdsSettingKey(ApprovalProcessCodes.Purchase));

            Assert.IsNotNull(transferSetting);
            Assert.AreEqual("502", transferSetting.SettingValue);
            Assert.IsNotNull(disposalSetting);
            Assert.AreEqual("501", disposalSetting.SettingValue);
            Assert.IsNotNull(purchaseSetting);
            Assert.AreEqual("503", purchaseSetting.SettingValue);
        }

        [Test]
        public void StageRoleIdsAreValidForOrganization_RejectsForeignRoleIds()
        {
            var valid = OrganizationApprovalDefaults.StageRoleIdsAreValidForOrganization(
                "2,5",
                new HashSet<int> { 501, 502, 503 });

            Assert.IsFalse(valid);
        }

        [Test]
        public void IsApprovalSettingKey_MatchesApprovalKeys()
        {
            Assert.IsTrue(OrganizationApprovalDefaults.IsApprovalSettingKey("Approval.Process.Transfer.StageRoleIds"));
            Assert.IsTrue(OrganizationApprovalDefaults.IsApprovalSettingKey("Approval.RequireTransferApproval"));
            Assert.IsFalse(OrganizationApprovalDefaults.IsApprovalSettingKey("Finance.DefaultCurrency"));
        }
    }
}
