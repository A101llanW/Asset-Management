using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Services
{
    public class PendingApprovalQueryService : IPendingApprovalQueryService
    {
        private readonly IPendingApprovalQueryRepository _queryRepository;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IDepartmentScopeService _departmentScope;
        private readonly IReferenceDataCache _referenceDataCache;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuthorizationService _authorizationService;
        private readonly ICurrentUserContext _currentUser;
        private readonly IUserService _userService;
        private StageApproverScopeResolver _scopeResolver;

        public PendingApprovalQueryService(
            IPendingApprovalQueryRepository queryRepository,
            IOrganizationScopeService organizationScope,
            IDepartmentScopeService departmentScope,
            IReferenceDataCache referenceDataCache,
            IUnitOfWork unitOfWork,
            IAuthorizationService authorizationService = null,
            ICurrentUserContext currentUser = null,
            IUserService userService = null)
        {
            _userService = userService;
            _queryRepository = queryRepository;
            _organizationScope = organizationScope;
            _departmentScope = departmentScope;
            _referenceDataCache = referenceDataCache;
            _unitOfWork = unitOfWork;
            _authorizationService = authorizationService;
            _currentUser = currentUser;
        }

        public int CountGlobalPending()
        {
            var scope = ResolveScope();
            return _queryRepository.CountGlobalPending(
                scope.OrganizationId,
                scope.DepartmentId,
                scope.BypassesDepartmentScope,
                scope.DenyDepartmentScope,
                scope.BypassPurchaseDepartmentScope,
                scope.BypassAssetRequestDepartmentScope);
        }

        public PendingApprovalInboxResultVm BuildInbox(PendingApprovalUserContextVm context)
        {
            context = context ?? new PendingApprovalUserContextVm();
            var scope = ResolveScope();
            var roles = _referenceDataCache.GetRoles(scope.OrganizationId).ToDictionary(x => x.Id, x => x.Name);
            var sources = _queryRepository.GetPendingSources(
                scope.OrganizationId,
                scope.DepartmentId,
                scope.BypassesDepartmentScope,
                scope.DenyDepartmentScope,
                scope.BypassPurchaseDepartmentScope,
                scope.BypassAssetRequestDepartmentScope);
            var items = new List<PendingApprovalQueryItemVm>();
            _scopeResolver = _userService == null ? null : StageApproverScopeResolver.Create(_unitOfWork, _userService);

            // rel1009e: a department-scoped user can be the resolved approver of another unit's request (nearest-
            // ancestor fallback, e.g. the parent department's head for a room). Add those workflow items too, but
            // only the ones this user can act on per the shared resolver.
            if (_scopeResolver != null && !scope.BypassesDepartmentScope && !string.IsNullOrWhiteSpace(context.UserId))
            {
                var seen = new HashSet<string>(sources.Select(x => x.ProcessName + "#" + x.RequestId), StringComparer.OrdinalIgnoreCase);
                var orgWide = _queryRepository.GetPendingSources(scope.OrganizationId, null, true, false, true, false);
                var extra = new List<PendingApprovalSourceRow>();
                foreach (var row in orgWide)
                {
                    if (row == null || row.IsAssetRequest || !seen.Add(row.ProcessName + "#" + row.RequestId))
                    {
                        continue;
                    }

                    var rowRoleId = ApprovalWorkflowSettingsHelper.TryGetCurrentStageRoleId(row.ApprovalStageRoleIds, row.CurrentApprovalStage);
                    var rowUserId = ApprovalWorkflowSettingsHelper.TryGetCurrentStageUserId(row.ApprovalStageUserIds, row.CurrentApprovalStage);
                    if (ApprovalWorkflowHelper.CanUserActOnStage(
                            _unitOfWork,
                            row.RequestedById,
                            context.UserId,
                            false,
                            context.RoleId,
                            rowRoleId,
                            rowUserId,
                            _scopeResolver.Resolve(rowRoleId, rowUserId, row.DepartmentId, Helpers.CatalogRequisitionFlowResolver.IsCatalogSource(row.ApprovalFlowSource))))
                    {
                        extra.Add(row);
                    }
                }

                if (extra.Count > 0)
                {
                    sources = sources.Concat(extra).ToList();
                }
            }

            foreach (var source in sources)
            {
                if (source.IsAssetRequest)
                {
                    if (!context.CanApproveAssetRequests)
                    {
                        continue;
                    }

                    TryAddAssetRequestItem(items, context, source);
                    continue;
                }

                TryAddWorkflowItem(items, context, roles, source);
            }

            var ordered = items.OrderByDescending(x => x.RequestedDateUtc).ToList();
            return new PendingApprovalInboxResultVm
            {
                Items = ordered,
                TotalCount = ordered.Count,
                ActionableCount = ordered.Count(x => x.CanCurrentUserAct),
                RequestedByMeCount = ordered.Count(x => x.RequestedByCurrentUser)
            };
        }

        private QueryScope ResolveScope()
        {
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                throw new InvalidOperationException("Organization context is required for pending approval queries.");
            }

            int? departmentId = null;
            var bypassesDepartmentScope = _departmentScope.BypassesDepartmentScope;
            var denyDepartmentScope = false;
            if (!bypassesDepartmentScope)
            {
                departmentId = _departmentScope.ScopedDepartmentId;
                denyDepartmentScope = !departmentId.HasValue;
            }

            return new QueryScope
            {
                OrganizationId = organizationId.Value,
                DepartmentId = departmentId,
                BypassesDepartmentScope = bypassesDepartmentScope,
                DenyDepartmentScope = denyDepartmentScope,
                BypassPurchaseDepartmentScope = CanApprovePurchases(),
                BypassAssetRequestDepartmentScope = CanApproveAssetRequests()
            };
        }

        private bool CanApprovePurchases()
        {
            var userId = _currentUser == null ? null : _currentUser.UserId;
            return !string.IsNullOrWhiteSpace(userId)
                && _authorizationService != null
                && _authorizationService.HasPermission(userId, "Purchases.Approve");
        }

        private bool CanApproveAssetRequests()
        {
            var userId = _currentUser == null ? null : _currentUser.UserId;
            return !string.IsNullOrWhiteSpace(userId)
                && _authorizationService != null
                && _authorizationService.HasPermission(userId, "Assets.Request.Approve");
        }

        private void TryAddWorkflowItem(
            ICollection<PendingApprovalQueryItemVm> items,
            PendingApprovalUserContextVm context,
            IDictionary<int, string> roles,
            PendingApprovalSourceRow source)
        {
            var stageRoleId = ApprovalWorkflowSettingsHelper.TryGetCurrentStageRoleId(source.ApprovalStageRoleIds, source.CurrentApprovalStage);
            var stageUserId = ApprovalWorkflowSettingsHelper.TryGetCurrentStageUserId(source.ApprovalStageUserIds, source.CurrentApprovalStage);
            var isMine = IsRequestedByCurrentUser(source.RequestedById, context.UserId);
            var canAct = ApprovalWorkflowHelper.CanUserActOnStage(
                _unitOfWork,
                source.RequestedById,
                context.UserId,
                context.IsSuperAdmin,
                context.RoleId,
                stageRoleId,
                stageUserId,
                _scopeResolver == null ? null : _scopeResolver.Resolve(stageRoleId, stageUserId, source.DepartmentId, Helpers.CatalogRequisitionFlowResolver.IsCatalogSource(source.ApprovalFlowSource)));
            if (!ApprovalWorkflowHelper.ShouldIncludePendingItem(context.IsSuperAdmin, canAct, isMine))
            {
                return;
            }

            items.Add(new PendingApprovalQueryItemVm
            {
                ProcessName = source.ProcessName,
                RequestId = source.RequestId,
                AssetId = source.AssetId,
                AssetTag = source.AssetTag ?? source.DisplayTag,
                AssetName = source.AssetName ?? source.DisplayName,
                RequestedById = source.RequestedById,
                RequestedByName = source.RequestedByName,
                RequestedDateUtc = source.RequestedDateUtc,
                StageNumber = source.CurrentApprovalStage <= 0 ? 1 : source.CurrentApprovalStage,
                StageRoleName = ApprovalWorkflowSettingsHelper.ResolveRoleName(roles, stageRoleId, "No stage role configured"),
                CanCurrentUserAct = canAct,
                RequestedByCurrentUser = isMine,
                Summary = source.Summary,
                AgeDays = ResolveAgeDays(source.RequestedDateUtc),
                AgingBand = ResolveAgingBand(source.RequestedDateUtc)
            });
        }

        private void TryAddAssetRequestItem(
            ICollection<PendingApprovalQueryItemVm> items,
            PendingApprovalUserContextVm context,
            PendingApprovalSourceRow source)
        {
            var isMine = IsRequestedByCurrentUser(source.RequestedById, context.UserId);
            if (!context.IsSuperAdmin && isMine)
            {
                return;
            }

            items.Add(new PendingApprovalQueryItemVm
            {
                ProcessName = source.ProcessName,
                RequestId = source.RequestId,
                AssetId = source.AssetId,
                AssetTag = source.AssetTag,
                AssetName = source.AssetName,
                RequestedById = source.RequestedById,
                RequestedByName = source.RequestedByName,
                RequestedDateUtc = source.RequestedDateUtc,
                StageNumber = 1,
                StageRoleName = "Asset request reviewer",
                CanCurrentUserAct = !isMine,
                RequestedByCurrentUser = isMine,
                Summary = source.Summary,
                AgeDays = ResolveAgeDays(source.RequestedDateUtc),
                AgingBand = ResolveAgingBand(source.RequestedDateUtc)
            });
        }

        private static bool IsRequestedByCurrentUser(string requestedById, string currentUserId)
        {
            return !string.IsNullOrWhiteSpace(requestedById)
                && !string.IsNullOrWhiteSpace(currentUserId)
                && string.Equals(requestedById.Trim(), currentUserId.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static int ResolveAgeDays(DateTime requestedDateUtc)
        {
            return Math.Max(0, (int)(DateTime.UtcNow - requestedDateUtc).TotalDays);
        }

        private static string ResolveAgingBand(DateTime requestedDateUtc)
        {
            var ageDays = ResolveAgeDays(requestedDateUtc);
            if (ageDays >= 14)
            {
                return "Critical (14+ days)";
            }

            if (ageDays >= 7)
            {
                return "Warning (7-13 days)";
            }

            return "Current (0-6 days)";
        }

        private sealed class QueryScope
        {
            public int OrganizationId { get; set; }

            public int? DepartmentId { get; set; }

            public bool BypassesDepartmentScope { get; set; }

            public bool DenyDepartmentScope { get; set; }

            public bool BypassPurchaseDepartmentScope { get; set; }

            public bool BypassAssetRequestDepartmentScope { get; set; }
        }
    }
}
