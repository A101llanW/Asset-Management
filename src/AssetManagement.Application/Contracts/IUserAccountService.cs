using System.Collections.Generic;

namespace AssetManagement.Application.Contracts
{
    public class UserAccountCreateRequest
    {
        public string Email { get; set; }

        public string EmployeeNumber { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Phone { get; set; }

        public int? DepartmentId { get; set; }

        public string PositionTitle { get; set; }

        public int? RoleId { get; set; }

        public int? OrganizationId { get; set; }

        public bool RequirePasswordChange { get; set; }
    }

    public class UserAccountCreateResult
    {
        public bool Succeeded { get; set; }

        public string UserId { get; set; }

        public IEnumerable<string> Errors { get; set; }
    }

    public interface IUserAccountService
    {
        bool ValidateCredentials(string email, string password, out string userId);

        bool ValidateCredentials(string email, string password, string organizationSlug, out string userId);

        /// <summary>
        /// Re-hashes the password to the current algorithm after a successful legacy-format verification.
        /// </summary>
        void RehashPasswordOnLogin(string userId, string plainPassword);

        string FindUserIdByEmail(string email);

        UserAccountCreateResult CreateUser(UserAccountCreateRequest request, string password);

        bool ResetPassword(string userId, string newPassword);

        string RequestPasswordReset(string email);

        string RequestPasswordReset(string email, string organizationSlug);

        bool ResetPasswordWithToken(string email, string token, string newPassword);

        bool ResetPasswordWithToken(string email, string token, string newPassword, string organizationSlug);

        PasswordResetResult ResetPasswordWithTokenDetailed(string email, string token, string newPassword, string organizationSlug);

        System.Collections.Generic.IEnumerable<string> GetPasswordPolicyErrors(string password);

        bool ChangePassword(string userId, string currentPassword, string newPassword);

        bool UpdateProfile(string userId, string firstName, string lastName, string phone);
    }
}
