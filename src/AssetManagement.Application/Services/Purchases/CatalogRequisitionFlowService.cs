using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public class CatalogRequisitionFlowService : ICatalogRequisitionFlowService
    {
        public const string AuditAction = "Purchases.CatalogRequisitionFlow";
        public const string PolicyAuditAction = "Purchases.RequisitionPolicies";
        public const string DepartmentFallbackLabel = "Department requisition flow (else the organization Approval Matrix)";

        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditWriter _auditWriter;
        private readonly IOrganizationScopeService _organizationScope;

        public CatalogRequisitionFlowService(IUnitOfWork unitOfWork, IAuditWriter auditWriter, IOrganizationScopeService organizationScope)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
            _organizationScope = organizationScope;
        }

        public CatalogPickerVm GetCatalogPicker()
        {
            var organizationId = CurrentOrganizationId();
            var categories = _unitOfWork.Repository<AssetCategory>().GetAll()
                .Where(x => x.IsActive && InOrg(x.OrganizationId, organizationId)).ToList();
            var categoryIds = new HashSet<int>(categories.Select(x => x.Id));
            var types = _unitOfWork.Repository<AssetType>().GetAll()
                .Where(x => x.IsActive && InOrg(x.OrganizationId, organizationId) && categoryIds.Contains(x.AssetCategoryId)).ToList();
            var typeIds = new HashSet<int>(types.Select(x => x.Id));
            var subTypes = _unitOfWork.Repository<AssetSubType>().GetAll()
                .Where(x => x.IsActive && InOrg(x.OrganizationId, organizationId) && typeIds.Contains(x.AssetTypeId)).ToList();

            return new CatalogPickerVm
            {
                Categories = categories.OrderBy(x => x.Name).Select(x => new CatalogPickerItemVm { Id = x.Id, Name = x.Name }).ToList(),
                Types = types.OrderBy(x => x.Name).Select(x => new CatalogPickerItemVm { Id = x.Id, Name = x.Name, ParentId = x.AssetCategoryId }).ToList(),
                SubTypes = subTypes.OrderBy(x => x.Name).Select(x => new CatalogPickerItemVm { Id = x.Id, Name = x.Name, ParentId = x.AssetTypeId }).ToList()
            };
        }

        public CatalogRequisitionFlowEditVm GetFlowForEdit(CatalogFlowScope scope, int scopedEntityId)
        {
            var organizationId = CurrentOrganizationId();
            var node = LoadNode(scope, scopedEntityId, organizationId);
            if (node == null)
            {
                return null;
            }

            var flow = FindRow(scope, scopedEntityId, organizationId);
            var model = new CatalogRequisitionFlowEditVm
            {
                Scope = scope,
                ScopedEntityId = scopedEntityId,
                ScopedEntityName = node.Name,
                PathLabel = node.PathLabel,
                FlowMode = RequisitionFlowMode.InheritParent,
                InheritedFromLabel = DescribeInherited(node, organizationId)
            };

            if (flow != null && flow.IsActive)
            {
                model.Exists = true;
                model.FlowMode = flow.FlowMode;
                model.UpdatedAt = flow.UpdatedAt ?? flow.CreatedAt;
                if (flow.FlowMode == RequisitionFlowMode.Custom)
                {
                    var roleIds = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(flow.CustomStageRoleIds);
                    var userIds = ApprovalWorkflowSettingsHelper.ParseStageUserIds(flow.CustomStageUserIds);
                    model.StoredStageRoleIds = roleIds.ToList();
                    model.Stages = roleIds.Select((roleId, i) => new ApprovalStageSettingsVm
                    {
                        RoleId = roleId,
                        UserId = i < userIds.Count ? userIds[i] : null
                    }).ToList();
                }

                if (flow.FlowMode != RequisitionFlowMode.InheritParent)
                {
                    model.PurchaseOrderRaiserRoleId = flow.PurchaseOrderRaiserRoleId;
                    model.PurchaseOrderRaiserUserId = flow.PurchaseOrderRaiserUserId;
                    model.StoredPurchaseOrderRaiserRoleId = flow.PurchaseOrderRaiserRoleId;
                }
            }

            if (model.Stages.Count == 0)
            {
                model.Stages.Add(new ApprovalStageSettingsVm());
            }

            return model;
        }

        public void SaveFlow(CatalogRequisitionFlowEditVm model)
        {
            if (model == null)
            {
                throw new BusinessException("Requisition approval settings are required.");
            }

            var organizationId = CurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                throw new BusinessException("Open this page inside a company to set requisition approval paths.");
            }

            var node = LoadNode(model.Scope, model.ScopedEntityId, organizationId);
            if (node == null)
            {
                throw new BusinessException(CatalogRequisitionFlowResolver.ScopeLabel(model.Scope) + " not found in this organisation.");
            }

            if (!Enum.IsDefined(typeof(RequisitionFlowMode), model.FlowMode))
            {
                throw new BusinessException("Choose Inherit, Custom stages or Auto-approve.");
            }

            var roleIds = new List<int>();
            var userIds = new List<string>();
            if (model.FlowMode == RequisitionFlowMode.Custom)
            {
                foreach (var stage in model.Stages ?? new List<ApprovalStageSettingsVm>())
                {
                    if (stage == null || !stage.RoleId.HasValue || stage.RoleId.Value <= 0)
                    {
                        if (stage != null && !string.IsNullOrWhiteSpace(stage.UserId))
                        {
                            throw new BusinessException("Choose an approver role for every stage that names a person.");
                        }

                        continue; // blank rows are dropped (same as the department editor)
                    }

                    roleIds.Add(stage.RoleId.Value);
                    userIds.Add(string.IsNullOrWhiteSpace(stage.UserId) ? null : stage.UserId.Trim());
                }

                if (roleIds.Count == 0)
                {
                    throw new BusinessException("Custom approval needs at least one stage role. Choose Auto-approve for no stages.");
                }

                if (roleIds.Count > ApprovalWorkflowSettingsHelper.MaxApprovalStages)
                {
                    throw new BusinessException("At most " + ApprovalWorkflowSettingsHelper.MaxApprovalStages + " approval stages are allowed.");
                }
            }

            var keepRaiser = model.FlowMode != RequisitionFlowMode.InheritParent
                && model.PurchaseOrderRaiserRoleId.HasValue && model.PurchaseOrderRaiserRoleId.Value > 0;

            var now = DateTime.UtcNow;
            var row = FindRow(model.Scope, model.ScopedEntityId, organizationId);
            if (row == null && model.FlowMode == RequisitionFlowMode.InheritParent)
            {
                return; // nothing stored and nothing to store
            }

            var before = row == null ? null : Describe(row);
            if (row == null)
            {
                row = new CatalogRequisitionFlow
                {
                    OrganizationId = organizationId,
                    Scope = model.Scope,
                    ScopedEntityId = model.ScopedEntityId,
                    CreatedAt = now,
                    IsActive = true
                };
            }

            row.FlowMode = model.FlowMode;
            row.CustomStageRoleIds = roleIds.Count == 0 ? null : ApprovalWorkflowSettingsHelper.SerializeStageRoleIds(roleIds.Select(x => (int?)x));
            row.CustomStageUserIds = roleIds.Count == 0 || userIds.All(string.IsNullOrWhiteSpace)
                ? null
                : ApprovalWorkflowSettingsHelper.SerializeStageUserIds(userIds);
            row.PurchaseOrderRaiserRoleId = keepRaiser ? model.PurchaseOrderRaiserRoleId : null;
            row.PurchaseOrderRaiserUserId = keepRaiser && !string.IsNullOrWhiteSpace(model.PurchaseOrderRaiserUserId)
                ? model.PurchaseOrderRaiserUserId.Trim()
                : null;
            row.IsActive = true;

            var after = Describe(row);
            if (before == after)
            {
                return;
            }

            if (row.Id == 0)
            {
                _unitOfWork.Repository<CatalogRequisitionFlow>().Add(row);
            }
            else
            {
                row.UpdatedAt = now;
                _unitOfWork.Repository<CatalogRequisitionFlow>().Update(row);
            }

            _unitOfWork.SaveChanges();
            _auditWriter.Write(AuditAction, "CatalogRequisitionFlow", model.Scope + ":" + model.ScopedEntityId, before, after);
        }

        public IList<CatalogRequisitionFlowListItemVm> GetConfiguredFlows()
        {
            var organizationId = CurrentOrganizationId();
            var rows = _unitOfWork.Repository<CatalogRequisitionFlow>().GetAll()
                .Where(x => x.IsActive && x.FlowMode != RequisitionFlowMode.InheritParent && InOrg(x.OrganizationId, organizationId))
                .ToList();
            var result = new List<CatalogRequisitionFlowListItemVm>();
            foreach (var row in rows)
            {
                var node = LoadNode(row.Scope, row.ScopedEntityId, organizationId);
                if (node == null)
                {
                    continue;
                }

                result.Add(new CatalogRequisitionFlowListItemVm
                {
                    Scope = row.Scope,
                    ScopedEntityId = row.ScopedEntityId,
                    ScopedEntityName = node.Name,
                    PathLabel = node.PathLabel,
                    FlowMode = row.FlowMode,
                    StageRoleIds = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(row.CustomStageRoleIds).ToList(),
                    StageUserIds = ApprovalWorkflowSettingsHelper.ParseStageUserIds(row.CustomStageUserIds).ToList(),
                    PurchaseOrderRaiserRoleId = row.PurchaseOrderRaiserRoleId,
                    PurchaseOrderRaiserUserId = row.PurchaseOrderRaiserUserId,
                    UpdatedAt = row.UpdatedAt ?? row.CreatedAt
                });
            }

            return result.OrderBy(x => x.PathLabel, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public RequisitionPolicySettingsVm GetPolicies()
        {
            var settings = LoadSettings();
            return new RequisitionPolicySettingsVm
            {
                ClassificationMode = RequisitionPolicySettings.GetClassificationMode(_unitOfWork),
                MixedFlowPolicy = RequisitionPolicySettings.GetMixedFlowPolicy(_unitOfWork),
                IsStored = settings.Any(x => IsPolicyKey(x.SettingKey))
            };
        }

        public void SavePolicies(RequisitionPolicySettingsVm model)
        {
            if (model == null)
            {
                return;
            }

            var organizationId = CurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                throw new BusinessException("Open Settings inside a company to change requisition policies.");
            }

            if (!Enum.IsDefined(typeof(RequisitionClassificationMode), model.ClassificationMode)
                || !Enum.IsDefined(typeof(RequisitionMixedFlowPolicy), model.MixedFlowPolicy))
            {
                throw new BusinessException("Choose a valid requisition classification mode and mixed-path policy.");
            }

            var before = GetPolicies();
            var settings = LoadSettings();
            var now = DateTime.UtcNow;
            var changed = Upsert(settings, RequisitionPolicySettings.ClassificationModeKey, model.ClassificationMode.ToString(),
                "Requisition line classification: Optional, RequiredPerLine or RequiredHeader.", organizationId.Value, now);
            changed |= Upsert(settings, RequisitionPolicySettings.MixedFlowPolicyKey, model.MixedFlowPolicy.ToString(),
                "Lines with different approval paths: AutoSplit (one requisition per path) or Block.", organizationId.Value, now);
            if (!changed)
            {
                return;
            }

            _unitOfWork.SaveChanges();
            _auditWriter.Write(
                PolicyAuditAction,
                "SystemSetting",
                organizationId.Value.ToString(),
                before.IsStored ? before.ClassificationMode + "|" + before.MixedFlowPolicy : null,
                model.ClassificationMode + "|" + model.MixedFlowPolicy);
        }

        private bool Upsert(IList<SystemSetting> settings, string key, string value, string description, int organizationId, DateTime now)
        {
            var existing = settings.FirstOrDefault(x => string.Equals(x.SettingKey, key, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                _unitOfWork.Repository<SystemSetting>().Add(new SystemSetting
                {
                    OrganizationId = organizationId,
                    SettingKey = key,
                    SettingValue = value,
                    Description = description,
                    CreatedAt = now,
                    IsActive = true
                });
                return true;
            }

            if (string.Equals(existing.SettingValue, value, StringComparison.Ordinal) && existing.IsActive)
            {
                return false;
            }

            existing.SettingValue = value;
            existing.IsActive = true;
            existing.UpdatedAt = now;
            _unitOfWork.Repository<SystemSetting>().Update(existing);
            return true;
        }

        private IList<SystemSetting> LoadSettings()
        {
            var organizationId = CurrentOrganizationId();
#pragma warning disable 618 // tenant-filtered repository, same read as RequisitionPolicySettings
            return _unitOfWork.Repository<SystemSetting>().GetAll()
#pragma warning restore 618
                .Where(x => IsPolicyKey(x.SettingKey) && InOrg(x.OrganizationId, organizationId))
                .ToList();
        }

        private static bool IsPolicyKey(string key)
        {
            return string.Equals(key, RequisitionPolicySettings.ClassificationModeKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, RequisitionPolicySettings.MixedFlowPolicyKey, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class NodeInfo
        {
            public string Name { get; set; }

            public string PathLabel { get; set; }

            public int? CategoryId { get; set; }

            public int? TypeId { get; set; }
        }

        private NodeInfo LoadNode(CatalogFlowScope scope, int id, int? organizationId)
        {
            if (id <= 0)
            {
                return null;
            }

            switch (scope)
            {
                case CatalogFlowScope.Category:
                {
                    var category = _unitOfWork.Repository<AssetCategory>().GetById(id);
                    if (category == null || !InOrg(category.OrganizationId, organizationId))
                    {
                        return null;
                    }

                    return new NodeInfo { Name = category.Name, PathLabel = category.Name };
                }
                case CatalogFlowScope.Type:
                {
                    var type = _unitOfWork.Repository<AssetType>().GetById(id);
                    if (type == null || !InOrg(type.OrganizationId, organizationId))
                    {
                        return null;
                    }

                    var category = _unitOfWork.Repository<AssetCategory>().GetById(type.AssetCategoryId);
                    return new NodeInfo
                    {
                        Name = type.Name,
                        CategoryId = type.AssetCategoryId,
                        PathLabel = (category == null ? string.Empty : category.Name + " > ") + type.Name
                    };
                }
                case CatalogFlowScope.SubType:
                {
                    var subType = _unitOfWork.Repository<AssetSubType>().GetById(id);
                    if (subType == null || !InOrg(subType.OrganizationId, organizationId))
                    {
                        return null;
                    }

                    var type = _unitOfWork.Repository<AssetType>().GetById(subType.AssetTypeId);
                    var category = type == null ? null : _unitOfWork.Repository<AssetCategory>().GetById(type.AssetCategoryId);
                    return new NodeInfo
                    {
                        Name = subType.Name,
                        TypeId = subType.AssetTypeId,
                        CategoryId = type == null ? (int?)null : type.AssetCategoryId,
                        PathLabel = (category == null ? string.Empty : category.Name + " > ")
                            + (type == null ? string.Empty : type.Name + " > ")
                            + subType.Name
                    };
                }
                default:
                    return null;
            }
        }

        /// <summary>What a requisition line on this node uses while the node inherits (nearest configured ancestor).</summary>
        private string DescribeInherited(NodeInfo node, int? organizationId)
        {
            var hit = CatalogRequisitionFlowResolver.Resolve(
                RequisitionLineClassification.Create(node.CategoryId, node.TypeId, null),
                (scope, id) => RequisitionFlowResolver.FindCatalogFlow(_unitOfWork, scope, id, organizationId),
                (scope, id) => RequisitionFlowResolver.GetCatalogName(_unitOfWork, scope, id));
            return hit == null ? DepartmentFallbackLabel : hit.SourceLabel;
        }

        private CatalogRequisitionFlow FindRow(CatalogFlowScope scope, int id, int? organizationId)
        {
            return _unitOfWork.Repository<CatalogRequisitionFlow>()
                .Find(x => x.Scope == scope && x.ScopedEntityId == id)
                .Where(x => InOrg(x.OrganizationId, organizationId))
                .OrderByDescending(x => x.IsActive)
                .ThenBy(x => x.Id)
                .FirstOrDefault();
        }

        private static string Describe(CatalogRequisitionFlow row)
        {
            return "Mode=" + row.FlowMode
                + ";Roles=" + (row.CustomStageRoleIds ?? string.Empty)
                + ";Users=" + (row.CustomStageUserIds ?? string.Empty)
                + ";PoRole=" + (row.PurchaseOrderRaiserRoleId.HasValue ? row.PurchaseOrderRaiserRoleId.Value.ToString() : string.Empty)
                + ";PoUser=" + (row.PurchaseOrderRaiserUserId ?? string.Empty)
                + ";Active=" + row.IsActive;
        }

        private int? CurrentOrganizationId()
        {
            return _organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId();
        }

        private static bool InOrg(int? rowOrganizationId, int? organizationId)
        {
            return !organizationId.HasValue || !rowOrganizationId.HasValue || rowOrganizationId.Value == organizationId.Value;
        }
    }
}
