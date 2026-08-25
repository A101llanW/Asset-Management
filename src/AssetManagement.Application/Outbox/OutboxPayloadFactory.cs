using System;
using System.Text;

namespace AssetManagement.Application.Outbox
{
    public static class OutboxPayloadFactory
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
            var builder = new StringBuilder();
            builder.Append('{');
            AppendJsonProperty(builder, "action", action, true);
            AppendJsonProperty(builder, "entityType", entityType, false);
            AppendJsonProperty(builder, "entityId", entityId, false);
            AppendJsonProperty(builder, "oldValues", oldValues, false);
            AppendJsonProperty(builder, "newValues", newValues, false);
            AppendJsonProperty(builder, "actorUserId", actorUserId, false);
            AppendJsonProperty(builder, "ipAddress", ipAddress, false);
            AppendJsonProperty(builder, "organizationId", organizationId.HasValue ? organizationId.Value.ToString() : null, false, organizationId.HasValue);
            builder.Append('}');
            return builder.ToString();
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
            var builder = new StringBuilder();
            builder.Append('{');
            AppendJsonProperty(builder, "userId", userId, true);
            AppendJsonProperty(builder, "type", type.ToString(), false, true);
            AppendJsonProperty(builder, "subject", subject, false);
            AppendJsonProperty(builder, "message", message, false);
            AppendJsonProperty(builder, "linkUrl", linkUrl, false);
            AppendJsonProperty(builder, "idempotencyKey", idempotencyKey, false);
            AppendJsonProperty(builder, "organizationId", organizationId.HasValue ? organizationId.Value.ToString() : null, false, organizationId.HasValue);
            builder.Append('}');
            return builder.ToString();
        }

        public static string BuildWebhookDeliveryPayload(int deliveryId)
        {
            return "{\"deliveryId\":" + deliveryId + "}";
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, string value, bool first, bool isNumber)
        {
            if (!first)
            {
                builder.Append(',');
            }

            builder.Append('"').Append(Escape(name)).Append('"').Append(':');
            if (isNumber)
            {
                builder.Append(value ?? "0");
                return;
            }

            if (value == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append('"').Append(Escape(value)).Append('"');
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, string value, bool first)
        {
            AppendJsonProperty(builder, name, value, first, false);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
