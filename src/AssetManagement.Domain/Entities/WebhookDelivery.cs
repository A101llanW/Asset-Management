using System;
using AssetManagement.Domain.Common;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Domain.Entities
{
    public class WebhookDelivery : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public int WebhookSubscriptionId { get; set; }

        public string EventType { get; set; }

        public string PayloadJson { get; set; }

        public WebhookDeliveryStatus Status { get; set; }

        public int Attempts { get; set; }

        public DateTime? NextRetryUtc { get; set; }

        public string LastError { get; set; }

        public DateTime? ProcessedAt { get; set; }
    }
}
