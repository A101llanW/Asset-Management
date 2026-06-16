using System;
using System.Collections.Generic;
using System.Data;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Queries
{
    public class NotificationQueryService : INotificationQueryService
    {
        private const string InboxSql = @"
SELECT TOP (@Take)
    n.[Id],
    n.[UserId],
    n.[Type],
    n.[Subject],
    n.[Message],
    n.[Status],
    n.[CreatedAt],
    n.[LinkUrl]
FROM [Notification] n
WHERE n.[OrganizationId] = @OrganizationId
  AND n.[IsActive] = 1
  AND (n.[UserId] IS NULL OR n.[UserId] = @UserId)
  AND (@UnreadOnly = 0 OR n.[Status] = @UnreadStatus)
ORDER BY n.[CreatedAt] DESC, n.[Id] DESC";

        private const string ExistsByIdempotencyKeySql = @"
SELECT CASE WHEN EXISTS (
    SELECT 1
    FROM [Notification] n
    WHERE n.[OrganizationId] = @OrganizationId
      AND n.[IsActive] = 1
      AND n.[IdempotencyKey] = @IdempotencyKey
      AND ((@UserId IS NULL AND n.[UserId] IS NULL) OR n.[UserId] = @UserId)
) THEN 1 ELSE 0 END";

        private readonly ISqlConnectionFactory _connectionFactory;
        private readonly IOrganizationScopeService _organizationScope;

        public NotificationQueryService(
            ISqlConnectionFactory connectionFactory,
            IOrganizationScopeService organizationScope)
        {
            _connectionFactory = connectionFactory;
            _organizationScope = organizationScope;
        }

        public IList<NotificationInboxVm> GetInbox(string userId, bool unreadOnly, int take)
        {
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                throw new InvalidOperationException("Organization context is required for notification inbox queries.");
            }

            var safeTake = take <= 0 ? 25 : Math.Min(take, 100);
            var items = new List<NotificationInboxVm>();

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = InboxSql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId.Value);
                    SqlQueryHelper.AddParameter(command, "@UserId", string.IsNullOrWhiteSpace(userId) ? (object)DBNull.Value : userId.Trim());
                    SqlQueryHelper.AddParameter(command, "@UnreadOnly", unreadOnly ? 1 : 0);
                    SqlQueryHelper.AddParameter(command, "@UnreadStatus", (int)NotificationStatus.Unread);
                    SqlQueryHelper.AddParameter(command, "@Take", safeTake);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(MapInboxItem(reader));
                        }
                    }
                }
            }

            return items;
        }

        public bool ExistsByIdempotencyKey(string userId, string idempotencyKey)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return false;
            }

            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return false;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = ExistsByIdempotencyKeySql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId.Value);
                    SqlQueryHelper.AddParameter(command, "@UserId", string.IsNullOrWhiteSpace(userId) ? (object)DBNull.Value : userId.Trim());
                    SqlQueryHelper.AddParameter(command, "@IdempotencyKey", idempotencyKey.Trim());
                    return Convert.ToInt32(command.ExecuteScalar()) == 1;
                }
            }
        }

        private static NotificationInboxVm MapInboxItem(IDataRecord record)
        {
            var userId = SqlQueryHelper.GetString(record, "UserId");
            return new NotificationInboxVm
            {
                Id = Convert.ToInt32(record["Id"]),
                UserId = userId,
                IsPersonal = !string.IsNullOrWhiteSpace(userId),
                Type = ((NotificationType)Convert.ToInt32(record["Type"])).ToString(),
                Subject = SqlQueryHelper.GetString(record, "Subject"),
                Message = SqlQueryHelper.GetString(record, "Message"),
                Status = ((NotificationStatus)Convert.ToInt32(record["Status"])).ToString(),
                CreatedAt = Convert.ToDateTime(record["CreatedAt"]),
                LinkUrl = SqlQueryHelper.GetString(record, "LinkUrl")
            };
        }
    }
}
