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
    public class AssetBulkService : IAssetBulkService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditWriter _auditWriter;
        private readonly IDepartmentScopeService _departmentScope;
        private readonly IAssetWorkflowGuard _workflowGuard;
        private readonly IAuthorizationService _authorizationService;

        public AssetBulkService(
            IUnitOfWork unitOfWork,
            IAuditWriter auditWriter,
            IDepartmentScopeService departmentScope,
            IAssetWorkflowGuard workflowGuard,
            IAuthorizationService authorizationService)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
            _departmentScope = departmentScope;
            _workflowGuard = workflowGuard;
            _authorizationService = authorizationService;
        }

        public AssetBulkActionResultVm Execute(AssetBulkActionRequestVm request, string actorUserId)
        {
            if (request == null)
            {
                throw new BusinessException("Bulk action request is required.");
            }

            if (request.AssetIds == null || request.AssetIds.Count == 0)
            {
                throw new BusinessException("Select at least one asset.");
            }

            if (string.IsNullOrWhiteSpace(actorUserId))
            {
                throw new BusinessException("Current user is required for bulk actions.");
            }

            var action = (request.Action ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(action))
            {
                throw new BusinessException("Bulk action is required.");
            }

            var messages = new List<string>();
            var processed = 0;
            var skipped = 0;

            foreach (var assetId in request.AssetIds.Distinct())
            {
                var asset = _unitOfWork.Repository<Asset>().GetById(assetId);
                if (asset == null || !asset.IsActive)
                {
                    skipped++;
                    messages.Add("Asset #" + assetId + " was not found or is archived.");
                    continue;
                }

                try
                {
                    _departmentScope.EnsureCanAccessAsset(asset);
                    _workflowGuard.EnsureNoBlockingWorkflow(assetId);

                    if (string.Equals(action, "department", StringComparison.OrdinalIgnoreCase))
                    {
                        RequirePermission(actorUserId, "Assets.Edit");
                        if (!request.TargetDepartmentId.HasValue)
                        {
                            throw new BusinessException("Target department is required.");
                        }

                        asset.DepartmentId = request.TargetDepartmentId.Value;
                        asset.UpdatedAt = DateTime.UtcNow;
                        _unitOfWork.Repository<Asset>().Update(asset);
                        processed++;
                    }
                    else if (string.Equals(action, "status", StringComparison.OrdinalIgnoreCase))
                    {
                        RequirePermission(actorUserId, "Assets.Edit");
                        if (!request.TargetStatus.HasValue)
                        {
                            throw new BusinessException("Target status is required.");
                        }

                        if (asset.CurrentStatus == AssetStatus.Disposed)
                        {
                            throw new BusinessException("Disposed assets cannot be bulk updated.");
                        }

                        asset.CurrentStatus = request.TargetStatus.Value;
                        asset.UpdatedAt = DateTime.UtcNow;
                        _unitOfWork.Repository<Asset>().Update(asset);
                        processed++;
                    }
                    else if (string.Equals(action, "maintenance", StringComparison.OrdinalIgnoreCase))
                    {
                        RequirePermission(actorUserId, "Assets.Edit");
                        if (asset.CurrentStatus == AssetStatus.Disposed)
                        {
                            throw new BusinessException("Disposed assets cannot be marked for maintenance.");
                        }

                        asset.CurrentStatus = AssetStatus.UnderMaintenance;
                        asset.UpdatedAt = DateTime.UtcNow;
                        _unitOfWork.Repository<Asset>().Update(asset);
                        processed++;
                    }
                    else
                    {
                        throw new BusinessException("Unsupported bulk action: " + action + ".");
                    }
                }
                catch (BusinessException ex)
                {
                    skipped++;
                    messages.Add(asset.AssetTag + ": " + ex.Message);
                }
            }

            if (processed > 0)
            {
                _unitOfWork.SaveChanges();
                _auditWriter.Write(
                    "Assets.Bulk." + action,
                    nameof(Asset),
                    string.Join(",", request.AssetIds.Distinct()),
                    null,
                    "processed=" + processed + ";skipped=" + skipped + ";notes=" + (request.Notes ?? string.Empty));
            }

            return new AssetBulkActionResultVm
            {
                ProcessedCount = processed,
                SkippedCount = skipped,
                Messages = messages
            };
        }

        private void RequirePermission(string actorUserId, string required)
        {
            if (!_authorizationService.HasPermission(actorUserId, required))
            {
                throw new BusinessException("You do not have permission for this bulk action (" + required + ").");
            }
        }
    }
}
