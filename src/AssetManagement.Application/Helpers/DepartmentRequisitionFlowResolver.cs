using System;
using System.Collections.Generic;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    public static class DepartmentRequisitionFlowResolver
    {
        public sealed class ResolveResult
        {
            public ApprovalProcessConfiguration Configuration { get; set; }

            /// <summary>CustomDepartment | Inherited | AutoApprove | OrganizationMatrix</summary>
            public string SourceKind { get; set; }

            public int? SourceDepartmentId { get; set; }

            public string SourceDepartmentName { get; set; }

            public string SourceLabel
            {
                get
                {
                    if (string.Equals(SourceKind, "Inherited", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(SourceDepartmentName))
                    {
                        return "Inherited from " + SourceDepartmentName;
                    }

                    if (string.Equals(SourceKind, "CustomDepartment", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(SourceDepartmentName))
                    {
                        return "Custom on " + SourceDepartmentName;
                    }

                    if (string.Equals(SourceKind, "AutoApprove", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(SourceDepartmentName))
                    {
                        return "Auto-approve on " + SourceDepartmentName;
                    }

                    return "Organization Approval Matrix";
                }
            }
        }

        public static ApprovalProcessConfiguration Resolve(
            Department start,
            Func<int, Department> getById,
            ApprovalProcessConfiguration orgDefault)
        {
            return ResolveDetailed(start, getById, orgDefault).Configuration;
        }

        public static ResolveResult ResolveDetailed(
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
                // Walk Class -> Grade -> ... and Admin/Sub/Room parent chain the same way.
                if (current.RequisitionFlowMode == RequisitionFlowMode.AutoApprove)
                {
                    var inheritedAuto = start != null && current.Id != start.Id;
                    return new ResolveResult
                    {
                        SourceKind = inheritedAuto ? "Inherited" : "AutoApprove",
                        SourceDepartmentId = current.Id,
                        SourceDepartmentName = current.Name,
                        Configuration = new ApprovalProcessConfiguration
                        {
                            ProcessCode = ApprovalProcessCodes.Purchase,
                            DisplayName = ApprovalProcessCodes.GetDisplayName(ApprovalProcessCodes.Purchase),
                            RequiresApproval = false,
                            StageRoleIds = new List<int>(),
                            StageUserIds = new List<string>()
                        }
                    };
                }

                var treatsAsCustom = current.RequisitionFlowMode == RequisitionFlowMode.Custom
                    || current.UseCustomRequisitionApproval;
                if (treatsAsCustom)
                {
                    var roles = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(current.CustomStageRoleIds);
                    var users = ApprovalWorkflowSettingsHelper.ParseStageUserIds(current.CustomStageUserIds);
                    if (roles.Count == 0 && current.UseCustomRequisitionApproval)
                    {
                        roles = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(current.RequisitionApprovalStageRoleIds);
                        users = ApprovalWorkflowSettingsHelper.ParseStageUserIds(current.RequisitionApprovalStageUserIds);
                    }

                    var inherited = start != null && current.Id != start.Id;
                    return new ResolveResult
                    {
                        SourceKind = inherited ? "Inherited" : (roles.Count == 0 ? "AutoApprove" : "CustomDepartment"),
                        SourceDepartmentId = current.Id,
                        SourceDepartmentName = current.Name,
                        Configuration = new ApprovalProcessConfiguration
                        {
                            ProcessCode = ApprovalProcessCodes.Purchase,
                            DisplayName = ApprovalProcessCodes.GetDisplayName(ApprovalProcessCodes.Purchase),
                            RequiresApproval = roles.Count > 0,
                            StageRoleIds = roles,
                            StageUserIds = users
                        }
                    };
                }

                if (!current.ParentDepartmentId.HasValue || getById == null)
                {
                    break;
                }

                current = getById(current.ParentDepartmentId.Value);
                guard++;
            }

            return new ResolveResult
            {
                SourceKind = "OrganizationMatrix",
                Configuration = orgDefault
            };
        }
    }
}
