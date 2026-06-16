using System;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Outbox;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public static class NotificationHelper
    {
        public static void AddNotification(
            IUnitOfWork unitOfWork,
            IOutboxWriter outboxWriter,
            IOrganizationScopeService organizationScope,
            string userId,
            NotificationType type,
            string subject,
            string message,
            string linkUrl)
        {
            var idempotencyKey = BuildIdempotencyKey(userId, type, subject, linkUrl);
            var payload = OutboxPayloadFactory.BuildNotificationPayload(
                userId,
                (int)type,
                subject,
                message,
                linkUrl,
                idempotencyKey,
                organizationScope == null ? null : organizationScope.GetCurrentOrganizationId());

            outboxWriter.Enqueue(OutboxMessageTypes.Notification, payload);
        }

        public static void AddRoleStageNotification(
            IUnitOfWork unitOfWork,
            IOutboxWriter outboxWriter,
            IOrganizationScopeService organizationScope,
            IUserService userService,
            int roleId,
            string subject,
            string message,
            string linkUrl)
        {
            AddStageApproverNotification(
                unitOfWork,
                outboxWriter,
                organizationScope,
                userService,
                roleId,
                null,
                subject,
                message,
                linkUrl);
        }

        public static void AddStageApproverNotification(
            IUnitOfWork unitOfWork,
            IOutboxWriter outboxWriter,
            IOrganizationScopeService organizationScope,
            IUserService userService,
            int roleId,
            string approverUserId,
            string subject,
            string message,
            string linkUrl)
        {
            if (roleId <= 0 || userService == null)
            {
                return;
            }

            var roleName = unitOfWork.Repository<Role>().GetById(roleId)?.Name ?? ("Role #" + roleId);
            var approverUserIds = !string.IsNullOrWhiteSpace(approverUserId)
                ? new[] { approverUserId.Trim() }
                : userService.GetAll()
                    .Where(x => x.IsActive && x.RoleId == roleId && !string.IsNullOrWhiteSpace(x.Id))
                    .Select(x => x.Id)
                    .Distinct()
                    .ToArray();

            if (approverUserIds.Length == 0)
            {
                return;
            }

            var notificationSubject = subject + " (" + roleName + ")";
            var notificationMessage = message + " Approver role: " + roleName + ".";
            foreach (var userId in approverUserIds)
            {
                AddNotification(
                    unitOfWork,
                    outboxWriter,
                    organizationScope,
                    userId,
                    NotificationType.PendingApproval,
                    notificationSubject,
                    notificationMessage,
                    linkUrl);
            }
        }

        public static string BuildIdempotencyKey(string userId, NotificationType type, string subject, string linkUrl)
        {
            var key = ((int)type) + "|" + (userId ?? string.Empty) + "|" + (subject ?? string.Empty) + "|" + (linkUrl ?? string.Empty);
            if (key.Length <= 200)
            {
                return key;
            }

            return key.Substring(0, 200);
        }
    }
}
