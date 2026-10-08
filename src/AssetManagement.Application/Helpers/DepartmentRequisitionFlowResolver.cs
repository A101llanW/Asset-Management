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

            /// <summary>Kind of the department that supplied the flow (null for org matrix).</summary>
            public DepartmentKind? SourceDepartmentKind { get; set; }

            /// <summary>The start unit's explicit inherit choice, when it was applied (ancestor found).</summary>
            public RequisitionInheritSource? AppliedInheritChoice { get; set; }

            public int? ChosenAncestorId { get; set; }

            public string ChosenAncestorName { get; set; }

            public string SourceLabel
            {
                get
                {
                    if (AppliedInheritChoice.HasValue && !string.IsNullOrWhiteSpace(ChosenAncestorName))
                    {
                        return InheritSourceDisplay;
                    }

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

            /// <summary>
            /// Effective source for an Inherit-mode unit, e.g. "Inherit - from Sub: Office Services",
            /// "Inherit - from Dept: Facilities (via org matrix)", "Inherit - org matrix".
            /// </summary>
            public string InheritSourceDisplay
            {
                get
                {
                    var isMatrix = string.Equals(SourceKind, "OrganizationMatrix", StringComparison.OrdinalIgnoreCase);
                    if (AppliedInheritChoice.HasValue && !string.IsNullOrWhiteSpace(ChosenAncestorName))
                    {
                        var head = "Inherit - from "
                            + (AppliedInheritChoice.Value == RequisitionInheritSource.SubDepartment ? "Sub" : "Dept")
                            + ": " + ChosenAncestorName;
                        if (isMatrix)
                        {
                            return head + " (org matrix)";
                        }

                        if (SourceDepartmentId.HasValue
                            && ChosenAncestorId.HasValue
                            && SourceDepartmentId.Value != ChosenAncestorId.Value
                            && !string.IsNullOrWhiteSpace(SourceDepartmentName))
                        {
                            return head + " (via " + ShortKindLabel(SourceDepartmentKind) + ": " + SourceDepartmentName + ")";
                        }

                        return head;
                    }

                    if (!isMatrix && !string.IsNullOrWhiteSpace(SourceDepartmentName))
                    {
                        return "Inherit - from " + ShortKindLabel(SourceDepartmentKind) + ": " + SourceDepartmentName;
                    }

                    return "Inherit - org matrix";
                }
            }
        }

        public static string ShortKindLabel(DepartmentKind? kind)
        {
            if (!kind.HasValue)
            {
                return "Unit";
            }

            switch (kind.Value)
            {
                case DepartmentKind.Administrative:
                    return "Dept";
                case DepartmentKind.SubDepartment:
                    return "Sub";
                case DepartmentKind.Room:
                    return "Room";
                case DepartmentKind.Grade:
                    return "Grade";
                case DepartmentKind.Class:
                    return "Class";
                default:
                    return kind.Value.ToString();
            }
        }

        /// <summary>Null for Nearest/unset, otherwise the explicit choice.</summary>
        public static RequisitionInheritSource? NormalizeInheritChoice(RequisitionInheritSource? choice)
        {
            if (!choice.HasValue)
            {
                return null;
            }

            if (choice.Value == RequisitionInheritSource.SubDepartment
                || choice.Value == RequisitionInheritSource.Department)
            {
                return choice.Value;
            }

            return null;
        }

        /// <summary>
        /// Finds the ancestor matching an inherit choice by walking live parent links (no names/codes).
        /// SubDepartment = nearest Sub-department ancestor; Department = top-level Administrative ancestor.
        /// </summary>
        public static Department FindInheritAncestor(
            Department unit,
            RequisitionInheritSource source,
            Func<int, Department> getById)
        {
            if (unit == null || getById == null || !unit.ParentDepartmentId.HasValue)
            {
                return null;
            }

            if (source != RequisitionInheritSource.SubDepartment && source != RequisitionInheritSource.Department)
            {
                return null;
            }

            var visited = new HashSet<int> { unit.Id };
            var nextId = unit.ParentDepartmentId;
            var guard = 0;
            while (nextId.HasValue && visited.Add(nextId.Value) && guard < 50)
            {
                var ancestor = getById(nextId.Value);
                if (ancestor == null)
                {
                    return null;
                }

                if (source == RequisitionInheritSource.SubDepartment
                    && ancestor.DepartmentKind == DepartmentKind.SubDepartment)
                {
                    return ancestor;
                }

                if (source == RequisitionInheritSource.Department
                    && ancestor.DepartmentKind == DepartmentKind.Administrative
                    && !ancestor.ParentDepartmentId.HasValue)
                {
                    return ancestor;
                }

                nextId = ancestor.ParentDepartmentId;
                guard++;
            }

            return null;
        }

        public sealed class InheritOption
        {
            public RequisitionInheritSource Source { get; set; }

            public int AncestorId { get; set; }

            public string AncestorName { get; set; }

            public string AncestorCode { get; set; }
        }

        /// <summary>Inherit choices that actually exist in this unit's ancestry (Sub first, then Dept).</summary>
        public static IList<InheritOption> GetInheritOptions(Department unit, Func<int, Department> getById)
        {
            var options = new List<InheritOption>();
            foreach (var source in new[] { RequisitionInheritSource.SubDepartment, RequisitionInheritSource.Department })
            {
                var ancestor = FindInheritAncestor(unit, source, getById);
                if (ancestor != null)
                {
                    options.Add(new InheritOption
                    {
                        Source = source,
                        AncestorId = ancestor.Id,
                        AncestorName = ancestor.Name,
                        AncestorCode = ancestor.Code
                    });
                }
            }

            return options;
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

            RequisitionInheritSource? appliedChoice = null;
            Department chosenAncestor = null;
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
                        SourceDepartmentKind = current.DepartmentKind,
                        AppliedInheritChoice = appliedChoice,
                        ChosenAncestorId = chosenAncestor == null ? (int?)null : chosenAncestor.Id,
                        ChosenAncestorName = chosenAncestor == null ? null : chosenAncestor.Name,
                        Configuration = new ApprovalProcessConfiguration
                        {
                            ProcessCode = ApprovalProcessCodes.Purchase,
                            DisplayName = ApprovalProcessCodes.GetDisplayName(ApprovalProcessCodes.Purchase),
                            RequiresApproval = false,
                            StageRoleIds = new List<int>(),
                            StageUserIds = new List<string>(),
                            PurchaseOrderRaiserRoleId = NormalizeRaiserRoleId(current.PurchaseOrderRaiserRoleId),
                            PurchaseOrderRaiserUserId = NormalizeRaiserUserId(current),
                            PurchaseOrderRaiserSourceDepartmentId = current.Id,
                            PurchaseOrderRaiserSourceDepartmentName = current.Name
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
                        SourceDepartmentKind = current.DepartmentKind,
                        AppliedInheritChoice = appliedChoice,
                        ChosenAncestorId = chosenAncestor == null ? (int?)null : chosenAncestor.Id,
                        ChosenAncestorName = chosenAncestor == null ? null : chosenAncestor.Name,
                        Configuration = new ApprovalProcessConfiguration
                        {
                            ProcessCode = ApprovalProcessCodes.Purchase,
                            DisplayName = ApprovalProcessCodes.GetDisplayName(ApprovalProcessCodes.Purchase),
                            RequiresApproval = roles.Count > 0,
                            StageRoleIds = roles,
                            StageUserIds = users,
                            // PO raiser comes from the same unit that supplies the flow (Custom unit).
                            PurchaseOrderRaiserRoleId = NormalizeRaiserRoleId(current.PurchaseOrderRaiserRoleId),
                            PurchaseOrderRaiserUserId = NormalizeRaiserUserId(current),
                            PurchaseOrderRaiserSourceDepartmentId = current.Id,
                            PurchaseOrderRaiserSourceDepartmentName = current.Name
                        }
                    };
                }

                if (getById == null)
                {
                    break;
                }

                // Inherit: honour an explicit ancestor choice (Sub / Dept) when that ancestor exists;
                // otherwise fall back to the direct parent (legacy behaviour).
                Department next = null;
                var choice = NormalizeInheritChoice(current.RequisitionInheritFrom);
                if (choice.HasValue)
                {
                    next = FindInheritAncestor(current, choice.Value, getById);
                    if (next != null && start != null && current.Id == start.Id)
                    {
                        appliedChoice = choice;
                        chosenAncestor = next;
                    }
                }

                if (next == null)
                {
                    if (!current.ParentDepartmentId.HasValue)
                    {
                        break;
                    }

                    next = getById(current.ParentDepartmentId.Value);
                }

                current = next;
                guard++;
            }

            return new ResolveResult
            {
                // Org matrix carries no PO raiser: unconfigured = legacy behaviour.
                SourceKind = "OrganizationMatrix",
                AppliedInheritChoice = appliedChoice,
                ChosenAncestorId = chosenAncestor == null ? (int?)null : chosenAncestor.Id,
                ChosenAncestorName = chosenAncestor == null ? null : chosenAncestor.Name,
                Configuration = orgDefault
            };
        }

        private static int? NormalizeRaiserRoleId(int? roleId)
        {
            return roleId.HasValue && roleId.Value > 0 ? roleId : null;
        }

        private static string NormalizeRaiserUserId(Department source)
        {
            if (source == null || NormalizeRaiserRoleId(source.PurchaseOrderRaiserRoleId) == null
                || string.IsNullOrWhiteSpace(source.PurchaseOrderRaiserUserId))
            {
                return null;
            }

            return source.PurchaseOrderRaiserUserId.Trim();
        }
    }
}
