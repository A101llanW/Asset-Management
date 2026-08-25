using System;
using System.Linq;
using System.Web;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Domain.Common;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Services
{
    public class OrganizationScopeService : IOrganizationScopeService
    {
        private const string FilterOverrideKey = "OrganizationFilterOverride";
        private const string AuthenticatedOrgKey = "AuthenticatedOrganizationId";

        private readonly ICurrentUserContext _currentUser;
        private readonly UserAccountRepository _users;
        private bool _profileLoaded;
        private ApplicationUser _user;
        private string _roleName;
        private int? _instanceFilterOverride;
        private bool _instancePlatformAdminOverride;
        private bool _instanceCompanyAdminOverride;
        private TenantExecutionContext _executionContext;

        public OrganizationScopeService(ICurrentUserContext currentUser, ISqlConnectionFactory connectionFactory)
        {
            _currentUser = currentUser;
            _users = new UserAccountRepository(connectionFactory);
        }

        public void SetOrganizationFilterOverride(int? organizationId)
        {
            if (_executionContext != null)
            {
                _executionContext.OrganizationFilterOverride = organizationId;
                return;
            }

            var context = HttpContext.Current;
            if (context == null)
            {
                _instanceFilterOverride = organizationId;
                return;
            }

            if (organizationId.HasValue)
            {
                context.Items[FilterOverrideKey] = organizationId.Value;
            }
            else
            {
                context.Items.Remove(FilterOverrideKey);
            }
        }

        public void SetPlatformAdminOverride(bool isPlatformAdmin)
        {
            _instancePlatformAdminOverride = isPlatformAdmin;
        }

        public void SetCompanyAdminOverride(bool isCompanyAdmin)
        {
            _instanceCompanyAdminOverride = isCompanyAdmin;
        }

        public void SetExecutionContext(TenantExecutionContext context)
        {
            _executionContext = context ?? new TenantExecutionContext();
        }

        public void ClearExecutionContext()
        {
            _executionContext = null;
        }

        public int? GetCurrentOrganizationId()
        {
            if (_executionContext != null)
            {
                if (_executionContext.OrganizationFilterOverride.HasValue)
                {
                    return _executionContext.OrganizationFilterOverride;
                }

                return _executionContext.OrganizationId;
            }

            if (HttpContext.Current == null && _instanceFilterOverride.HasValue)
            {
                return _instanceFilterOverride;
            }

            int? overrideId;
            if (TryGetContextItemInt(FilterOverrideKey, out overrideId))
            {
                return overrideId;
            }

            int? impersonatedId;
            if (TryGetImpersonatedOrganizationId(out impersonatedId))
            {
                return impersonatedId;
            }

            if (IsActualPlatformAdmin() && !IsImpersonating())
            {
                return null;
            }

            int? authenticatedId;
            if (TryGetAuthenticatedOrganizationId(out authenticatedId))
            {
                return authenticatedId;
            }

            int? tenantContextId;
            if (TryGetContextItemInt("TenantContext", out tenantContextId))
            {
                return tenantContextId;
            }

            return null;
        }

        public int? GetTenantFilterOrganizationId(Type entityType)
        {
            if (entityType == typeof(Permission) || entityType == typeof(Organization))
            {
                return null;
            }

            if (entityType == typeof(ImpersonationRequest) || entityType == typeof(TemporaryCredential))
            {
                return null;
            }

            var orgId = GetCurrentOrganizationId();
            if (orgId.HasValue)
            {
                return orgId;
            }

            if (IsActualPlatformAdmin() && !IsImpersonating())
            {
                return null;
            }

            return -1;
        }

        public bool IsImpersonating()
        {
            if (_executionContext != null)
            {
                return _executionContext.IsImpersonating;
            }

            int? id;
            return TryGetImpersonatedOrganizationId(out id) && id.HasValue;
        }

        public bool IsPlatformAdmin()
        {
            if (_executionContext != null)
            {
                return _executionContext.IsPlatformAdmin && !_executionContext.IsImpersonating;
            }

            if (IsImpersonating())
            {
                return false;
            }

            return IsActualPlatformAdmin();
        }

        public bool IsActualPlatformAdmin()
        {
            if (_executionContext != null)
            {
                return _executionContext.IsPlatformAdmin;
            }

            if (HttpContext.Current == null && _instancePlatformAdminOverride)
            {
                return true;
            }

            EnsureProfileLoaded();
            return _user != null
                && string.Equals(_roleName, "Platform Admin", StringComparison.OrdinalIgnoreCase);
        }

        public bool IsCompanyAdmin()
        {
            if (_executionContext != null)
            {
                return _executionContext.IsCompanyAdmin || _executionContext.IsImpersonating;
            }

            if (HttpContext.Current == null && _instanceCompanyAdminOverride)
            {
                return true;
            }

            if (IsImpersonating())
            {
                return true;
            }

            EnsureProfileLoaded();
            return string.Equals(_roleName, "Company Admin", StringComparison.OrdinalIgnoreCase);
        }

        public string GetImpersonationReason()
        {
            if (_executionContext != null)
            {
                return _executionContext.ImpersonationReason;
            }

            var session = HttpContext.Current != null ? HttpContext.Current.Session : null;
            return session != null ? session["ImpersonationReason"] as string : null;
        }

        public IQueryable<T> ApplyOrganizationFilter<T>(IQueryable<T> query) where T : class
        {
            if (query == null)
            {
                return query;
            }

            var filterOrgId = GetTenantFilterOrganizationId(typeof(T));
            if (!filterOrgId.HasValue)
            {
                return query;
            }

            if (filterOrgId.Value < 0)
            {
                // Sentinel means "no tenant context": hide tenant-scoped rows, keep global entities readable.
                if (!(typeof(ITenantEntity).IsAssignableFrom(typeof(T))))
                {
                    return query;
                }

                return query.Where(x => false);
            }

            if (!(typeof(ITenantEntity).IsAssignableFrom(typeof(T))))
            {
                return query;
            }

            var orgId = filterOrgId.Value;
            return query.Where(x => ((ITenantEntity)x).OrganizationId == orgId);
        }

        private bool TryGetImpersonatedOrganizationId(out int? organizationId)
        {
            organizationId = null;
            var session = HttpContext.Current != null ? HttpContext.Current.Session : null;
            if (session == null || session["ImpersonatedOrganizationId"] == null)
            {
                return false;
            }

            organizationId = (int?)session["ImpersonatedOrganizationId"];
            return true;
        }

        private bool TryGetAuthenticatedOrganizationId(out int? organizationId)
        {
            organizationId = null;
            if (TryGetContextItemInt(AuthenticatedOrgKey, out organizationId))
            {
                return true;
            }

            EnsureProfileLoaded();
            if (_user != null && _user.OrganizationId.HasValue)
            {
                organizationId = _user.OrganizationId;
                var context = HttpContext.Current;
                if (context != null)
                {
                    context.Items[AuthenticatedOrgKey] = organizationId;
                }

                return true;
            }

            return false;
        }

        private static bool TryGetContextItemInt(string key, out int? value)
        {
            value = null;
            var context = HttpContext.Current;
            if (context == null || !context.Items.Contains(key))
            {
                return false;
            }

            value = (int?)context.Items[key];
            return true;
        }

        private void EnsureProfileLoaded()
        {
            if (_profileLoaded)
            {
                return;
            }

            _profileLoaded = true;
            var userId = _currentUser == null ? null : _currentUser.UserId;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            _user = _users.FindById(userId);
            _roleName = _users.FindRoleNameByUserId(userId);
        }
    }
}
