using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Outbox;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Application.Services.Webhooks;

namespace AssetManagement.Application.Services
{
    public class WebhookService : IWebhookService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditWriter _auditWriter;
        private readonly IOutboxWriter _outboxWriter;
        private readonly IOrganizationScopeService _organizationScope;

        public WebhookService(
            IUnitOfWork unitOfWork,
            IAuditWriter auditWriter,
            IOutboxWriter outboxWriter,
            IOrganizationScopeService organizationScope)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
            _outboxWriter = outboxWriter;
            _organizationScope = organizationScope;
        }

        public IEnumerable<WebhookSubscriptionVm> GetSubscriptions()
        {
            return _unitOfWork.Repository<WebhookSubscription>().GetAll()
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new WebhookSubscriptionVm
                {
                    Id = x.Id,
                    EventType = x.EventType,
                    TargetUrl = x.TargetUrl,
                    IsActive = x.IsActive,
                    CreatedByUserId = x.CreatedByUserId
                })
                .ToList();
        }

        public int Register(WebhookSubscriptionEditVm model, string createdByUserId)
        {
            if (model == null)
            {
                throw new BusinessException("Webhook subscription is required.");
            }

            if (string.IsNullOrWhiteSpace(model.EventType) || string.IsNullOrWhiteSpace(model.TargetUrl))
            {
                throw new BusinessException("Event type and target URL are required.");
            }

            try
            {
                WebhookUrlValidator.EnsureSafeWebhookUrl(model.TargetUrl);
            }
            catch (InvalidOperationException ex)
            {
                throw new BusinessException(ex.Message);
            }

            var entity = new WebhookSubscription
            {
                EventType = model.EventType.Trim(),
                TargetUrl = model.TargetUrl.Trim(),
                Secret = string.IsNullOrWhiteSpace(model.Secret) ? null : model.Secret.Trim(),
                IsActive = true,
                CreatedByUserId = createdByUserId,
                CreatedAt = DateTime.UtcNow
            };

            _unitOfWork.Repository<WebhookSubscription>().Add(entity);
            _unitOfWork.SaveChanges();
            _auditWriter.Write("Webhooks.Register", nameof(WebhookSubscription), entity.Id.ToString(), null, entity.EventType);
            return entity.Id;
        }

        public void Deactivate(int id)
        {
            var entity = _unitOfWork.Repository<WebhookSubscription>().GetById(id);
            if (entity == null)
            {
                throw new BusinessException("Webhook subscription not found.");
            }

            entity.IsActive = false;
            entity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<WebhookSubscription>().Update(entity);
            _unitOfWork.SaveChanges();
            _auditWriter.Write("Webhooks.Deactivate", nameof(WebhookSubscription), entity.Id.ToString(), "Active", "Inactive");
        }

        public void QueueDelivery(string eventType, string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(eventType))
            {
                return;
            }

            var subscribers = _unitOfWork.Repository<WebhookSubscription>().Find(x =>
                x.IsActive && x.EventType == eventType).ToList();

            if (subscribers.Count == 0)
            {
                return;
            }

            var organizationId = _organizationScope.GetCurrentOrganizationId();
            foreach (var subscription in subscribers)
            {
                var delivery = new WebhookDelivery
                {
                    OrganizationId = organizationId,
                    WebhookSubscriptionId = subscription.Id,
                    EventType = eventType.Trim(),
                    PayloadJson = payloadJson ?? "{}",
                    Status = WebhookDeliveryStatus.Pending,
                    Attempts = 0,
                    NextRetryUtc = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _unitOfWork.Repository<WebhookDelivery>().Add(delivery);
                _unitOfWork.SaveChanges();

                _outboxWriter.Enqueue(
                    OutboxMessageTypes.WebhookDelivery,
                    OutboxPayloadFactory.BuildWebhookDeliveryPayload(delivery.Id));
            }
        }
    }
}
