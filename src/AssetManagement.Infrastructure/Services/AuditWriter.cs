using System;
using System.Configuration;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Outbox;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Infrastructure.Services
{
    public class AuditWriter : IAuditWriter
    {
        private readonly IOutboxWriter _outboxWriter;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserContext _currentUser;
        private readonly IOrganizationScopeService _organizationScope;

        public AuditWriter(
            IOutboxWriter outboxWriter,
            IUnitOfWork unitOfWork,
            ICurrentUserContext currentUser,
            IOrganizationScopeService organizationScope)
        {
            _outboxWriter = outboxWriter;
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _organizationScope = organizationScope;
        }

        public void Write(string action, string entityType, string entityId, string oldValues, string newValues)
        {
            var organizationId = ResolveOrganizationId(entityType, entityId);
            if (!organizationId.HasValue)
            {
                throw new InvalidOperationException(
                    string.Format(
                        "Cannot write audit log for action '{0}' on {1} {2}: organization context is required.",
                        action,
                        entityType,
                        entityId));
            }

            var payload = OutboxPayloadFactory.BuildAuditPayload(
                action,
                entityType,
                entityId,
                oldValues,
                newValues,
                _currentUser == null ? null : _currentUser.UserId,
                _currentUser == null ? null : _currentUser.IPAddress,
                organizationId);

            if (ShouldWriteAuditSynchronously())
            {
                var parsed = OutboxPayloadBuilder.ParseAuditPayload(payload);
                _unitOfWork.Repository<AuditLog>().Add(new AuditLog
                {
                    OrganizationId = parsed.OrganizationId ?? organizationId,
                    ActorUserId = parsed.ActorUserId,
                    Action = parsed.Action,
                    EntityType = parsed.EntityType,
                    EntityId = parsed.EntityId,
                    OldValues = parsed.OldValues,
                    NewValues = parsed.NewValues,
                    Timestamp = DateTime.UtcNow,
                    IPAddress = parsed.IPAddress,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                });
                _unitOfWork.SaveChanges();
                return;
            }

            _outboxWriter.Enqueue(OutboxMessageTypes.AuditLog, payload);
        }

        private int? ResolveOrganizationId(string entityType, string entityId)
        {
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (organizationId.HasValue)
            {
                return organizationId;
            }

            if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(entityId))
            {
                return null;
            }

            int parsedId;
            if (!int.TryParse(entityId, out parsedId))
            {
                return null;
            }

            if (string.Equals(entityType, "Organization", StringComparison.OrdinalIgnoreCase))
            {
                return parsedId;
            }

            if (string.Equals(entityType, "OrganizationLicense", StringComparison.OrdinalIgnoreCase))
            {
                var license = _unitOfWork.Repository<OrganizationLicense>().GetById(parsedId);
                return license == null ? null : (int?)license.OrganizationId;
            }

            if (string.Equals(entityType, "ImpersonationRequest", StringComparison.OrdinalIgnoreCase))
            {
                var request = _unitOfWork.Repository<ImpersonationRequest>().GetById(parsedId);
                return request == null ? null : request.OrganizationId;
            }

            return null;
        }

        private static bool ShouldWriteAuditSynchronously()
        {
            var setting = ConfigurationManager.AppSettings["SyncAuditWrites"];
            return setting != null && setting.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
