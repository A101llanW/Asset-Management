using AssetManagement.Application.Services;
using AssetManagement.Domain.Enums;
using NUnit.Framework;

namespace AssetManagement.Tests.Assets
{
    [TestFixture]
    public class AssetCustodyRulesTests
    {
        [Test]
        public void HasAnyQuickAction_ReturnsTrueWhenAssignAllowedForInStoreAsset()
        {
            Assert.IsTrue(AssetCustodyRules.HasAnyQuickAction(
                AssetStatus.InStore,
                canAssign: true,
                canTransfer: false,
                canReturn: false,
                canReportIncident: false));
        }

        [Test]
        public void HasAnyQuickAction_ReturnsFalseWhenOnlyViewLevelPermissionsWouldApply()
        {
            Assert.IsFalse(AssetCustodyRules.HasAnyQuickAction(
                AssetStatus.Assigned,
                canAssign: false,
                canTransfer: false,
                canReturn: false,
                canReportIncident: false));
        }

        [Test]
        public void HasAnyQuickAction_ReturnsTrueForIncidentPermissionRegardlessOfStatus()
        {
            Assert.IsTrue(AssetCustodyRules.HasAnyQuickAction(
                AssetStatus.Disposed,
                canAssign: false,
                canTransfer: false,
                canReturn: false,
                canReportIncident: true));
        }
    }
}
