using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Helpers;
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
            var departmentMoves = new List<string>();
            var processedIds = new List<int>();
            var moveByAsset = new Dictionary<int, string>();
            Department targetDepartment = null;
            if (string.Equals(action, "department", StringComparison.OrdinalIgnoreCase) && request.TargetDepartmentId.HasValue)
            {
                targetDepartment = _unitOfWork.Repository<Department>().GetById(request.TargetDepartmentId.Value);
                if (targetDepartment == null || !targetDepartment.IsActive || targetDepartment.DepartmentKind == DepartmentKind.Grade)
                {
                    throw new BusinessException("Target department was not found, is inactive, or is a grade. Choose an active department, sub-department, class or room.");
                }
            }

            var bulkBatchId = AssetMovementRecorder.NewBatchId();
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

                        if (targetDepartment == null
                            || (asset.OrganizationId.HasValue && targetDepartment.OrganizationId.HasValue
                                && targetDepartment.OrganizationId != asset.OrganizationId))
                        {
                            // Same-org guard: never point an asset at another organisation's department.
                            if (asset.OrganizationId.HasValue)
                            {
                                new OrgReferenceValidator(_unitOfWork, _auditWriter).Report(
                                    asset.OrganizationId.Value,
                                    "Assets.Bulk.Department",
                                    nameof(Asset),
                                    asset.Id.ToString(),
                                    "DepartmentId",
                                    request.TargetDepartmentId.Value.ToString(),
                                    false);
                            }

                            throw new BusinessException("Target department does not belong to this company.");
                        }

                        var fromDepartmentId = asset.DepartmentId.HasValue && asset.DepartmentId.Value > 0 ? asset.DepartmentId : null;
                        var toDepartmentId = request.TargetDepartmentId.Value;
                        if (fromDepartmentId.HasValue && fromDepartmentId.Value != toDepartmentId)
                        {
                            var fromDepartment = _unitOfWork.Repository<Department>().GetById(fromDepartmentId.Value);
                            AssetMoveRules.Ensure(fromDepartment == null ? (DepartmentKind?)null : fromDepartment.DepartmentKind, AssetMoveRules.IsAdministrationRoot(fromDepartment), targetDepartment.DepartmentKind);
                        }
                        var now = DateTime.UtcNow;
                        asset.DepartmentId = toDepartmentId;
                        asset.UpdatedAt = now;
                        _unitOfWork.Repository<Asset>().Update(asset);
                        if (!fromDepartmentId.HasValue || fromDepartmentId.Value != toDepartmentId)
                        {
                            var bulkEvent = new AssetCustodyEvent
                            {
                                AssetId = asset.Id,
                                ActionType = CustodyActionType.Transferred,
                                ActionDate = now,
                                FromDepartmentId = fromDepartmentId,
                                ToDepartmentId = toDepartmentId,
                                Reason = "Department changed (bulk move)",
                                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                                CreatedAt = now,
                                IsActive = true
                            };
                            _unitOfWork.Repository<AssetCustodyEvent>().Add(bulkEvent);
                            AssetMovementRecorder.FromCustodyEvent(_unitOfWork, asset, bulkEvent, AssetMovementType.BulkDepartment, actorUserId, bulkBatchId);
                        }

                        var move = asset.Id + ":" + (fromDepartmentId.HasValue ? fromDepartmentId.Value.ToString() : "none") + ">" + toDepartmentId;
                        departmentMoves.Add(move);
                        moveByAsset[asset.Id] = move;
                        processedIds.Add(asset.Id);
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
                        processedIds.Add(asset.Id);
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
                        processedIds.Add(asset.Id);
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
                // One audit row per asset: EntityId is a single Id, so the per-asset history tab
                // (IX_AuditLog_Org_Entity seek) finds it, and no Id list can overflow the column.
                // An audit failure must not turn a committed move into a 500 for the user.
                var batchSuffix = ";batch=" + bulkBatchId + ";batchProcessed=" + processed + ";batchSkipped=" + skipped
                    + (targetDepartment != null ? ";toDepartmentId=" + targetDepartment.Id + ";toDepartment=" + targetDepartment.Name : string.Empty)
                    + ";notes=" + (request.Notes ?? string.Empty);
                foreach (var id in processedIds)
                {
                    string move;
                    try
                    {
                        _auditWriter.Write(
                            "Assets.Bulk." + action,
                            nameof(Asset),
                            id.ToString(),
                            null,
                            (moveByAsset.TryGetValue(id, out move) ? "move(assetId:from>to)=" + move : "asset=" + id) + batchSuffix);
                    }
                    catch (Exception ex)
                    {
                        messages.Add("Asset #" + id + " was updated but its audit entry failed: " + ex.Message);
                    }
                }
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
