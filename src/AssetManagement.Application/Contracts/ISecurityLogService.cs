using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface ISecurityLogService
    {
        SecurityLogsPageVm GetLogs(SecurityLogFilterVm filter, bool crossTenant);
    }
}
