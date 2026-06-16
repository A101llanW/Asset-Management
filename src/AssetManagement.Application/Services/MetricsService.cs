using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public class MetricsService : IMetricsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDepartmentScopeService _departmentScope;
        private readonly IAssetService _assetService;
        private readonly IPendingApprovalQueryService _pendingApprovalQuery;
        private readonly ICurrentUserContext _currentUser;
        private readonly IUserService _userService;
        private readonly IAuthorizationService _authorizationService;

        public MetricsService(
            IUnitOfWork unitOfWork,
            IDepartmentScopeService departmentScope,
            IAssetService assetService,
            IPendingApprovalQueryService pendingApprovalQuery,
            ICurrentUserContext currentUser,
            IUserService userService,
            IAuthorizationService authorizationService)
        {
            _unitOfWork = unitOfWork;
            _departmentScope = departmentScope;
            _assetService = assetService;
            _pendingApprovalQuery = pendingApprovalQuery;
            _currentUser = currentUser;
            _userService = userService;
            _authorizationService = authorizationService;
        }

        public int CountDepartments(bool activeOnly = true)
        {
            return _departmentScope.CountVisibleDepartments(activeOnly);
        }

        public int CountAssets(AssetFilterVm filter)
        {
            return _assetService.CountAssets(filter);
        }

        public int CountPendingApprovals(PendingApprovalCountMode mode)
        {
            if (mode == PendingApprovalCountMode.GlobalPending)
            {
                return _pendingApprovalQuery.CountGlobalPending();
            }

            var inbox = _pendingApprovalQuery.BuildInbox(BuildCurrentUserContext());
            if (mode == PendingApprovalCountMode.UserActionable)
            {
                return inbox.ActionableCount;
            }

            return inbox.TotalCount;
        }

        public int CountExpiringWarranties(int days)
        {
            var threshold = DateTime.UtcNow.AddDays(days);
            return _departmentScope.ApplyAssetScope(_unitOfWork.Repository<Asset>().Query())
                .Count(x => x.IsActive
                    && x.WarrantyEndDate.HasValue
                    && x.WarrantyEndDate.Value <= threshold);
        }

        public int CountExpiringInsurance(int days)
        {
            var threshold = DateTime.UtcNow.AddDays(days);
            var scopedAssetIds = _departmentScope.ApplyAssetScope(_unitOfWork.Repository<Asset>().Query())
                .Where(x => x.IsActive)
                .Select(x => x.Id)
                .ToList();

            return _unitOfWork.Repository<InsurancePolicy>().GetAll()
                .Count(x => scopedAssetIds.Contains(x.AssetId) && x.PolicyEndDate <= threshold);
        }

        public int CountCustodyMovements(DateTime from, DateTime to)
        {
            var fromDate = from.Date;
            var toDate = to.Date;
            var assetIds = _departmentScope.ApplyAssetScope(_unitOfWork.Repository<Asset>().Query())
                .Select(x => x.Id)
                .ToList();

            var transferCount = _unitOfWork.Repository<AssetTransfer>().GetAll()
                .Count(x => assetIds.Contains(x.AssetId)
                    && x.TransferDate.Date >= fromDate
                    && x.TransferDate.Date <= toDate);

            var assignmentCount = _unitOfWork.Repository<AssetAssignment>().GetAll()
                .Count(x => assetIds.Contains(x.AssetId)
                    && x.AssignedDate.Date >= fromDate
                    && x.AssignedDate.Date <= toDate);

            return transferCount + assignmentCount;
        }

        public int GetWarrantyThresholdDays()
        {
            return NotificationSettingsHelper.GetWarrantyThresholdDays(_unitOfWork.Repository<SystemSetting>().GetAll());
        }

        public int GetInsuranceThresholdDays()
        {
            return NotificationSettingsHelper.GetInsuranceThresholdDays(_unitOfWork.Repository<SystemSetting>().GetAll());
        }

        public int GetMaintenanceThresholdDays()
        {
            return NotificationSettingsHelper.GetMaintenanceThresholdDays(_unitOfWork.Repository<SystemSetting>().GetAll());
        }

        private PendingApprovalUserContextVm BuildCurrentUserContext()
        {
            var userId = _currentUser == null ? null : _currentUser.UserId;
            var profile = string.IsNullOrWhiteSpace(userId) ? null : _userService.GetById(userId);
            var roleId = profile == null ? null : profile.RoleId;
            var isSuperAdmin = IsSuperAdmin(roleId);
            var canApproveAssetRequests = !string.IsNullOrWhiteSpace(userId)
                && _authorizationService.HasPermission(userId, "Assets.Request.Approve");

            return new PendingApprovalUserContextVm
            {
                UserId = userId,
                RoleId = roleId,
                IsSuperAdmin = isSuperAdmin,
                CanApproveAssetRequests = canApproveAssetRequests
            };
        }

        private bool IsSuperAdmin(int? roleId)
        {
            if (!roleId.HasValue)
            {
                return false;
            }

            var role = _unitOfWork.Repository<Role>().GetById(roleId.Value);
            return role != null
                && role.IsSystemRole
                && string.Equals(role.Name, "Company Admin", StringComparison.OrdinalIgnoreCase);
        }
    }
}
