using System;
using System.Collections.Generic;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    public static class DepartmentRequisitionFlowResolver
    {
        public static ApprovalProcessConfiguration Resolve(
            Department start,
            Func<int, Department> getById,
            ApprovalProcessConfiguration orgDefault)
        {
            if (orgDefault == null)
            {
                orgDefault = new ApprovalProcessConfiguration
                {
                    ProcessCode = ApprovalProcessCodes.Purchase,
                    DisplayName = ApprovalProcessCodes.GetDisplayName(ApprovalProcessCodes.Purchase),
                    RequiresApproval = false
                };
            }

            var current = start;
            var guard = 0;
            while (current != null && guard < 50)
            {
                if (current.RequisitionFlowMode == RequisitionFlowMode.Custom)
                {
                    var roles = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(current.CustomStageRoleIds);
                    var users = ApprovalWorkflowSettingsHelper.ParseStageUserIds(current.CustomStageUserIds);
                    return new ApprovalProcessConfiguration
                    {
                        ProcessCode = ApprovalProcessCodes.Purchase,
                        DisplayName = ApprovalProcessCodes.GetDisplayName(ApprovalProcessCodes.Purchase),
                        RequiresApproval = roles.Count > 0,
                        StageRoleIds = roles,
                        StageUserIds = users
                    };
                }

                if (!current.ParentDepartmentId.HasValue || getById == null)
                {
                    break;
                }

                current = getById(current.ParentDepartmentId.Value);
                guard++;
            }

            return orgDefault;
        }
    }
}
