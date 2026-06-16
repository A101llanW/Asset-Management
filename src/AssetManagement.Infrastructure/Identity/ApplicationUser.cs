using System;
using AssetManagement.Domain.Common;

namespace AssetManagement.Infrastructure.Identity
{
    public class ApplicationUser : ITenantEntity
    {
        public string Id { get; set; }

        public string Email { get; set; }

        public bool EmailConfirmed { get; set; }

        public string PasswordHash { get; set; }

        public string SecurityStamp { get; set; }

        public string PhoneNumber { get; set; }

        public bool PhoneNumberConfirmed { get; set; }

        public bool TwoFactorEnabled { get; set; }

        public string MfaMethod { get; set; }

        public string TwoFactorCode { get; set; }

        public DateTime? TwoFactorExpiryUtc { get; set; }

        public DateTime? PrivacyAcceptedAt { get; set; }

        public DateTime? TermsAcceptedAt { get; set; }

        public string PrivacyVersion { get; set; }

        public string TermsVersion { get; set; }

        public DateTime? LockoutEndDateUtc { get; set; }

        public bool LockoutEnabled { get; set; }

        public int AccessFailedCount { get; set; }

        public string UserName { get; set; }

        public string EmployeeNumber { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Phone { get; set; }

        public int? DepartmentId { get; set; }

        public string PositionTitle { get; set; }

        public bool IsActive { get; set; }

        public int? RoleId { get; set; }

        public int? OrganizationId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string FullName { get { return (FirstName + " " + LastName).Trim(); } }
    }
}
