using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface INotificationService
    {
        void GenerateSystemNotifications();

        void TryGenerateScheduledNotifications();

        IEnumerable<NotificationInboxVm> GetInboxForUser(string userId, bool unreadOnly, int maxItems);

        void MarkAsRead(int notificationId, string userId);
    }
}
