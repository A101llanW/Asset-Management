using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IAuditLogService
    {
        IEnumerable<AuditLogVm> GetLogs(AuditLogFilterVm filter);
    }
}
