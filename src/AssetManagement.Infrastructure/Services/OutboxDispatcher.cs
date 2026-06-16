using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Outbox;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Application.Services.Webhooks;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Services
{
    public class OutboxDispatcher : IOutboxDispatcher
    {
        private const int MaxAttempts = 8;

        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationQueryService _notificationQueryService;

        public OutboxDispatcher(IUnitOfWork unitOfWork, INotificationQueryService notificationQueryService)
        {
            _unitOfWork = unitOfWork;
            _notificationQueryService = notificationQueryService;
        }

        public void ProcessPending(int batchSize)
        {
            var safeBatch = batchSize <= 0 ? 25 : Math.Min(batchSize, 100);
            ProcessOutboxMessages(safeBatch);
            ProcessRetryableWebhookDeliveries(safeBatch);
        }

        private void ProcessOutboxMessages(int batchSize)
        {
            var pending = _unitOfWork.Repository<OutboxMessage>()
                .Find(x => x.IsActive && x.Status == OutboxMessageStatus.Pending)
                .OrderBy(x => x.CreatedAt)
                .Take(batchSize)
                .ToList();

            foreach (var message in pending)
            {
                ProcessSingleOutboxMessage(message);
            }
        }

        private void ProcessSingleOutboxMessage(OutboxMessage message)
        {
            message.Status = OutboxMessageStatus.Processing;
            message.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<OutboxMessage>().Update(message);
            _unitOfWork.SaveChanges();

            try
            {
                if (string.Equals(message.MessageType, OutboxMessageTypes.AuditLog, StringComparison.OrdinalIgnoreCase))
                {
                    ProcessAuditMessage(message);
                }
                else if (string.Equals(message.MessageType, OutboxMessageTypes.Notification, StringComparison.OrdinalIgnoreCase))
                {
                    ProcessNotificationMessage(message);
                }
                else if (string.Equals(message.MessageType, OutboxMessageTypes.WebhookDelivery, StringComparison.OrdinalIgnoreCase))
                {
                    var deliveryId = OutboxPayloadBuilder.ParseWebhookDeliveryId(message.Payload);
                    DeliverWebhook(deliveryId);
                }
                else
                {
                    throw new InvalidOperationException("Unsupported outbox message type: " + message.MessageType);
                }

                message.Status = OutboxMessageStatus.Completed;
                message.ProcessedAt = DateTime.UtcNow;
                message.LastError = null;
            }
            catch (Exception ex)
            {
                message.Attempts++;
                message.LastError = TruncateError(ex.Message);
                if (message.Attempts >= MaxAttempts)
                {
                    message.Status = OutboxMessageStatus.Failed;
                }
                else
                {
                    message.Status = OutboxMessageStatus.Pending;
                }
            }

            message.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<OutboxMessage>().Update(message);
            _unitOfWork.SaveChanges();
        }

        private void ProcessAuditMessage(OutboxMessage message)
        {
            var payload = OutboxPayloadBuilder.ParseAuditPayload(message.Payload);
            _unitOfWork.Repository<AuditLog>().Add(new AuditLog
            {
                OrganizationId = payload.OrganizationId ?? message.OrganizationId,
                ActorUserId = payload.ActorUserId,
                Action = payload.Action,
                EntityType = payload.EntityType,
                EntityId = payload.EntityId,
                OldValues = payload.OldValues,
                NewValues = payload.NewValues,
                Timestamp = DateTime.UtcNow,
                IPAddress = payload.IPAddress,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            _unitOfWork.SaveChanges();
        }

        private void ProcessNotificationMessage(OutboxMessage message)
        {
            var payload = OutboxPayloadBuilder.ParseNotificationPayload(message.Payload);
            if (!string.IsNullOrWhiteSpace(payload.IdempotencyKey)
                && _notificationQueryService.ExistsByIdempotencyKey(payload.UserId, payload.IdempotencyKey))
            {
                return;
            }

            _unitOfWork.Repository<Notification>().Add(new Notification
            {
                OrganizationId = payload.OrganizationId ?? message.OrganizationId,
                UserId = payload.UserId,
                Type = (NotificationType)payload.Type,
                Subject = payload.Subject,
                Message = payload.Message,
                LinkUrl = payload.LinkUrl,
                IdempotencyKey = payload.IdempotencyKey,
                Status = NotificationStatus.Unread,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            _unitOfWork.SaveChanges();
        }

        private void ProcessRetryableWebhookDeliveries(int batchSize)
        {
            var now = DateTime.UtcNow;
            var pending = _unitOfWork.Repository<WebhookDelivery>()
                .Find(x => x.IsActive
                    && (x.Status == WebhookDeliveryStatus.Pending || x.Status == WebhookDeliveryStatus.Failed)
                    && (!x.NextRetryUtc.HasValue || x.NextRetryUtc.Value <= now))
                .OrderBy(x => x.CreatedAt)
                .Take(batchSize)
                .ToList();

            foreach (var delivery in pending)
            {
                DeliverWebhook(delivery.Id);
            }
        }

        private void DeliverWebhook(int deliveryId)
        {
            var delivery = _unitOfWork.Repository<WebhookDelivery>().GetById(deliveryId);
            if (delivery == null || !delivery.IsActive)
            {
                return;
            }

            if (delivery.Status == WebhookDeliveryStatus.Delivered)
            {
                return;
            }

            var subscription = _unitOfWork.Repository<WebhookSubscription>().GetById(delivery.WebhookSubscriptionId);
            if (subscription == null || !subscription.IsActive)
            {
                delivery.Status = WebhookDeliveryStatus.Failed;
                delivery.LastError = "Webhook subscription is inactive or missing.";
                delivery.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Repository<WebhookDelivery>().Update(delivery);
                _unitOfWork.SaveChanges();
                return;
            }

            delivery.Status = WebhookDeliveryStatus.Delivering;
            delivery.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<WebhookDelivery>().Update(delivery);
            _unitOfWork.SaveChanges();

            try
            {
                WebhookUrlValidator.EnsureSafeWebhookUrl(subscription.TargetUrl);
                WebhookHttpClient.PostSignedPayload(subscription.TargetUrl, subscription.Secret, delivery.PayloadJson);
                delivery.Status = WebhookDeliveryStatus.Delivered;
                delivery.ProcessedAt = DateTime.UtcNow;
                delivery.LastError = null;
            }
            catch (Exception ex)
            {
                delivery.Attempts++;
                delivery.LastError = TruncateError(ex.Message);
                if (delivery.Attempts >= MaxAttempts)
                {
                    delivery.Status = WebhookDeliveryStatus.Failed;
                }
                else
                {
                    delivery.Status = WebhookDeliveryStatus.Pending;
                    delivery.NextRetryUtc = DateTime.UtcNow.AddMinutes(Math.Pow(2, Math.Min(delivery.Attempts, 6)));
                }
            }

            delivery.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<WebhookDelivery>().Update(delivery);
            _unitOfWork.SaveChanges();
        }

        private static string TruncateError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "Unknown error.";
            }

            return message.Length <= 1000 ? message : message.Substring(0, 1000);
        }
    }
}
