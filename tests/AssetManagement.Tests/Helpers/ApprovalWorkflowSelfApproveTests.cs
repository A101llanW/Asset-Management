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

        // Stale (catalog-requisition-flows, 2026-10-12): references members removed from the production code;
        // excluded so the test project builds. Define LEGACY_ELIGIBLE_SELF_APPROVAL_API to resurrect if the API returns.
#if LEGACY_ELIGIBLE_SELF_APPROVAL_API
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
#endif

        [Test]
        public void EnsureUserCanApprove_Purchase_SelfRequesterWhoIsStageUser_IsBlocked()
        {
            // Current behaviour: self-approval is blocked unless break-glass admin bypass + Settings.AllowAdminSelfApproval.
            var ex = Assert.Throws<BusinessException>(() => ApprovalWorkflowHelper.EnsureUserCanApprove(
                StageRoleId,
                false,
                StageRoleId,
                Requester,
                Requester,
                Requester,
                ApprovalProcessCodes.Purchase));
            Assert.AreEqual("The requester cannot approve their own requisition.", ex.Message);
        }

        [Test]
        public void EnsureUserCanApprove_Transfer_SelfRequesterWhoIsStageUser_IsBlocked()
        {
            // Current behaviour: self-approval is blocked unless break-glass admin bypass + Settings.AllowAdminSelfApproval.
            var ex = Assert.Throws<BusinessException>(() => ApprovalWorkflowHelper.EnsureUserCanApprove(
                StageRoleId,
                false,
                StageRoleId,
                Requester,
                Requester,
                Requester,
                ApprovalProcessCodes.Transfer));
            Assert.AreEqual("The requester cannot approve their own transfer request.", ex.Message);
        }

        [Test]
        public void EnsureUserCanApprove_Disposal_SelfRequesterWhoIsStageUser_IsBlocked()
        {
            // Current behaviour: self-approval is blocked unless break-glass admin bypass + Settings.AllowAdminSelfApproval.
            var ex = Assert.Throws<BusinessException>(() => ApprovalWorkflowHelper.EnsureUserCanApprove(
                StageRoleId,
                false,
                StageRoleId,
                Requester,
                Requester,
                Requester,
                ApprovalProcessCodes.Disposal));
            Assert.AreEqual("The requester cannot approve their own disposal request.", ex.Message);
        }

        [Test]
        public void EnsureUserCanApprove_Transfer_SelfRequesterWithMatchingRole_IsBlocked()
        {
            var ex = Assert.Throws<BusinessException>(() => ApprovalWorkflowHelper.EnsureUserCanApprove(
                StageRoleId,
                false,
                StageRoleId,
                null,
                Requester,
                Requester,
                ApprovalProcessCodes.Transfer));
            Assert.AreEqual("The requester cannot approve their own transfer request.", ex.Message);
        }

        [Test]
        public void EnsureUserCanApprove_Transfer_SelfRequesterNotStageApprover_ThrowsSelfMessage()
        {
            // The self-approval block runs before the stage check, so the requester gets the self message.
            var ex = Assert.Throws<BusinessException>(() => ApprovalWorkflowHelper.EnsureUserCanApprove(
                StageRoleId,
                false,
                StageRoleId,
                Other,
                Requester,
                Requester,
                ApprovalProcessCodes.Transfer));
            Assert.AreEqual("The requester cannot approve their own transfer request.", ex.Message);
        }

        [Test]
        public void EnsureUserCanApprove_Disposal_SelfRequesterWrongRole_ThrowsSelfMessage()
        {
            var ex = Assert.Throws<BusinessException>(() => ApprovalWorkflowHelper.EnsureUserCanApprove(
                OtherRoleId,
                false,
                StageRoleId,
                null,
                Requester,
                Requester,
                ApprovalProcessCodes.Disposal));
            Assert.AreEqual("The requester cannot approve their own disposal request.", ex.Message);
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

        // Stale (catalog-requisition-flows, 2026-10-12): references members removed from the production code;
        // excluded so the test project builds. Define LEGACY_ELIGIBLE_SELF_APPROVAL_API to resurrect if the API returns.
#if LEGACY_ELIGIBLE_SELF_APPROVAL_API
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
#endif
    }
}
