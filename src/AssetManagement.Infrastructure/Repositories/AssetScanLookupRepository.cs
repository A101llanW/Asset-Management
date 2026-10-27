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

        // rel1013b: old tags kept by the rename tool (AssetTagAlias, active rows) still resolve to the asset.
        private static readonly string AliasMatchSql = "EXISTS (SELECT 1 FROM [AssetTagAlias] al WHERE al.[AssetId] = a.[Id] AND al.[OrganizationId] = a.[OrganizationId] AND al.[IsActive] = 1 AND "
            + string.Format(NormalizeScanKeySql, "al.[AliasTag]") + " = @LookupKey)";

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
    END AS CustodianName,
    a.[CurrentCustodianId] AS CustodianUserId,
    NULLIF(LTRIM(RTRIM(ISNULL(u.[FirstName], '') + ' ' + ISNULL(u.[LastName], ''))), '') AS CustodianPersonName,
    u.[Email] AS CustodianEmail,
    t.[Name] AS AssetTypeName,
    st.[Name] AS AssetSubTypeName,
    a.[Condition],
    a.[PurchaseDate],
    (SELECT MIN(r.[ReceivedDate]) FROM [AssetReceiving] r
      WHERE r.[AssetId] = a.[Id] AND r.[OrganizationId] = a.[OrganizationId] AND r.[IsActive] = 1) AS ReceivedDate,
    a.[DepartmentId],
    d.[Section],
    CASE WHEN EXISTS (SELECT 1 FROM [Department] dp
      WHERE dp.[OrganizationId] = a.[OrganizationId] AND dp.[IsActive] = 1 AND dp.[Section] = 1) THEN 1 ELSE 0 END AS OrganizationUsesPrep,
    ph.[FilePath] AS PhotoFilePath,
    ph.[ContentType] AS PhotoContentType,
    ph.[DocumentType] AS PhotoDocumentType,
    a.[AcquisitionCost],
    a.[Currency],
    s.[SupplierName]
FROM [Asset] a
LEFT JOIN [Department] d ON d.[Id] = a.[DepartmentId]
LEFT JOIN [AssetCategory] c ON c.[Id] = a.[CategoryId]
LEFT JOIN [AssetType] t ON t.[Id] = a.[AssetTypeId]
LEFT JOIN [AssetSubType] st ON st.[Id] = a.[AssetSubTypeId]
LEFT JOIN [Supplier] s ON s.[Id] = a.[SupplierId]
LEFT JOIN [Users] u ON u.[Id] = a.[CurrentCustodianId]
OUTER APPLY (
    SELECT TOP 1 doc.[FilePath], doc.[ContentType], doc.[DocumentType]
    FROM [AssetDocument] doc
    WHERE doc.[AssetId] = a.[Id] AND doc.[OrganizationId] = a.[OrganizationId] AND doc.[IsActive] = 1
      AND doc.[ContentType] IN ('image/jpeg', 'image/pjpeg', 'image/png', 'image/gif')
      AND (doc.[DocumentType] LIKE 'Asset photo%' OR doc.[DocumentType] IN ('Location photo', 'Serial plate photo'))
    ORDER BY CASE WHEN doc.[DocumentType] LIKE 'Asset photo%' THEN 0 ELSE 1 END, doc.[UploadedAt] DESC
) ph
WHERE a.[IsActive] = 1
  AND a.[OrganizationId] = @OrganizationId
  AND (@DepartmentId IS NULL OR a.[DepartmentId] = @DepartmentId)
  AND ({0} = @LookupKey OR {1} = @LookupKey OR {2} = @LookupKey OR {3})
ORDER BY CASE
    WHEN {0} = @LookupKey THEN 0
    WHEN {1} = @LookupKey THEN 1
    WHEN {2} = @LookupKey THEN 2
    ELSE 3
END";

        // rel1027: the asset's unit and its parents (same organisation), top level first.
        private const string LocationPathSql = @"
WITH chain AS (
    SELECT d.[Id], d.[Name], d.[DepartmentKind], d.[ParentDepartmentId], 0 AS Depth
    FROM [Department] d
    WHERE d.[Id] = @DepartmentId AND d.[OrganizationId] = @OrganizationId
    UNION ALL
    SELECT p.[Id], p.[Name], p.[DepartmentKind], p.[ParentDepartmentId], c.Depth + 1
    FROM [Department] p
    INNER JOIN chain c ON p.[Id] = c.[ParentDepartmentId]
    WHERE p.[OrganizationId] = @OrganizationId AND c.Depth < 12
)
SELECT [Name], [DepartmentKind] FROM chain ORDER BY Depth DESC
OPTION (MAXRECURSION 20)";

        private const string ExistsSql = @"
SELECT TOP 1 1
FROM [Asset] a
WHERE a.[IsActive] = 1
  AND a.[OrganizationId] = @OrganizationId
  AND ({0} = @LookupKey OR {1} = @LookupKey OR {2} = @LookupKey OR {3})";

        private readonly string _lookupSql;
        private readonly string _existsSql;
        private readonly string _lookupSqlNoSerial;
        private readonly string _existsSqlNoSerial;
        private readonly ISqlConnectionFactory _connectionFactory;

        // Review S8: NULL never equals @LookupKey, so the serial-number branch matches nothing for public viewers.
        private const string NoSerialKeySql = "NULL";

        public AssetScanLookupRepository(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
            _lookupSql = string.Format(LookupSql, AssetTagKeySql, BarcodeKeySql, SerialKeySql, AliasMatchSql);
            _existsSql = string.Format(ExistsSql, AssetTagKeySql, BarcodeKeySql, SerialKeySql, AliasMatchSql);
            _lookupSqlNoSerial = string.Format(LookupSql, AssetTagKeySql, BarcodeKeySql, NoSerialKeySql, AliasMatchSql);
            _existsSqlNoSerial = string.Format(ExistsSql, AssetTagKeySql, BarcodeKeySql, NoSerialKeySql, AliasMatchSql);
        }

        public bool ExistsByScanCode(string code, int organizationId, bool matchSerialNumber = true)
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
                    command.CommandText = matchSerialNumber ? _existsSql : _existsSqlNoSerial;
                    AddParameter(command, "@LookupKey", code.Trim());
                    AddParameter(command, "@OrganizationId", organizationId);
                    var result = command.ExecuteScalar();
                    return result != null && result != DBNull.Value;
                }
            }
        }

        public AssetScanLookupResult FindByScanCode(string code, int organizationId, int? departmentId, bool matchSerialNumber = true)
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
                    command.CommandText = matchSerialNumber ? _lookupSql : _lookupSqlNoSerial;
                    AddParameter(command, "@LookupKey", code.Trim());
                    AddParameter(command, "@OrganizationId", organizationId);
                    AddParameter(command, "@DepartmentId", departmentId.HasValue ? (object)departmentId.Value : DBNull.Value);

                    AssetScanLookupResult result;
                    using (var reader = command.ExecuteReader())
                    {
                        result = reader.Read() ? MapResult(reader) : null;
                    }

                    if (result != null && result.DepartmentId.HasValue)
                    {
                        result.LocationPath = ReadLocationPath(connection, result.DepartmentId.Value, organizationId);
                    }

                    return result;
                }
            }
        }

        private static System.Collections.Generic.List<AssetScanLocationLevel> ReadLocationPath(IDbConnection connection, int departmentId, int organizationId)
        {
            var levels = new System.Collections.Generic.List<AssetScanLocationLevel>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = LocationPathSql;
                AddParameter(command, "@DepartmentId", departmentId);
                AddParameter(command, "@OrganizationId", organizationId);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        levels.Add(new AssetScanLocationLevel
                        {
                            Name = GetString(reader, "Name"),
                            Kind = (DepartmentKind)Convert.ToInt32(reader["DepartmentKind"])
                        });
                    }
                }
            }

            return levels;
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
                CustodianName = GetString(record, "CustodianName"),
                CustodianUserId = GetString(record, "CustodianUserId"),
                CustodianPersonName = GetString(record, "CustodianPersonName"),
                CustodianEmail = GetString(record, "CustodianEmail"),
                AssetTypeName = GetString(record, "AssetTypeName"),
                AssetSubTypeName = GetString(record, "AssetSubTypeName"),
                Condition = record["Condition"] == DBNull.Value ? (AssetCondition?)null : (AssetCondition)Convert.ToInt32(record["Condition"]),
                PurchaseDate = record["PurchaseDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(record["PurchaseDate"]),
                ReceivedDate = record["ReceivedDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(record["ReceivedDate"]),
                DepartmentId = record["DepartmentId"] == DBNull.Value ? (int?)null : Convert.ToInt32(record["DepartmentId"]),
                Section = record["Section"] == DBNull.Value ? (DepartmentSection?)null : (DepartmentSection)Convert.ToInt32(record["Section"]),
                OrganizationUsesPrep = Convert.ToInt32(record["OrganizationUsesPrep"]) == 1,
                PhotoFilePath = GetString(record, "PhotoFilePath"),
                PhotoContentType = GetString(record, "PhotoContentType"),
                PhotoDocumentType = GetString(record, "PhotoDocumentType"),
                AcquisitionCost = record["AcquisitionCost"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(record["AcquisitionCost"]),
                Currency = GetString(record, "Currency"),
                SupplierName = GetString(record, "SupplierName")
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
