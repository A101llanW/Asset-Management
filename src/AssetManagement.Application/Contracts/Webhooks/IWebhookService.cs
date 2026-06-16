using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IWebhookService
    {
        IEnumerable<WebhookSubscriptionVm> GetSubscriptions();

        int Register(WebhookSubscriptionEditVm model, string createdByUserId);

        void Deactivate(int id);

        void QueueDelivery(string eventType, string payloadJson);
    }
}
