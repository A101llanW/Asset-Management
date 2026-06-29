using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public class PurchaseRequestService : IPurchaseRequestService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditWriter _auditWriter;
        private readonly IUserService _userService;
        private readonly IDepartmentScopeService _departmentScope;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IOutboxWriter _outboxWriter;
        private readonly IWebhookService _webhookService;
        private readonly IApprovalWorkflowEngine _approvalEngine;
        private readonly IOperationsQueryRepository _operationsQueryRepository;
        private readonly IAuthorizationService _authorizationService;
        private readonly ICurrentUserContext _currentUser;

        public PurchaseRequestService(
            IUnitOfWork unitOfWork,
            IAuditWriter auditWriter,
            IUserService userService,
            IDepartmentScopeService departmentScope,
            IOrganizationScopeService organizationScope,
            IOutboxWriter outboxWriter,
            IWebhookService webhookService,
            IApprovalWorkflowEngine approvalEngine,
            IOperationsQueryRepository operationsQueryRepository,
            IAuthorizationService authorizationService = null,
            ICurrentUserContext currentUser = null)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
            _userService = userService;
            _departmentScope = departmentScope;
            _organizationScope = organizationScope;
            _outboxWriter = outboxWriter;
            _webhookService = webhookService;
            _approvalEngine = approvalEngine;
            _operationsQueryRepository = operationsQueryRepository;
            _authorizationService = authorizationService;
            _currentUser = currentUser;
        }

        public IEnumerable<PurchaseRequestListItemVm> GetAll()
        {
            if (IsDepartmentScopeDenied())
            {
                return new List<PurchaseRequestListItemVm>();
            }

            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return new List<PurchaseRequestListItemVm>();
            }

            var bypassDepartmentScope = _departmentScope.BypassesDepartmentScope || CanApprovePurchases();
            int? departmentId = null;
            var denyDepartmentScope = false;
            if (!bypassDepartmentScope)
            {
                departmentId = _departmentScope.ScopedDepartmentId;
                denyDepartmentScope = !departmentId.HasValue;
            }

            return _operationsQueryRepository.GetPurchaseRequestList(
                organizationId.Value,
                departmentId,
                bypassDepartmentScope,
                denyDepartmentScope);
        }

        public PurchaseRequestDetailVm GetById(int id)
        {
            var entity = _unitOfWork.Repository<PurchaseRequest>().GetById(id);
            if (entity == null)
            {
                return null;
            }

            EnsureCanAccessPurchaseRequest(entity);

            var organizationId = entity.OrganizationId ?? _organizationScope.GetCurrentOrganizationId();
            var roleLookup = _unitOfWork.Repository<Role>().GetAll()
                .Where(x => !organizationId.HasValue || x.OrganizationId == organizationId.Value)
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First().Name);
            var departmentLookup = _unitOfWork.Repository<Department>().GetAll()
                .Where(x => !organizationId.HasValue || x.OrganizationId == organizationId.Value)
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First().Name);
            var actions = _unitOfWork.Repository<PurchaseApprovalAction>().Find(x => x.PurchaseRequestId == id)
                .OrderBy(x => x.StageNumber).ThenBy(x => x.DecisionDate).ToList();
            var stageRoleIds = ResolveStageRoleIds(entity, persistBackfill: false);
            var stageUserIds = ResolveStageUserIds(entity, persistBackfill: false);
            var stageNumber = entity.CurrentApprovalStage <= 0 ? 1 : entity.CurrentApprovalStage;
            var stageRoleId = stageNumber > 0 && stageNumber <= stageRoleIds.Count
                ? (int?)stageRoleIds[stageNumber - 1]
                : null;
            var stageUserId = ApprovalWorkflowSettingsHelper.TryGetCurrentStageUserId(
                ApprovalWorkflowSettingsHelper.SerializeStageUserIds(stageUserIds),
                stageNumber);
            var linkedRecord = _unitOfWork.Repository<PurchaseRecord>().Find(x => x.PurchaseRequestId == id).FirstOrDefault();
            var targetAsset = entity.TargetAssetId.HasValue
                ? _unitOfWork.Repository<Asset>().GetById(entity.TargetAssetId.Value)
                : null;

            return new PurchaseRequestDetailVm
            {
                Id = entity.Id,
                RequestNumber = entity.RequestNumber,
                RequestedById = entity.RequestedById,
                ApprovedById = entity.ApprovedById,
                ApprovalStatus = entity.ApprovalStatus.ToString(),
                DepartmentId = entity.DepartmentId,
                DepartmentName = departmentLookup.ContainsKey(entity.DepartmentId) ? departmentLookup[entity.DepartmentId] : null,
                Justification = entity.Justification,
                ItemDescription = entity.ItemDescription,
                QuantityInStock = entity.QuantityInStock,
                RequiredDate = entity.RequiredDate,
                OrderByUserId = entity.OrderByUserId,
                EstimatedUnitCost = entity.EstimatedUnitCost,
                Quantity = entity.Quantity,
                Currency = entity.Currency,
                Notes = entity.Notes,
                AttachmentFileName = entity.AttachmentFileName,
                AttachmentContentType = entity.AttachmentContentType,
                HasAttachment = !string.IsNullOrWhiteSpace(entity.AttachmentFilePath),
                ApprovedAt = entity.ApprovedAt,
                CreatedAt = entity.CreatedAt,
                CurrentApprovalStage = entity.CurrentApprovalStage,
                CurrentStageRoleId = stageRoleId,
                CurrentStageRoleName = ApprovalWorkflowSettingsHelper.ResolveRoleName(roleLookup, stageRoleId),
                CurrentStageUserId = stageUserId,
                CurrentStageUserName = ResolveApproverDisplayName(stageUserId),
                IsPending = entity.ApprovalStatus == ApprovalStatus.Pending,
                IsApproved = entity.ApprovalStatus == ApprovalStatus.Approved,
                HasPurchaseRecord = linkedRecord != null,
                LinkedPurchaseRecordId = linkedRecord?.Id,
                TargetAssetId = entity.TargetAssetId,
                TargetAssetTag = targetAsset?.AssetTag,
                TargetAssetName = targetAsset?.AssetName,
                ApprovalHistory = ApprovalWorkflowHelper.MapDecisionHistory(
                    actions.Select(x => ApprovalWorkflowHelper.ToSnapshot(
                        x.StageNumber, x.RoleId, x.ApproverUserId, x.Decision, x.Notes, x.DecisionDate)),
                    roleLookup)
            };
        }

        public int Submit(PurchaseRequestCreateVm model, string requestedByUserId)
        {
            if (model == null)
            {
                throw new BusinessException("Purchase request is required.");
            }

            if (string.IsNullOrWhiteSpace(requestedByUserId))
            {
                throw new BusinessException("Current user is required.");
            }

            if (!model.EstimatedUnitCost.HasValue || model.EstimatedUnitCost.Value <= 0)
            {
                throw new BusinessException("Estimated unit cost must be greater than zero.");
            }

            var department = _unitOfWork.Repository<Department>().GetById(model.DepartmentId);
            if (department == null)
            {
                throw new BusinessException("Department not found.");
            }

            _departmentScope.EnsureCanAccessDepartment(department);
            _departmentScope.EnsureCanAccessDepartmentId(model.DepartmentId);

            if (string.IsNullOrWhiteSpace(model.ItemDescription))
            {
                throw new BusinessException("Item description is required.");
            }

            if (!string.IsNullOrWhiteSpace(model.OrderByUserId))
            {
                var orderByUser = _userService.GetById(model.OrderByUserId);
                if (orderByUser == null || !orderByUser.IsActive)
                {
                    throw new BusinessException("Selected order-by employee was not found or is inactive.");
                }

                if (orderByUser.DepartmentId != model.DepartmentId)
                {
                    throw new BusinessException("Order-by employee must belong to the request department.");
                }
            }

            var targetAssetId = ResolveTargetAssetId(model.TargetAssetId);

            var approvalConfig = ApprovalWorkflowHelper.GetProcessConfiguration(_unitOfWork, ApprovalProcessCodes.Purchase);
            var entity = new PurchaseRequest
            {
                RequestNumber = "PENDING",
                RequestedById = requestedByUserId.Trim(),
                DepartmentId = model.DepartmentId,
                ItemDescription = model.ItemDescription.Trim(),
                Justification = (model.Justification ?? string.Empty).Trim(),
                QuantityInStock = model.QuantityInStock,
                RequiredDate = model.RequiredDate,
                OrderByUserId = string.IsNullOrWhiteSpace(model.OrderByUserId) ? null : model.OrderByUserId.Trim(),
                EstimatedUnitCost = model.EstimatedUnitCost.Value,
                Quantity = model.Quantity,
                Currency = string.IsNullOrWhiteSpace(model.Currency)
                    ? ApprovalWorkflowSettingsHelper.GetDefaultCurrencyCode(_unitOfWork.Repository<SystemSetting>().GetAll())
                    : model.Currency.Trim().ToUpperInvariant(),
                Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
                TargetAssetId = targetAssetId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            if (!approvalConfig.UsesApproval)
            {
                if (approvalConfig.RequiresApproval)
                {
                    throw new BusinessException(
                        "Requisition approval is enabled but Stage 1 approver role is not configured. "
                        + "Go to Settings → Approval Matrix, assign a role for Requisition (e.g. Procurement Officer), and save.");
                }

                entity.ApprovalStatus = ApprovalStatus.Approved;
                entity.CurrentApprovalStage = 0;
                entity.ApprovalStageRoleIds = null;
                entity.ApprovalStageUserIds = null;
                entity.ApprovedById = requestedByUserId.Trim();
                entity.ApprovedAt = DateTime.UtcNow;
                _unitOfWork.Repository<PurchaseRequest>().Add(entity);
                _unitOfWork.SaveChanges();
                entity.RequestNumber = "PR-" + entity.Id.ToString("D6");
                entity.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Repository<PurchaseRequest>().Update(entity);
                _unitOfWork.SaveChanges();
                NotificationHelper.AddNotification(
                    _unitOfWork,
                    _outboxWriter,
                    _organizationScope,
                    requestedByUserId,
                    NotificationType.General,
                    "Purchase request approved",
                    "Purchase request " + entity.RequestNumber + " was approved automatically (approval workflow disabled).",
                    "/PurchaseRequests/Details/" + entity.Id);
                _unitOfWork.SaveChanges();
                _auditWriter.Write("Purchases.Request", nameof(PurchaseRequest), entity.Id.ToString(), null, "AutoApproved");
                return entity.Id;
            }

            entity.ApprovalStatus = ApprovalStatus.Pending;
            entity.CurrentApprovalStage = 1;
            entity.ApprovalStageRoleIds = ApprovalWorkflowSettingsHelper.SerializeStageRoleIds(approvalConfig.StageRoleIds.Select(x => (int?)x));
            entity.ApprovalStageUserIds = ApprovalWorkflowSettingsHelper.SerializeStageUserIds(approvalConfig.StageUserIds);
            entity.ApprovedAt = null;
            entity.ApprovedById = null;
            _unitOfWork.Repository<PurchaseRequest>().Add(entity);
            _unitOfWork.SaveChanges();
            entity.RequestNumber = "PR-" + entity.Id.ToString("D6");
            entity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<PurchaseRequest>().Update(entity);
            _unitOfWork.SaveChanges();
            NotificationHelper.AddNotification(
                _unitOfWork,
                _outboxWriter,
                _organizationScope,
                requestedByUserId,
                NotificationType.PendingApproval,
                "Purchase request submitted",
                "Purchase request " + entity.RequestNumber + " is pending approval.",
                "/PurchaseRequests/Details/" + entity.Id);
            NotificationHelper.AddStageApproverNotification(
                _unitOfWork,
                _outboxWriter,
                _organizationScope,
                _userService,
                approvalConfig.StageRoleIds.FirstOrDefault(),
                ApprovalWorkflowHelper.GetStageUserId(approvalConfig.StageUserIds, 1),
                "Purchase approval required",
                "Purchase request " + entity.RequestNumber + " is awaiting Stage 1 approval.",
                "/PurchaseRequests/Details/" + entity.Id);
            _unitOfWork.SaveChanges();
            _auditWriter.Write("Purchases.Request", nameof(PurchaseRequest), entity.Id.ToString(), null, "PendingApproval");
            return entity.Id;
        }

        public void SaveAttachment(int purchaseRequestId, PurchaseRequestAttachmentInfo attachment)
        {
            if (attachment == null || string.IsNullOrWhiteSpace(attachment.FilePath))
            {
                return;
            }

            var entity = _unitOfWork.Repository<PurchaseRequest>().GetById(purchaseRequestId);
            if (entity == null)
            {
                throw new BusinessException("Purchase request not found.");
            }

            EnsureCanAccessPurchaseRequest(entity);
            entity.AttachmentFileName = attachment.FileName;
            entity.AttachmentFilePath = attachment.FilePath;
            entity.AttachmentContentType = attachment.ContentType;
            entity.AttachmentFileSizeBytes = attachment.FileSizeBytes;
            entity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<PurchaseRequest>().Update(entity);
            _unitOfWork.SaveChanges();
        }

        public string GetAttachmentRelativePath(int purchaseRequestId)
        {
            var entity = _unitOfWork.Repository<PurchaseRequest>().GetById(purchaseRequestId);
            if (entity == null || string.IsNullOrWhiteSpace(entity.AttachmentFilePath))
            {
                return null;
            }

            EnsureCanAccessPurchaseRequest(entity);
            return entity.AttachmentFilePath;
        }

        public void Approve(PurchaseRequestApprovalVm model, string approvedByUserId, int? approverRoleId, bool isSuperAdmin)
        {
            if (model == null)
            {
                throw new BusinessException("Approval payload is required.");
            }

            var request = GetPendingRequest(model.PurchaseRequestId);
            EnsureCanAccessPurchaseRequest(request);
            var stageRoleIds = ResolveStageRoleIds(request, persistBackfill: true);
            var stageUserIds = ResolveStageUserIds(request, persistBackfill: true);
            var stageNumber = request.CurrentApprovalStage <= 0 ? 1 : request.CurrentApprovalStage;
            var expectedRoleId = ApprovalWorkflowHelper.GetStageRoleId(stageRoleIds, stageNumber, ApprovalProcessCodes.Purchase);
            var expectedUserId = ApprovalWorkflowHelper.GetStageUserId(stageUserIds, stageNumber);
            var isFinalStage = stageNumber >= stageRoleIds.Count;

            _approvalEngine.ExecuteStageDecision(new ApprovalStageDecisionRequest
            {
                ProcessCode = ApprovalProcessCodes.Purchase,
                ActingUserId = approvedByUserId,
                ApproverRoleId = approverRoleId,
                IsSuperAdmin = isSuperAdmin,
                RequesterUserId = request.RequestedById,
                Notes = NormalizeText(model.Notes),
                Decision = ApprovalStatus.Approved,
                RequestEntity = request,
                RequestEntityType = typeof(PurchaseRequest),
                StageNumber = stageNumber,
                StageRoleIds = stageRoleIds,
                ExpectedRoleId = expectedRoleId,
                ExpectedUserId = expectedUserId,
                ApprovalActionEntity = new PurchaseApprovalAction
                {
                    PurchaseRequestId = request.Id,
                    StageNumber = stageNumber,
                    RoleId = expectedRoleId,
                    ApproverUserId = approvedByUserId,
                    Decision = ApprovalStatus.Approved,
                    Notes = NormalizeText(model.Notes),
                    DecisionDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                },
                WriteNotifications = uow =>
                {
                    if (isFinalStage)
                    {
                        NotificationHelper.AddNotification(
                            uow,
                            _outboxWriter,
                            _organizationScope,
                            request.RequestedById,
                            NotificationType.General,
                            "Purchase request approved",
                            "Purchase request " + request.RequestNumber + " has been fully approved. You can record the purchase order when ready.",
                            "/PurchaseRequests/Details/" + request.Id);
                    }
                    else
                    {
                        NotificationHelper.AddNotification(
                            uow,
                            _outboxWriter,
                            _organizationScope,
                            request.RequestedById,
                            NotificationType.PendingApproval,
                            "Purchase stage approved",
                            "Purchase request " + request.RequestNumber + " moved to Stage " + request.CurrentApprovalStage + " approval.",
                            "/PurchaseRequests/Details/" + request.Id);
                        NotificationHelper.AddStageApproverNotification(
                            uow,
                            _outboxWriter,
                            _organizationScope,
                            _userService,
                            ApprovalWorkflowHelper.GetStageRoleId(stageRoleIds, request.CurrentApprovalStage, ApprovalProcessCodes.Purchase),
                            ApprovalWorkflowHelper.GetStageUserId(stageUserIds, request.CurrentApprovalStage),
                            "Purchase approval stage advanced",
                            "Purchase request " + request.RequestNumber + " is now awaiting Stage " + request.CurrentApprovalStage + " approval.",
                            "/PurchaseRequests/Details/" + request.Id);
                    }
                }
            });

            _auditWriter.Write(
                "Purchases.Approve",
                nameof(PurchaseRequest),
                request.Id.ToString(),
                isFinalStage ? "PendingApproval" : "Stage" + stageNumber,
                isFinalStage ? "Approved" : "Stage" + request.CurrentApprovalStage);

            if (isFinalStage)
            {
                _webhookService.QueueDelivery(
                    "purchase.approved",
                    "{\"purchaseRequestId\":" + request.Id + ",\"requestNumber\":\"" + (request.RequestNumber ?? string.Empty) + "\"}");
            }
        }

        public void Reject(PurchaseRequestApprovalVm model, string rejectedByUserId, int? approverRoleId, bool isSuperAdmin)
        {
            if (model == null)
            {
                throw new BusinessException("Rejection payload is required.");
            }

            var request = GetPendingRequest(model.PurchaseRequestId);
            EnsureCanAccessPurchaseRequest(request);
            var stageRoleIds = ResolveStageRoleIds(request, persistBackfill: true);
            var stageUserIds = ResolveStageUserIds(request, persistBackfill: true);
            var stageNumber = request.CurrentApprovalStage <= 0 ? 1 : request.CurrentApprovalStage;
            var expectedRoleId = ApprovalWorkflowHelper.GetStageRoleId(stageRoleIds, stageNumber, ApprovalProcessCodes.Purchase);
            var expectedUserId = ApprovalWorkflowHelper.GetStageUserId(stageUserIds, stageNumber);

            _approvalEngine.ExecuteStageDecision(new ApprovalStageDecisionRequest
            {
                ProcessCode = ApprovalProcessCodes.Purchase,
                ActingUserId = rejectedByUserId,
                ApproverRoleId = approverRoleId,
                IsSuperAdmin = isSuperAdmin,
                RequesterUserId = request.RequestedById,
                Notes = NormalizeText(model.Notes),
                Decision = ApprovalStatus.Rejected,
                RequestEntity = request,
                RequestEntityType = typeof(PurchaseRequest),
                StageNumber = stageNumber,
                StageRoleIds = stageRoleIds,
                ExpectedRoleId = expectedRoleId,
                ExpectedUserId = expectedUserId,
                ApprovalActionEntity = new PurchaseApprovalAction
                {
                    PurchaseRequestId = request.Id,
                    StageNumber = stageNumber,
                    RoleId = expectedRoleId,
                    ApproverUserId = rejectedByUserId,
                    Decision = ApprovalStatus.Rejected,
                    Notes = NormalizeText(model.Notes),
                    DecisionDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                },
                WriteNotifications = uow =>
                {
                    NotificationHelper.AddNotification(
                        uow,
                        _outboxWriter,
                        _organizationScope,
                        request.RequestedById,
                        NotificationType.General,
                        "Purchase request rejected",
                        "Purchase request " + request.RequestNumber + " was rejected at Stage " + stageNumber + ".",
                        "/PurchaseRequests/Details/" + request.Id);
                }
            });

            _auditWriter.Write("Purchases.Approve", nameof(PurchaseRequest), request.Id.ToString(), "PendingApproval", "Rejected");
        }

        private PurchaseRequest GetPendingRequest(int id)
        {
            var request = _unitOfWork.Repository<PurchaseRequest>().GetById(id);
            if (request == null)
            {
                throw new BusinessException("Purchase request not found.");
            }

            if (request.ApprovalStatus != ApprovalStatus.Pending)
            {
                throw new BusinessException("This purchase request is no longer pending approval.");
            }

            return request;
        }

        private IList<int> ResolveStageRoleIds(PurchaseRequest request, bool persistBackfill)
        {
            var stageRoleIds = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(request.ApprovalStageRoleIds);
            if (stageRoleIds.Count > 0)
            {
                return stageRoleIds;
            }

            var config = ApprovalWorkflowHelper.GetProcessConfiguration(_unitOfWork, ApprovalProcessCodes.Purchase);
            if (!config.UsesApproval)
            {
                throw new BusinessException(
                    "This requisition has no approval stages recorded and requisition approval is not configured. "
                    + "Go to Settings → Approval Matrix, enable Requisition, assign Stage 1 (e.g. Procurement Officer), save, then resubmit.");
            }

            if (persistBackfill)
            {
                request.ApprovalStageRoleIds = ApprovalWorkflowSettingsHelper.SerializeStageRoleIds(
                    config.StageRoleIds.Select(x => (int?)x));
                if (config.StageUserIds != null && config.StageUserIds.Count > 0)
                {
                    request.ApprovalStageUserIds = ApprovalWorkflowSettingsHelper.SerializeStageUserIds(config.StageUserIds);
                }
                if (request.CurrentApprovalStage <= 0)
                {
                    request.CurrentApprovalStage = 1;
                }

                request.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Repository<PurchaseRequest>().Update(request);
                _unitOfWork.SaveChanges();
            }

            return config.StageRoleIds;
        }

        private IList<string> ResolveStageUserIds(PurchaseRequest request, bool persistBackfill)
        {
            var stageUserIds = ApprovalWorkflowSettingsHelper.ParseStageUserIds(request.ApprovalStageUserIds);
            if (stageUserIds.Count > 0)
            {
                return stageUserIds;
            }

            var config = ApprovalWorkflowHelper.GetProcessConfiguration(_unitOfWork, ApprovalProcessCodes.Purchase);
            if (!config.UsesApproval)
            {
                return stageUserIds;
            }

            if (persistBackfill && config.StageUserIds != null && config.StageUserIds.Count > 0)
            {
                request.ApprovalStageUserIds = ApprovalWorkflowSettingsHelper.SerializeStageUserIds(config.StageUserIds);
                request.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Repository<PurchaseRequest>().Update(request);
                _unitOfWork.SaveChanges();
            }

            return config.StageUserIds ?? new List<string>();
        }

        private string ResolveApproverDisplayName(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId) || _userService == null)
            {
                return userId;
            }

            var user = _userService.GetById(userId);
            if (user == null)
            {
                return userId;
            }

            var name = ((user.FirstName ?? string.Empty) + " " + (user.LastName ?? string.Empty)).Trim();
            return string.IsNullOrWhiteSpace(name) ? (user.Email ?? userId) : name;
        }

        private bool CanApprovePurchases()
        {
            var userId = _currentUser == null ? null : _currentUser.UserId;
            return !string.IsNullOrWhiteSpace(userId)
                && _authorizationService != null
                && _authorizationService.HasPermission(userId, "Purchases.Approve");
        }

        private bool CanAccessPurchaseRequest(PurchaseRequest request)
        {
            if (request == null || _departmentScope.BypassesDepartmentScope || CanApprovePurchases())
            {
                return true;
            }

            var departmentId = _departmentScope.ScopedDepartmentId;
            if (!departmentId.HasValue)
            {
                return true;
            }

            return request.DepartmentId == departmentId.Value;
        }

        private void EnsureCanAccessPurchaseRequest(PurchaseRequest request)
        {
            if (!CanAccessPurchaseRequest(request))
            {
                throw new BusinessException("You do not have access to this department's purchase requests.");
            }
        }

        private bool IsDepartmentScopeDenied()
        {
            return !_departmentScope.BypassesDepartmentScope
                && !CanApprovePurchases()
                && !_departmentScope.ScopedDepartmentId.HasValue;
        }

        private static string NormalizeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private int? ResolveTargetAssetId(int? targetAssetId)
        {
            if (!targetAssetId.HasValue || targetAssetId.Value <= 0)
            {
                return null;
            }

            var asset = _unitOfWork.Repository<Asset>().GetById(targetAssetId.Value);
            if (asset == null || !asset.IsActive)
            {
                throw new BusinessException("Selected asset was not found or is inactive.");
            }

            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (organizationId.HasValue
                && asset.OrganizationId.HasValue
                && asset.OrganizationId.Value != organizationId.Value)
            {
                throw new BusinessException("Selected asset does not belong to this organization.");
            }

            return asset.Id;
        }
    }
}
