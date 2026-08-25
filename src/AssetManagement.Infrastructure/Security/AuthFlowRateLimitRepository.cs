using System;
using System.Data;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Security
{
    public class AuthFlowRateLimitRepository
    {
        private readonly ISqlConnectionFactory _connectionFactory;

        public AuthFlowRateLimitRepository(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public bool TryAcquireCounter(string scopeKey, int maxRequests, TimeSpan window)
        {
            if (string.IsNullOrWhiteSpace(scopeKey))
            {
                return true;
            }

            var now = DateTime.UtcNow;
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    var state = LoadState(connection, transaction, scopeKey);
                    if (state == null)
                    {
                        InsertCounterState(connection, transaction, scopeKey, now, 1);
                        transaction.Commit();
                        return true;
                    }

                    if (!state.WindowStartUtc.HasValue || now - state.WindowStartUtc.Value >= window)
                    {
                        UpdateCounterState(connection, transaction, scopeKey, now, 1);
                        transaction.Commit();
                        return true;
                    }

                    if (state.Counter >= maxRequests)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    UpdateCounterState(connection, transaction, scopeKey, state.WindowStartUtc.Value, state.Counter + 1);
                    transaction.Commit();
                    return true;
                }
            }
        }

        public bool IsLockoutActive(string scopeKey, out int minutesRemaining)
        {
            minutesRemaining = 0;
            if (string.IsNullOrWhiteSpace(scopeKey))
            {
                return true;
            }

            var now = DateTime.UtcNow;
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                var lockedUntilUtc = LoadLockedUntilUtc(connection, null, scopeKey);
                if (!lockedUntilUtc.HasValue)
                {
                    return true;
                }

                if (lockedUntilUtc.Value > now)
                {
                    minutesRemaining = Math.Max(1, (int)Math.Ceiling((lockedUntilUtc.Value - now).TotalMinutes));
                    return false;
                }

                ClearLockoutState(connection, null, scopeKey);
                return true;
            }
        }

        public void RecordFailure(string scopeKey, int maxFailures, TimeSpan failureWindow, TimeSpan lockoutDuration)
        {
            if (string.IsNullOrWhiteSpace(scopeKey))
            {
                return;
            }

            var now = DateTime.UtcNow;
            var cutoffUtc = now - failureWindow;
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    InsertFailure(connection, transaction, scopeKey, now);
                    DeleteFailuresBefore(connection, transaction, scopeKey, cutoffUtc);

                    var failureCount = CountFailuresSince(connection, transaction, scopeKey, cutoffUtc);
                    if (failureCount >= maxFailures)
                    {
                        UpsertLockout(connection, transaction, scopeKey, now.Add(lockoutDuration));
                        DeleteFailuresForScope(connection, transaction, scopeKey);
                    }

                    transaction.Commit();
                }
            }
        }

        public void ClearFailures(string scopeKey)
        {
            if (string.IsNullOrWhiteSpace(scopeKey))
            {
                return;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    ClearLockoutState(connection, transaction, scopeKey);
                    DeleteFailuresForScope(connection, transaction, scopeKey);
                    transaction.Commit();
                }
            }
        }

        private static CounterState LoadState(IDbConnection connection, IDbTransaction transaction, string scopeKey)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
SELECT [WindowStartUtc], [Counter], [LockedUntilUtc]
FROM [AuthFlowRateLimitState] WITH (UPDLOCK, HOLDLOCK)
WHERE [ScopeKey] = @ScopeKey";
                AddParameter(command, "@ScopeKey", scopeKey);

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    return new CounterState
                    {
                        WindowStartUtc = reader.IsDBNull(0) ? (DateTime?)null : reader.GetDateTime(0),
                        Counter = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1)),
                        LockedUntilUtc = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2)
                    };
                }
            }
        }

        private static DateTime? LoadLockedUntilUtc(IDbConnection connection, IDbTransaction transaction, string scopeKey)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
SELECT [LockedUntilUtc]
FROM [AuthFlowRateLimitState] WITH (UPDLOCK, HOLDLOCK)
WHERE [ScopeKey] = @ScopeKey";
                AddParameter(command, "@ScopeKey", scopeKey);
                var value = command.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                {
                    return null;
                }

                return Convert.ToDateTime(value);
            }
        }

        private static void InsertCounterState(IDbConnection connection, IDbTransaction transaction, string scopeKey, DateTime windowStartUtc, int counter)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO [AuthFlowRateLimitState] ([ScopeKey], [WindowStartUtc], [Counter], [UpdatedAtUtc])
VALUES (@ScopeKey, @WindowStartUtc, @Counter, @UpdatedAtUtc)";
                AddParameter(command, "@ScopeKey", scopeKey);
                AddParameter(command, "@WindowStartUtc", windowStartUtc);
                AddParameter(command, "@Counter", counter);
                AddParameter(command, "@UpdatedAtUtc", DateTime.UtcNow);
                command.ExecuteNonQuery();
            }
        }

        private static void UpdateCounterState(IDbConnection connection, IDbTransaction transaction, string scopeKey, DateTime windowStartUtc, int counter)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
UPDATE [AuthFlowRateLimitState]
SET [WindowStartUtc] = @WindowStartUtc,
    [Counter] = @Counter,
    [UpdatedAtUtc] = @UpdatedAtUtc
WHERE [ScopeKey] = @ScopeKey";
                AddParameter(command, "@ScopeKey", scopeKey);
                AddParameter(command, "@WindowStartUtc", windowStartUtc);
                AddParameter(command, "@Counter", counter);
                AddParameter(command, "@UpdatedAtUtc", DateTime.UtcNow);
                command.ExecuteNonQuery();
            }
        }

        private static void InsertFailure(IDbConnection connection, IDbTransaction transaction, string scopeKey, DateTime failedAtUtc)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO [AuthFlowRateLimitFailures] ([ScopeKey], [FailedAtUtc])
VALUES (@ScopeKey, @FailedAtUtc)";
                AddParameter(command, "@ScopeKey", scopeKey);
                AddParameter(command, "@FailedAtUtc", failedAtUtc);
                command.ExecuteNonQuery();
            }
        }

        private static void DeleteFailuresBefore(IDbConnection connection, IDbTransaction transaction, string scopeKey, DateTime cutoffUtc)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
DELETE FROM [AuthFlowRateLimitFailures]
WHERE [ScopeKey] = @ScopeKey
  AND [FailedAtUtc] < @CutoffUtc";
                AddParameter(command, "@ScopeKey", scopeKey);
                AddParameter(command, "@CutoffUtc", cutoffUtc);
                command.ExecuteNonQuery();
            }
        }

        private static int CountFailuresSince(IDbConnection connection, IDbTransaction transaction, string scopeKey, DateTime cutoffUtc)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
SELECT COUNT(1)
FROM [AuthFlowRateLimitFailures]
WHERE [ScopeKey] = @ScopeKey
  AND [FailedAtUtc] >= @CutoffUtc";
                AddParameter(command, "@ScopeKey", scopeKey);
                AddParameter(command, "@CutoffUtc", cutoffUtc);
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private static void UpsertLockout(IDbConnection connection, IDbTransaction transaction, string scopeKey, DateTime lockedUntilUtc)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
IF EXISTS (SELECT 1 FROM [AuthFlowRateLimitState] WHERE [ScopeKey] = @ScopeKey)
BEGIN
    UPDATE [AuthFlowRateLimitState]
    SET [LockedUntilUtc] = @LockedUntilUtc,
        [UpdatedAtUtc] = @UpdatedAtUtc
    WHERE [ScopeKey] = @ScopeKey;
END
ELSE
BEGIN
    INSERT INTO [AuthFlowRateLimitState] ([ScopeKey], [LockedUntilUtc], [UpdatedAtUtc])
    VALUES (@ScopeKey, @LockedUntilUtc, @UpdatedAtUtc);
END";
                AddParameter(command, "@ScopeKey", scopeKey);
                AddParameter(command, "@LockedUntilUtc", lockedUntilUtc);
                AddParameter(command, "@UpdatedAtUtc", DateTime.UtcNow);
                command.ExecuteNonQuery();
            }
        }

        private static void ClearLockoutState(IDbConnection connection, IDbTransaction transaction, string scopeKey)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM [AuthFlowRateLimitState] WHERE [ScopeKey] = @ScopeKey";
                AddParameter(command, "@ScopeKey", scopeKey);
                command.ExecuteNonQuery();
            }
        }

        private static void DeleteFailuresForScope(IDbConnection connection, IDbTransaction transaction, string scopeKey)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM [AuthFlowRateLimitFailures] WHERE [ScopeKey] = @ScopeKey";
                AddParameter(command, "@ScopeKey", scopeKey);
                command.ExecuteNonQuery();
            }
        }

        private static void AddParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        private sealed class CounterState
        {
            public DateTime? WindowStartUtc { get; set; }

            public int Counter { get; set; }

            public DateTime? LockedUntilUtc { get; set; }
        }
    }
}
