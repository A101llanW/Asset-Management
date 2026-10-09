using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Security;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Security;

namespace AssetManagement.Infrastructure.Services
{
    public class UserInvitationService : IUserInvitationService
    {
        private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

        private readonly UserInvitationRepository _invitations;
        private readonly RoleDepartmentScopeRepository _roleDepartments;
        private readonly UserAccountRepository _users;
        private readonly IUserAccountService _userAccountService;
        private readonly IEmailService _emailService;
        private readonly IPlatformSettingsService _platformSettings;
        private readonly IAuditWriter _auditWriter;

        public UserInvitationService(
            ISqlConnectionFactory connectionFactory,
            IUserAccountService userAccountService,
            IEmailService emailService,
            IPlatformSettingsService platformSettings,
            IAuditWriter auditWriter = null)
        {
            _invitations = new UserInvitationRepository(connectionFactory);
            _roleDepartments = new RoleDepartmentScopeRepository(connectionFactory);
            _users = new UserAccountRepository(connectionFactory);
            _userAccountService = userAccountService;
            _emailService = emailService;
            _platformSettings = platformSettings;
            _auditWriter = auditWriter;
        }

        public UserInvitationCreateResult CreateInvitation(UserInvitationCreateRequest request)
        {
            var errors = ValidateCreateRequest(request).ToList();
            if (errors.Count > 0)
            {
                return new UserInvitationCreateResult { Succeeded = false, Errors = errors };
            }

            var token = InvitationTokenHelper.GenerateToken();
            var tokenHash = InvitationTokenHelper.ComputeTokenHash(token);
            var now = DateTime.UtcNow;
            var invitation = new UserInvitation
            {
                TokenHash = tokenHash,
                OrganizationId = request.OrganizationId,
                InvitedByUserId = request.InvitedByUserId,
                Email = request.Email.Trim(),
                RoleId = request.RoleId,
                DepartmentId = request.DepartmentId,
                ExpiresAtUtc = now.Add(InvitationLifetime),
                CreatedAtUtc = now
            };

            var invitationId = _invitations.Create(invitation);
            var inviteLink = BuildInviteLink(token, request.OrganizationSlug);
            var emailSent = TrySendInvitationEmail(request.Email.Trim(), inviteLink, request.OrganizationName, request.OrganizationId);

            if (!emailSent && DeploymentSecuritySettings.RequiresSmtpForAuthEmails)
            {
                return new UserInvitationCreateResult
                {
                    Succeeded = false,
                    Errors = new[] { "Invitation was created but the email could not be sent. Configure SMTP in Platform Settings." }
                };
            }

            _auditWriter?.Write("Users.Invite", nameof(UserInvitation), invitationId.ToString(), null, request.Email);

            return new UserInvitationCreateResult
            {
                Succeeded = true,
                InvitationId = invitationId,
                InviteLink = inviteLink,
                Errors = new string[0]
            };
        }

        public UserInvitationValidationResult ValidateInvitation(string token, int organizationId)
        {
            var invitation = FindValidInvitation(token, organizationId);
            if (invitation == null)
            {
                return new UserInvitationValidationResult { IsValid = false };
            }

            return new UserInvitationValidationResult
            {
                IsValid = true,
                Email = invitation.Email,
                OrganizationName = null
            };
        }

        public UserInvitationAcceptResult AcceptInvitation(UserInvitationAcceptRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Token))
            {
                return InvalidTokenResult();
            }

            var tokenHash = InvitationTokenHelper.ComputeTokenHash(request.Token.Trim());
            var invitation = _invitations.FindValidByTokenHash(tokenHash);
            if (invitation == null || invitation.OrganizationId != request.OrganizationId)
            {
                return InvalidTokenResult();
            }

            var policyErrors = _userAccountService.GetPasswordPolicyErrors(request.Password).ToList();
            if (policyErrors.Count > 0)
            {
                return new UserInvitationAcceptResult
                {
                    Succeeded = false,
                    FailureReason = UserInvitationAcceptFailureReason.PolicyViolation,
                    Errors = policyErrors
                };
            }

            invitation = _invitations.TryMarkUsed(tokenHash);
            if (invitation == null)
            {
                return InvalidTokenResult();
            }

            // Always the role stored with the invitation (nothing posted by the invitee is used).
            // If it was deleted, deactivated or is not this organization's, fall back to the pre-change
            // default for invitations without a role: the account is created with no role (no permissions).
            var roleId = invitation.RoleId;
            if (roleId.HasValue && !_invitations.IsActiveTenantRole(roleId.Value, invitation.OrganizationId))
            {
                try
                {
                    _auditWriter?.Write("Users.InviteRoleFallback", nameof(UserInvitation), invitation.Id.ToString(), "RoleId=" + roleId.Value, "RoleId=none");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine("Invite role fallback audit failed: " + ex.Message);
                }

                roleId = null;
            }

            // Always the department stored with the invitation (nothing posted by the invitee is used). If it was
            // deleted, deactivated or is not this organization's, register with no department and audit the fallback.
            var departmentId = invitation.DepartmentId;
            if (departmentId.HasValue && !_roleDepartments.IsActiveTenantDepartment(departmentId.Value, invitation.OrganizationId))
            {
                try
                {
                    _auditWriter?.Write("Users.InviteDepartmentFallback", nameof(UserInvitation), invitation.Id.ToString(), "DepartmentId=" + departmentId.Value, "DepartmentId=none");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine("Invite department fallback audit failed: " + ex.Message);
                }

                departmentId = null;
            }

            var createRequest = new UserAccountCreateRequest
            {
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Phone = request.Phone,
                RoleId = roleId,
                DepartmentId = departmentId,
                OrganizationId = invitation.OrganizationId
            };

            var createResult = _userAccountService.CreateUser(createRequest, request.Password);
            if (!createResult.Succeeded)
            {
                return new UserInvitationAcceptResult
                {
                    Succeeded = false,
                    FailureReason = UserInvitationAcceptFailureReason.UserCreationFailed,
                    Errors = createResult.Errors ?? new string[0]
                };
            }

            _invitations.SetUsedByUserId(tokenHash, createResult.UserId);

            _auditWriter?.Write("Users.InviteAccept", nameof(ApplicationUser), createResult.UserId, null, request.Email);

            return new UserInvitationAcceptResult
            {
                Succeeded = true,
                FailureReason = UserInvitationAcceptFailureReason.None,
                Errors = new string[0]
            };
        }

        public PagedListVm<UserInvitationListItemVm> GetListPage(int organizationId, int page, int pageSize)
        {
            var safePageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100);
            var totalCount = _invitations.CountByOrganization(organizationId);
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / safePageSize));
            var safePage = Math.Min(Math.Max(page, 1), totalPages);
            var skip = (safePage - 1) * safePageSize;
            var rows = _invitations.GetByOrganization(organizationId, skip, safePageSize);
            var now = DateTime.UtcNow;

            return new PagedListVm<UserInvitationListItemVm>
            {
                Items = rows.Select(row => new UserInvitationListItemVm
                {
                    Id = row.Id,
                    Email = row.Email,
                    RoleName = row.RoleName,
                    DepartmentName = row.DepartmentName,
                    ExpiresAtUtc = row.ExpiresAtUtc,
                    UsedAtUtc = row.UsedAtUtc,
                    CreatedAtUtc = row.CreatedAtUtc,
                    Status = ResolveStatus(row, now)
                }).ToList(),
                TotalCount = totalCount,
                Page = safePage,
                PageSize = safePageSize
            };
        }

        private UserInvitation FindValidInvitation(string token, int organizationId)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var tokenHash = InvitationTokenHelper.ComputeTokenHash(token.Trim());
            var invitation = _invitations.FindValidByTokenHash(tokenHash);
            if (invitation == null || invitation.OrganizationId != organizationId)
            {
                return null;
            }

            return invitation;
        }

        private static string ResolveStatus(UserInvitationListRow row, DateTime nowUtc)
        {
            if (row.UsedAtUtc.HasValue)
            {
                return "Used";
            }

            if (row.ExpiresAtUtc <= nowUtc)
            {
                return "Expired";
            }

            return "Pending";
        }

        private static UserInvitationAcceptResult InvalidTokenResult()
        {
            return new UserInvitationAcceptResult
            {
                Succeeded = false,
                FailureReason = UserInvitationAcceptFailureReason.InvalidOrExpiredToken,
                Errors = new[] { "This invitation link is invalid or has expired." }
            };
        }

        private IEnumerable<string> ValidateCreateRequest(UserInvitationCreateRequest request)
        {
            if (request == null)
            {
                yield return "Invitation request is required.";
                yield break;
            }

            if (request.OrganizationId <= 0)
            {
                yield return "Organization context is required.";
            }

            if (string.IsNullOrWhiteSpace(request.InvitedByUserId))
            {
                yield return "Inviting user is required.";
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                yield return "Email is required to send an invitation.";
            }
            else if (!request.Email.Contains("@"))
            {
                yield return "A valid email address is required.";
            }
            else if (request.OrganizationId > 0
                && _users.FindByEmailAndOrganization(request.Email.Trim(), request.OrganizationId) != null)
            {
                yield return "A user with this email already exists in this organization.";
            }

            if (string.IsNullOrWhiteSpace(request.OrganizationSlug))
            {
                yield return "Organization portal slug is required.";
            }

            // The invitee's role is chosen and locked in before the link is sent.
            if (!request.RoleId.HasValue || request.RoleId.Value <= 0)
            {
                yield return "Select a role for the invitee.";
            }
            else if (request.OrganizationId > 0 && !_invitations.IsActiveTenantRole(request.RoleId.Value, request.OrganizationId))
            {
                yield return "The selected role is not an active role of this organization.";
            }

            // The invitee's department is locked in too: an active department of this organization, and the role
            // must apply in it (organization-wide roles apply everywhere; a department role only in its department).
            if (request.DepartmentId.HasValue && request.OrganizationId > 0
                && !_roleDepartments.IsActiveTenantDepartment(request.DepartmentId.Value, request.OrganizationId))
            {
                yield return "The selected department is not an active department of this organization.";
            }
            else if (request.RoleId.HasValue && request.RoleId.Value > 0
                && !_roleDepartments.RoleAppliesInDepartment(request.RoleId.Value, request.DepartmentId))
            {
                yield return "The selected role does not apply in the selected department.";
            }
        }

        private bool TrySendInvitationEmail(string email, string inviteLink, string organizationName, int organizationId)
        {
            // Invitations belong to the inviting organization: its SMTP when on and complete, else the platform default.
            var mailer = _emailService == null ? null : _emailService.ForOrganization(organizationId);
            if (mailer == null || !mailer.IsConfigured)
            {
                if (DeploymentSecuritySettings.MfaAllowAnyCode)
                {
                    SecurityDiagnostics.LogInvitationLink(email, inviteLink);
                }

                return DeploymentSecuritySettings.MfaAllowAnyCode;
            }

            try
            {
                mailer.SendUserInvitationEmail(email, inviteLink, organizationName);
                if (DeploymentSecuritySettings.MfaAllowAnyCode)
                {
                    SecurityDiagnostics.LogInvitationLink(email, inviteLink);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("User invitation email failed: " + ex.Message);
                if (DeploymentSecuritySettings.MfaAllowAnyCode)
                {
                    SecurityDiagnostics.LogInvitationLink(email, inviteLink);
                    return true;
                }

                return false;
            }
        }

        private string BuildInviteLink(string token, string organizationSlug)
        {
            var configuredBaseUrl = _platformSettings == null ? null : _platformSettings.GetExternalBaseUrl();
            var requestUrl = HttpContext.Current != null && HttpContext.Current.Request != null
                ? HttpContext.Current.Request.Url
                : null;
            var baseUrl = AssetScanUrlHelper.ResolvePasswordResetBaseUrl(configuredBaseUrl, requestUrl, HttpRuntime.AppDomainAppVirtualPath);
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                System.Diagnostics.Trace.TraceWarning(
                    "BuildInviteLink: no ExternalBaseUrl and no request URL; the link falls back to http://localhost. Set ExternalBaseUrl in Platform settings.");
                baseUrl = "http://localhost";
            }

            baseUrl = baseUrl.Trim().TrimEnd('/');
            var path = "/" + organizationSlug.Trim('/') + "/Account/AcceptInvite";
            return baseUrl + path + "?code=" + Uri.EscapeDataString(token);
        }
    }
}
