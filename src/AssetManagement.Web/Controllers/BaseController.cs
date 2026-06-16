using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Application;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Infrastructure.Repositories;
using AssetManagement.Infrastructure.Services;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Web.ViewModels;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;

namespace AssetManagement.Web.Controllers
{
    [TenantAuthorize]
    public abstract class BaseController : Controller
    {
        protected readonly IUnitOfWork UnitOfWork;
        protected readonly ICurrentUserContext CurrentUserContext;
        protected readonly IAuditWriter AuditWriter;

        protected BaseController()
        {
            UnitOfWork = DependencyResolver.Current.GetService<IUnitOfWork>();
            CurrentUserContext = DependencyResolver.Current.GetService<ICurrentUserContext>();
            AuditWriter = DependencyResolver.Current.GetService<IAuditWriter>();
        }

        protected string GetDefaultCurrencyCode()
        {
            var orgId = ResolveCurrentOrganizationId();
            if (!orgId.HasValue)
            {
                return ApprovalWorkflowSettingsHelper.GetDefaultCurrencyCode(UnitOfWork.Repository<SystemSetting>().GetAll());
            }

            var settings = BuildReferenceDataCache().GetSettings(orgId.Value);
            string currency;
            if (!settings.TryGetValue(FinanceDefaults.DefaultCurrencySettingKey, out currency) || string.IsNullOrWhiteSpace(currency))
            {
                return FinanceDefaults.DefaultCurrencyCode;
            }

            return currency.Trim().ToUpperInvariant();
        }

        protected IAssetService BuildAssetService() => DependencyResolver.Current.GetService<IAssetService>();

        protected IAssetRequestService BuildAssetRequestService() => DependencyResolver.Current.GetService<IAssetRequestService>();

        protected IRoleService BuildRoleService() => DependencyResolver.Current.GetService<IRoleService>();

        protected IPermissionService BuildPermissionService() => DependencyResolver.Current.GetService<IPermissionService>();

        protected IDepartmentService BuildDepartmentService() => DependencyResolver.Current.GetService<IDepartmentService>();

        protected ISupplierService BuildSupplierService() => DependencyResolver.Current.GetService<ISupplierService>();

        protected IAssignmentService BuildAssignmentService() => DependencyResolver.Current.GetService<IAssignmentService>();

        protected ITransferService BuildTransferService() => DependencyResolver.Current.GetService<ITransferService>();

        protected IReturnService BuildReturnService() => DependencyResolver.Current.GetService<IReturnService>();

        protected IMaintenanceService BuildMaintenanceService() => DependencyResolver.Current.GetService<IMaintenanceService>();

        protected IIncidentService BuildIncidentService() => DependencyResolver.Current.GetService<IIncidentService>();

        protected IClaimService BuildClaimService() => DependencyResolver.Current.GetService<IClaimService>();

        protected IPurchaseService BuildPurchaseService() => DependencyResolver.Current.GetService<IPurchaseService>();

        protected IReceivingService BuildReceivingService() => DependencyResolver.Current.GetService<IReceivingService>();

        protected IPurchaseRequestService BuildPurchaseRequestService() => DependencyResolver.Current.GetService<IPurchaseRequestService>();

        protected IAuditLogService BuildAuditLogService() => DependencyResolver.Current.GetService<IAuditLogService>();

        protected IReportService BuildReportService() => DependencyResolver.Current.GetService<IReportService>();

        protected IMetricsService BuildMetricsService() => DependencyResolver.Current.GetService<IMetricsService>();

        protected IPendingApprovalQueryService BuildPendingApprovalQueryService() => DependencyResolver.Current.GetService<IPendingApprovalQueryService>();

        protected INotificationService BuildNotificationService() => DependencyResolver.Current.GetService<INotificationService>();

        protected IUserService BuildUserService() => DependencyResolver.Current.GetService<IUserService>();

        protected IAuthorizationService BuildAuthorizationService() => DependencyResolver.Current.GetService<IAuthorizationService>();

        protected IApprovalWorkflowService BuildApprovalWorkflowService() => DependencyResolver.Current.GetService<IApprovalWorkflowService>();

        protected IAssetDocumentService BuildAssetDocumentService() => DependencyResolver.Current.GetService<IAssetDocumentService>();

        protected IReferenceDataCache BuildReferenceDataCache() => DependencyResolver.Current.GetService<IReferenceDataCache>();

        protected IEnumerable<UserVm> GetActiveUsers()
        {
            var orgId = ResolveCurrentOrganizationId();
            if (!orgId.HasValue)
            {
                return BuildUserService().GetAll()
                    .Where(x => x.IsActive)
                    .OrderBy(x => BuildUserLabel(x))
                    .ToList();
            }

            return BuildReferenceDataCache()
                .GetUsersForDropdown(orgId.Value)
                .OrderBy(x => BuildUserLabel(x))
                .ToList();
        }

        protected IEnumerable<UserVm> GetActiveUsersForDepartment(int? departmentId)
        {
            if (!departmentId.HasValue)
            {
                return GetActiveUsers();
            }

            var orgId = ResolveCurrentOrganizationId();
            if (orgId.HasValue)
            {
                return BuildReferenceDataCache()
                    .GetUsersForDropdown(orgId.Value, departmentId)
                    .OrderBy(x => BuildUserLabel(x))
                    .ToList();
            }

            return GetActiveUsers().Where(x => x.DepartmentId == departmentId.Value).ToList();
        }

        protected UserVm GetCurrentUserProfile()
        {
            var userId = User.GetUserId();
            return string.IsNullOrWhiteSpace(userId) ? null : BuildUserService().GetById(userId);
        }

        protected int? GetCurrentUserDepartmentId()
        {
            return GetCurrentUserProfile()?.DepartmentId;
        }

        protected SelectList BuildActiveUserSelectList(string selectedUserId = null, int? departmentId = null)
        {
            var users = (departmentId.HasValue ? GetActiveUsersForDepartment(departmentId) : GetActiveUsers())
                .Select(x => new
                {
                    x.Id,
                    Name = BuildUserLabel(x)
                })
                .ToList();
            return new SelectList(users, "Id", "Name", selectedUserId);
        }

        protected void SetWorkflowFormConfig(WorkflowFormConfigVm config)
        {
            ViewBag.WorkflowFormConfig = config;
        }

        protected WorkflowFormConfigVm BuildWorkflowFormConfig(
            IEnumerable<UserVm> users,
            IEnumerable<WorkflowDepartmentUserPairVm> pairs,
            IEnumerable<WorkflowLockedFieldVm> lockedFields = null)
        {
            return new WorkflowFormConfigVm
            {
                UsersByDepartmentJson = DepartmentUserWorkflowHelper.BuildUsersByDepartmentJson(users),
                DepartmentUserPairs = pairs == null ? new List<WorkflowDepartmentUserPairVm>() : pairs.ToList(),
                LockedFields = lockedFields == null ? new List<WorkflowLockedFieldVm>() : lockedFields.ToList()
            };
        }

        protected bool ValidateUserBelongsToDepartment(string userId, int? departmentId)
        {
            return DepartmentUserWorkflowHelper.UserBelongsToDepartment(userId, departmentId, GetActiveUsers());
        }

        protected void ApplyLockedUserDepartment(int? lockedDepartmentId, Action<int?> applyDepartment)
        {
            if (!lockedDepartmentId.HasValue || applyDepartment == null || IsCurrentUserSuperAdmin())
            {
                return;
            }

            applyDepartment(lockedDepartmentId);
        }

        protected bool EnsureAssetInCurrentUserDepartment(Asset asset, out string errorMessage)
        {
            errorMessage = null;
            if (asset == null || IsCurrentUserSuperAdmin())
            {
                return true;
            }

            var userDepartmentId = GetCurrentUserDepartmentId();
            if (!userDepartmentId.HasValue)
            {
                return true;
            }

            if (asset.DepartmentId != userDepartmentId.Value)
            {
                errorMessage = "This asset belongs to another department. Only administrators can act on it.";
                return false;
            }

            return true;
        }

        protected SelectList BuildDepartmentSelectList(int? selectedDepartmentId = null, bool activeOnly = true)
        {
            var orgId = ResolveCurrentOrganizationId();
            if (!orgId.HasValue)
            {
                var departments = BuildDepartmentService().GetAll();
                if (activeOnly)
                {
                    departments = departments.Where(x => x.IsActive);
                }

                return new SelectList(departments.OrderBy(x => x.Name).ToList(), "Id", "Name", selectedDepartmentId);
            }

            var cachedDepartments = BuildReferenceDataCache().GetDepartments(orgId.Value, activeOnly);
            return new SelectList(cachedDepartments.OrderBy(x => x.Name).ToList(), "Id", "Name", selectedDepartmentId);
        }

        protected SelectList BuildRoleSelectList(int? selectedRoleId = null)
        {
            var orgId = ResolveCurrentOrganizationId();
            if (!orgId.HasValue)
            {
                return new SelectList(BuildRoleService().GetRoles().OrderBy(x => x.Name).ToList(), "Id", "Name", selectedRoleId);
            }

            return new SelectList(BuildReferenceDataCache().GetRoles(orgId.Value).OrderBy(x => x.Name).ToList(), "Id", "Name", selectedRoleId);
        }

        protected IList<SelectListItem> BuildRoleOptionList()
        {
            var orgId = ResolveCurrentOrganizationId();
            var roles = orgId.HasValue
                ? BuildReferenceDataCache().GetRoles(orgId.Value)
                : BuildRoleService().GetRoles();

            return roles
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = x.Name
                })
                .ToList();
        }

        protected SelectList BuildSupplierSelectList(int? selectedSupplierId = null, bool activeOnly = true)
        {
            var suppliers = BuildSupplierService().GetAll();
            if (activeOnly)
            {
                suppliers = suppliers.Where(x => x.IsActive);
            }

            return new SelectList(suppliers.OrderBy(x => x.SupplierName).ToList(), "Id", "SupplierName", selectedSupplierId);
        }

        protected SelectList BuildAssetConditionSelectList(string selectedCondition = null)
        {
            var items = Enum.GetNames(typeof(AssetCondition))
                .Select(name => new SelectListItem
                {
                    Value = name,
                    Text = FormatEnumLabel(name)
                })
                .ToList();

            return new SelectList(items, "Value", "Text", selectedCondition);
        }

        protected static string FormatEnumLabel(string enumName)
        {
            if (string.IsNullOrWhiteSpace(enumName))
            {
                return enumName;
            }

            return Regex.Replace(enumName, "([a-z])([A-Z])", "$1 $2");
        }

        protected static string BuildUserLabel(UserVm user)
        {
            return DepartmentUserWorkflowHelper.BuildUserLabel(user);
        }

        protected string ResolveReturnUrl(string returnUrl, string fallbackAction, string fallbackController = null, object routeValues = null)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return returnUrl;
            }

            return string.IsNullOrWhiteSpace(fallbackController)
                ? Url.Action(fallbackAction, routeValues)
                : Url.Action(fallbackAction, fallbackController, routeValues);
        }

        protected ActionResult RedirectToReturnUrl(string returnUrl, string fallbackAction, string fallbackController = null, object routeValues = null)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return string.IsNullOrWhiteSpace(fallbackController)
                ? RedirectToAction(fallbackAction, routeValues)
                : RedirectToAction(fallbackAction, fallbackController, routeValues);
        }

        protected ListPageViewModel<T> BuildListPage<T>(IEnumerable<T> source, string search, string sort, string direction, int page, int pageSize)
        {
            var safePageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100);
            var totalCount = source.Count();
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / safePageSize));
            var safePage = Math.Min(Math.Max(page, 1), totalPages);

            return new ListPageViewModel<T>
            {
                Items = source.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToList(),
                Search = search,
                Sort = sort,
                Direction = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc",
                Page = safePage,
                PageSize = safePageSize,
                TotalCount = totalCount
            };
        }

        protected ListPageViewModel<AssetListVm> ToAssetListPage(AssetListPageVm source)
        {
            return new ListPageViewModel<AssetListVm>
            {
                Items = source.Items.ToList(),
                Search = source.Search,
                Sort = source.Sort,
                Direction = source.Direction,
                Page = source.Page,
                PageSize = source.PageSize,
                TotalCount = source.TotalCount
            };
        }

        protected AssetWorkflowContextViewModel BuildAssetWorkflowContext(int assetId)
        {
            var asset = UnitOfWork.Repository<Asset>().GetById(assetId);
            if (asset == null)
            {
                return null;
            }

            var departmentName = asset.DepartmentId > 0
                ? UnitOfWork.Repository<Department>().GetById(asset.DepartmentId)?.Name
                : null;
            var userQuery = DependencyResolver.Current.GetService<IUserAccountQueryRepository>();
            var orgId = ResolveCurrentOrganizationId();
            var custodian = !string.IsNullOrWhiteSpace(asset.CurrentCustodianId) && userQuery != null && orgId.HasValue
                ? userQuery.GetDisplayById(asset.CurrentCustodianId, orgId.Value)
                : null;
            var custodianName = custodian == null ? null : custodian.DisplayName;

            return new AssetWorkflowContextViewModel
            {
                AssetId = asset.Id,
                AssetName = asset.AssetName,
                AssetTag = asset.AssetTag,
                Status = asset.CurrentStatus.ToString(),
                DepartmentName = departmentName,
                CustodianName = string.IsNullOrWhiteSpace(custodianName) ? custodian?.Email : custodianName
            };
        }

        protected int? GetCurrentUserRoleId()
        {
            var userId = User.GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            var userQuery = DependencyResolver.Current.GetService<IUserAccountQueryRepository>();
            return userQuery == null ? null : userQuery.GetRoleIdByUserId(userId);
        }

        protected bool IsCurrentUserCompanyAdmin()
        {
            var orgScope = DependencyResolver.Current.GetService<AssetManagement.Application.Contracts.Security.IOrganizationScopeService>();
            if (orgScope != null && orgScope.IsCompanyAdmin())
            {
                return true;
            }

            var roleId = GetCurrentUserRoleId();
            if (!roleId.HasValue)
            {
                return false;
            }

            var role = UnitOfWork.Repository<Role>().GetById(roleId.Value);
            return role != null && role.IsSystemRole && string.Equals(role.Name, "Company Admin", StringComparison.OrdinalIgnoreCase);
        }

        protected bool IsActualPlatformAdmin()
        {
            var orgScope = DependencyResolver.Current.GetService<AssetManagement.Application.Contracts.Security.IOrganizationScopeService>();
            return orgScope != null && orgScope.IsActualPlatformAdmin();
        }

        protected bool IsCurrentUserSuperAdmin()
        {
            return IsCurrentUserCompanyAdmin();
        }

        protected int? ResolveCurrentOrganizationId()
        {
            var orgScope = DependencyResolver.Current.GetService<AssetManagement.Application.Contracts.Security.IOrganizationScopeService>();
            return orgScope == null ? null : orgScope.GetCurrentOrganizationId();
        }

        protected ApprovalProcessConfiguration GetApprovalProcessConfiguration(string processCode)
        {
            return BuildApprovalWorkflowService().GetProcessConfiguration(processCode);
        }

        protected static IEnumerable<T> FilterBySearch<T>(IEnumerable<T> items, string search, Func<T, string, bool> matches)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return items;
            }

            var term = search.Trim().ToLowerInvariant();
            return items.Where(x => matches(x, term));
        }

        protected void SetListSortViewBag(string sort, string direction)
        {
            ViewBag.Sort = sort;
            ViewBag.Direction = direction;
        }

        protected ActionResult RedirectToAssetDetails(int assetId)
        {
            return RedirectToTenantAware("Assets", "Details", new { id = assetId });
        }

        protected ActionResult RedirectToAssetsIndex()
        {
            return RedirectToTenantAware("Assets", "Index");
        }

        protected ActionResult RedirectToTenantAware(string controller, string action, object routeValues = null)
        {
            var tenant = TenantUrlHelper.GetTenantToken(Request.RequestContext.RouteData);
            object id = null;
            if (routeValues != null)
            {
                var extra = new System.Web.Routing.RouteValueDictionary(routeValues);
                if (extra.ContainsKey("id"))
                {
                    id = extra["id"];
                }
            }

            if (TenantUrlHelper.IsValidTenantSlug(tenant))
            {
                return TenantUrlHelper.CreateTenantRedirect(tenant, controller, action, id);
            }

            return RedirectToAction(action, controller, routeValues);
        }

        protected string BuildApprovalProcessSummary(string processCode)
        {
            var config = GetApprovalProcessConfiguration(processCode);
            var roleLookup = BuildRoleNameLookup();
            return !config.UsesApproval
                ? "This process completes immediately without a separate approval step."
                : ApprovalWorkflowSettingsHelper.BuildStageSummary(config.StageRoleIds, roleLookup);
        }

        protected string BuildAssetApprovalProcessSummary(Asset asset, string processCode)
        {
            var config = ApprovalWorkflowHelper.GetAssetProcessConfiguration(UnitOfWork, asset, processCode);
            var roleLookup = BuildRoleNameLookup(asset == null ? null : asset.OrganizationId);
            if (!config.UsesApproval)
            {
                return "This process completes immediately without a separate approval step.";
            }

            var orgId = asset == null ? ResolveCurrentOrganizationId() : asset.OrganizationId;
            var users = orgId.HasValue
                ? BuildReferenceDataCache().GetUsersForDropdown(orgId.Value)
                : GetActiveUsers();
            var userLookup = ApproverPickerHelper.BuildUserNameLookup(users);
            return ApprovalWorkflowSettingsHelper.BuildAssetStageSummary(
                config.StageRoleIds.Select(x => (int?)x),
                config.StageUserIds,
                roleLookup,
                userLookup);
        }

        protected void PopulateAssetApproverPickerOptions(int? organizationId = null)
        {
            ViewBag.UseApproverUserPicker = true;
            var orgId = organizationId ?? ResolveCurrentOrganizationId();
            var users = orgId.HasValue
                ? BuildReferenceDataCache().GetUsersForDropdown(orgId.Value)
                : GetActiveUsers();
            ViewBag.ApproverRoleUsersJson = ApproverPickerHelper.BuildRoleUsersJson(GetRolesForOrganization(orgId), users);
        }

        protected IEnumerable<RoleVm> GetRolesForOrganization(int? organizationId = null)
        {
            var orgId = organizationId ?? ResolveCurrentOrganizationId();
            return orgId.HasValue
                ? BuildReferenceDataCache().GetRoles(orgId.Value)
                : BuildRoleService().GetRoles();
        }

        protected IDictionary<int, string> BuildRoleNameLookup(int? organizationId = null)
        {
            var lookup = new Dictionary<int, string>();
            foreach (var role in GetRolesForOrganization(organizationId))
            {
                if (role == null || role.Id <= 0 || lookup.ContainsKey(role.Id))
                {
                    continue;
                }

                lookup[role.Id] = role.Name;
            }

            return lookup;
        }

        protected void EnrichAssetListCustodianNames(IList<AssetListVm> items)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            var userIds = items.Where(x => !string.IsNullOrWhiteSpace(x.CurrentCustodianId))
                .Select(x => x.CurrentCustodianId)
                .Distinct()
                .ToList();
            if (userIds.Count == 0)
            {
                return;
            }

            var orgId = ResolveCurrentOrganizationId();
            Dictionary<string, string> users;
            if (orgId.HasValue)
            {
                users = BuildReferenceDataCache()
                    .GetUsersByIds(orgId.Value, userIds)
                    .ToDictionary(x => x.Id, BuildUserLabel);
            }
            else
            {
                users = BuildUserService().GetAll()
                    .Where(x => userIds.Contains(x.Id))
                    .ToDictionary(x => x.Id, BuildUserLabel);
            }
            foreach (var item in items)
            {
                if (!string.IsNullOrWhiteSpace(item.CurrentCustodianId) && users.ContainsKey(item.CurrentCustodianId))
                {
                    item.CurrentCustodianName = users[item.CurrentCustodianId];
                }
            }
        }

        protected void EnrichAssetDetails(AssetDetailsVm model)
        {
            if (model == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(model.CurrentCustodianId))
            {
                var custodian = BuildUserService().GetById(model.CurrentCustodianId);
                model.CurrentCustodianName = custodian == null ? model.CurrentCustodianId : BuildUserLabel(custodian);
            }

            if (model.PendingDisposal != null && !string.IsNullOrWhiteSpace(model.PendingDisposal.RequestedByName))
            {
                var requester = BuildUserService().GetById(model.PendingDisposal.RequestedByName);
                if (requester != null)
                {
                    model.PendingDisposal.RequestedByName = BuildUserLabel(requester);
                }
            }
        }
    }
}
