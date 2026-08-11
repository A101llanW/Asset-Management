using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Organizations;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Security;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Security;

namespace AssetManagement.Infrastructure.Services
{
    public class MembershipService : IUserAccountService
    {
        private readonly UserAccountRepository _users;
        private readonly PasswordResetRepository _resetTokens;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IOrganizationLicenseService _licenseService;
        private readonly IEmailService _emailService;
        private readonly IPlatformSettingsService _platformSettings;
        private readonly IUnitOfWorkEnlistment _unitOfWorkEnlistment;
        private readonly IAuditWriter _auditWriter;

        public MembershipService(
            ISqlConnectionFactory connectionFactory,
            IOrganizationScopeService organizationScope,
            IOrganizationLicenseService licenseService,
            IEmailService emailService,
            IPlatformSettingsService platformSettings,
            IUnitOfWorkEnlistment unitOfWorkEnlistment,
            IAuditWriter auditWriter = null)
        {
            _users = new UserAccountRepository(connectionFactory);
            _resetTokens = new PasswordResetRepository(connectionFactory);
            _organizationScope = organizationScope;
            _licenseService = licenseService;
            _emailService = emailService;
            _platformSettings = platformSettings;
            _unitOfWorkEnlistment = unitOfWorkEnlistment;
            _auditWriter = auditWriter;
        }

        public bool ValidateCredentials(string email, string password, out string userId)
        {
            return ValidateCredentials(email, password, null, out userId);
        }

        public bool ValidateCredentials(string email, string password, string organizationSlug, out string userId)
        {
            userId = null;
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            {
                return false;
            }

            var normalizedEmail = DemoLoginEmailHelper.ResolveLoginEmail(email, organizationSlug);
            ApplicationUser user = null;

            if (!string.IsNullOrWhiteSpace(organizationSlug))
            {
                var orgId = _users.FindOrganizationIdBySlug(organizationSlug);
                if (orgId.HasValue)
                {
                    user = _users.FindByEmailAndOrganization(normalizedEmail, orgId.Value);
                }
            }
            else
            {
                user = _users.FindPlatformAdminByEmail(normalizedEmail)
                    ?? _users.FindActiveUserByEmail(normalizedEmail);
            }

            if (user == null || !user.IsActive)
            {
                return false;
            }

            var verifyResult = PasswordHasher.VerifyHashedPassword(user.PasswordHash, password);
            if (verifyResult == PasswordVerificationResult.Failed)
            {
                return false;
            }

            if (verifyResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                UpgradePasswordHash(user, password);
            }

            userId = user.Id;
            return true;
        }

        public string FindUserIdByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            var orgId = _organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId();
            ApplicationUser user;
            if (orgId.HasValue)
            {
                user = _users.FindByEmailAndOrganization(email.Trim(), orgId.Value);
            }
            else
            {
                user = _users.FindByEmail(email.Trim());
            }

            return user == null ? null : user.Id;
        }

        public UserAccountCreateResult CreateUser(UserAccountCreateRequest request, string password)
        {
            var organizationId = request == null ? null : request.OrganizationId;
            if (!organizationId.HasValue && _organizationScope != null)
            {
                organizationId = _organizationScope.GetCurrentOrganizationId();
            }

            var user = new ApplicationUser
            {
                Email = request == null ? null : request.Email,
                EmployeeNumber = request == null ? null : request.EmployeeNumber,
                FirstName = request == null ? null : request.FirstName,
                LastName = request == null ? null : request.LastName,
                Phone = request == null ? null : request.Phone,
                DepartmentId = request == null ? null : request.DepartmentId,
                PositionTitle = request == null ? null : request.PositionTitle,
                RoleId = request == null ? null : request.RoleId,
                OrganizationId = organizationId,
                IsActive = true,
                EmailConfirmed = true,
                AccessToken = SecurePasswordGenerator.GenerateAccessToken(),
                RequirePasswordChange = request != null && request.RequirePasswordChange,
                IsEmailVerified = !IsEmailVerificationRequired()
            };

            var errors = ValidateNewUser(user, password).ToList();
            foreach (var seatError in ValidateSeatCap(user.OrganizationId))
            {
                errors.Add(seatError);
            }

            if (errors.Count > 0)
            {
                return new UserAccountCreateResult { Succeeded = false, Errors = errors };
            }

            if (!user.OrganizationId.HasValue)
            {
                if (_organizationScope == null || !_organizationScope.IsActualPlatformAdmin())
                {
                    return new UserAccountCreateResult
                    {
                        Succeeded = false,
                        Errors = new[] { "Organization context is required to create a user." }
                    };
                }

                if (_users.FindPlatformAdminByEmail(user.Email) != null)
                {
                    return new UserAccountCreateResult
                    {
                        Succeeded = false,
                        Errors = new[] { "Email '" + user.Email + "' is already taken for a system user." }
                    };
                }
            }
            else
            {
                if (_users.FindByEmailAndOrganization(user.Email, user.OrganizationId.Value) != null)
                {
                    return new UserAccountCreateResult
                    {
                        Succeeded = false,
                        Errors = new[] { "Email '" + user.Email + "' is already taken in this organization." }
                    };
                }
            }

            user.PasswordHash = PasswordHasher.HashPassword(password);
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.UserName = user.Email;

            IDbConnection enlistedConnection;
            IDbTransaction enlistedTransaction;
            if (_unitOfWorkEnlistment != null
                && _unitOfWorkEnlistment.TryGetActiveTransaction(out enlistedConnection, out enlistedTransaction))
            {
                _users.Insert(user, enlistedConnection as SqlConnection, enlistedTransaction as SqlTransaction);
            }
            else
            {
                _users.Insert(user);
            }

            _auditWriter?.Write("Users.Create", nameof(ApplicationUser), user.Id, null, user.Email);

            return new UserAccountCreateResult
            {
                Succeeded = true,
                UserId = user.Id,
                Errors = new string[0]
            };
        }

        public string RequestPasswordReset(string email)
        {
            return RequestPasswordReset(email, null);
        }

        public string RequestPasswordReset(string email, string organizationSlug)
        {
            ApplicationUser user = null;
            if (!string.IsNullOrWhiteSpace(organizationSlug))
            {
                var orgId = _users.FindOrganizationIdBySlug(organizationSlug);
                if (orgId.HasValue)
                {
                    user = _users.FindByEmailAndOrganization(email, orgId.Value);
                }
            }
            else
            {
                user = _users.FindPlatformAdminByEmail(email)
                    ?? _users.FindActiveUserByEmail(email);
            }

            if (user == null || !user.IsActive)
            {
                return null;
            }

            var relaxed = DeploymentSecuritySettings.MfaAllowAnyCode;
            if (!relaxed && (_emailService == null || !_emailService.IsConfigured))
            {
                System.Diagnostics.Trace.WriteLine(
                    "Password reset suppressed: SMTP is not configured while MfaAllowAnyCode=false.");
                return null;
            }

            var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                .Replace("+", string.Empty)
                .Replace("/", string.Empty)
                .TrimEnd('=');
            var tokenHash = ComputeTokenHash(user.SecurityStamp, token);
            var resetLink = BuildPasswordResetLink(token, user.Email, organizationSlug);

            if (_emailService != null && _emailService.IsConfigured)
            {
                try
                {
                    _emailService.SendPasswordResetEmail(user.Email, resetLink);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine("Password reset email failed: " + ex.Message);
                    if (!relaxed)
                    {
                        return null;
                    }
                }
            }
            else if (!relaxed)
            {
                return null;
            }

            _resetTokens.CreateToken(user.Id, tokenHash, DateTime.UtcNow.AddHours(24));
            if (relaxed)
            {
                SecurityDiagnostics.LogPasswordResetLink(user.Email, resetLink);
            }

            return token;
        }

        public IEnumerable<string> GetPasswordPolicyErrors(string password)
        {
            return PasswordPolicy.Validate(password).ToList();
        }

        public bool ChangePassword(string userId, string currentPassword, string newPassword)
        {
            var user = _users.FindById(userId);
            if (user == null)
            {
                return false;
            }

            var currentVerifyResult = PasswordHasher.VerifyHashedPassword(user.PasswordHash, currentPassword);
            if (string.IsNullOrEmpty(currentPassword)
                || currentVerifyResult == PasswordVerificationResult.Failed)
            {
                return false;
            }

            if (PasswordHasher.VerifyHashedPassword(user.PasswordHash, newPassword) != PasswordVerificationResult.Failed)
            {
                return false;
            }

            return ResetPassword(userId, newPassword);
        }

        public bool UpdateProfile(string userId, string firstName, string lastName, string phone)
        {
            var user = _users.FindById(userId);
            if (user == null)
            {
                return false;
            }

            user.FirstName = firstName == null ? null : firstName.Trim();
            user.LastName = lastName == null ? null : lastName.Trim();
            user.Phone = phone == null ? null : phone.Trim();
            user.PhoneNumber = user.Phone;
            _users.Update(user);
            _auditWriter?.Write("Users.EditProfile", nameof(ApplicationUser), userId, null, user.Email);
            return true;
        }

        private string BuildPasswordResetLink(string token, string email, string organizationSlug)
        {
            var configuredBaseUrl = _platformSettings == null ? null : _platformSettings.GetExternalBaseUrl();
            var requestUrl = HttpContext.Current != null && HttpContext.Current.Request != null
                ? HttpContext.Current.Request.Url
                : null;
            var baseUrl = AssetScanUrlHelper.ResolvePasswordResetBaseUrl(configuredBaseUrl, requestUrl);
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = "http://localhost";
            }

            baseUrl = baseUrl.Trim().TrimEnd('/');
            var path = string.IsNullOrWhiteSpace(organizationSlug)
                ? "/Account/ResetPassword"
                : "/" + organizationSlug.Trim('/') + "/Account/ResetPassword";
            return baseUrl + path + "?code=" + Uri.EscapeDataString(token) + "&email=" + Uri.EscapeDataString(email ?? string.Empty);
        }

        public bool ResetPasswordWithToken(string email, string token, string newPassword)
        {
            return ResetPasswordWithToken(email, token, newPassword, null);
        }

        public bool ResetPasswordWithToken(string email, string token, string newPassword, string organizationSlug)
        {
            return ResetPasswordWithTokenDetailed(email, token, newPassword, organizationSlug).Succeeded;
        }

        public PasswordResetResult ResetPasswordWithTokenDetailed(string email, string token, string newPassword, string organizationSlug)
        {
            var result = new PasswordResetResult
            {
                PolicyErrors = new List<string>()
            };

            var user = ResolveUserForPasswordReset(email, organizationSlug);
            if (user == null)
            {
                result.FailureReason = PasswordResetFailureReason.UserNotFound;
                return result;
            }

            var policyErrors = ValidatePasswordPolicy(newPassword).ToList();
            if (policyErrors.Count > 0)
            {
                result.FailureReason = PasswordResetFailureReason.PolicyViolation;
                result.PolicyErrors = policyErrors;
                return result;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                result.FailureReason = PasswordResetFailureReason.InvalidOrExpiredToken;
                return result;
            }

            var tokenHash = ComputeTokenHash(user.SecurityStamp, token.Trim());
            string consumedUserId;
            if (!_resetTokens.TryConsumeToken(user.Id, tokenHash, out consumedUserId))
            {
                result.FailureReason = PasswordResetFailureReason.InvalidOrExpiredToken;
                return result;
            }

            if (!ApplyPasswordReset(user.Id, newPassword))
            {
                result.FailureReason = PasswordResetFailureReason.InvalidOrExpiredToken;
                return result;
            }

            result.Succeeded = true;
            return result;
        }

        private ApplicationUser ResolveUserForPasswordReset(string email, string organizationSlug)
        {
            if (!string.IsNullOrWhiteSpace(organizationSlug))
            {
                var orgId = _users.FindOrganizationIdBySlug(organizationSlug);
                if (orgId.HasValue)
                {
                    return _users.FindByEmailAndOrganization(email, orgId.Value);
                }

                return null;
            }

            return _users.FindPlatformAdminByEmail(email)
                ?? _users.FindActiveUserByEmail(email);
        }

        public bool ResetPassword(string userId, string newPassword)
        {
            var errors = ValidatePasswordPolicy(newPassword).ToList();
            if (errors.Count > 0)
            {
                return false;
            }

            return ApplyPasswordReset(userId, newPassword);
        }

        private bool ApplyPasswordReset(string userId, string newPassword)
        {
            var user = _users.FindById(userId);
            if (user == null)
            {
                return false;
            }

            user.PasswordHash = PasswordHasher.HashPassword(newPassword);
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.AccessToken = SecurePasswordGenerator.GenerateAccessToken();
            user.LastPasswordChange = DateTime.UtcNow;
            user.RequirePasswordChange = false;
            _users.Update(user);
            _auditWriter?.Write("Users.PasswordChanged", nameof(ApplicationUser), userId, null, null);
            return true;
        }

        private bool IsEmailVerificationRequired()
        {
            var setting = System.Configuration.ConfigurationManager.AppSettings["EmailVerificationRequired"];
            return string.Equals(setting, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(setting, "1", StringComparison.OrdinalIgnoreCase);
        }

        private IEnumerable<string> ValidateSeatCap(int? organizationId)
        {
            if (!organizationId.HasValue || _licenseService == null)
            {
                yield break;
            }

            var license = _licenseService.GetLicenseForOrganization(organizationId.Value);
            if (license == null || !license.MaxUsers.HasValue)
            {
                yield break;
            }

            var activeUsers = _users.CountActiveUsersInOrganization(organizationId.Value);
            if (activeUsers >= license.MaxUsers.Value)
            {
                yield return "This organization has reached its licensed user seat limit (" + license.MaxUsers.Value + ").";
            }
        }

        private static IEnumerable<string> ValidateNewUser(ApplicationUser user, string password)
        {
            if (user == null)
            {
                yield return "User profile is required.";
                yield break;
            }

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                yield return "Email is required.";
            }

            foreach (var error in ValidatePasswordPolicy(password))
            {
                yield return error;
            }
        }

        private static string ComputeTokenHash(string securityStamp, string token)
        {
            var key = string.IsNullOrWhiteSpace(securityStamp) ? "asset-management-reset" : securityStamp;
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
            {
                return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(token ?? string.Empty)));
            }
        }

        public void RehashPasswordOnLogin(string userId, string plainPassword)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrEmpty(plainPassword))
            {
                return;
            }

            var user = _users.FindById(userId);
            if (user == null)
            {
                return;
            }

            var verifyResult = PasswordHasher.VerifyHashedPassword(user.PasswordHash, plainPassword);
            if (verifyResult != PasswordVerificationResult.SuccessRehashNeeded)
            {
                return;
            }

            UpgradePasswordHash(user, plainPassword);
        }

        private void UpgradePasswordHash(ApplicationUser user, string plainPassword)
        {
            user.PasswordHash = PasswordHasher.HashPassword(plainPassword);
            _users.Update(user);
        }

        private static IEnumerable<string> ValidatePasswordPolicy(string password)
        {
            return PasswordPolicy.Validate(password);
        }
    }
}
