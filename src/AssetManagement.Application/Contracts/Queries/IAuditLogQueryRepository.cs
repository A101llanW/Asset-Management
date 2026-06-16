using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts.Queries
{
    public interface IAuditLogQueryRepository
    {
        IList<AuditLogVm> GetLogs(AuditLogFilterVm filter, int organizationId, int? departmentId, bool bypassDepartmentScope, bool denyDepartmentScope, string actorUserId);
    }
}
