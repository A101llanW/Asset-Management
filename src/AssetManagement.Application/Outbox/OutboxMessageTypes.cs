namespace AssetManagement.Application.Outbox
{
    public static class OutboxMessageTypes
    {
        public const string AuditLog = "AuditLog";

        public const string Notification = "Notification";

        public const string WebhookDelivery = "WebhookDelivery";
    }
}
