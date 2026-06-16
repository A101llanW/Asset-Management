using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Services
{
    public class SecurityLogService : ISecurityLogService
    {
        private readonly ISecurityLogQueryRepository _repository;
        private readonly IOrganizationScopeService _organizationScope;

        public SecurityLogService(
            ISecurityLogQueryRepository repository,
            IOrganizationScopeService organizationScope)
        {
            _repository = repository;
            _organizationScope = organizationScope;
        }

        public SecurityLogsPageVm GetLogs(SecurityLogFilterVm filter, bool crossTenant)
        {
            var normalized = filter ?? new SecurityLogFilterVm();
            int? organizationId = null;
            if (!crossTenant)
            {
                organizationId = _organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId();
            }

            return new SecurityLogsPageVm
            {
                Filter = normalized,
                LoginAttempts = _repository.GetLoginAttempts(normalized, organizationId, 1000),
                SecurityEvents = _repository.GetSecurityEvents(normalized, organizationId, 1000),
                TotalLoginAttempts = _repository.CountLoginAttempts(normalized, organizationId),
                SuccessfulLogins = _repository.CountSuccessfulLogins(normalized, organizationId),
                FailedLoginAttempts = _repository.CountFailedLogins(normalized, organizationId),
                TotalSecurityEvents = _repository.CountSecurityEvents(normalized, organizationId)
            };
        }
    }
}
