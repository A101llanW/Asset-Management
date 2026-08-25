using System;
using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    public class WebhookSubscription : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public string EventType { get; set; }

        public string TargetUrl { get; set; }

        public string Secret { get; set; }

        public string CreatedByUserId { get; set; }
    }
}
