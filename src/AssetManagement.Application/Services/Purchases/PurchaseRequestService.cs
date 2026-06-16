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

        public PurchaseRequestService(
            IUnitOfWork unitOfWork,
            IAuditWriter auditWriter,
            IUserService userService,
            IDepartmentScopeService departmentScope,
            IOrganizationScopeService organizationScope,
            IOutboxWriter outboxWriter,
            IWebhookService webhookService,
            IApprovalWorkflowEngine approvalEngine,
            IOperationsQueryRepository operationsQueryRepository)
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

            var bypassDepartmentScope = _departmentScope.BypassesDepartmentScope;
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

            var roleLookup = _unitOfWork.Repository<Role>().GetAll().ToDictionary(x => x.Id, x => x.Name);
            var departmentLookup = _unitOfWork.Repository<Department>().GetAll().ToDictionary(x => x.Id, x => x.Name);
            var actions = _unitOfWork.Repository<PurchaseApprovalAction>().Find(x => x.PurchaseRequestId == id)
                .OrderBy(x => x.StageNumber).ThenBy(x => x.DecisionDate).ToList();
            var stageRoleIds = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(entity.ApprovalStageRoleIds);
            var stageRoleId = entity.CurrentApprovalStage > 0 && entity.CurrentApprovalStage <= stageRoleIds.Count
                ? (int?)stageRoleIds[entity.CurrentApprovalStage - 1]
                : null;
            var linkedRecord = _unitOfWork.Repository<PurchaseRecord>().Find(x => x.PurchaseRequestId == id).FirstOrDefault();

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
                EstimatedUnitCost = entity.EstimatedUnitCost,
                Quantity = entity.Quantity,
                Currency = entity.Currency,
                Notes = entity.Notes,
                ApprovedAt = entity.ApprovedAt,
                CreatedAt = entity.CreatedAt,
                CurrentApprovalStage = entity.CurrentApprovalStage,
                CurrentStageRoleId = stageRoleId,
                CurrentStageRoleName = ApprovalWorkflowSettingsHelper.ResolveRoleName(roleLookup, stageRoleId),
                IsPending = entity.ApprovalStatus == ApprovalStatus.Pending,
                IsApproved = entity.ApprovalStatus == ApprovalStatus.Approved,
                HasPurchaseRecord = linkedRecord != null,
                LinkedPurchaseRecordId = linkedRecord?.Id,
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

            var approvalConfig = ApprovalWorkflowHelper.GetProcessConfiguration(_unitOfWork, ApprovalProcessCodes.Purchase);
            var entity = new PurchaseRequest
            {
                RequestNumber = "PENDING",
                RequestedById = requestedByUserId.Trim(),
                DepartmentId = model.DepartmentId,
                Justification = (model.Justification ?? string.Empty).Trim(),
                EstimatedUnitCost = model.EstimatedUnitCost.Value,
                Quantity = model.Quantity,
                Currency = string.IsNullOrWhiteSpace(model.Currency)
                    ? ApprovalWorkflowSettingsHelper.GetDefaultCurrencyCode(_unitOfWork.Repository<SystemSetting>().GetAll())
                    : model.Currency.Trim().ToUpperInvariant(),
                Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            if (!approvalConfig.UsesApproval)
            {
                entity.ApprovalStatus = ApprovalStatus.Approved;
                entity.CurrentApprovalStage = 0;
                entity.ApprovalStageRoleIds = null;
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
            NotificationHelper.AddRoleStageNotification(
                _unitOfWork,
                _outboxWriter,
                _organizationScope,
                _userService,
                approvalConfig.StageRoleIds.FirstOrDefault(),
                "Purchase approval required",
                "Purchase request " + entity.RequestNumber + " is awaiting Stage 1 approval.",
                "/PurchaseRequests/Details/" + entity.Id);
            _unitOfWork.SaveChanges();
            _auditWriter.Write("Purchases.Request", nameof(PurchaseRequest), entity.Id.ToString(), null, "PendingApproval");
            return entity.Id;
        }

        public void Approve(PurchaseRequestApprovalVm model, string approvedByUserId, int? approverRoleId, bool isSuperAdmin)
        {
            if (model == null)
            {
                throw new BusinessException("Approval payload is required.");
            }

            var request = GetPendingRequest(model.PurchaseRequestId);
            EnsureCanAccessPurchaseRequest(request);
            var stageRoleIds = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(request.ApprovalStageRoleIds);
            var stageNumber = request.CurrentApprovalStage <= 0 ? 1 : request.CurrentApprovalStage;
            var expectedRoleId = ApprovalWorkflowHelper.GetStageRoleId(stageRoleIds, stageNumber, ApprovalProcessCodes.Purchase);
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
                        NotificationHelper.AddRoleStageNotification(
                            uow,
                            _outboxWriter,
                            _organizationScope,
                            _userService,
                            ApprovalWorkflowHelper.GetStageRoleId(stageRoleIds, request.CurrentApprovalStage, ApprovalProcessCodes.Purchase),
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
            var stageRoleIds = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(request.ApprovalStageRoleIds);
            var stageNumber = request.CurrentApprovalStage <= 0 ? 1 : request.CurrentApprovalStage;
            var expectedRoleId = ApprovalWorkflowHelper.GetStageRoleId(stageRoleIds, stageNumber, ApprovalProcessCodes.Purchase);

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

        private void EnsureCanAccessPurchaseRequest(PurchaseRequest request)
        {
            if (request == null)
            {
                throw new BusinessException("Purchase request not found.");
            }

            _departmentScope.EnsureCanAccessDepartmentId(request.DepartmentId);
        }

        private bool IsDepartmentScopeDenied()
        {
            return !_departmentScope.BypassesDepartmentScope && !_departmentScope.ScopedDepartmentId.HasValue;
        }

        private static string NormalizeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
