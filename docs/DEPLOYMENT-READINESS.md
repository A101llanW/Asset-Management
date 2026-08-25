# Deployment Readiness

Design improvements split by when they should land relative to the first production deployment.

## Before deployment (must-have for go-live)

These address security, data integrity, and operational safety. **HTTPS and mandatory MFA are implemented via Release transforms** — publish with **Release** configuration to activate them.

### Security and transport (implemented)

| Item | Status | Notes |
|------|--------|--------|
| **HTTPS only** | Implemented | `RequireHttpsRedirect=true` in Release; redirects HTTP → HTTPS (respects `X-Forwarded-Proto` behind a reverse proxy). HSTS + secure cookies already in `Web.Release.config`. |
| **Mandatory MFA for all users** | Implemented | `RequireMfaForAllUsers=true` in Release; `MfaAllowAnyCode=false`. All sign-ins require MFA setup/verification; enrolled users without MFA are redirected to setup. |
| **Secure auth cookies** | Implemented | `RequireSecureCookies=true`, `httpCookies requireSSL/httpOnly/sameSite=Lax` in Release; base `Web.config` sets HttpOnly + SameSite for all environments. |
| **Session invalidation on lockout/password change** | Implemented | Access token rotated on lockout threshold, password change/reset, and admin unlock; `AuthSessionHelper` clears cookie + abandons session on log off. |
| **Explicit machineKey** | Manual | Release transform references external `machineKey.config` (copy from `machineKey.config.example`); required for web farms; see [Forms auth machineKey](#forms-auth-machinekey) below. |
| **Disable MFA dev bypass** | Implemented | `MfaAllowAnyCode=false` in Release (dev/E2E keep `true` in Debug). |
| **Production error handling** | Implemented | `customErrors=RemoteOnly`, `debug=false` in Release. |
| **Configure SMTP for MFA/reset emails** | Implemented | Platform Settings → Email, or production `Web.config` app settings. **Required when `MfaAllowAnyCode=false`.** Startup logs a Trace error if SMTP is missing; MFA send and password reset are blocked until configured. |
| **Replace default encryption key** | Manual | Set a strong unique `SystemEncryptionKey` in production app settings (not in source control). |
| **External connection strings** | Manual | Copy `connectionStrings.config.example` → `connectionStrings.config`; never commit secrets. |
| **Disable auto DB init on startup** | Implemented | `AutoInitializeDatabase=false` in Release; run `tools/database/Initialize-Database.ps1` explicitly. |

Release app settings (via `Web.Release.config`):

```xml
RequireSecureCookies=true
RequireHttpsRedirect=true
RequireMfaForAllUsers=true
MfaAllowAnyCode=false
AutoInitializeDatabase=false
LoginCaptchaEnabled=true
MigrationContinueOnError=false
```

Development defaults (`Web.config` / Debug) keep HTTP, optional MFA for non-admin roles, and relaxed MFA codes for E2E.

**Architecture reference:** [AUTH-ARCHITECTURE.md](./AUTH-ARCHITECTURE.md) — Forms auth flow, cookie binding, session invalidation, and filter stack.

### SMTP configuration (required for production auth email)

When `MfaAllowAnyCode=false` (Release/production), the application **requires SMTP** for:

- MFA verification codes at sign-in and MFA setup
- Password reset links
- Email verification codes (when `EmailVerificationRequired=true`)

Without SMTP, these flows fail gracefully: users see a generic “could not send verification code” message; password reset still returns the standard “If that email is registered…” response but **no email is sent** and no reset token is created. Application startup writes a **Trace error** describing missing settings.

#### Configure via Platform Settings (recommended)

1. Sign in as a **Platform Admin**.
2. Open **Platform → Email Settings** (`/Platform/PlatformSettings/Email`).
3. Set at minimum:
   - **SMTP host** — e.g. `smtp.office365.com`, `email-smtp.us-east-1.amazonaws.com`
   - **SMTP port** — usually `587` (STARTTLS) or `465` (SSL)
   - **From email** — verified sender address accepted by your provider
   - **From name** — display name shown to recipients
4. If your provider requires authentication, set **SMTP username** and **SMTP password**.
5. Set **External base URL** to your public HTTPS site root (e.g. `https://assets.example.com`) so password reset links are correct behind reverse proxies.
6. Click **Save settings**, then use **Send test email** to confirm delivery.

Settings are stored in the `SystemSetting` table (platform scope). They override `Web.config` when set.

#### Configure via Web.config (alternative)

Add app settings on the production server only (do **not** commit credentials):

```xml
<add key="SmtpHost" value="smtp.example.com" />
<add key="SmtpPort" value="587" />
<add key="SmtpUser" value="smtp-user" />
<add key="SmtpPassword" value="*** set on server only ***" />
<add key="SmtpEnableSsl" value="true" />
<add key="FromEmail" value="noreply@yourdomain.com" />
<add key="FromName" value="Asset Management Module" />
<add key="ExternalBaseUrl" value="https://assets.example.com" />
```

Platform Settings values take precedence over these keys when both are set.

#### App settings reference

| Key | Required (production) | Default | Purpose |
|-----|----------------------|---------|---------|
| `SmtpHost` | Yes | — | SMTP server hostname |
| `SmtpPort` | No | `587` | SMTP port |
| `SmtpUser` | Provider-dependent | — | SMTP authentication username |
| `SmtpPassword` | Provider-dependent | — | SMTP authentication password |
| `SmtpEnableSsl` | No | `true` | Enable TLS/SSL for SMTP |
| `FromEmail` | Yes | — | Sender address (must be set explicitly; placeholder defaults do not count) |
| `FromName` | No | `Asset Management Module` | Sender display name |
| `ExternalBaseUrl` | Recommended | — | Public HTTPS base URL for password reset links |
| `MfaAllowAnyCode` | Release: `false` | Debug: `true` | When `false`, SMTP is mandatory for auth email |

#### Troubleshooting

- Check **Trace** output or `email_errors.txt` in the site root for send failures (includes retry attempts).
- Confirm firewall allows outbound SMTP from the app server.
- For Microsoft 365 / Google Workspace, use an app password or SMTP relay authorized for the From address.
- After changing settings, send a test email from Platform → Email Settings before go-live. Debug builds use IIS auto-generated machine keys (no external file).

### Forms auth machineKey

Auth cookies are encrypted with `FormsAuthentication.Encrypt` / `Decrypt` (`CurrentUserExtensions.SetAuthCookie`, `Global.asax.cs` `Application_PostAuthenticateRequest`). Both operations require a stable `machineKey`.

| Deployment | Recommendation |
|------------|----------------|
| **Single IIS server** | Copy `machineKey.config.example` → `machineKey.config`, generate unique keys, deploy with the site. Explicit keys survive registry changes and make rotation deliberate. |
| **IIS web farm (multiple nodes)** | **Required:** deploy the **same** `machineKey.config` to every node. Without it, a cookie issued on server A cannot be decrypted on server B and users are logged out unpredictably. |
| **Web farm alternative** | Configure load-balancer **sticky sessions** (session affinity) so each user always hits the same node. Still recommend a shared explicit `machineKey` so affinity loss or node drain does not invalidate all sessions. |

Release publish adds `<machineKey configSource="machineKey.config" />` via `Web.Release.config`. The file is gitignored — never commit production keys.

Generate keys (PowerShell, run twice for two different values):

```powershell
# validationKey — 128 hex characters (64 bytes)
$b = New-Object byte[] 64
(New-Object Security.Cryptography.RNGCryptoServiceProvider).GetBytes($b)
[BitConverter]::ToString($b).Replace('-','').ToLower()

# decryptionKey — 64 hex characters (32 bytes)
$b = New-Object byte[] 32
(New-Object Security.Cryptography.RNGCryptoServiceProvider).GetBytes($b)
[BitConverter]::ToString($b).Replace('-','').ToLower()
```

Paste into `machineKey.config` (from the example template). On startup, non-Debug builds log a **warning** (via `Trace`) if `machineKey` is missing, auto-generated, or still contains `REPLACE_WITH_*` placeholders.

**Rotating keys** invalidates all existing auth cookies — schedule during a maintenance window and expect users to sign in again.

### Data and multitenancy (recommended before go-live)

| Item | Status | Notes |
|------|--------|--------|
| **SQL-scoped tenant reads** | Implemented | `AdoRepository`/`EntitySqlReader.ReadAll` apply `@OrganizationId` (or `1=0` when tenant context is missing) for `ITenantEntity` types instead of loading full tables. |
| **Migration history table** | Implemented | `SchemaMigrationHistory` tracks applied scripts; legacy DBs are bootstrapped once; Release sets `MigrationContinueOnError=false` so deploy fails on migration errors. |
| **Atomic org provisioning** | Implemented | `CreateOrganization` runs in a single transaction; company admin user insert enlists in the same transaction. |
| **Run migration 056/057/058** | Required | Approval defaults backfill, seeded supplier cleanup, and migration history on existing DBs. |
| **Remove demo credentials from production** | Required | Do not deploy with default `P@ssw0rd!` users; rotate or delete seed accounts. |
| **Login captcha** | Implemented | `LoginCaptchaEnabled=true` in Release. |
| **Generic auth messages** | Implemented | `GenericAuthMessagesEnabled=true` in Release reduces user enumeration. |

### Pre-deploy checklist

1. Publish with **Release** configuration.
2. Configure HTTPS certificate on IIS/reverse proxy; confirm `X-Forwarded-Proto` if terminated at proxy.
3. Copy `connectionStrings.config.example` → `connectionStrings.config`; set production SQL connection string.
4. Copy `machineKey.config.example` → `machineKey.config`; generate and set unique validation/decryption keys (same file on every web-farm node).
5. Run `.\tools\database\Initialize-Database.ps1` against production (once, controlled), or `.\tools\database\Invoke-Migrations.ps1` for incremental updates.
6. Configure SMTP (see **SMTP configuration** above); verify with Platform → Email Settings test email.
7. Set a unique `SystemEncryptionKey`.
8. Create real company admin accounts; disable or remove demo seed users.
9. Smoke-test: login → MFA setup → tenant URL → create asset (suppliers are no longer seeded).

---

## After deployment (post go-live improvements)

Safe to schedule once the system is live and stable. These improve maintainability and scale; they are not blockers for HTTPS/MFA go-live.

| Item | Benefit |
|------|---------|
| **Explicit `ITenantContext`** | Testable tenant scope; remove HttpContext coupling from `OrganizationScopeService`. |
| **Constructor DI in Web layer** | Replace `DependencyResolver.Current` and slim `BaseController`. |
| **Web → Application-only references** | Controllers stop importing Infrastructure types directly. |
| **Typed approval process handlers** | Replace reflection in `ApprovalWorkflowEngine` with per-process handlers. |
| **Move orchestration services to Application** | `UserService`, `MembershipService`, scope services as Application rules + Infrastructure adapters. |
| **Background job runner** | Extract notification/license/outbox work from `Global.asax` thread pool. |
| **Auto-register `EntityMapRegistry`** | Reduce manual entity map drift. |
| **Platform modernization (.NET 4.8 / ASP.NET Core)** | Modern auth, async I/O, first-class DI. |
| **SQL integration tests for tenant isolation** | Regression suite for multitenancy and provisioning. |
| **Demo vs required bootstrap tiers** | Optional demo packs separate from tenant provisioning (partially done). |
| **Supplier catalog starter packs** | Optional post-deploy import templates if customers want catalog comparison seeds. |

---

## Suggested timeline

```text
Before deploy     → HTTPS, MFA, SMTP, secrets, migrations, demo user cleanup (all implemented)
First week live   → SQL tenant filters on remaining hot paths (query services)
First quarter     → Web DI cleanup, approval handler refactor
Long term         → ASP.NET Core migration, dedicated job host
```

See also [README.md](README.md) § Production Deployment.
