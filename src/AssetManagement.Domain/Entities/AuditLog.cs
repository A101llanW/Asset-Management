using System;
using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    public class AuditLog : AuditableEntity
    {
        public int Id { get; set; }

        public string ActorUserId { get; set; }

        public string Action { get; set; }

        public string EntityType { get; set; }

        public string EntityId { get; set; }

        public string OldValues { get; set; }

        public string NewValues { get; set; }

        public DateTime Timestamp { get; set; }

        public string IPAddress { get; set; }
    }
}
