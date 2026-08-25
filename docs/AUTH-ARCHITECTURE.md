# Forms Authentication Architecture

Built-in **Forms Authentication** (no SSO/OIDC). Identity is stored in SQL Server; the auth cookie carries a server-validated session binding.

## Sign-in flow

```
Login (tenant or platform URL)
  → CAPTCHA (Release) + IP rate limit + account lockout check
  → Credential validation (scoped by tenant slug when present)
  → Legal consent (if policy versions changed)
  → MFA setup or verification (Release: all users; Debug: privileged roles only)
  → Email verification (when EmailVerificationRequired=true)
  → Forms auth cookie issued
  → Redirect (tenant portal or Platform area)
```

**Tenant vs platform:** Tenant routes use `/{slug}/Account/Login`. Platform admins sign in at `/Account/Login` with `OrganizationId IS NULL`. `MembershipService.ValidateCredentials` and password reset both scope lookups by organization slug when a tenant portal is used.

## Auth cookie contents

The encrypted Forms ticket `UserData` pipe-delimited payload:

| Field | Purpose |
|-------|---------|
| UserId | Primary key |
| OrganizationId | Tenant binding (empty for platform users) |
| AccessToken | Server-side session nonce; rotated on password change, lockout, admin unlock |
| UserAgent hash | Optional binding to issuing browser |
| RequirePasswordChange | Limits navigation until password is changed |

Cookie flags: **HttpOnly**, **SameSite=Lax**, **Secure** when HTTPS or `RequireSecureCookies=true` (Release).

## Request-time validation (`Global.asax` → `Application_PostAuthenticateRequest`)

On each authenticated request (except public auth paths):

1. Decrypt Forms ticket and parse `UserData`.
2. Validate `AccessToken` against the database (invalidates stale sessions after password change, lockout, or admin unlock).
3. Validate User-Agent fingerprint when present.
4. Reject if account is **locked** (failed login threshold).
5. Redirect to **Change Password** when `RequirePasswordChange` is set and path is not exempt.
6. Set `Context.User` and tenant context items.

## Session invalidation triggers

| Event | Mechanism |
|-------|-----------|
| Password change (user) | `RotateUserAccessToken` + re-issue cookie for current browser |
| Password reset (token) | `AccessToken` + `SecurityStamp` rotated in `MembershipService.ResetPassword` |
| Account lockout (5 failures) | `RotateUserAccessToken` when lockout threshold crossed |
| Admin unlock | `ClearFailedLoginAttemptsForUser` + `RotateUserAccessToken` |
| Role assignment | `AccessToken` rotated in `UserService.AssignRole` |
| Log off / invalid session | `AuthSessionHelper.SignOut` — expire cookie + abandon ASP.NET session |
| Deactivated user | `ValidateAccessToken` fails (`IsActive=0`) |

## Filters (global)

| Filter | Role |
|--------|------|
| `AntiForgeryExceptionFilter` | Sign out on CSRF token mismatch |
| `TenantFilter` | Tenant URL alignment, license block, email verification gate |
| `RequireMfaEnforcementFilter` | Redirect enrolled-but-not-setup users when `RequireMfaForAllUsers` |
| `FreshPortalSecurity` | Restrict platform admin in fresh-portal demo mode |

## Configuration

| Setting | Debug default | Release |
|---------|---------------|---------|
| `RequireSecureCookies` | false | true |
| `RequireHttpsRedirect` | false | true |
| `RequireMfaForAllUsers` | false | true |
| `MfaAllowAnyCode` | true (E2E) | false |
| `LoginLockoutEnabled` | true | true |
| `LoginCaptchaEnabled` | false | true |
| `GenericAuthMessagesEnabled` | — | true |

Release also sets `forms requireSSL="true"`, `httpCookies requireSSL/httpOnly`, HSTS, and external `machineKey.config` for multi-instance cookie decryption.

## Operational notes

- Configure SMTP before disabling `MfaAllowAnyCode`.
- Replace default `SystemEncryptionKey` and provide production `machineKey.config` (not in source control).
- Publish with **Release** configuration for production security defaults.
- See [DEPLOYMENT-READINESS.md](./DEPLOYMENT-READINESS.md) for the pre-deploy checklist.
