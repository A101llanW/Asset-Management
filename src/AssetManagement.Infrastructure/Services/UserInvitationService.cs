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
            var emailSent = TrySendInvitationEmail(request.Email.Trim(), inviteLink, request.OrganizationName);

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

            var createRequest = new UserAccountCreateRequest
            {
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Phone = request.Phone,
                RoleId = invitation.RoleId,
                DepartmentId = invitation.DepartmentId,
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

            if (string.IsNullOrWhiteSpace(request.OrganizationSlug))
            {
                yield return "Organization portal slug is required.";
            }
        }

        private bool TrySendInvitationEmail(string email, string inviteLink, string organizationName)
        {
            if (_emailService == null || !_emailService.IsConfigured)
            {
                if (DeploymentSecuritySettings.MfaAllowAnyCode)
                {
                    SecurityDiagnostics.LogInvitationLink(email, inviteLink);
                }

                return DeploymentSecuritySettings.MfaAllowAnyCode;
            }

            try
            {
                _emailService.SendUserInvitationEmail(email, inviteLink, organizationName);
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
            var baseUrl = AssetScanUrlHelper.ResolvePasswordResetBaseUrl(configuredBaseUrl, requestUrl);
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = "http://localhost";
            }

            baseUrl = baseUrl.Trim().TrimEnd('/');
            var path = "/" + organizationSlug.Trim('/') + "/Account/AcceptInvite";
            return baseUrl + path + "?code=" + Uri.EscapeDataString(token);
        }
    }
}
