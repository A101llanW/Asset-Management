using System;
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
    public class AssetRequestService : IAssetRequestService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditWriter _auditWriter;
        private readonly IAssignmentService _assignmentService;
        private readonly ISegregationOfDutiesService _segregationOfDuties;
        private readonly IDepartmentScopeService _departmentScope;
        private readonly IUserService _userService;
        private readonly IAuthorizationService _authorizationService;
        private readonly ICurrentUserContext _currentUser;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IOutboxWriter _outboxWriter;
        private readonly IOperationsQueryRepository _operationsQueryRepository;

        public AssetRequestService(
            IUnitOfWork unitOfWork,
            IAuditWriter auditWriter,
            IAssignmentService assignmentService,
            ISegregationOfDutiesService segregationOfDuties,
            IDepartmentScopeService departmentScope,
            IUserService userService,
            IAuthorizationService authorizationService,
            ICurrentUserContext currentUser,
            IOrganizationScopeService organizationScope,
            IOutboxWriter outboxWriter,
            IOperationsQueryRepository operationsQueryRepository)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
            _assignmentService = assignmentService;
            _segregationOfDuties = segregationOfDuties;
            _departmentScope = departmentScope;
            _userService = userService;
            _authorizationService = authorizationService;
            _currentUser = currentUser;
            _organizationScope = organizationScope;
            _outboxWriter = outboxWriter;
            _operationsQueryRepository = operationsQueryRepository;
        }

        public AssetRequestListPageVm GetRequests(AssetRequestFilterVm filter, string sort, string direction, int page, int pageSize)
        {
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return new AssetRequestListPageVm
                {
                    Items = new System.Collections.Generic.List<AssetRequestListVm>(),
                    TotalCount = 0,
                    Page = 1,
                    PageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100)
                };
            }

            var bypassDepartmentScope = _departmentScope.BypassesDepartmentScope;
            int? departmentId = null;
            var denyDepartmentScope = false;
            if (!bypassDepartmentScope)
            {
                departmentId = _departmentScope.ScopedDepartmentId;
                denyDepartmentScope = !departmentId.HasValue;
            }

            var restrictToOwnDepartment = departmentId.HasValue && !CanApproveRequests();
            return _operationsQueryRepository.GetAssetRequestListPage(
                filter,
                sort,
                direction,
                page,
                pageSize,
                organizationId.Value,
                departmentId,
                bypassDepartmentScope,
                denyDepartmentScope,
                restrictToOwnDepartment);
        }

        public AssetRequestDetailsVm GetById(int id)
        {
            var entity = _unitOfWork.Repository<AssetRequest>().GetById(id);
            if (entity == null || !entity.IsActive || !CanAccessRequest(entity))
            {
                return null;
            }

            return new AssetRequestDetailsVm
            {
                Id = entity.Id,
                RequestedById = entity.RequestedById,
                DepartmentId = entity.DepartmentId,
                DepartmentName = entity.DepartmentId.HasValue && entity.Department != null ? entity.Department.Name : null,
                CategoryId = entity.CategoryId,
                CategoryName = entity.CategoryId.HasValue && entity.Category != null ? entity.Category.Name : null,
                RequestedAssetId = entity.RequestedAssetId,
                RequestedAssetName = ResolveRequestedAssetName(entity),
                RequestedAssetTag = entity.RequestedAssetTag,
                Justification = entity.Justification,
                Status = entity.Status,
                FulfilledAssetId = entity.FulfilledAssetId,
                FulfilledAssetTag = ResolveFulfilledAssetTag(entity),
                ReviewedByName = entity.ReviewedById,
                ReviewedAt = entity.ReviewedAt,
                ReviewNotes = entity.ReviewNotes,
                CreatedAt = entity.CreatedAt
            };
        }

        public int Submit(AssetRequestCreateVm model, string requestedByUserId)
        {
            if (model == null)
            {
                throw new BusinessException("Request details are required.");
            }

            if (string.IsNullOrWhiteSpace(requestedByUserId))
            {
                throw new BusinessException("Requester is required.");
            }

            if (string.IsNullOrWhiteSpace(model.Justification))
            {
                throw new BusinessException("Justification is required.");
            }

            if (!model.DepartmentId.HasValue)
            {
                throw new BusinessException("Department is required.");
            }

            if (!model.CategoryId.HasValue)
            {
                throw new BusinessException("Asset category is required.");
            }

            if (!model.RequestedAssetId.HasValue)
            {
                throw new BusinessException("Please select the asset you want to request.");
            }

            var asset = _unitOfWork.Repository<Asset>().GetById(model.RequestedAssetId.Value);
            if (asset == null || !asset.IsActive)
            {
                throw new BusinessException("Selected asset was not found.");
            }

            if (asset.CurrentStatus != AssetStatus.InStore)
            {
                throw new BusinessException("Selected asset is not available for request.");
            }

            if (asset.DepartmentId != model.DepartmentId.Value)
            {
                throw new BusinessException("Selected asset does not belong to the chosen department.");
            }

            if (asset.CategoryId != model.CategoryId.Value)
            {
                throw new BusinessException("Selected asset does not match the chosen category.");
            }

            var entity = new AssetRequest
            {
                RequestedById = requestedByUserId.Trim(),
                DepartmentId = model.DepartmentId,
                CategoryId = model.CategoryId,
                RequestedAssetId = asset.Id,
                RequestedAssetTag = asset.AssetTag,
                Justification = model.Justification.Trim(),
                Status = AssetRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _unitOfWork.Repository<AssetRequest>().Add(entity);
            _unitOfWork.SaveChanges();
            NotificationHelper.AddNotification(
                _unitOfWork,
                _outboxWriter,
                _organizationScope,
                requestedByUserId,
                NotificationType.General,
                "Asset request submitted",
                "Your asset request #" + entity.Id + " is pending review.",
                "/AssetRequests/Details/" + entity.Id);
            var managerRole = _unitOfWork.Repository<Role>().Query()
                .FirstOrDefault(x => x.IsActive && x.Name == "Asset Manager");
            if (managerRole != null)
            {
                NotificationHelper.AddRoleStageNotification(
                    _unitOfWork,
                    _outboxWriter,
                    _organizationScope,
                    _userService,
                    managerRole.Id,
                    "Asset request pending",
                    "Asset request #" + entity.Id + " requires review.",
                    "/AssetRequests/Details/" + entity.Id);
            }
            _unitOfWork.SaveChanges();
            _auditWriter.Write("AssetRequests.Submit", nameof(AssetRequest), entity.Id.ToString(), null, entity.Status.ToString());
            return entity.Id;
        }

        public void Approve(int id, string reviewedByUserId, string notes)
        {
            var entity = GetPendingRequest(id);
            _segregationOfDuties.EnsureActorIsNotRequester(entity.RequestedById, reviewedByUserId, ApprovalProcessCodes.AssetRequest);
            entity.Status = AssetRequestStatus.Approved;
            entity.ReviewedById = reviewedByUserId;
            entity.ReviewedAt = DateTime.UtcNow;
            entity.ReviewNotes = NormalizeText(notes);
            entity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<AssetRequest>().Update(entity);
            _unitOfWork.SaveChanges();
            NotificationHelper.AddNotification(
                _unitOfWork,
                _outboxWriter,
                _organizationScope,
                entity.RequestedById,
                NotificationType.General,
                "Asset request approved",
                "Asset request #" + entity.Id + " was approved. An asset can now be assigned to fulfill it.",
                "/AssetRequests/Details/" + entity.Id);
            _unitOfWork.SaveChanges();
            _auditWriter.Write("AssetRequests.Approve", nameof(AssetRequest), entity.Id.ToString(), AssetRequestStatus.Pending.ToString(), AssetRequestStatus.Approved.ToString());
        }

        public void Reject(int id, string reviewedByUserId, string notes)
        {
            var entity = GetPendingRequest(id);
            _segregationOfDuties.EnsureActorIsNotRequester(entity.RequestedById, reviewedByUserId, ApprovalProcessCodes.AssetRequest);
            if (string.IsNullOrWhiteSpace(notes))
            {
                throw new BusinessException("Rejection notes are required.");
            }

            entity.Status = AssetRequestStatus.Rejected;
            entity.ReviewedById = reviewedByUserId;
            entity.ReviewedAt = DateTime.UtcNow;
            entity.ReviewNotes = notes.Trim();
            entity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<AssetRequest>().Update(entity);
            _unitOfWork.SaveChanges();
            NotificationHelper.AddNotification(
                _unitOfWork,
                _outboxWriter,
                _organizationScope,
                entity.RequestedById,
                NotificationType.General,
                "Asset request rejected",
                "Asset request #" + entity.Id + " was rejected.",
                "/AssetRequests/Details/" + entity.Id);
            _unitOfWork.SaveChanges();
            _auditWriter.Write("AssetRequests.Reject", nameof(AssetRequest), entity.Id.ToString(), AssetRequestStatus.Pending.ToString(), AssetRequestStatus.Rejected.ToString());
        }

        public void Fulfill(int id, int assetId, string fulfilledByUserId, AssetAssignmentVm assignment)
        {
            var entity = _unitOfWork.Repository<AssetRequest>().GetById(id);
            if (entity == null || !entity.IsActive)
            {
                throw new BusinessException("Asset request not found.");
            }

            if (entity.Status != AssetRequestStatus.Approved)
            {
                throw new BusinessException("Only approved requests can be fulfilled.");
            }

            _segregationOfDuties.EnsureActorIsNotRequester(entity.RequestedById, fulfilledByUserId, ApprovalProcessCodes.AssetRequest);
            assignment.AssetId = assetId;
            assignment.ToUserId = string.IsNullOrWhiteSpace(assignment.ToUserId) ? entity.RequestedById : assignment.ToUserId;

            _unitOfWork.ExecuteInTransaction(() =>
            {
                _assignmentService.AssignWithoutSave(assignment);
                entity.Status = AssetRequestStatus.Fulfilled;
                entity.FulfilledAssetId = assetId;
                entity.ReviewedById = fulfilledByUserId;
                entity.ReviewedAt = DateTime.UtcNow;
                entity.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Repository<AssetRequest>().Update(entity);
                NotificationHelper.AddNotification(
                    _unitOfWork,
                    _outboxWriter,
                    _organizationScope,
                    entity.RequestedById,
                    NotificationType.General,
                    "Asset request fulfilled",
                    "Asset request #" + entity.Id + " has been fulfilled with a tagged asset.",
                    "/Assets/Details/" + assetId);
            });

            _auditWriter.Write("AssetRequests.Fulfill", nameof(AssetRequest), entity.Id.ToString(), AssetRequestStatus.Approved.ToString(), AssetRequestStatus.Fulfilled.ToString());
        }

        private AssetRequest GetPendingRequest(int id)
        {
            var entity = _unitOfWork.Repository<AssetRequest>().GetById(id);
            if (entity == null || !entity.IsActive)
            {
                throw new BusinessException("Asset request not found.");
            }

            if (entity.Status != AssetRequestStatus.Pending)
            {
                throw new BusinessException("Only pending requests can be approved or rejected.");
            }

            EnsureCanAccessRequest(entity);
            return entity;
        }

        private bool CanAccessRequest(AssetRequest entity)
        {
            if (entity == null || _departmentScope.BypassesDepartmentScope || CanApproveRequests())
            {
                return true;
            }

            var departmentId = _departmentScope.ScopedDepartmentId;
            if (!departmentId.HasValue)
            {
                return true;
            }

            return entity.DepartmentId.HasValue && entity.DepartmentId.Value == departmentId.Value;
        }

        private bool CanApproveRequests()
        {
            var userId = _currentUser == null ? null : _currentUser.UserId;
            return !string.IsNullOrWhiteSpace(userId)
                && _authorizationService != null
                && _authorizationService.HasPermission(userId, "Assets.Request.Approve");
        }

        private void EnsureCanAccessRequest(AssetRequest entity)
        {
            if (!CanAccessRequest(entity))
            {
                throw new BusinessException("This asset request belongs to another department.");
            }
        }

        private static IQueryable<AssetRequest> ApplyRequestSort(IQueryable<AssetRequest> query, string sort, string direction)
        {
            var desc = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
            switch ((sort ?? string.Empty).ToLowerInvariant())
            {
                case "status":
                    return desc ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status);
                case "department":
                    return desc
                        ? query.OrderByDescending(x => x.Department.Name)
                        : query.OrderBy(x => x.Department.Name);
                default:
                    return desc ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt);
            }
        }

        private static IQueryable<AssetRequestListVm> ProjectRequestList(IQueryable<AssetRequest> query)
        {
            return query.Select(x => new AssetRequestListVm
            {
                Id = x.Id,
                RequestedByName = x.RequestedById,
                DepartmentName = x.DepartmentId.HasValue && x.Department != null ? x.Department.Name : null,
                CategoryName = x.CategoryId.HasValue && x.Category != null ? x.Category.Name : null,
                RequestedAssetTag = x.RequestedAssetTag,
                Status = x.Status,
                CreatedAt = x.CreatedAt
            });
        }

        private static string NormalizeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private string ResolveFulfilledAssetTag(AssetRequest entity)
        {
            if (entity == null || !entity.FulfilledAssetId.HasValue)
            {
                return null;
            }

            if (entity.FulfilledAsset != null && !string.IsNullOrWhiteSpace(entity.FulfilledAsset.AssetTag))
            {
                return entity.FulfilledAsset.AssetTag;
            }

            var asset = _unitOfWork.Repository<Asset>().GetById(entity.FulfilledAssetId.Value);
            return asset == null ? null : asset.AssetTag;
        }

        private string ResolveRequestedAssetName(AssetRequest entity)
        {
            if (entity == null || !entity.RequestedAssetId.HasValue)
            {
                return null;
            }

            if (entity.RequestedAsset != null && !string.IsNullOrWhiteSpace(entity.RequestedAsset.AssetName))
            {
                return entity.RequestedAsset.AssetName;
            }

            var asset = _unitOfWork.Repository<Asset>().GetById(entity.RequestedAssetId.Value);
            return asset == null ? null : asset.AssetName;
        }
    }
}
