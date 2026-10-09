using System.Collections.Generic;
using System.Linq;
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

        public AuditLogPageVm GetLogPage(AuditLogFilterVm filter)
        {
            filter = filter ?? new AuditLogFilterVm();
            var page = AuditLogFilterHelper.NormalizePage(filter.Page);
            var pageSize = AuditLogFilterHelper.NormalizePageSize(filter.PageSize);
            var result = QueryPage(filter, (page - 1) * pageSize, pageSize);
            result.Page = page;
            result.PageSize = pageSize;
            return result;
        }

        public byte[] ExportCsv(AuditLogFilterVm filter)
        {
            var rows = new List<string[]>
            {
                new[] { "Timestamp (EAT)", "ActorUserId", "Action", "EntityType", "EntityId", "IPAddress" }
            };

            var exportRows = filter != null && !filter.RelatedAssetId.HasValue && filter.OrganizationWide
                ? QueryPage(filter, 0, AuditLogFilterHelper.MaxExportRows).Items
                : QueryLogs(filter);
            foreach (var log in exportRows)
            {
                rows.Add(new[]
                {
                    KenyaTimeHelper.FormatUtc(log.Timestamp),
                    string.IsNullOrWhiteSpace(log.ActorDisplayName) ? (log.ActorUserId ?? string.Empty) : log.ActorDisplayName,
                    log.ActionLabel ?? log.Action ?? string.Empty,
                    log.EntityTypeLabel ?? log.EntityType ?? string.Empty,
                    log.EntityId ?? string.Empty,
                    log.IPAddress ?? string.Empty
                });
            }

            return CsvExportHelper.ToUtf8Bytes(rows);
        }

        private AuditLogPageVm QueryPage(AuditLogFilterVm filter, int skip, int take)
        {
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return new AuditLogPageVm();
            }

            // Users who hold AuditLogs.View (the controller sets OrganizationWide) read the whole organization.
            var bypassDepartmentScope = filter.OrganizationWide || _departmentScope.BypassesDepartmentScope;
            int? departmentId = null;
            var denyDepartmentScope = false;
            if (!bypassDepartmentScope)
            {
                departmentId = _departmentScope.ScopedDepartmentId;
                denyDepartmentScope = !departmentId.HasValue;
            }

            var actorId = _currentUser == null ? null : _currentUser.UserId;
            var result = _auditLogQueryRepository.GetLogPage(
                filter,
                organizationId.Value,
                departmentId,
                bypassDepartmentScope,
                denyDepartmentScope,
                actorId,
                skip,
                take) ?? new AuditLogPageVm();
            foreach (var log in result.Items)
            {
                AuditDisplayLabelHelper.ApplyDisplayLabels(log);
            }

            return result;
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

            if (filter != null && filter.BusinessEventsOnly)
            {
                logs = AssetAuditTrailFilterHelper.FilterBusinessEvents(logs).ToList();
            }

            foreach (var log in logs)
            {
                AuditDisplayLabelHelper.ApplyDisplayLabels(log);
            }

            return logs;
        }
    }
}
