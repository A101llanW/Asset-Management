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
            int? requestDepartmentId,
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
                requestDepartmentId,
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
            int? requestDepartmentId,
            string subject,
            string message,
            string linkUrl,
            bool catalogPathFirst = false)
        {
            if (roleId <= 0 || userService == null)
            {
                return;
            }

            var roleName = unitOfWork.Repository<Role>().GetById(roleId)?.Name ?? ("Role #" + roleId);
            // rel1009e: same resolver as approval eligibility (named person; else role holders of the request's
            // department, nearest ancestor, holders with no department, then org-wide as the last resort).
            var scope = StageApproverScopeResolver.ResolveFor(unitOfWork, userService, roleId, approverUserId, requestDepartmentId, catalogPathFirst);
            var approverUserIds = scope.UserIds.ToArray();

            if (approverUserIds.Length == 0)
            {
                return;
            }

            var notificationSubject = subject + " (" + roleName + ")";
            var notificationMessage = message + " Approver role: " + roleName
                + (scope.Kind == StageApproverScopeKind.SectionHead ? " (you are the section head for this department)" : string.Empty) + ".";
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
