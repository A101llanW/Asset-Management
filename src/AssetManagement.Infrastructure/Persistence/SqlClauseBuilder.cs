using System;

namespace AssetManagement.Infrastructure.Persistence
{
    public static class SqlClauseBuilder
    {
        public static void AppendCondition(ref string sql, string condition)
        {
            if (string.IsNullOrWhiteSpace(sql) || string.IsNullOrWhiteSpace(condition))
            {
                return;
            }

            sql += HasWhereClause(sql)
                ? " AND " + condition
                : " WHERE " + condition;
        }

        public static bool HasWhereClause(string sql)
        {
            return !string.IsNullOrEmpty(sql)
                && sql.IndexOf(" WHERE ", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
