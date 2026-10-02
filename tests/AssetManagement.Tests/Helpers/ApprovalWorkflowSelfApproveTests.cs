using System;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class ApprovalWorkflowSelfApproveTests
    {
        private const string Requester = "user-requester";
        private const string Other = "user-other";
        private const int StageRoleId = 7;
        private const int OtherRoleId = 9;

        [Test]
        public void AllowsEligibleSelfApproval_IncludesPurchaseTransferDisposal()
        {
            Assert.IsTrue(ApprovalWorkflowHelper.AllowsEligibleSelfApproval(ApprovalProcessCodes.Purchase));
            Assert.IsTrue(ApprovalWorkflowHelper.AllowsEligibleSelfApproval(ApprovalProcessCodes.Transfer));
            Assert.IsTrue(ApprovalWorkflowHelper.AllowsEligibleSelfApproval(ApprovalProcessCodes.Disposal));
            Assert.IsFalse(ApprovalWorkflowHelper.AllowsEligibleSelfApproval(ApprovalProcessCodes.AssetRequest));
            Assert.IsFalse(ApprovalWorkflowHelper.AllowsEligibleSelfApproval("Unknown"));
        }

        [Test]
        public void AllowsEligibleSelfApprovalForProcessName_MatchesInboxLabels()
        {
            Assert.IsTrue(ApprovalWorkflowHelper.AllowsEligibleSelfApprovalForProcessName("Requisition"));
            Assert.IsTrue(ApprovalWorkflowHelper.AllowsEligibleSelfApprovalForProcessName("Purchase Request"));
            Assert.IsTrue(ApprovalWorkflowHelper.AllowsEligibleSelfApprovalForProcessName("Asset Transfer"));
            Assert.IsTrue(ApprovalWorkflowHelper.AllowsEligibleSelfApprovalForProcessName("Asset Disposal"));
            Assert.IsTrue(ApprovalWorkflowHelper.AllowsEligibleSelfApprovalForProcessName("Transfer"));
            Assert.IsTrue(ApprovalWorkflowHelper.AllowsEligibleSelfApprovalForProcessName("Disposal"));
            Assert.IsFalse(ApprovalWorkflowHelper.AllowsEligibleSelfApprovalForProcessName("Asset Request"));
            Assert.IsFalse(ApprovalWorkflowHelper.AllowsEligibleSelfApprovalForProcessName(null));
        }

        [Test]
        public void EnsureUserCanApprove_Purchase_SelfRequesterWhoIsStageUser_Succeeds()
        {
            Assert.DoesNotThrow(() => ApprovalWorkflowHelper.EnsureUserCanApprove(
                StageRoleId,
                false,
                StageRoleId,
                Requester,
                Requester,
                Requester,
                ApprovalProcessCodes.Purchase));
        }

        [Test]
        public void EnsureUserCanApprove_Transfer_SelfRequesterWhoIsStageUser_Succeeds()
        {
            Assert.DoesNotThrow(() => ApprovalWorkflowHelper.EnsureUserCanApprove(
                StageRoleId,
                false,
                StageRoleId,
                Requester,
                Requester,
                Requester,
                ApprovalProcessCodes.Transfer));
        }

        [Test]
        public void EnsureUserCanApprove_Disposal_SelfRequesterWhoIsStageUser_Succeeds()
        {
            Assert.DoesNotThrow(() => ApprovalWorkflowHelper.EnsureUserCanApprove(
                StageRoleId,
                false,
                StageRoleId,
                Requester,
                Requester,
                Requester,
                ApprovalProcessCodes.Disposal));
        }

        [Test]
        public void EnsureUserCanApprove_Transfer_SelfRequesterWithMatchingRole_Succeeds()
        {
            Assert.DoesNotThrow(() => ApprovalWorkflowHelper.EnsureUserCanApprove(
                StageRoleId,
                false,
                StageRoleId,
                null,
                Requester,
                Requester,
                ApprovalProcessCodes.Transfer));
        }

        [Test]
        public void EnsureUserCanApprove_Transfer_SelfRequesterNotStageApprover_ThrowsSelfMessage()
        {
            // Fall-through reaches stage check: wrong user configured as stage approver.
            var ex = Assert.Throws<BusinessException>(() => ApprovalWorkflowHelper.EnsureUserCanApprove(
                StageRoleId,
                false,
                StageRoleId,
                Other,
                Requester,
                Requester,
                ApprovalProcessCodes.Transfer));
            Assert.AreEqual("You are not configured to approve this stage.", ex.Message);
        }

        [Test]
        public void EnsureUserCanApprove_Disposal_SelfRequesterWrongRole_ThrowsStageMessage()
        {
            var ex = Assert.Throws<BusinessException>(() => ApprovalWorkflowHelper.EnsureUserCanApprove(
                OtherRoleId,
                false,
                StageRoleId,
                null,
                Requester,
                Requester,
                ApprovalProcessCodes.Disposal));
            Assert.AreEqual("You are not configured to approve this stage.", ex.Message);
        }

        [Test]
        public void EnsureUserCanApprove_Transfer_OtherUserAsStageApprover_Succeeds()
        {
            Assert.DoesNotThrow(() => ApprovalWorkflowHelper.EnsureUserCanApprove(
                StageRoleId,
                false,
                StageRoleId,
                Other,
                Requester,
                Other,
                ApprovalProcessCodes.Transfer));
        }

        [Test]
        public void CanUserActOnStage_TransferSelf_WithoutEligibleFlag_Blocked()
        {
            Assert.IsFalse(ApprovalWorkflowHelper.CanUserActOnStage(
                Requester,
                Requester,
                false,
                StageRoleId,
                StageRoleId,
                Requester,
                allowEligibleSelfApproval: false));
        }

        [Test]
        public void CanUserActOnStage_TransferSelf_WithEligibleFlagAndStageUser_Allowed()
        {
            Assert.IsTrue(ApprovalWorkflowHelper.CanUserActOnStage(
                Requester,
                Requester,
                false,
                StageRoleId,
                StageRoleId,
                Requester,
                allowEligibleSelfApproval: true));
        }

        [Test]
        public void CanUserActOnStage_TransferSelf_WithEligibleFlagButWrongStageUser_Blocked()
        {
            Assert.IsFalse(ApprovalWorkflowHelper.CanUserActOnStage(
                Requester,
                Requester,
                false,
                StageRoleId,
                StageRoleId,
                Other,
                allowEligibleSelfApproval: true));
        }

        [Test]
        public void CanUserActOnStage_PurchaseSelf_WithEligibleFlagAndMatchingRole_Allowed()
        {
            Assert.IsTrue(ApprovalWorkflowHelper.CanUserActOnStage(
                Requester,
                Requester,
                false,
                StageRoleId,
                StageRoleId,
                null,
                allowEligibleSelfApproval: true));
        }
    }
}
