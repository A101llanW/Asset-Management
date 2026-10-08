using System;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.DTOs;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Queries
{
    public class DepartmentKindCountQuery : IDepartmentKindCountQuery
    {
        // Single pass over the org's Department rows (conditional aggregation by kind); no N+1.
        private const string CountsSql = @"
SELECT
    ISNULL(SUM(CASE WHEN d.[IsActive] = 1 AND d.[DepartmentKind] = @AdminKind AND d.[ParentDepartmentId] IS NULL THEN 1 ELSE 0 END), 0) AS Departments,
    ISNULL(SUM(CASE WHEN d.[IsActive] = 1 AND d.[DepartmentKind] = @AdminKind AND d.[ParentDepartmentId] IS NOT NULL THEN 1 ELSE 0 END), 0) AS NestedAdministrative,
    ISNULL(SUM(CASE WHEN d.[IsActive] = 1 AND d.[DepartmentKind] = @SubKind THEN 1 ELSE 0 END), 0) AS SubDepartments,
    ISNULL(SUM(CASE WHEN d.[IsActive] = 1 AND d.[DepartmentKind] = @RoomKind THEN 1 ELSE 0 END), 0) AS Rooms,
    ISNULL(SUM(CASE WHEN d.[IsActive] = 1 AND d.[DepartmentKind] = @GradeKind THEN 1 ELSE 0 END), 0) AS Grades,
    ISNULL(SUM(CASE WHEN d.[IsActive] = 1 AND d.[DepartmentKind] = @ClassKind THEN 1 ELSE 0 END), 0) AS Classes,
    ISNULL(SUM(CASE WHEN d.[IsActive] = 1 THEN 1 ELSE 0 END), 0) AS ActiveTotal,
    ISNULL(SUM(CASE WHEN d.[IsActive] = 0 THEN 1 ELSE 0 END), 0) AS InactiveTotal
FROM [Department] d
WHERE d.[OrganizationId] = @OrganizationId";

        private readonly ISqlConnectionFactory _connectionFactory;

        public DepartmentKindCountQuery(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public DepartmentKindCountsDto GetCounts(int organizationId)
        {
            var result = new DepartmentKindCountsDto();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = CountsSql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    SqlQueryHelper.AddParameter(command, "@AdminKind", (int)DepartmentKind.Administrative);
                    SqlQueryHelper.AddParameter(command, "@SubKind", (int)DepartmentKind.SubDepartment);
                    SqlQueryHelper.AddParameter(command, "@RoomKind", (int)DepartmentKind.Room);
                    SqlQueryHelper.AddParameter(command, "@GradeKind", (int)DepartmentKind.Grade);
                    SqlQueryHelper.AddParameter(command, "@ClassKind", (int)DepartmentKind.Class);
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            result.Departments = Convert.ToInt32(reader["Departments"]);
                            result.NestedAdministrative = Convert.ToInt32(reader["NestedAdministrative"]);
                            result.SubDepartments = Convert.ToInt32(reader["SubDepartments"]);
                            result.Rooms = Convert.ToInt32(reader["Rooms"]);
                            result.Grades = Convert.ToInt32(reader["Grades"]);
                            result.Classes = Convert.ToInt32(reader["Classes"]);
                            result.ActiveTotal = Convert.ToInt32(reader["ActiveTotal"]);
                            result.InactiveTotal = Convert.ToInt32(reader["InactiveTotal"]);
                        }
                    }
                }
            }

            return result;
        }
    }
}
