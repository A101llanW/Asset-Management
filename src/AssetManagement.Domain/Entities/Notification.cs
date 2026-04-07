using System;
using AssetManagement.Domain.Common;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Domain.Entities
{
    public class Notification : AuditableEntity
    {
        public int Id { get; set; }

        public string UserId { get; set; }

        public NotificationType Type { get; set; }

        public string Subject { get; set; }

        public string Message { get; set; }

        public NotificationStatus Status { get; set; }

        public DateTime? ReadAt { get; set; }

        public string LinkUrl { get; set; }
    }
}
