-- Distributed auth-flow rate limiting (MFA, registration, password reset)

IF OBJECT_ID(N'[AuthFlowRateLimitState]', N'U') IS NULL
BEGIN
    CREATE TABLE [AuthFlowRateLimitState] (
        [ScopeKey] NVARCHAR(450) NOT NULL PRIMARY KEY,
        [WindowStartUtc] DATETIME NULL,
        [Counter] INT NOT NULL CONSTRAINT [DF_AuthFlowRateLimitState_Counter] DEFAULT(0),
        [LockedUntilUtc] DATETIME NULL,
        [UpdatedAtUtc] DATETIME NOT NULL CONSTRAINT [DF_AuthFlowRateLimitState_UpdatedAtUtc] DEFAULT(GETUTCDATE())
    );
END
GO

IF OBJECT_ID(N'[AuthFlowRateLimitFailures]', N'U') IS NULL
BEGIN
    CREATE TABLE [AuthFlowRateLimitFailures] (
        [Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ScopeKey] NVARCHAR(450) NOT NULL,
        [FailedAtUtc] DATETIME NOT NULL CONSTRAINT [DF_AuthFlowRateLimitFailures_FailedAtUtc] DEFAULT(GETUTCDATE())
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_AuthFlowRateLimitFailures_ScopeKey_FailedAtUtc'
      AND object_id = OBJECT_ID(N'dbo.AuthFlowRateLimitFailures')
)
BEGIN
    CREATE INDEX [IX_AuthFlowRateLimitFailures_ScopeKey_FailedAtUtc]
        ON [AuthFlowRateLimitFailures] ([ScopeKey], [FailedAtUtc]);
END
GO
