using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts.Queries
{
    public interface INotificationQueryService
    {
        IList<NotificationInboxVm> GetInbox(string userId, bool unreadOnly, int take);

        bool ExistsByIdempotencyKey(string userId, string idempotencyKey);
    }
}
