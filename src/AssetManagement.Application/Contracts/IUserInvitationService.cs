using System;
using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public class UserInvitationCreateRequest
    {
        public int OrganizationId { get; set; }

        public string InvitedByUserId { get; set; }

        public string Email { get; set; }

        public int? RoleId { get; set; }

        public int? DepartmentId { get; set; }

        public string OrganizationSlug { get; set; }

        public string OrganizationName { get; set; }
    }

    public class UserInvitationCreateResult
    {
        public bool Succeeded { get; set; }

        public int InvitationId { get; set; }

        public string InviteLink { get; set; }

        public IEnumerable<string> Errors { get; set; }
    }

    public class UserInvitationValidationResult
    {
        public bool IsValid { get; set; }

        public string Email { get; set; }

        public bool EmailLocked { get; set; }

        public string OrganizationName { get; set; }
    }

    public class UserInvitationAcceptRequest
    {
        public string Token { get; set; }

        public int OrganizationId { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Email { get; set; }

        public string Phone { get; set; }

        public string Password { get; set; }
    }

    public enum UserInvitationAcceptFailureReason
    {
        None = 0,
        InvalidOrExpiredToken,
        EmailMismatch,
        PolicyViolation,
        UserCreationFailed
    }

    public class UserInvitationAcceptResult
    {
        public bool Succeeded { get; set; }

        public UserInvitationAcceptFailureReason FailureReason { get; set; }

        public IEnumerable<string> Errors { get; set; }
    }

    public class UserInvitationListItemVm
    {
        public int Id { get; set; }

        public string Email { get; set; }

        public string RoleName { get; set; }

        public string DepartmentName { get; set; }

        public DateTime ExpiresAtUtc { get; set; }

        public DateTime? UsedAtUtc { get; set; }

        public string Status { get; set; }

        public DateTime CreatedAtUtc { get; set; }
    }

    public interface IUserInvitationService
    {
        UserInvitationCreateResult CreateInvitation(UserInvitationCreateRequest request);

        UserInvitationValidationResult ValidateInvitation(string token, int organizationId);

        UserInvitationAcceptResult AcceptInvitation(UserInvitationAcceptRequest request);

        PagedListVm<UserInvitationListItemVm> GetListPage(int organizationId, int page, int pageSize);
    }
}
