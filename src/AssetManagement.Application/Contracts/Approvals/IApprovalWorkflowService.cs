using System.Collections.Generic;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Contracts
{
    public interface IApprovalWorkflowService
    {
        ApprovalProcessConfiguration GetProcessConfiguration(string processCode);

        void EnsureUserCanApprove(
            int? approverRoleId,
            bool isSuperAdmin,
            int expectedRoleId,
            string requesterUserId,
            string actingUserId,
            string processCode);

        int GetStageRoleId(IList<int> stageRoleIds, int stageNumber, string processCode);

        IList<ApprovalDecisionHistoryVm> MapDecisionHistory(
            IEnumerable<ApprovalActionSnapshot> actions,
            IDictionary<int, string> roleLookup);

        bool CanUserActOnStage(string requesterUserId, string currentUserId, bool isSuperAdmin, int? currentRoleId, int? stageRoleId);

        bool ShouldIncludePendingItem(bool isSuperAdmin, bool canAct, bool isMine);
    }
}
