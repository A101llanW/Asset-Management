using System;
using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts.Queries
{
    public interface ISecurityLogQueryRepository
    {
        IList<LoginAttemptLogVm> GetLoginAttempts(SecurityLogFilterVm filter, int? organizationId, int take);

        int CountLoginAttempts(SecurityLogFilterVm filter, int? organizationId);

        int CountSuccessfulLogins(SecurityLogFilterVm filter, int? organizationId);

        int CountFailedLogins(SecurityLogFilterVm filter, int? organizationId);

        IList<SecurityEventLogVm> GetSecurityEvents(SecurityLogFilterVm filter, int? organizationId, int take);

        int CountSecurityEvents(SecurityLogFilterVm filter, int? organizationId);
    }
}
