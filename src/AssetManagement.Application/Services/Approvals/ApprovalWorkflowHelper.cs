using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public static class ApprovalWorkflowHelper
    {
        public const string AllowAdminSelfApprovalSettingKey = "Settings.AllowAdminSelfApproval";

        public static ApprovalProcessConfiguration GetProcessConfiguration(IUnitOfWork unitOfWork, string processCode)
        {
            var settings = ApprovalWorkflowSettingsHelper.ToDictionary(unitOfWork.Repository<SystemSetting>().GetAll());
            var requiresApproval = ApprovalWorkflowSettingsHelper.GetBool(
                settings,
                ApprovalProcessCodes.GetEnabledSettingKey(processCode),
                ApprovalWorkflowSettingsHelper.GetBool(settings, ApprovalProcessCodes.GetLegacyRequireSettingKey(processCode), false));

            return new ApprovalProcessConfiguration
            {
                ProcessCode = processCode,
                DisplayName = ApprovalProcessCodes.GetDisplayName(processCode),
                RequiresApproval = requiresApproval,
                StageRoleIds = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(
                    ApprovalWorkflowSettingsHelper.GetString(settings, ApprovalProcessCodes.GetStageRoleIdsSettingKey(processCode))),
                StageUserIds = ApprovalWorkflowSettingsHelper.ParseStageUserIds(
                    ApprovalWorkflowSettingsHelper.GetString(settings, ApprovalProcessCodes.GetStageUserIdsSettingKey(processCode)))
            };
        }

        public static ApprovalProcessConfiguration GetAssetProcessConfiguration(IUnitOfWork unitOfWork, Asset asset, string processCode)
        {
            if (asset == null
                || (!string.Equals(processCode, ApprovalProcessCodes.Transfer, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(processCode, ApprovalProcessCodes.Disposal, StringComparison.OrdinalIgnoreCase)))
            {
                return GetProcessConfiguration(unitOfWork, processCode);
            }

            var systemConfig = GetProcessConfiguration(unitOfWork, processCode);
            var requiresApproval = string.Equals(processCode, ApprovalProcessCodes.Transfer, StringComparison.OrdinalIgnoreCase)
                ? asset.RequireTransferApproval
                : asset.RequireDisposalApproval;
            var configuredStages = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(
                string.Equals(processCode, ApprovalProcessCodes.Transfer, StringComparison.OrdinalIgnoreCase)
                    ? asset.TransferApprovalStageRoleIds
                    : asset.DisposalApprovalStageRoleIds);
            var configuredUsers = ApprovalWorkflowSettingsHelper.ParseStageUserIds(
                string.Equals(processCode, ApprovalProcessCodes.Transfer, StringComparison.OrdinalIgnoreCase)
                    ? asset.TransferApprovalStageUserIds
                    : asset.DisposalApprovalStageUserIds);
            var stageRoleIds = configuredStages.Count > 0 ? configuredStages : systemConfig.StageRoleIds;
            var stageUserIds = configuredStages.Count > 0 ? configuredUsers : systemConfig.StageUserIds;

            return new ApprovalProcessConfiguration
            {
                ProcessCode = processCode,
                DisplayName = ApprovalProcessCodes.GetDisplayName(processCode),
                RequiresApproval = requiresApproval,
                StageRoleIds = stageRoleIds,
                StageUserIds = stageUserIds
            };
        }

        public static void EnsureUserCanApprove(
            IUnitOfWork unitOfWork,
            IAuditWriter auditWriter,
            int? approverRoleId,
            bool bypassesApprovalRoleCheck,
            int expectedRoleId,
            string expectedUserId,
            string requesterUserId,
            string actingUserId,
            string processCode)
        {
            var requester = NormalizeUserId(requesterUserId);
            var actor = NormalizeUserId(actingUserId);
            var isSelfApproval = !string.IsNullOrWhiteSpace(requester)
                && !string.IsNullOrWhiteSpace(actor)
                && string.Equals(requester, actor, StringComparison.OrdinalIgnoreCase);

            if (isSelfApproval)
            {
                if (!bypassesApprovalRoleCheck || !IsAdminSelfApprovalAllowed(unitOfWork))
                {
                    throw new BusinessException(GetSelfApprovalMessage(processCode));
                }

                WriteBreakGlassAudit(auditWriter, "Approval.SelfApprove", processCode, actor, requester);
                return;
            }

            if (bypassesApprovalRoleCheck)
            {
                if (!string.IsNullOrWhiteSpace(expectedUserId)
                    && !string.Equals(NormalizeUserId(actor), NormalizeUserId(expectedUserId), StringComparison.OrdinalIgnoreCase))
                {
                    WriteBreakGlassAudit(
                        auditWriter,
                        "Approval.UserBypass",
                        processCode,
                        actor,
                        "ExpectedUser:" + expectedUserId);
                }
                else if (!approverRoleId.HasValue || approverRoleId.Value != expectedRoleId)
                {
                    WriteBreakGlassAudit(
                        auditWriter,
                        "Approval.RoleBypass",
                        processCode,
                        actor,
                        "ExpectedRole:" + expectedRoleId);
                }

                return;
            }

            if (!string.IsNullOrWhiteSpace(expectedUserId))
            {
                if (string.IsNullOrWhiteSpace(actor)
                    || !string.Equals(NormalizeUserId(actor), NormalizeUserId(expectedUserId), StringComparison.OrdinalIgnoreCase))
                {
                    throw new BusinessException("You are not configured to approve this stage.");
                }

                return;
            }

            if (!approverRoleId.HasValue || approverRoleId.Value != expectedRoleId)
            {
                throw new BusinessException("You are not configured to approve this stage.");
            }
        }

        public static void EnsureUserCanApprove(
            int? approverRoleId,
            bool bypassesApprovalRoleCheck,
            int expectedRoleId,
            string expectedUserId,
            string requesterUserId,
            string actingUserId,
            string processCode)
        {
            EnsureUserCanApprove(
                null,
                null,
                approverRoleId,
                bypassesApprovalRoleCheck,
                expectedRoleId,
                expectedUserId,
                requesterUserId,
                actingUserId,
                processCode);
        }

        public static int GetStageRoleId(IList<int> stageRoleIds, int stageNumber, string processCode)
        {
            if (stageRoleIds == null || stageNumber <= 0 || stageNumber > stageRoleIds.Count)
            {
                throw new BusinessException(GetMisconfiguredWorkflowMessage(processCode));
            }

            return stageRoleIds[stageNumber - 1];
        }

        public static string GetStageUserId(IList<string> stageUserIds, int stageNumber)
        {
            if (stageUserIds == null || stageNumber <= 0 || stageNumber > stageUserIds.Count)
            {
                return null;
            }

            var userId = stageUserIds[stageNumber - 1];
            return string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();
        }

        public static IList<ApprovalDecisionHistoryVm> MapDecisionHistory(
            IEnumerable<ApprovalActionSnapshot> actions,
            IDictionary<int, string> roleLookup)
        {
            return (actions ?? Enumerable.Empty<ApprovalActionSnapshot>())
                .Select(x => new ApprovalDecisionHistoryVm
                {
                    StageNumber = x.StageNumber,
                    RoleName = ApprovalWorkflowSettingsHelper.ResolveRoleName(roleLookup, x.RoleId),
                    ApproverName = x.ApproverUserId,
                    Decision = x.Decision.ToString(),
                    Notes = x.Notes,
                    DecisionDateText = x.DecisionDate.ToString("yyyy-MM-dd HH:mm")
                })
                .ToList();
        }

        public static ApprovalActionSnapshot ToSnapshot(
            int stageNumber,
            int roleId,
            string approverUserId,
            ApprovalStatus decision,
            string notes,
            DateTime decisionDate)
        {
            return new ApprovalActionSnapshot
            {
                StageNumber = stageNumber,
                RoleId = roleId,
                ApproverUserId = approverUserId,
                Decision = decision,
                Notes = notes,
                DecisionDate = decisionDate
            };
        }

        public static bool CanUserActOnStage(
            IUnitOfWork unitOfWork,
            string requesterUserId,
            string currentUserId,
            bool bypassesApprovalRoleCheck,
            int? currentRoleId,
            int? stageRoleId,
            string stageUserId = null)
        {
            var isMine = !string.IsNullOrWhiteSpace(requesterUserId)
                && !string.IsNullOrWhiteSpace(currentUserId)
                && string.Equals(NormalizeUserId(requesterUserId), NormalizeUserId(currentUserId), StringComparison.OrdinalIgnoreCase);

            if (isMine)
            {
                return bypassesApprovalRoleCheck && IsAdminSelfApprovalAllowed(unitOfWork);
            }

            if (!string.IsNullOrWhiteSpace(stageUserId))
            {
                return bypassesApprovalRoleCheck
                    || (!string.IsNullOrWhiteSpace(currentUserId)
                        && string.Equals(NormalizeUserId(currentUserId), NormalizeUserId(stageUserId), StringComparison.OrdinalIgnoreCase));
            }

            return bypassesApprovalRoleCheck
                || (stageRoleId.HasValue && currentRoleId.HasValue && stageRoleId.Value == currentRoleId.Value);
        }

        public static bool CanUserActOnStage(
            string requesterUserId,
            string currentUserId,
            bool bypassesApprovalRoleCheck,
            int? currentRoleId,
            int? stageRoleId,
            string stageUserId = null)
        {
            return CanUserActOnStage(null, requesterUserId, currentUserId, bypassesApprovalRoleCheck, currentRoleId, stageRoleId, stageUserId);
        }

        public static bool ShouldIncludePendingItem(bool bypassesApprovalRoleCheck, bool canAct, bool isMine)
        {
            return bypassesApprovalRoleCheck || canAct || isMine;
        }

        public static bool IsAdminSelfApprovalAllowed(IUnitOfWork unitOfWork)
        {
            if (unitOfWork == null)
            {
                return false;
            }

            var settings = ApprovalWorkflowSettingsHelper.ToDictionary(unitOfWork.Repository<SystemSetting>().GetAll());
            return ApprovalWorkflowSettingsHelper.GetBool(settings, AllowAdminSelfApprovalSettingKey, false);
        }

        private static void WriteBreakGlassAudit(
            IAuditWriter auditWriter,
            string action,
            string processCode,
            string actorUserId,
            string detail)
        {
            if (auditWriter == null)
            {
                return;
            }

            auditWriter.Write(action, "ApprovalWorkflow", processCode, actorUserId, detail);
        }

        private static string NormalizeUserId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string GetSelfApprovalMessage(string processCode)
        {
            switch (processCode)
            {
                case ApprovalProcessCodes.Transfer:
                    return "The requester cannot approve their own transfer request.";
                case ApprovalProcessCodes.Disposal:
                    return "The requester cannot approve their own disposal request.";
                case ApprovalProcessCodes.Purchase:
                    return "The requester cannot approve their own requisition.";
                default:
                    return "The requester cannot approve their own request.";
            }
        }

        private static string GetMisconfiguredWorkflowMessage(string processCode)
        {
            switch (processCode)
            {
                case ApprovalProcessCodes.Transfer:
                    return "The approval workflow is not configured correctly for this transfer.";
                case ApprovalProcessCodes.Disposal:
                    return "The approval workflow is not configured correctly for this disposal request.";
                case ApprovalProcessCodes.Purchase:
                    return "The approval workflow is not configured correctly for this requisition.";
                default:
                    return "The approval workflow is not configured correctly for this request.";
            }
        }

        public static bool CanAccessAssetForPendingApproval(
            IUnitOfWork unitOfWork,
            IUserService userService,
            string userId,
            Asset asset,
            bool bypassesApprovalRoleCheck = false)
        {
            if (unitOfWork == null || userService == null || asset == null || string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            var user = userService.GetById(userId);
            if (user == null || !user.RoleId.HasValue)
            {
                return false;
            }

            var pendingTransfer = unitOfWork.Repository<AssetTransfer>()
                .Find(x => x.AssetId == asset.Id && x.ApprovalStatus == ApprovalStatus.Pending && x.IsActive)
                .FirstOrDefault();
            if (pendingTransfer != null)
            {
                return CanUserActOnPendingTransfer(
                    pendingTransfer,
                    userId,
                    user.RoleId,
                    bypassesApprovalRoleCheck);
            }

            var pendingDisposal = unitOfWork.Repository<DisposalRecord>()
                .Find(x => x.AssetId == asset.Id && x.ApprovalStatus == ApprovalStatus.Pending && x.IsActive)
                .FirstOrDefault();
            if (pendingDisposal != null)
            {
                return CanUserActOnPendingDisposal(
                    pendingDisposal,
                    userId,
                    user.RoleId,
                    bypassesApprovalRoleCheck);
            }

            return false;
        }

        public static bool CanAccessAssetForReceiving(IUnitOfWork unitOfWork, string userId, Asset asset)
        {
            if (unitOfWork == null || asset == null || string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            return unitOfWork.Repository<AssetReceiving>()
                .Find(x => x.AssetId == asset.Id && x.IsActive && x.ReceivedById == userId)
                .Any();
        }

        private static bool CanUserActOnPendingTransfer(
            AssetTransfer pending,
            string userId,
            int? roleId,
            bool bypassesApprovalRoleCheck)
        {
            var stageRoleIds = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(pending.ApprovalStageRoleIds);
            if (stageRoleIds == null || stageRoleIds.Count == 0)
            {
                return false;
            }

            var stageUserIds = ApprovalWorkflowSettingsHelper.ParseStageUserIds(pending.ApprovalStageUserIds);
            var stageNumber = pending.CurrentApprovalStage <= 0 ? 1 : pending.CurrentApprovalStage;
            if (stageNumber <= 0 || stageNumber > stageRoleIds.Count)
            {
                return false;
            }

            var stageRoleId = stageRoleIds[stageNumber - 1];
            var stageUserId = GetStageUserId(stageUserIds, stageNumber);
            return CanUserActOnStage(
                null,
                pending.RequestedById,
                userId,
                bypassesApprovalRoleCheck,
                roleId,
                stageRoleId,
                stageUserId);
        }

        private static bool CanUserActOnPendingDisposal(
            DisposalRecord pending,
            string userId,
            int? roleId,
            bool bypassesApprovalRoleCheck)
        {
            var stageRoleIds = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(pending.ApprovalStageRoleIds);
            if (stageRoleIds == null || stageRoleIds.Count == 0)
            {
                return false;
            }

            var stageUserIds = ApprovalWorkflowSettingsHelper.ParseStageUserIds(pending.ApprovalStageUserIds);
            var stageNumber = pending.CurrentApprovalStage <= 0 ? 1 : pending.CurrentApprovalStage;
            if (stageNumber <= 0 || stageNumber > stageRoleIds.Count)
            {
                return false;
            }

            var stageRoleId = stageRoleIds[stageNumber - 1];
            var stageUserId = GetStageUserId(stageUserIds, stageNumber);
            return CanUserActOnStage(
                null,
                pending.RequestedById,
                userId,
                bypassesApprovalRoleCheck,
                roleId,
                stageRoleId,
                stageUserId);
        }
    }

    public struct ApprovalActionSnapshot
    {
        public int StageNumber { get; set; }

        public int RoleId { get; set; }

        public string ApproverUserId { get; set; }

        public ApprovalStatus Decision { get; set; }

        public string Notes { get; set; }

        public DateTime DecisionDate { get; set; }
    }
}
