using AssetManagement.Application.Outbox;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AssetManagement.Infrastructure.Services
{
    public static class OutboxPayloadBuilder
    {
        public static string BuildAuditPayload(
            string action,
            string entityType,
            string entityId,
            string oldValues,
            string newValues,
            string actorUserId,
            string ipAddress,
            int? organizationId)
        {
            return OutboxPayloadFactory.BuildAuditPayload(
                action, entityType, entityId, oldValues, newValues, actorUserId, ipAddress, organizationId);
        }

        public static string BuildNotificationPayload(
            string userId,
            int type,
            string subject,
            string message,
            string linkUrl,
            string idempotencyKey,
            int? organizationId)
        {
            return OutboxPayloadFactory.BuildNotificationPayload(
                userId, type, subject, message, linkUrl, idempotencyKey, organizationId);
        }

        public static string BuildWebhookDeliveryPayload(int deliveryId)
        {
            return OutboxPayloadFactory.BuildWebhookDeliveryPayload(deliveryId);
        }

        public static AuditPayload ParseAuditPayload(string payload)
        {
            var token = JObject.Parse(payload);
            return new AuditPayload
            {
                Action = token.Value<string>("action"),
                EntityType = token.Value<string>("entityType"),
                EntityId = token.Value<string>("entityId"),
                OldValues = token.Value<string>("oldValues"),
                NewValues = token.Value<string>("newValues"),
                ActorUserId = token.Value<string>("actorUserId"),
                IPAddress = token.Value<string>("ipAddress"),
                OrganizationId = token.Value<int?>("organizationId")
            };
        }

        public static NotificationPayload ParseNotificationPayload(string payload)
        {
            var token = JObject.Parse(payload);
            return new NotificationPayload
            {
                UserId = token.Value<string>("userId"),
                Type = token.Value<int>("type"),
                Subject = token.Value<string>("subject"),
                Message = token.Value<string>("message"),
                LinkUrl = token.Value<string>("linkUrl"),
                IdempotencyKey = token.Value<string>("idempotencyKey"),
                OrganizationId = token.Value<int?>("organizationId")
            };
        }

        public static int ParseWebhookDeliveryId(string payload)
        {
            var token = JObject.Parse(payload);
            return token.Value<int>("deliveryId");
        }
    }

    public sealed class AuditPayload
    {
        public string Action { get; set; }

        public string EntityType { get; set; }

        public string EntityId { get; set; }

        public string OldValues { get; set; }

        public string NewValues { get; set; }

        public string ActorUserId { get; set; }

        public string IPAddress { get; set; }

        public int? OrganizationId { get; set; }
    }

    public sealed class NotificationPayload
    {
        public string UserId { get; set; }

        public int Type { get; set; }

        public string Subject { get; set; }

        public string Message { get; set; }

        public string LinkUrl { get; set; }

        public string IdempotencyKey { get; set; }

        public int? OrganizationId { get; set; }
    }
}
