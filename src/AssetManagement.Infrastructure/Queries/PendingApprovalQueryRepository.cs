using System;
using System.Collections.Generic;
using System.Data;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.DTOs;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Queries
{
    public class PendingApprovalQueryRepository : IPendingApprovalQueryRepository
    {
        private static readonly string PurchaseRequestDepartmentScopeSql =
            "(@BypassDepartmentScope = 1 OR @BypassPurchaseDepartmentScope = 1 OR (@DenyDepartmentScope = 0 AND @DepartmentId IS NOT NULL AND p.[DepartmentId] = @DepartmentId))";

        private static readonly string AssetRequestDepartmentScopeSql =
            "(@BypassDepartmentScope = 1 OR @BypassAssetRequestDepartmentScope = 1 OR (@DenyDepartmentScope = 0 AND @DepartmentId IS NOT NULL AND r.[DepartmentId] = @DepartmentId))";

        private static readonly string CountSql = @"
SELECT COUNT(*)
FROM (
    SELECT t.[Id]
    FROM [AssetTransfer] t
    INNER JOIN [Asset] a ON a.[Id] = t.[AssetId]
    WHERE t.[OrganizationId] = @OrganizationId
      AND t.[IsActive] = 1
      AND t.[ApprovalStatus] = @PendingStatus
      AND a.[IsActive] = 1
      AND " + SqlQueryHelper.FormatAssetDepartmentScopeSql("a") + @"

    UNION ALL

    SELECT d.[Id]
    FROM [DisposalRecord] d
    INNER JOIN [Asset] a ON a.[Id] = d.[AssetId]
    WHERE d.[OrganizationId] = @OrganizationId
      AND d.[IsActive] = 1
      AND d.[ApprovalStatus] = @PendingStatus
      AND a.[IsActive] = 1
      AND " + SqlQueryHelper.FormatAssetDepartmentScopeSql("a") + @"

    UNION ALL

    SELECT p.[Id]
    FROM [PurchaseRequest] p
    WHERE p.[OrganizationId] = @OrganizationId
      AND p.[IsActive] = 1
      AND p.[ApprovalStatus] = @PendingStatus
      AND " + PurchaseRequestDepartmentScopeSql + @"

    UNION ALL

    SELECT r.[Id]
    FROM [AssetRequest] r
    WHERE r.[OrganizationId] = @OrganizationId
      AND r.[IsActive] = 1
      AND r.[Status] = @AssetRequestPendingStatus
      AND " + AssetRequestDepartmentScopeSql + @"
) pending";

        private static readonly string SourcesSql = @"
SELECT
    N'Asset Transfer' AS ProcessName,
    t.[Id] AS RequestId,
    t.[AssetId],
    a.[AssetTag],
    a.[AssetName],
    t.[RequestedById],
    t.[TransferDate] AS RequestedDateUtc,
    t.[CurrentApprovalStage],
    t.[ApprovalStageRoleIds],
    t.[ApprovalStageUserIds],
    CASE
        WHEN NULLIF(LTRIM(RTRIM(t.[Reason])), '') IS NULL THEN N'Transfer request pending approval.'
        ELSE t.[Reason]
    END AS Summary,
    NULL AS DisplayTag,
    NULL AS DisplayName,
    a.[DepartmentId],
    0 AS IsAssetRequest
FROM [AssetTransfer] t
INNER JOIN [Asset] a ON a.[Id] = t.[AssetId]
WHERE t.[OrganizationId] = @OrganizationId
  AND t.[IsActive] = 1
  AND t.[ApprovalStatus] = @PendingStatus
  AND a.[IsActive] = 1
  AND " + SqlQueryHelper.FormatAssetDepartmentScopeSql("a") + @"

UNION ALL

SELECT
    N'Asset Disposal' AS ProcessName,
    d.[Id] AS RequestId,
    d.[AssetId],
    a.[AssetTag],
    a.[AssetName],
    d.[RequestedById],
    d.[DisposalRequestDate] AS RequestedDateUtc,
    d.[CurrentApprovalStage],
    d.[ApprovalStageRoleIds],
    d.[ApprovalStageUserIds],
    CAST(d.[DisposalMethod] AS nvarchar(20)) + N': ' + ISNULL(d.[DisposalReason], N'') AS Summary,
    NULL AS DisplayTag,
    NULL AS DisplayName,
    a.[DepartmentId],
    0 AS IsAssetRequest
FROM [DisposalRecord] d
INNER JOIN [Asset] a ON a.[Id] = d.[AssetId]
WHERE d.[OrganizationId] = @OrganizationId
  AND d.[IsActive] = 1
  AND d.[ApprovalStatus] = @PendingStatus
  AND a.[IsActive] = 1
  AND " + SqlQueryHelper.FormatAssetDepartmentScopeSql("a") + @"

UNION ALL

SELECT
    N'Requisition' AS ProcessName,
    p.[Id] AS RequestId,
    0 AS AssetId,
    p.[RequestNumber] AS AssetTag,
    dept.[Name] AS AssetName,
    p.[RequestedById],
    p.[CreatedAt] AS RequestedDateUtc,
    p.[CurrentApprovalStage],
    p.[ApprovalStageRoleIds],
    p.[ApprovalStageUserIds],
    CASE
        WHEN NULLIF(LTRIM(RTRIM(p.[ItemDescription])), '') IS NOT NULL THEN
            CASE WHEN LEN(p.[ItemDescription]) > 120 THEN LEFT(p.[ItemDescription], 120) + N'...' ELSE p.[ItemDescription] END
        WHEN NULLIF(LTRIM(RTRIM(p.[Justification])), '') IS NULL THEN N'Purchase request pending approval.'
        WHEN LEN(p.[Justification]) > 120 THEN LEFT(p.[Justification], 120) + N'...'
        ELSE p.[Justification]
    END AS Summary,
    p.[RequestNumber] AS DisplayTag,
    dept.[Name] AS DisplayName,
    p.[DepartmentId],
    0 AS IsAssetRequest
FROM [PurchaseRequest] p
LEFT JOIN [Department] dept ON dept.[Id] = p.[DepartmentId]
WHERE p.[OrganizationId] = @OrganizationId
  AND p.[IsActive] = 1
  AND p.[ApprovalStatus] = @PendingStatus
  AND " + PurchaseRequestDepartmentScopeSql + @"

UNION ALL

SELECT
    N'Asset Request' AS ProcessName,
    r.[Id] AS RequestId,
    ISNULL(r.[FulfilledAssetId], 0) AS AssetId,
    ISNULL(NULLIF(LTRIM(RTRIM(r.[RequestedAssetTag])), ''), N'REQ-' + CAST(r.[Id] AS nvarchar(20))) AS AssetTag,
    ISNULL(NULLIF(LTRIM(RTRIM(ra.[AssetName])), ''), N'Employee asset request') AS AssetName,
    r.[RequestedById],
    r.[CreatedAt] AS RequestedDateUtc,
    1 AS CurrentApprovalStage,
    NULL AS ApprovalStageRoleIds,
    NULL AS ApprovalStageUserIds,
    CASE
        WHEN NULLIF(LTRIM(RTRIM(r.[Justification])), '') IS NULL THEN N'Asset request pending review.'
        WHEN LEN(r.[Justification]) > 120 THEN LEFT(r.[Justification], 120) + N'...'
        ELSE r.[Justification]
    END AS Summary,
    NULL AS DisplayTag,
    NULL AS DisplayName,
    r.[DepartmentId],
    1 AS IsAssetRequest
FROM [AssetRequest] r
LEFT JOIN [Asset] ra ON ra.[Id] = r.[RequestedAssetId]
WHERE r.[OrganizationId] = @OrganizationId
  AND r.[IsActive] = 1
  AND r.[Status] = @AssetRequestPendingStatus
  AND " + AssetRequestDepartmentScopeSql + @"

ORDER BY RequestedDateUtc DESC";

        private readonly ISqlConnectionFactory _connectionFactory;

        public PendingApprovalQueryRepository(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public int CountGlobalPending(
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            bool bypassPurchaseDepartmentScope,
            bool bypassAssetRequestDepartmentScope)
        {
            if (denyDepartmentScope)
            {
                return 0;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = CountSql;
                    AddScopeParameters(
                        command,
                        organizationId,
                        departmentId,
                        bypassDepartmentScope,
                        denyDepartmentScope,
                        bypassPurchaseDepartmentScope,
                        bypassAssetRequestDepartmentScope);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        public IList<PendingApprovalSourceRow> GetPendingSources(
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            bool bypassPurchaseDepartmentScope,
            bool bypassAssetRequestDepartmentScope)
        {
            if (denyDepartmentScope)
            {
                return new List<PendingApprovalSourceRow>();
            }

            var items = new List<PendingApprovalSourceRow>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = SourcesSql;
                    AddScopeParameters(
                        command,
                        organizationId,
                        departmentId,
                        bypassDepartmentScope,
                        denyDepartmentScope,
                        bypassPurchaseDepartmentScope,
                        bypassAssetRequestDepartmentScope);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new PendingApprovalSourceRow
                            {
                                ProcessName = SqlQueryHelper.GetString(reader, "ProcessName"),
                                RequestId = Convert.ToInt32(reader["RequestId"]),
                                AssetId = Convert.ToInt32(reader["AssetId"]),
                                AssetTag = SqlQueryHelper.GetString(reader, "AssetTag"),
                                AssetName = SqlQueryHelper.GetString(reader, "AssetName"),
                                RequestedById = SqlQueryHelper.GetString(reader, "RequestedById"),
                                RequestedDateUtc = Convert.ToDateTime(reader["RequestedDateUtc"]),
                                CurrentApprovalStage = Convert.ToInt32(reader["CurrentApprovalStage"]),
                                ApprovalStageRoleIds = SqlQueryHelper.GetString(reader, "ApprovalStageRoleIds"),
                                ApprovalStageUserIds = SqlQueryHelper.GetString(reader, "ApprovalStageUserIds"),
                                Summary = SqlQueryHelper.GetString(reader, "Summary"),
                                DisplayTag = SqlQueryHelper.GetString(reader, "DisplayTag"),
                                DisplayName = SqlQueryHelper.GetString(reader, "DisplayName"),
                                DepartmentId = reader["DepartmentId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["DepartmentId"]),
                                IsAssetRequest = Convert.ToInt32(reader["IsAssetRequest"]) == 1
                            });
                        }
                    }
                }
            }

            return items;
        }

        private static void AddScopeParameters(
            IDbCommand command,
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            bool bypassPurchaseDepartmentScope,
            bool bypassAssetRequestDepartmentScope)
        {
            SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
            SqlQueryHelper.AddDepartmentScopeParameters(command, bypassDepartmentScope, denyDepartmentScope, departmentId);
            SqlQueryHelper.AddParameter(command, "@BypassPurchaseDepartmentScope", bypassPurchaseDepartmentScope ? 1 : 0);
            SqlQueryHelper.AddParameter(command, "@BypassAssetRequestDepartmentScope", bypassAssetRequestDepartmentScope ? 1 : 0);
            SqlQueryHelper.AddParameter(command, "@PendingStatus", (int)ApprovalStatus.Pending);
            SqlQueryHelper.AddParameter(command, "@AssetRequestPendingStatus", (int)AssetRequestStatus.Pending);
        }
    }
}
