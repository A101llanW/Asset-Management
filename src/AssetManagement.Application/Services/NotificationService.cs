using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Outbox;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public class NotificationService : INotificationService
    {
        private const string LastGeneratedSettingKey = "Notifications.LastGeneratedUtc";

        private readonly IUnitOfWork _unitOfWork;
        private readonly IDepartmentScopeService _departmentScope;
        private readonly INotificationQueryService _notificationQueryService;
        private readonly INotificationScheduleQueryService _scheduleQueryService;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IOutboxWriter _outboxWriter;

        public NotificationService(
            IUnitOfWork unitOfWork,
            IDepartmentScopeService departmentScope,
            INotificationQueryService notificationQueryService,
            INotificationScheduleQueryService scheduleQueryService,
            IOrganizationScopeService organizationScope,
            IOutboxWriter outboxWriter)
        {
            _unitOfWork = unitOfWork;
            _departmentScope = departmentScope;
            _notificationQueryService = notificationQueryService;
            _scheduleQueryService = scheduleQueryService;
            _organizationScope = organizationScope;
            _outboxWriter = outboxWriter;
        }

        public IEnumerable<NotificationInboxVm> GetInboxForUser(string userId, bool unreadOnly, int maxItems)
        {
            return _notificationQueryService.GetInbox(userId, unreadOnly, maxItems);
        }

        public void MarkAsRead(int notificationId, string userId)
        {
            var notification = _unitOfWork.Repository<Notification>().GetById(notificationId);
            if (notification == null || !notification.IsActive)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(notification.UserId)
                && !string.Equals(notification.UserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (notification.Status == NotificationStatus.Read)
            {
                return;
            }

            notification.Status = NotificationStatus.Read;
            notification.ReadAt = DateTime.UtcNow;
            notification.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Notification>().Update(notification);
            _unitOfWork.SaveChanges();
        }

        public void TryGenerateScheduledNotifications()
        {
            var organizationIds = _scheduleQueryService.GetActiveOrganizationIds();
            foreach (var organizationId in organizationIds)
            {
                _organizationScope.SetOrganizationFilterOverride(organizationId);
                try
                {
                    var settings = _unitOfWork.Repository<SystemSetting>().GetAll().ToList();
                    var lastGenerated = GetLastGeneratedUtc(settings);
                    if (lastGenerated.HasValue && (DateTime.UtcNow - lastGenerated.Value).TotalHours < 24)
                    {
                        continue;
                    }

                    GenerateSystemNotificationsForCurrentOrganization(settings);
                    UpsertLastGeneratedUtc(settings, DateTime.UtcNow);
                    _unitOfWork.SaveChanges();
                }
                finally
                {
                    _organizationScope.SetOrganizationFilterOverride(null);
                }
            }
        }

        public void GenerateSystemNotifications()
        {
            var settings = _unitOfWork.Repository<SystemSetting>().GetAll().ToList();
            GenerateSystemNotificationsForCurrentOrganization(settings);
            _unitOfWork.SaveChanges();
        }

        private void GenerateSystemNotificationsForCurrentOrganization(IList<SystemSetting> settings)
        {
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var warrantyThresholdDays = NotificationSettingsHelper.GetWarrantyThresholdDays(settings);
            var insuranceThresholdDays = NotificationSettingsHelper.GetInsuranceThresholdDays(settings);
            var maintenanceThresholdDays = NotificationSettingsHelper.GetMaintenanceThresholdDays(settings);

            foreach (var asset in _scheduleQueryService.GetExpiringWarranties(organizationId.Value, now, warrantyThresholdDays))
            {
                EnqueueScheduledNotification(
                    null,
                    NotificationType.WarrantyExpiry,
                    "Warranty expiring",
                    "Warranty nearing expiry for asset " + asset.AssetTag + " on " + asset.WarrantyEndDate.ToString("yyyy-MM-dd") + ".",
                    "/Assets/Details/" + asset.Id,
                    "WarrantyExpiry:" + asset.Id + ":" + asset.WarrantyEndDate.ToString("yyyyMMdd"));
            }

            foreach (var policy in _scheduleQueryService.GetExpiringInsurance(organizationId.Value, now, insuranceThresholdDays))
            {
                var assetLabel = string.IsNullOrWhiteSpace(policy.AssetTag) ? ("Asset #" + policy.AssetId) : policy.AssetTag;
                EnqueueScheduledNotification(
                    null,
                    NotificationType.InsuranceExpiry,
                    "Insurance policy expiring",
                    "Insurance policy " + policy.PolicyNumber + " for " + assetLabel + " expires on " + policy.PolicyEndDate.ToString("yyyy-MM-dd") + ".",
                    "/Assets/Details/" + policy.AssetId,
                    "InsuranceExpiry:" + policy.AssetId + ":" + policy.PolicyNumber + ":" + policy.PolicyEndDate.ToString("yyyyMMdd"));
            }

            foreach (var assignment in _scheduleQueryService.GetDueSoonAssignments(organizationId.Value, now, maintenanceThresholdDays))
            {
                var assetLabel = string.IsNullOrWhiteSpace(assignment.AssetTag) ? ("Asset #" + assignment.AssetId) : assignment.AssetTag;
                EnqueueScheduledNotification(
                    assignment.ToUserId,
                    NotificationType.TemporaryAssignmentDue,
                    "Temporary assignment due soon",
                    assetLabel + " is due for return on " + assignment.ExpectedReturnDate.ToString("yyyy-MM-dd") + ".",
                    "/Assets/Details/" + assignment.AssetId,
                    "TemporaryAssignmentDue:" + assignment.AssetId + ":" + assignment.ExpectedReturnDate.ToString("yyyyMMdd"));
            }

            foreach (var assignment in _scheduleQueryService.GetOverdueAssignments(organizationId.Value, now))
            {
                if (assignment.AssetStatus != (int)AssetStatus.Assigned)
                {
                    continue;
                }

                EnqueueScheduledNotification(
                    assignment.ToUserId,
                    NotificationType.OverdueReturn,
                    "Asset return overdue",
                    assignment.AssetTag + " was due on " + assignment.ExpectedReturnDate.ToString("yyyy-MM-dd") + " and is still assigned.",
                    "/Assets/Details/" + assignment.AssetId,
                    "OverdueReturn:" + assignment.AssetId + ":" + assignment.ExpectedReturnDate.ToString("yyyyMMdd"));
            }

            foreach (var transfer in _scheduleQueryService.GetPendingTransferApprovals(organizationId.Value))
            {
                if (string.IsNullOrWhiteSpace(transfer.RequestedById))
                {
                    continue;
                }

                EnqueueScheduledNotification(
                    transfer.RequestedById,
                    NotificationType.PendingApproval,
                    "Transfer awaiting approval",
                    "Your transfer request is still pending approval.",
                    "/PendingApprovals",
                    "PendingTransfer:" + transfer.EntityId);
            }

            foreach (var disposal in _scheduleQueryService.GetPendingDisposalApprovals(organizationId.Value))
            {
                if (string.IsNullOrWhiteSpace(disposal.RequestedById))
                {
                    continue;
                }

                EnqueueScheduledNotification(
                    disposal.RequestedById,
                    NotificationType.PendingApproval,
                    "Disposal awaiting approval",
                    "Your disposal request is still pending approval.",
                    "/Assets/Details/" + disposal.AssetId,
                    "PendingDisposal:" + disposal.EntityId);
            }
        }

        private void EnqueueScheduledNotification(
            string userId,
            NotificationType type,
            string subject,
            string message,
            string linkUrl,
            string idempotencyKey)
        {
            if (_notificationQueryService.ExistsByIdempotencyKey(userId, idempotencyKey))
            {
                return;
            }

            var payload = OutboxPayloadFactory.BuildNotificationPayload(
                userId,
                (int)type,
                subject,
                message,
                linkUrl,
                idempotencyKey,
                _organizationScope.GetCurrentOrganizationId());

            _outboxWriter.Enqueue(OutboxMessageTypes.Notification, payload);
        }

        private static DateTime? GetLastGeneratedUtc(IEnumerable<SystemSetting> settings)
        {
            var setting = settings.FirstOrDefault(x => string.Equals(x.SettingKey, LastGeneratedSettingKey, StringComparison.OrdinalIgnoreCase));
            if (setting == null || string.IsNullOrWhiteSpace(setting.SettingValue))
            {
                return null;
            }

            DateTime parsed;
            return DateTime.TryParse(setting.SettingValue, out parsed) ? parsed.ToUniversalTime() : (DateTime?)null;
        }

        private void UpsertLastGeneratedUtc(IList<SystemSetting> settings, DateTime utcNow)
        {
            var setting = settings.FirstOrDefault(x => string.Equals(x.SettingKey, LastGeneratedSettingKey, StringComparison.OrdinalIgnoreCase));
            if (setting == null)
            {
                setting = new SystemSetting
                {
                    SettingKey = LastGeneratedSettingKey,
                    SettingValue = utcNow.ToString("o"),
                    Description = "UTC timestamp of the last automated notification generation run.",
                    CreatedAt = utcNow,
                    IsActive = true
                };
                _unitOfWork.Repository<SystemSetting>().Add(setting);
                settings.Add(setting);
                return;
            }

            setting.SettingValue = utcNow.ToString("o");
            setting.UpdatedAt = utcNow;
            _unitOfWork.Repository<SystemSetting>().Update(setting);
        }
    }
}
