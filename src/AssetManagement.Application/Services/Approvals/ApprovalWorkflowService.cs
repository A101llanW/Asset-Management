using System.Collections.Generic;

using AssetManagement.Application.Contracts;

using AssetManagement.Application.Services;

using AssetManagement.Application.ViewModels;



namespace AssetManagement.Application.Services

{

    public class ApprovalWorkflowService : IApprovalWorkflowService

    {

        private readonly IUnitOfWork _unitOfWork;

        private readonly IAuditWriter _auditWriter;



        public ApprovalWorkflowService(IUnitOfWork unitOfWork, IAuditWriter auditWriter)

        {

            _unitOfWork = unitOfWork;

            _auditWriter = auditWriter;

        }



        public ApprovalProcessConfiguration GetProcessConfiguration(string processCode)

        {

            return ApprovalWorkflowHelper.GetProcessConfiguration(_unitOfWork, processCode);

        }



        public void EnsureUserCanApprove(

            int? approverRoleId,

            bool isSuperAdmin,

            int expectedRoleId,

            string requesterUserId,

            string actingUserId,

            string processCode)

        {

            ApprovalWorkflowHelper.EnsureUserCanApprove(

                _unitOfWork,

                _auditWriter,

                approverRoleId,

                isSuperAdmin,

                expectedRoleId,

                null,

                requesterUserId,

                actingUserId,

                processCode);

        }



        public int GetStageRoleId(IList<int> stageRoleIds, int stageNumber, string processCode)

        {

            return ApprovalWorkflowHelper.GetStageRoleId(stageRoleIds, stageNumber, processCode);

        }



        public IList<ApprovalDecisionHistoryVm> MapDecisionHistory(

            IEnumerable<ApprovalActionSnapshot> actions,

            IDictionary<int, string> roleLookup)

        {

            return ApprovalWorkflowHelper.MapDecisionHistory(actions, roleLookup);

        }



        public bool CanUserActOnStage(string requesterUserId, string currentUserId, bool isSuperAdmin, int? currentRoleId, int? stageRoleId)

        {

            return ApprovalWorkflowHelper.CanUserActOnStage(

                _unitOfWork,

                requesterUserId,

                currentUserId,

                isSuperAdmin,

                currentRoleId,

                stageRoleId);

        }



        public bool ShouldIncludePendingItem(bool isSuperAdmin, bool canAct, bool isMine)

        {

            return ApprovalWorkflowHelper.ShouldIncludePendingItem(isSuperAdmin, canAct, isMine);

        }

    }

}


