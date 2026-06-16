using System;
using AssetManagement.Domain.Common;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Domain.Entities
{
    public class OutboxMessage : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public string MessageType { get; set; }

        public string Payload { get; set; }

        public OutboxMessageStatus Status { get; set; }

        public int Attempts { get; set; }

        public string LastError { get; set; }

        public DateTime? ProcessedAt { get; set; }
    }
}
