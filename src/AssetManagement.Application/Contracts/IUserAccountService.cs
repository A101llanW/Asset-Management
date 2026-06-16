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

        string FindUserIdByEmail(string email);

        UserAccountCreateResult CreateUser(UserAccountCreateRequest request, string password);

        bool ResetPassword(string userId, string newPassword);

        string RequestPasswordReset(string email);

        string RequestPasswordReset(string email, string organizationSlug);

        bool ResetPasswordWithToken(string email, string token, string newPassword);

        System.Collections.Generic.IEnumerable<string> GetPasswordPolicyErrors(string password);

        bool ChangePassword(string userId, string currentPassword, string newPassword);

        bool UpdateProfile(string userId, string firstName, string lastName, string phone);
    }
}
