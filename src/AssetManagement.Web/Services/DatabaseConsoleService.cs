using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Services
{
    /// <summary>
    /// SuperAdmin SQL console runner. Executes arbitrary SQL via ADO with multi-result support.
    /// Display capped at 500 rows per result set; command timeout 120s.
    /// </summary>
    public class DatabaseConsoleService
    {
        public const int MaxDisplayRows = 500;
        private const int CommandTimeoutSeconds = 120;

        private readonly ISqlConnectionFactory _connectionFactory;

        public DatabaseConsoleService(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public DatabaseConsoleViewModel Execute(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                return ErrorResult(sql, "SQL cannot be empty.");
            }

            var trimmed = sql.Trim();
            var sw = Stopwatch.StartNew();
            var model = new DatabaseConsoleViewModel { Sql = trimmed };

            try
            {
                using (var connection = _connectionFactory.CreateConnection())
                {
                    connection.Open();
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = trimmed;
                        cmd.CommandTimeout = CommandTimeoutSeconds;

                        int rowsAffected;
                        model.ResultSets = ReadAllResults(cmd, out rowsAffected);
                        model.RowsAffected = rowsAffected > 0 ? rowsAffected : (int?)null;

                        if (model.ResultSets.Count > 0)
                        {
                            model.Message = string.Format(
                                "{0} result set(s) returned.",
                                model.ResultSets.Count);
                        }
                        else if (model.RowsAffected.HasValue)
                        {
                            model.Message = string.Format(
                                "{0} row(s) affected.",
                                model.RowsAffected.Value);
                        }
                        else
                        {
                            model.Message = "Command completed successfully.";
                        }
                    }
                }

                model.IsError = false;
            }
            catch (Exception ex)
            {
                model = ErrorResult(trimmed, ex.Message);
            }

            sw.Stop();
            model.ExecutionTimeMs = sw.ElapsedMilliseconds;
            return model;
        }

        public void LoadSchema(DatabaseConsoleViewModel model)
        {
            if (model == null)
            {
                return;
            }

            try
            {
                using (var connection = _connectionFactory.CreateConnection())
                {
                    connection.Open();
                    model.DatabaseName = GetScalarString(connection, "SELECT DB_NAME()");
                    model.Tables = GetTableNames(connection);
                }
            }
            catch
            {
                model.DatabaseName = model.DatabaseName ?? "Unknown";
            }
        }

        private static List<QueryResultSetViewModel> ReadAllResults(DbCommand cmd, out int totalRowsAffected)
        {
            var resultSets = new List<QueryResultSetViewModel>();
            totalRowsAffected = 0;
            var setIndex = 0;

            using (var reader = cmd.ExecuteReader())
            {
                do
                {
                    if (reader.FieldCount > 0)
                    {
                        setIndex++;
                        resultSets.Add(ReadResultSet(reader, setIndex));
                    }
                    else if (reader.RecordsAffected >= 0)
                    {
                        totalRowsAffected += reader.RecordsAffected;
                    }
                }
                while (reader.NextResult());
            }

            return resultSets;
        }

        private static QueryResultSetViewModel ReadResultSet(IDataReader reader, int setIndex)
        {
            var result = new QueryResultSetViewModel { Index = setIndex };

            for (var i = 0; i < reader.FieldCount; i++)
            {
                result.Columns.Add(reader.GetName(i));
            }

            var rowCount = 0;
            while (reader.Read())
            {
                rowCount++;
                if (result.Rows.Count < MaxDisplayRows)
                {
                    var row = new List<string>();
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        row.Add(reader.IsDBNull(i) ? "NULL" : Convert.ToString(reader.GetValue(i)));
                    }

                    result.Rows.Add(row);
                }
            }

            result.TotalRowCount = rowCount;
            result.Truncated = rowCount > MaxDisplayRows;
            return result;
        }

        private static List<string> GetTableNames(DbConnection connection)
        {
            var tables = new List<string>();

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
SELECT TABLE_SCHEMA + '.' + TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE'
ORDER BY TABLE_SCHEMA, TABLE_NAME";
                cmd.CommandTimeout = 30;

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tables.Add(reader.GetString(0));
                    }
                }
            }

            return tables;
        }

        private static string GetScalarString(DbConnection connection, string sql)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.CommandTimeout = 10;
                var value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? null : Convert.ToString(value);
            }
        }

        private static DatabaseConsoleViewModel ErrorResult(string sql, string message)
        {
            return new DatabaseConsoleViewModel
            {
                Sql = sql,
                Message = message,
                IsError = true
            };
        }
    }
}
