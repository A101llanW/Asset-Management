using System;
using System.Data;

namespace AssetManagement.Infrastructure.Queries
{
    public static class SqlQueryHelper
    {
        public const string AssetDepartmentScopeSql =
            "(@BypassDepartmentScope = 1 OR (@DenyDepartmentScope = 0 AND @DepartmentId IS NOT NULL AND {0}.[DepartmentId] = @DepartmentId))";

        public static string FormatAssetDepartmentScopeSql(string tableAlias)
        {
            return string.Format(AssetDepartmentScopeSql, tableAlias);
        }

        public static void AddDepartmentScopeParameters(
            IDbCommand command,
            bool bypassesDepartmentScope,
            bool denyDepartmentScope,
            int? departmentId)
        {
            AddParameter(command, "@BypassDepartmentScope", bypassesDepartmentScope ? 1 : 0);
            AddParameter(command, "@DenyDepartmentScope", denyDepartmentScope ? 1 : 0);
            AddParameter(command, "@DepartmentId", departmentId.HasValue ? (object)departmentId.Value : DBNull.Value);
        }

        public static void AddParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        public static string GetString(IDataRecord record, string columnName)
        {
            var value = record[columnName];
            return value == DBNull.Value ? null : value.ToString();
        }

        public static string BuildPrefixPattern(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return null;
            }

            return term.Trim() + "%";
        }

        public static string BuildContainsPattern(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return null;
            }

            return "%" + term.Trim() + "%";
        }
    }
}
