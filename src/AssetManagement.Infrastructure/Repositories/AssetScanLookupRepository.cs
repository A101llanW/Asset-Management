using System;
using System.Data;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Repositories
{
    public class AssetScanLookupRepository : IAssetScanLookupRepository
    {
        private const string NormalizeScanKeySql = "UPPER(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(ISNULL({0}, ''))), ' ', ''), CHAR(9), ''), '-', ''))";

        private static readonly string AssetTagKeySql = string.Format(NormalizeScanKeySql, "a.[AssetTag]");
        private static readonly string BarcodeKeySql = string.Format(NormalizeScanKeySql, "a.[BarcodeOrQRCode]");
        private static readonly string SerialKeySql = string.Format(NormalizeScanKeySql, "a.[SerialNumber]");

        private const string LookupSql = @"
SELECT TOP 1
    a.[Id],
    a.[AssetTag],
    a.[AssetName],
    a.[SerialNumber],
    a.[Brand],
    a.[Model],
    a.[CurrentStatus],
    a.[BarcodeOrQRCode],
    d.[Name] AS DepartmentName,
    c.[Name] AS CategoryName,
    CASE
        WHEN NULLIF(LTRIM(RTRIM(ISNULL(u.[FirstName], '') + ' ' + ISNULL(u.[LastName], ''))), '') IS NOT NULL
        THEN LTRIM(RTRIM(ISNULL(u.[FirstName], '') + ' ' + ISNULL(u.[LastName], '')))
        ELSE u.[Email]
    END AS CustodianName
FROM [Asset] a
LEFT JOIN [Department] d ON d.[Id] = a.[DepartmentId]
LEFT JOIN [AssetCategory] c ON c.[Id] = a.[CategoryId]
LEFT JOIN [Users] u ON u.[Id] = a.[CurrentCustodianId]
WHERE a.[IsActive] = 1
  AND a.[OrganizationId] = @OrganizationId
  AND (@DepartmentId IS NULL OR a.[DepartmentId] = @DepartmentId)
  AND ({0} = @LookupKey OR {1} = @LookupKey OR {2} = @LookupKey)
ORDER BY CASE
    WHEN {0} = @LookupKey THEN 0
    WHEN {1} = @LookupKey THEN 1
    WHEN {2} = @LookupKey THEN 2
    ELSE 3
END";

        private const string ExistsSql = @"
SELECT TOP 1 1
FROM [Asset] a
WHERE a.[IsActive] = 1
  AND a.[OrganizationId] = @OrganizationId
  AND ({0} = @LookupKey OR {1} = @LookupKey OR {2} = @LookupKey)";

        private readonly string _lookupSql;
        private readonly string _existsSql;
        private readonly ISqlConnectionFactory _connectionFactory;

        public AssetScanLookupRepository(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
            _lookupSql = string.Format(LookupSql, AssetTagKeySql, BarcodeKeySql, SerialKeySql);
            _existsSql = string.Format(ExistsSql, AssetTagKeySql, BarcodeKeySql, SerialKeySql);
        }

        public bool ExistsByScanCode(string code, int organizationId)
        {
            if (string.IsNullOrWhiteSpace(code) || organizationId <= 0)
            {
                return false;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = _existsSql;
                    AddParameter(command, "@LookupKey", code.Trim());
                    AddParameter(command, "@OrganizationId", organizationId);
                    var result = command.ExecuteScalar();
                    return result != null && result != DBNull.Value;
                }
            }
        }

        public AssetScanLookupResult FindByScanCode(string code, int organizationId, int? departmentId)
        {
            if (string.IsNullOrWhiteSpace(code) || organizationId <= 0)
            {
                return null;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = _lookupSql;
                    AddParameter(command, "@LookupKey", code.Trim());
                    AddParameter(command, "@OrganizationId", organizationId);
                    AddParameter(command, "@DepartmentId", departmentId.HasValue ? (object)departmentId.Value : DBNull.Value);

                    using (var reader = command.ExecuteReader())
                    {
                        return reader.Read() ? MapResult(reader) : null;
                    }
                }
            }
        }

        private static AssetScanLookupResult MapResult(IDataRecord record)
        {
            return new AssetScanLookupResult
            {
                Id = Convert.ToInt32(record["Id"]),
                AssetTag = GetString(record, "AssetTag"),
                AssetName = GetString(record, "AssetName"),
                CurrentStatus = (AssetStatus)Convert.ToInt32(record["CurrentStatus"]),
                BarcodeOrQRCode = GetString(record, "BarcodeOrQRCode"),
                DepartmentName = GetString(record, "DepartmentName"),
                SerialNumber = GetString(record, "SerialNumber"),
                Brand = GetString(record, "Brand"),
                Model = GetString(record, "Model"),
                CategoryName = GetString(record, "CategoryName"),
                CustodianName = GetString(record, "CustodianName")
            };
        }

        private static string GetString(IDataRecord record, string columnName)
        {
            var value = record[columnName];
            return value == DBNull.Value ? null : value.ToString();
        }

        private static void AddParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
}
