-- =============================================================================
-- PrePublish_SuperAdmin_SchemaCheck_SELECTS_ONLY.sql
-- =============================================================================
-- PURPOSE: READ-ONLY / SELECT-only checks for the SuperAdmin / Platform Admin
--          login path before publishing SoT (9dc7a7e+) to live
--          https://nanosoft.africa/NanoAssetSuite.
--
-- SAFE FOR: DataConsole / SSMS / sqlcmd on LIVE or local DB.
-- DO-NOT-MODIFY DB: this file contains SELECT / metadata queries ONLY.
--                   No INSERT/UPDATE/DELETE/ALTER/CREATE/DROP.
--
-- HOW TO USE (Allan / DataConsole):
--   1. Open /Sys/Database (or SSMS) against the LIVE database that
--      /NanoAssetSuite uses.
--   2. Paste and run this entire script (or section by section).
--   3. Every check row should show Status = PRESENT / OK.
--   4. If any Status = MISSING, apply the named migration (schema-only)
--      BEFORE publishing bits. Do NOT run 083 on live without an explicit
--      password-change plan (083 is data UPDATE, not required for column).
--
-- SCOPE: SuperAdmin path only (login -> lockout -> legal -> MFA -> cookie ->
--        RequirePasswordChange gate -> /Platform/Organizations/Index).
-- ALSO lists tenant-nav column UseCustomRequisitionApproval (past 500 pattern)
-- so you catch post-login tenant pages if SuperAdmin later impersonates.
-- =============================================================================

SET NOCOUNT ON;

PRINT N'=== A) Database identity ===';
SELECT
    DB_NAME()              AS [DatabaseName],
    @@SERVERNAME           AS [ServerName],
    SUSER_SNAME()          AS [LoginName],
    SYSUTCDATETIME()       AS [CheckedAtUtc];

PRINT N'=== B) Users columns required by AccountSecurity / UserAccountRepository (SELECT *) ===';
;WITH Need AS (
    SELECT v.ColName
    FROM (VALUES
        (N'RequirePasswordChange'),   -- 046; cookie + Global.asax force-change
        (N'MfaMethod'),               -- 023
        (N'TwoFactorCode'),           -- 023
        (N'TwoFactorExpiryUtc'),      -- 023
        (N'TwoFactorEnabled'),        -- identity base / MFA
        (N'PrivacyAcceptedAt'),       -- 023 legal
        (N'TermsAcceptedAt'),         -- 023 legal
        (N'PrivacyVersion'),          -- 023 legal
        (N'TermsVersion'),            -- 023 legal
        (N'AccessToken'),             -- session binding
        (N'LastPasswordChange'),      -- 046 family
        (N'IsEmailVerified'),         -- email-verify gate (only if EmailVerificationRequired=true)
        (N'EmailVerificationCode'),
        (N'EmailVerificationExpiryUtc'),
        (N'LockoutEndDateUtc'),
        (N'LockoutEnabled'),
        (N'AccessFailedCount'),
        (N'OrganizationId'),
        (N'RoleId'),
        (N'IsActive'),
        (N'PasswordHash'),
        (N'Email')
    ) AS v(ColName)
)
SELECT
    n.ColName,
    CASE WHEN COL_LENGTH(N'dbo.Users', n.ColName) IS NULL THEN N'MISSING' ELSE N'PRESENT' END AS [Status],
    N'Users' AS [TableName]
FROM Need n
ORDER BY n.ColName;

PRINT N'=== C) Roles.IsSystemRole (Phase B Platform/Company Admin gate + RequiresPrivilegedMfa) ===';
SELECT
    N'IsSystemRole' AS ColName,
    CASE WHEN COL_LENGTH(N'dbo.Roles', N'IsSystemRole') IS NULL THEN N'MISSING' ELSE N'PRESENT' END AS [Status],
    N'Roles' AS [TableName];

PRINT N'=== D) Lockout / MFA rate-limit / security-event tables ===';
;WITH Need AS (
    SELECT v.TableName, v.NeededBy
    FROM (VALUES
        (N'LoginAttempts',              N'024_LoginSecurity / AUTH lockout 5 fails'),
        (N'SecurityEvents',             N'023_AccountSecurity / AUTH_LOCKOUT soft-fail row'),
        (N'AuthFlowRateLimitState',     N'065_AuthFlowRateLimits / MFA send+verify'),
        (N'AuthFlowRateLimitFailures',  N'065_AuthFlowRateLimits / MFA verify lockout'),
        (N'AuditLog',                   N'DatabaseConsole + org audit (not login-critical)')
    ) AS v(TableName, NeededBy)
)
SELECT
    n.TableName,
    CASE WHEN OBJECT_ID(N'dbo.' + n.TableName, N'U') IS NULL THEN N'MISSING' ELSE N'PRESENT' END AS [Status],
    n.NeededBy
FROM Need n
ORDER BY n.TableName;

PRINT N'=== E) SecurityEvents columns used by AUTH_LOCKOUT insert ===';
;WITH Need AS (
    SELECT v.ColName
    FROM (VALUES
        (N'EventType'),
        (N'Email'),
        (N'IpAddress'),
        (N'OrganizationId'),
        (N'CreatedAtUtc')
    ) AS v(ColName)
)
SELECT
    n.ColName,
    CASE WHEN COL_LENGTH(N'dbo.SecurityEvents', n.ColName) IS NULL THEN N'MISSING' ELSE N'PRESENT' END AS [Status]
FROM Need n
ORDER BY n.ColName;

PRINT N'=== F) LoginAttempts columns used by lockout counters ===';
;WITH Need AS (
    SELECT v.ColName
    FROM (VALUES
        (N'Username'),
        (N'IpAddress'),
        (N'AttemptedAtUtc'),
        (N'Success'),
        (N'FailureReason'),
        (N'OrganizationId')
    ) AS v(ColName)
)
SELECT
    n.ColName,
    CASE WHEN COL_LENGTH(N'dbo.LoginAttempts', n.ColName) IS NULL THEN N'MISSING' ELSE N'PRESENT' END AS [Status]
FROM Need n
ORDER BY n.ColName;

PRINT N'=== G) AuthFlowRateLimit* columns (MFA send/verify after SuperAdmin credentials) ===';
;WITH Need AS (
    SELECT v.TableName, v.ColName
    FROM (VALUES
        (N'AuthFlowRateLimitState', N'ScopeKey'),
        (N'AuthFlowRateLimitState', N'WindowStartUtc'),
        (N'AuthFlowRateLimitState', N'Counter'),
        (N'AuthFlowRateLimitState', N'LockedUntilUtc'),
        (N'AuthFlowRateLimitState', N'UpdatedAtUtc'),
        (N'AuthFlowRateLimitFailures', N'ScopeKey'),
        (N'AuthFlowRateLimitFailures', N'FailedAtUtc')
    ) AS v(TableName, ColName)
)
SELECT
    n.TableName,
    n.ColName,
    CASE WHEN COL_LENGTH(N'dbo.' + n.TableName, n.ColName) IS NULL THEN N'MISSING' ELSE N'PRESENT' END AS [Status]
FROM Need n
ORDER BY n.TableName, n.ColName;

PRINT N'=== H) Past live 500 pattern: Department.UseCustomRequisitionApproval (065_DepartmentRequisitionApproval) ===';
PRINT N'    Not required for Platform/Organizations Index, but required for tenant Assets/Index after login/nav.';
SELECT
    N'UseCustomRequisitionApproval' AS ColName,
    CASE WHEN COL_LENGTH(N'dbo.Department', N'UseCustomRequisitionApproval') IS NULL THEN N'MISSING' ELSE N'PRESENT' END AS [Status],
    N'Department' AS [TableName],
    N'065_DepartmentRequisitionApproval.sql' AS [Migration];

PRINT N'=== I) Related migrations often missing together (optional for SuperAdmin login; needed for tenant features) ===';
SELECT N'COLUMN' AS ObjectType, N'Department.RequisitionApprovalStageRoleIds' AS ObjectName, N'065_DepartmentRequisitionApproval' AS MigrationHint,
       CASE WHEN COL_LENGTH(N'dbo.Department', N'RequisitionApprovalStageRoleIds') IS NULL THEN N'MISSING' ELSE N'PRESENT' END AS [Status]
UNION ALL SELECT N'COLUMN', N'Department.RequisitionApprovalStageUserIds', N'065_DepartmentRequisitionApproval',
       CASE WHEN COL_LENGTH(N'dbo.Department', N'RequisitionApprovalStageUserIds') IS NULL THEN N'MISSING' ELSE N'PRESENT' END
UNION ALL SELECT N'TABLE', N'UserInvitation', N'069_UserInvitations',
       CASE WHEN OBJECT_ID(N'dbo.UserInvitation', N'U') IS NULL THEN N'MISSING' ELSE N'PRESENT' END
UNION ALL SELECT N'TABLE', N'PurchaseRequestLine', N'070_PurchaseRequestLineItems',
       CASE WHEN OBJECT_ID(N'dbo.PurchaseRequestLine', N'U') IS NULL THEN N'MISSING' ELSE N'PRESENT' END
UNION ALL SELECT N'COLUMN', N'Department.RowVersion', N'079_DepartmentRowVersion',
       CASE WHEN COL_LENGTH(N'dbo.Department', N'RowVersion') IS NULL THEN N'MISSING' ELSE N'PRESENT' END
UNION ALL SELECT N'TABLE', N'DepartmentApprovalStage', N'080_DepartmentApprovalStage',
       CASE WHEN OBJECT_ID(N'dbo.DepartmentApprovalStage', N'U') IS NULL THEN N'MISSING' ELSE N'PRESENT' END;
PRINT N'=== J) Platform Admin role health (IsSystemRole must be 1 or Phase B treats user as non-admin) ===';
SELECT
    r.[Id],
    r.[Name],
    r.[IsSystemRole],
    r.[OrganizationId],
    r.[IsActive],
    CASE
        WHEN r.[IsSystemRole] = 1 AND r.[IsActive] = 1 AND r.[OrganizationId] IS NULL THEN N'OK'
        ELSE N'REVIEW'
    END AS [Status]
FROM [Roles] r
WHERE r.[Name] = N'Platform Admin'
ORDER BY r.[Id];

PRINT N'=== K) Active platform users (OrganizationId IS NULL) — RequirePasswordChange / MFA flags ===';
SELECT
    u.[Id],
    u.[Email],
    u.[IsActive],
    u.[RequirePasswordChange],
    u.[TwoFactorEnabled],
    u.[MfaMethod],
    r.[Name] AS [RoleName],
    r.[IsSystemRole],
    CASE
        WHEN u.[IsActive] = 1
         AND r.[Name] = N'Platform Admin'
         AND r.[IsSystemRole] = 1
        THEN N'OK_PLATFORM_ADMIN'
        ELSE N'REVIEW'
    END AS [Status]
FROM [Users] u
LEFT JOIN [Roles] r ON r.[Id] = u.[RoleId]
WHERE u.[OrganizationId] IS NULL
ORDER BY u.[Email];

PRINT N'=== L) Platform.Organizations.View permission wired to Platform Admin role ===';
SELECT
    r.[Id] AS RoleId,
    r.[Name] AS RoleName,
    p.[Code] AS PermissionCode,
    CASE WHEN rp.[RoleId] IS NULL THEN N'MISSING_GRANT' ELSE N'OK' END AS [Status]
FROM [Roles] r
CROSS JOIN [Permission] p
LEFT JOIN [RolePermission] rp
    ON rp.[RoleId] = r.[Id]
   AND rp.[PermissionId] = p.[Id]
WHERE r.[Name] = N'Platform Admin'
  AND r.[OrganizationId] IS NULL
  AND p.[Code] IN (N'Platform.Organizations.View', N'Platform.Organizations.Manage');

PRINT N'=== M) SchemaMigrationHistory presence for key scripts (informational; column may be AppliedAt) ===';
IF OBJECT_ID(N'dbo.SchemaMigrationHistory', N'U') IS NULL
BEGIN
    SELECT N'SchemaMigrationHistory' AS [TableName], N'MISSING' AS [Status],
           N'History table absent — rely on COL_LENGTH / OBJECT_ID checks above.' AS [Note];
END
ELSE
BEGIN
    SELECT
        v.ScriptName,
        CASE WHEN EXISTS (
            SELECT 1 FROM [SchemaMigrationHistory] h WHERE h.[ScriptName] = v.ScriptName
        ) THEN N'RECORDED' ELSE N'NOT_IN_HISTORY' END AS [Status]
    FROM (VALUES
        (N'023_AccountSecurity.sql'),
        (N'024_LoginSecurity.sql'),
        (N'046_RecruitmentAuthAndOrgProfile.sql'),
        (N'065_AuthFlowRateLimits.sql'),
        (N'065_DepartmentRequisitionApproval.sql'),
        (N'069_UserInvitations.sql'),
        (N'070_PurchaseRequestLineItems.sql'),
        (N'079_DepartmentRowVersion.sql'),
        (N'080_DepartmentApprovalStage.sql'),
        (N'081_DepartmentAcademicAndRoomInherit.sql'),
        (N'082_DepartmentClassTeacherRoomCustodian.sql'),
        (N'083_SeededAccountsRequirePasswordChange.sql')
    ) AS v(ScriptName)
    ORDER BY v.ScriptName;
END

PRINT N'=== N) Roll-up: any SuperAdmin-critical MISSING objects? ===';
SELECT
    CASE WHEN SUM(v.IsMissing) = 0 THEN N'PASS_SUPERADMIN_SCHEMA' ELSE N'FAIL_SUPERADMIN_SCHEMA' END AS [Rollup],
    SUM(v.IsMissing) AS [MissingCount]
FROM (
    SELECT CASE WHEN COL_LENGTH(N'dbo.Users', N'RequirePasswordChange') IS NULL THEN 1 ELSE 0 END AS IsMissing
    UNION ALL SELECT CASE WHEN COL_LENGTH(N'dbo.Users', N'MfaMethod') IS NULL THEN 1 ELSE 0 END
    UNION ALL SELECT CASE WHEN COL_LENGTH(N'dbo.Users', N'TwoFactorCode') IS NULL THEN 1 ELSE 0 END
    UNION ALL SELECT CASE WHEN COL_LENGTH(N'dbo.Users', N'TwoFactorExpiryUtc') IS NULL THEN 1 ELSE 0 END
    UNION ALL SELECT CASE WHEN COL_LENGTH(N'dbo.Users', N'AccessToken') IS NULL THEN 1 ELSE 0 END
    UNION ALL SELECT CASE WHEN COL_LENGTH(N'dbo.Users', N'PrivacyAcceptedAt') IS NULL THEN 1 ELSE 0 END
    UNION ALL SELECT CASE WHEN COL_LENGTH(N'dbo.Roles', N'IsSystemRole') IS NULL THEN 1 ELSE 0 END
    UNION ALL SELECT CASE WHEN OBJECT_ID(N'dbo.LoginAttempts', N'U') IS NULL THEN 1 ELSE 0 END
    UNION ALL SELECT CASE WHEN OBJECT_ID(N'dbo.SecurityEvents', N'U') IS NULL THEN 1 ELSE 0 END
    UNION ALL SELECT CASE WHEN OBJECT_ID(N'dbo.AuthFlowRateLimitState', N'U') IS NULL THEN 1 ELSE 0 END
    UNION ALL SELECT CASE WHEN OBJECT_ID(N'dbo.AuthFlowRateLimitFailures', N'U') IS NULL THEN 1 ELSE 0 END
) v;

SELECT v.Obj AS [MissingObject]
FROM (
    SELECT N'Users.RequirePasswordChange' AS Obj, CASE WHEN COL_LENGTH(N'dbo.Users', N'RequirePasswordChange') IS NULL THEN 1 ELSE 0 END AS IsMissing
    UNION ALL SELECT N'Users.MfaMethod', CASE WHEN COL_LENGTH(N'dbo.Users', N'MfaMethod') IS NULL THEN 1 ELSE 0 END
    UNION ALL SELECT N'Users.TwoFactorCode', CASE WHEN COL_LENGTH(N'dbo.Users', N'TwoFactorCode') IS NULL THEN 1 ELSE 0 END
    UNION ALL SELECT N'Users.TwoFactorExpiryUtc', CASE WHEN COL_LENGTH(N'dbo.Users', N'TwoFactorExpiryUtc') IS NULL THEN 1 ELSE 0 END
    UNION ALL SELECT N'Users.AccessToken', CASE WHEN COL_LENGTH(N'dbo.Users', N'AccessToken') IS NULL THEN 1 ELSE 0 END
    UNION ALL SELECT N'Users.PrivacyAcceptedAt', CASE WHEN COL_LENGTH(N'dbo.Users', N'PrivacyAcceptedAt') IS NULL THEN 1 ELSE 0 END
    UNION ALL SELECT N'Roles.IsSystemRole', CASE WHEN COL_LENGTH(N'dbo.Roles', N'IsSystemRole') IS NULL THEN 1 ELSE 0 END
    UNION ALL SELECT N'table.LoginAttempts', CASE WHEN OBJECT_ID(N'dbo.LoginAttempts', N'U') IS NULL THEN 1 ELSE 0 END
    UNION ALL SELECT N'table.SecurityEvents', CASE WHEN OBJECT_ID(N'dbo.SecurityEvents', N'U') IS NULL THEN 1 ELSE 0 END
    UNION ALL SELECT N'table.AuthFlowRateLimitState', CASE WHEN OBJECT_ID(N'dbo.AuthFlowRateLimitState', N'U') IS NULL THEN 1 ELSE 0 END
    UNION ALL SELECT N'table.AuthFlowRateLimitFailures', CASE WHEN OBJECT_ID(N'dbo.AuthFlowRateLimitFailures', N'U') IS NULL THEN 1 ELSE 0 END
) v
WHERE v.IsMissing = 1;
PRINT N'=== O) Config checklist (NOT SQL — verify on live Web.config / smtp.secrets / Platform Email) ===';
PRINT N'  MfaAllowAnyCode     : live HTML currently shows MFA any-code banner (true).';
PRINT N'                       SoT default is false. PRESERVE live true OR ensure SMTP works,';
PRINT N'                       else SuperAdmin MFA send fails after publish.';
PRINT N'  SmtpHost/User/Pass  : required when MfaAllowAnyCode=false.';
PRINT N'  ExternalBaseUrl     : should be https://nanosoft.africa/NanoAssetSuite (email/QR links).';
PRINT N'                       Does not block login itself.';
PRINT N'  DatabaseConsoleAllowedIps : new in Phase B; defaults to 127.0.0.1,::1 if unset.';
PRINT N'                       Only affects /Sys/Database, not login.';
PRINT N'  EmailVerificationRequired : if true, needs IsEmailVerified columns (checked above).';
PRINT N'  LoginLockoutEnabled : defaults true when unset.';
PRINT N'  RequireMfaForAllUsers : Platform Admin always MFA via RequiresPrivilegedMfa regardless.';
PRINT N'=== END SELECT-ONLY CHECK ===';


