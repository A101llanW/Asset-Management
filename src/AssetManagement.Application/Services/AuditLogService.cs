using System.Collections.Generic;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly IAuditLogQueryRepository _auditLogQueryRepository;
        private readonly IDepartmentScopeService _departmentScope;
        private readonly ICurrentUserContext _currentUser;
        private readonly IOrganizationScopeService _organizationScope;

        public AuditLogService(
            IAuditLogQueryRepository auditLogQueryRepository,
            IDepartmentScopeService departmentScope,
            ICurrentUserContext currentUser,
            IOrganizationScopeService organizationScope)
        {
            _auditLogQueryRepository = auditLogQueryRepository;
            _departmentScope = departmentScope;
            _currentUser = currentUser;
            _organizationScope = organizationScope;
        }

        public IEnumerable<AuditLogVm> GetLogs(AuditLogFilterVm filter)
        {
            return QueryLogs(filter);
        }

        public byte[] ExportCsv(AuditLogFilterVm filter)
        {
            var rows = new List<string[]>
            {
                new[] { "Timestamp", "ActorUserId", "Action", "EntityType", "EntityId", "IPAddress" }
            };

            foreach (var log in QueryLogs(filter))
            {
                rows.Add(new[]
                {
                    log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    log.ActorUserId ?? string.Empty,
                    log.ActionLabel ?? log.Action ?? string.Empty,
                    log.EntityTypeLabel ?? log.EntityType ?? string.Empty,
                    log.EntityId ?? string.Empty,
                    log.IPAddress ?? string.Empty
                });
            }

            return CsvExportHelper.ToUtf8Bytes(rows);
        }

        private IList<AuditLogVm> QueryLogs(AuditLogFilterVm filter)
        {
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return new List<AuditLogVm>();
            }

            var bypassDepartmentScope = _departmentScope.BypassesDepartmentScope;
            int? departmentId = null;
            var denyDepartmentScope = false;
            if (!bypassDepartmentScope)
            {
                departmentId = _departmentScope.ScopedDepartmentId;
                denyDepartmentScope = !departmentId.HasValue;
            }

            var actorId = _currentUser == null ? null : _currentUser.UserId;
            var logs = _auditLogQueryRepository.GetLogs(
                filter,
                organizationId.Value,
                departmentId,
                bypassDepartmentScope,
                denyDepartmentScope,
                actorId);

            foreach (var log in logs)
            {
                AuditDisplayLabelHelper.ApplyDisplayLabels(log);
            }

            return logs;
        }
    }
}
