using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IAuditLogService
    {
        IEnumerable<AuditLogVm> GetLogs(AuditLogFilterVm filter);

        /// <summary>Audit Logs page: one page of rows (audit + sign-ins), newest first, with the total count.</summary>
        AuditLogPageVm GetLogPage(AuditLogFilterVm filter);

        byte[] ExportCsv(AuditLogFilterVm filter);
    }
}
