using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts.Queries
{
    public interface IAuditLogQueryRepository
    {
        IList<AuditLogVm> GetLogs(AuditLogFilterVm filter, int organizationId, int? departmentId, bool bypassDepartmentScope, bool denyDepartmentScope, string actorUserId);

        /// <summary>
        /// Server-side paged org list: AuditLog rows (page views hidden unless IncludePageViews) merged with
        /// LoginAttempts sign-in rows (unless HideLogins), newest first. take = rows to return after skip.
        /// </summary>
        AuditLogPageVm GetLogPage(AuditLogFilterVm filter, int organizationId, int? departmentId, bool bypassDepartmentScope, bool denyDepartmentScope, string actorUserId, int skip, int take);
    }
}
