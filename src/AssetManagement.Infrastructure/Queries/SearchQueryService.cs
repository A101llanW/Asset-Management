using System;
using System.Collections.Generic;
using System.Data;
using AssetManagement.Application;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Queries
{
    public class SearchQueryService : ISearchQueryService
    {
        private static readonly string SearchSql = @"
SELECT TOP (@MaxResults)
    a.[Id] AS AssetId,
    a.[AssetTag],
    a.[AssetName],
    a.[SerialNumber],
    a.[CurrentStatus],
    d.[Name] AS DepartmentName,
    CASE
        WHEN NULLIF(LTRIM(RTRIM(ISNULL(u.[FirstName], '') + ' ' + ISNULL(u.[LastName], ''))), '') IS NOT NULL
        THEN LTRIM(RTRIM(ISNULL(u.[FirstName], '') + ' ' + ISNULL(u.[LastName], '')))
        ELSE u.[Email]
    END AS CustodianName,
    CASE WHEN a.[AssetTag] LIKE @Prefix THEN 1 ELSE 0 END AS MatchTag,
    CASE WHEN a.[SerialNumber] LIKE @Prefix THEN 1 ELSE 0 END AS MatchSerial,
    CASE WHEN a.[AssetName] LIKE @Prefix THEN 1 ELSE 0 END AS MatchName,
    CASE WHEN d.[Name] LIKE @Prefix THEN 1 ELSE 0 END AS MatchDepartment,
    CASE
        WHEN u.[Id] IS NOT NULL AND (
            (ISNULL(u.[FirstName], '') + ' ' + ISNULL(u.[LastName], '')) LIKE @Prefix
            OR u.[Email] LIKE @Prefix
        ) THEN 1
        ELSE 0
    END AS MatchCustodian
FROM [Asset] a
LEFT JOIN [Department] d ON d.[Id] = a.[DepartmentId]
LEFT JOIN [Users] u ON u.[Id] = a.[CurrentCustodianId]
WHERE a.[OrganizationId] = @OrganizationId
  AND a.[IsActive] = 1
  AND " + SqlQueryHelper.FormatAssetDepartmentScopeSql("a") + @"
  AND (
    a.[AssetTag] LIKE @Prefix
    OR a.[SerialNumber] LIKE @Prefix
    OR a.[AssetName] LIKE @Prefix
    OR d.[Name] LIKE @Prefix
    OR (ISNULL(u.[FirstName], '') + ' ' + ISNULL(u.[LastName], '')) LIKE @Prefix
    OR u.[Email] LIKE @Prefix
  )
ORDER BY a.[AssetTag] ASC, a.[Id] ASC";

        private readonly ISqlConnectionFactory _connectionFactory;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IDepartmentScopeService _departmentScope;

        public SearchQueryService(
            ISqlConnectionFactory connectionFactory,
            IOrganizationScopeService organizationScope,
            IDepartmentScopeService departmentScope)
        {
            _connectionFactory = connectionFactory;
            _organizationScope = organizationScope;
            _departmentScope = departmentScope;
        }

        public GlobalSearchResultVm GlobalSearch(string term, int maxResults)
        {
            var query = (term ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                return new GlobalSearchResultVm
                {
                    Query = query,
                    Assets = new List<GlobalSearchHitVm>(),
                    TotalCount = 0
                };
            }

            var take = maxResults <= 0 ? 25 : Math.Min(maxResults, 25);
            var scope = TenantQueryScope.Resolve(_organizationScope, _departmentScope);
            var hits = new List<GlobalSearchHitVm>();

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = SearchSql;
                    scope.AddScopeParameters(command);
                    SqlQueryHelper.AddParameter(command, "@Prefix", SqlQueryHelper.BuildPrefixPattern(query));
                    SqlQueryHelper.AddParameter(command, "@MaxResults", take);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            hits.Add(MapHit(reader));
                        }
                    }
                }
            }

            return new GlobalSearchResultVm
            {
                Query = query,
                Assets = hits,
                TotalCount = hits.Count
            };
        }

        private static GlobalSearchHitVm MapHit(IDataRecord record)
        {
            var reasons = new List<string>();
            if (Convert.ToInt32(record["MatchTag"]) == 1)
            {
                reasons.Add("Asset tag");
            }

            if (Convert.ToInt32(record["MatchSerial"]) == 1)
            {
                reasons.Add("Serial number");
            }

            if (Convert.ToInt32(record["MatchName"]) == 1)
            {
                reasons.Add("Asset name");
            }

            if (Convert.ToInt32(record["MatchDepartment"]) == 1)
            {
                reasons.Add("Department");
            }

            if (Convert.ToInt32(record["MatchCustodian"]) == 1)
            {
                reasons.Add("Custodian");
            }

            return new GlobalSearchHitVm
            {
                AssetId = Convert.ToInt32(record["AssetId"]),
                AssetTag = SqlQueryHelper.GetString(record, "AssetTag"),
                AssetName = SqlQueryHelper.GetString(record, "AssetName"),
                SerialNumber = SqlQueryHelper.GetString(record, "SerialNumber"),
                DepartmentName = SqlQueryHelper.GetString(record, "DepartmentName"),
                CustodianName = SqlQueryHelper.GetString(record, "CustodianName") ?? DisplayText.Empty,
                Status = ((AssetStatus)Convert.ToInt32(record["CurrentStatus"])).ToString(),
                MatchReason = string.Join(", ", reasons.ToArray())
            };
        }
    }
}
