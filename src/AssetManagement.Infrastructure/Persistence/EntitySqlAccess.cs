using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using AssetManagement.Application.DTOs;

namespace AssetManagement.Infrastructure.Persistence
{
    public sealed class SqlWriteOptions
    {
        public SqlTransaction Transaction { get; set; }

        public int? RequireApprovalStatus { get; set; }

        public int? RequireApprovalStage { get; set; }
    }

    public static class EntitySqlReader
    {
        public static IList<T> ReadAll<T>(SqlConnection connection, EntityMap map, SqlTransaction transaction = null) where T : class, new()
        {
            var results = new List<T>();
            using (var command = connection.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Transaction = transaction;
                }

                command.CommandText = "SELECT * FROM [" + map.TableName + "]";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(ReadRow<T>(reader, map));
                    }
                }
            }

            return results;
        }

        public static T ReadById<T>(SqlConnection connection, EntityMap map, object id, int? organizationId, bool applyOrganizationFilter, SqlTransaction transaction = null) where T : class, new()
        {
            using (var command = connection.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Transaction = transaction;
                }

                var sql = "SELECT * FROM [" + map.TableName + "] WHERE [" + map.PrimaryKey + "]=@Id";
                if (applyOrganizationFilter && organizationId.HasValue)
                {
                    sql += " AND [OrganizationId]=@OrganizationId";
                }

                command.CommandText = sql;
                AddReadParameter(command, "@Id", id);
                if (applyOrganizationFilter && organizationId.HasValue)
                {
                    AddReadParameter(command, "@OrganizationId", organizationId.Value);
                }

                using (var reader = command.ExecuteReader())
                {
                    return reader.Read() ? ReadRow<T>(reader, map) : null;
                }
            }
        }

        private static void AddReadParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            if (value == null)
            {
                parameter.Value = DBNull.Value;
            }
            else if (value.GetType().IsEnum)
            {
                parameter.Value = (int)value;
            }
            else
            {
                parameter.Value = value;
            }

            command.Parameters.Add(parameter);
        }

        public static T ReadRow<T>(IDataRecord record, EntityMap map) where T : class, new()
        {
            var entity = new T();
            foreach (var property in map.ScalarProperties)
            {
                var value = record[property.Name];
                if (value == DBNull.Value)
                {
                    property.SetValue(entity, GetDefault(property.PropertyType), null);
                }
                else
                {
                    property.SetValue(entity, ConvertValue(value, property.PropertyType), null);
                }
            }

            return entity;
        }

        private static object ConvertValue(object value, Type propertyType)
        {
            var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            if (targetType.IsEnum)
            {
                return Enum.ToObject(targetType, value);
            }

            if (targetType == typeof(byte[]) && value is byte[])
            {
                return value;
            }

            return Convert.ChangeType(value, targetType);
        }

        private static object GetDefault(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }

    public static class EntitySqlWriter
    {
        public static void Insert(SqlConnection connection, EntityMap map, object entity, SqlWriteOptions options = null)
        {
            var primaryKeyProperty = map.EntityType.GetProperty(map.PrimaryKey);
            var primaryKeyValue = primaryKeyProperty.GetValue(entity, null);
            if (!map.PrimaryKeyIsIdentity && (primaryKeyValue == null || (primaryKeyValue is string && string.IsNullOrEmpty((string)primaryKeyValue))))
            {
                primaryKeyProperty.SetValue(entity, Guid.NewGuid().ToString(), null);
            }

            var columns = map.ScalarProperties
                .Where(x => x.Name != map.PrimaryKey || !map.PrimaryKeyIsIdentity)
                .Where(x => x.Name != "RowVersion")
                .ToList();
            using (var command = connection.CreateCommand())
            {
                if (options != null && options.Transaction != null)
                {
                    command.Transaction = options.Transaction;
                }

                var columnNames = string.Join(", ", columns.Select(x => "[" + x.Name + "]"));
                var parameterNames = string.Join(", ", columns.Select(x => "@" + x.Name));
                command.CommandText = "INSERT INTO [" + map.TableName + "] (" + columnNames + ") VALUES (" + parameterNames + ")";
                if (map.PrimaryKeyIsIdentity)
                {
                    command.CommandText += "; SELECT CAST(SCOPE_IDENTITY() AS int);";
                }

                AddParameters(command, columns, entity);

                if (map.PrimaryKeyIsIdentity)
                {
                    var newId = command.ExecuteScalar();
                    primaryKeyProperty.SetValue(entity, Convert.ChangeType(newId, primaryKeyProperty.PropertyType), null);
                }
                else
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        public static void Update(SqlConnection connection, EntityMap map, object entity, SqlWriteOptions options = null)
        {
            var columns = map.ScalarProperties.Where(x => x.Name != map.PrimaryKey && x.Name != "RowVersion").ToList();
            var rowVersionProperty = map.ScalarProperties.FirstOrDefault(x => x.Name == "RowVersion");
            var capturedRowVersion = rowVersionProperty == null ? null : rowVersionProperty.GetValue(entity, null) as byte[];

            using (var command = connection.CreateCommand())
            {
                if (options != null && options.Transaction != null)
                {
                    command.Transaction = options.Transaction;
                }

                var setClause = string.Join(", ", columns.Select(x => "[" + x.Name + "]=@" + x.Name));
                var whereClause = "[" + map.PrimaryKey + "]=@" + map.PrimaryKey;
                if (capturedRowVersion != null && capturedRowVersion.Length > 0)
                {
                    whereClause += " AND [RowVersion]=@RowVersion";
                }

                if (options != null && options.RequireApprovalStatus.HasValue)
                {
                    whereClause += " AND [ApprovalStatus]=@RequireApprovalStatus";
                }

                if (options != null && options.RequireApprovalStage.HasValue)
                {
                    whereClause += " AND [CurrentApprovalStage]=@RequireApprovalStage";
                }

                if (rowVersionProperty != null)
                {
                    command.CommandText = "UPDATE [" + map.TableName + "] SET " + setClause
                        + " OUTPUT inserted.[RowVersion] WHERE " + whereClause;
                }
                else
                {
                    command.CommandText = "UPDATE [" + map.TableName + "] SET " + setClause + " WHERE " + whereClause;
                }

                AddParameters(command, columns, entity);
                AddParameter(command, map.PrimaryKey, map.EntityType.GetProperty(map.PrimaryKey).GetValue(entity, null));
                if (capturedRowVersion != null && capturedRowVersion.Length > 0)
                {
                    AddParameter(command, "RowVersion", capturedRowVersion);
                }

                if (options != null && options.RequireApprovalStatus.HasValue)
                {
                    AddParameter(command, "RequireApprovalStatus", options.RequireApprovalStatus.Value);
                }

                if (options != null && options.RequireApprovalStage.HasValue)
                {
                    AddParameter(command, "RequireApprovalStage", options.RequireApprovalStage.Value);
                }

                if (rowVersionProperty != null)
                {
                    var result = command.ExecuteScalar();
                    if (result == null || result == DBNull.Value)
                    {
                        throw new ConcurrencyException("The record was modified by another user. Refresh and try again.");
                    }

                    rowVersionProperty.SetValue(entity, (byte[])result, null);
                    return;
                }

                var affected = command.ExecuteNonQuery();
                if (options != null
                    && (options.RequireApprovalStatus.HasValue || options.RequireApprovalStage.HasValue)
                    && affected == 0)
                {
                    throw new BusinessException("This approval request is no longer pending at the expected stage.");
                }

                if (capturedRowVersion != null && capturedRowVersion.Length > 0 && affected == 0)
                {
                    throw new ConcurrencyException("The record was modified by another user. Refresh and try again.");
                }
            }
        }

        public static void Delete(SqlConnection connection, EntityMap map, object entity, SqlWriteOptions options = null)
        {
            using (var command = connection.CreateCommand())
            {
                if (options != null && options.Transaction != null)
                {
                    command.Transaction = options.Transaction;
                }

                command.CommandText = "DELETE FROM [" + map.TableName + "] WHERE [" + map.PrimaryKey + "]=@" + map.PrimaryKey;
                AddParameter(command, map.PrimaryKey, map.EntityType.GetProperty(map.PrimaryKey).GetValue(entity, null));
                command.ExecuteNonQuery();
            }
        }

        public static int GetRemainingPurchaseQuantity(SqlConnection connection, SqlTransaction transaction, int purchaseRecordId)
        {
            using (var command = connection.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Transaction = transaction;
                }

                command.CommandText = @"
SELECT pr.[Quantity] - ISNULL((
    SELECT SUM(ar.[QuantityReceived])
    FROM [AssetReceiving] ar WITH (UPDLOCK, HOLDLOCK)
    WHERE ar.[PurchaseRecordId] = pr.[Id] AND ar.[IsActive] = 1
), 0)
FROM [PurchaseRecord] pr WITH (UPDLOCK, HOLDLOCK)
WHERE pr.[Id] = @PurchaseRecordId";
                AddParameter(command, "PurchaseRecordId", purchaseRecordId);
                var result = command.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                {
                    return 0;
                }

                return Convert.ToInt32(result);
            }
        }

        private static void AddParameters(SqlCommand command, IEnumerable<PropertyInfo> properties, object entity)
        {
            foreach (var property in properties)
            {
                AddParameter(command, property.Name, property.GetValue(entity, null));
            }
        }

        private static void AddParameter(SqlCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@" + name;
            if (value == null)
            {
                parameter.Value = DBNull.Value;
            }
            else if (value.GetType().IsEnum)
            {
                parameter.Value = (int)value;
            }
            else
            {
                parameter.Value = value;
            }

            command.Parameters.Add(parameter);
        }
    }
}
