using System;
using System.Collections.Generic;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Catalog layer of requisition approval resolution: sub-type, then type, then category override
    /// (<see cref="CatalogRequisitionFlow"/>). Pure functions over lookups so they can be unit-tested; returns null
    /// when no catalog node overrides, in which case the caller falls back to the department chain + org matrix.
    /// </summary>
    public static class CatalogRequisitionFlowResolver
    {
        public sealed class CatalogHit
        {
            public CatalogFlowScope Scope { get; set; }

            public int ScopedEntityId { get; set; }

            public string ScopedEntityName { get; set; }

            /// <summary>CatalogCustom | CatalogAutoApprove</summary>
            public string SourceKind { get; set; }

            public ApprovalProcessConfiguration Configuration { get; set; }

            public string SourceLabel
            {
                get
                {
                    var node = ScopeLabel(Scope) + (string.IsNullOrWhiteSpace(ScopedEntityName) ? " #" + ScopedEntityId : ": " + ScopedEntityName);
                    return string.Equals(SourceKind, "CatalogAutoApprove", StringComparison.OrdinalIgnoreCase)
                        ? "Auto-approve on " + node
                        : "Custom on " + node;
                }
            }

            /// <summary>Stable identity of the winning node (ids only, survives renames).</summary>
            public string SourceKey
            {
                get { return "Catalog:" + Scope + ":" + ScopedEntityId; }
            }
        }

        /// <summary>
        /// True when a requisition's stored ApprovalFlowSource label names a catalog node ("Custom on Type: X",
        /// "Auto-approve on Sub-type #5", ...). Used to resolve approvers catalog-path-first (owner decision).
        /// </summary>
        public static bool IsCatalogSource(string approvalFlowSource)
        {
            if (string.IsNullOrWhiteSpace(approvalFlowSource))
            {
                return false;
            }

            var text = approvalFlowSource.Trim();
            foreach (var prefix in new[] { "Custom on ", "Auto-approve on " })
            {
                if (!text.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var rest = text.Substring(prefix.Length);
                foreach (var scope in new[] { CatalogFlowScope.SubType, CatalogFlowScope.Type, CatalogFlowScope.Category })
                {
                    var label = ScopeLabel(scope);
                    if (rest.StartsWith(label + ": ", StringComparison.Ordinal) || rest.StartsWith(label + " #", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static string ScopeLabel(CatalogFlowScope scope)
        {
            switch (scope)
            {
                case CatalogFlowScope.Category:
                    return "Category";
                case CatalogFlowScope.Type:
                    return "Type";
                case CatalogFlowScope.SubType:
                    return "Sub-type";
                default:
                    return scope.ToString();
            }
        }

        /// <summary>
        /// Fills the parents implied by the most specific pick (sub-type implies type implies category) and rejects
        /// contradictory or foreign picks. Unknown/inactive ids are rejected so a forged post cannot pick a path.
        /// </summary>
        public static RequisitionLineClassification Normalize(
            RequisitionLineClassification input,
            Func<int, AssetSubType> getSubType,
            Func<int, AssetType> getType,
            Func<int, AssetCategory> getCategory,
            int? organizationId,
            string lineLabel)
        {
            if (input == null || !input.HasAny)
            {
                return new RequisitionLineClassification();
            }

            var prefix = string.IsNullOrWhiteSpace(lineLabel) ? string.Empty : lineLabel + ": ";
            var result = input.Clone();

            if (result.AssetSubTypeId.HasValue)
            {
                var subType = getSubType == null ? null : getSubType(result.AssetSubTypeId.Value);
                if (!IsUsable(subType == null ? (bool?)null : subType.IsActive, subType == null ? null : subType.OrganizationId, organizationId))
                {
                    throw new BusinessException(prefix + "the selected sub-type was not found or is inactive.");
                }

                if (result.AssetTypeId.HasValue && result.AssetTypeId.Value != subType.AssetTypeId)
                {
                    throw new BusinessException(prefix + "the selected sub-type does not belong to the selected type.");
                }

                result.AssetTypeId = subType.AssetTypeId;
            }

            if (result.AssetTypeId.HasValue)
            {
                var type = getType == null ? null : getType(result.AssetTypeId.Value);
                if (!IsUsable(type == null ? (bool?)null : type.IsActive, type == null ? null : type.OrganizationId, organizationId))
                {
                    throw new BusinessException(prefix + "the selected type was not found or is inactive.");
                }

                if (result.AssetCategoryId.HasValue && result.AssetCategoryId.Value != type.AssetCategoryId)
                {
                    throw new BusinessException(prefix + "the selected type does not belong to the selected category.");
                }

                result.AssetCategoryId = type.AssetCategoryId;
            }

            if (result.AssetCategoryId.HasValue)
            {
                var category = getCategory == null ? null : getCategory(result.AssetCategoryId.Value);
                if (!IsUsable(category == null ? (bool?)null : category.IsActive, category == null ? null : category.OrganizationId, organizationId))
                {
                    throw new BusinessException(prefix + "the selected category was not found or is inactive.");
                }
            }

            return result;
        }

        /// <summary>Most specific configured node wins; null = no catalog override for this line.</summary>
        public static CatalogHit Resolve(
            RequisitionLineClassification classification,
            Func<CatalogFlowScope, int, CatalogRequisitionFlow> getFlow,
            Func<CatalogFlowScope, int, string> getName)
        {
            if (classification == null || !classification.HasAny || getFlow == null)
            {
                return null;
            }

            var nodes = new List<KeyValuePair<CatalogFlowScope, int?>>
            {
                new KeyValuePair<CatalogFlowScope, int?>(CatalogFlowScope.SubType, classification.AssetSubTypeId),
                new KeyValuePair<CatalogFlowScope, int?>(CatalogFlowScope.Type, classification.AssetTypeId),
                new KeyValuePair<CatalogFlowScope, int?>(CatalogFlowScope.Category, classification.AssetCategoryId)
            };

            foreach (var node in nodes)
            {
                if (!node.Value.HasValue)
                {
                    continue;
                }

                var flow = getFlow(node.Key, node.Value.Value);
                var configuration = ToConfiguration(flow);
                if (configuration == null)
                {
                    continue;
                }

                return new CatalogHit
                {
                    Scope = node.Key,
                    ScopedEntityId = node.Value.Value,
                    ScopedEntityName = getName == null ? null : getName(node.Key, node.Value.Value),
                    SourceKind = configuration.StageRoleIds.Count == 0 ? "CatalogAutoApprove" : "CatalogCustom",
                    Configuration = configuration
                };
            }

            return null;
        }

        /// <summary>
        /// Maps a catalog row to a Purchase configuration (same semantics as a department unit). Null when the row is
        /// missing, inactive or InheritParent ("use department flow"), i.e. the cascade continues.
        /// </summary>
        public static ApprovalProcessConfiguration ToConfiguration(CatalogRequisitionFlow flow)
        {
            if (flow == null || !flow.IsActive || flow.FlowMode == RequisitionFlowMode.InheritParent)
            {
                return null;
            }

            var roles = flow.FlowMode == RequisitionFlowMode.AutoApprove
                ? new List<int>()
                : ApprovalWorkflowSettingsHelper.ParseStageRoleIds(flow.CustomStageRoleIds);
            var users = flow.FlowMode == RequisitionFlowMode.AutoApprove || roles.Count == 0
                ? new List<string>()
                : ApprovalWorkflowSettingsHelper.ParseStageUserIds(flow.CustomStageUserIds);
            var raiserRoleId = flow.PurchaseOrderRaiserRoleId.HasValue && flow.PurchaseOrderRaiserRoleId.Value > 0
                ? flow.PurchaseOrderRaiserRoleId
                : null;

            return new ApprovalProcessConfiguration
            {
                ProcessCode = ApprovalProcessCodes.Purchase,
                DisplayName = ApprovalProcessCodes.GetDisplayName(ApprovalProcessCodes.Purchase),
                RequiresApproval = roles.Count > 0,
                StageRoleIds = roles,
                StageUserIds = users,
                PurchaseOrderRaiserRoleId = raiserRoleId,
                PurchaseOrderRaiserUserId = raiserRoleId.HasValue && !string.IsNullOrWhiteSpace(flow.PurchaseOrderRaiserUserId)
                    ? flow.PurchaseOrderRaiserUserId.Trim()
                    : null
            };
        }

        private static bool IsUsable(bool? isActive, int? entityOrganizationId, int? organizationId)
        {
            if (!isActive.HasValue || !isActive.Value)
            {
                return false;
            }

            return !organizationId.HasValue || !entityOrganizationId.HasValue || entityOrganizationId.Value == organizationId.Value;
        }
    }
}
