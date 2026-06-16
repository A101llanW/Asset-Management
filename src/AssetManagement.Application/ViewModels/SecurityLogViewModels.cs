using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Application.ViewModels
{
    public class SecurityLogFilterVm
    {
        public string Username { get; set; }

        public string IpAddress { get; set; }

        public bool? WasSuccessful { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public string EventType { get; set; }
    }

    public class LoginAttemptLogVm
    {
        public int Id { get; set; }

        public string Username { get; set; }

        public string IpAddress { get; set; }

        public DateTime AttemptedAtUtc { get; set; }

        public bool WasSuccessful { get; set; }

        public string FailureReason { get; set; }

        public int? OrganizationId { get; set; }
    }

    public class SecurityEventLogVm
    {
        public int Id { get; set; }

        public string EventType { get; set; }

        public string Email { get; set; }

        public string IpAddress { get; set; }

        public int? OrganizationId { get; set; }

        public DateTime CreatedAtUtc { get; set; }
    }

    public class SecurityLogsPageVm
    {
        public SecurityLogFilterVm Filter { get; set; }

        public IList<LoginAttemptLogVm> LoginAttempts { get; set; }

        public IList<SecurityEventLogVm> SecurityEvents { get; set; }

        public int TotalLoginAttempts { get; set; }

        public int SuccessfulLogins { get; set; }

        public int FailedLoginAttempts { get; set; }

        public int TotalSecurityEvents { get; set; }
    }

    public class ProfileViewModel
    {
        [Display(Name = "First name")]
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; }

        [Display(Name = "Last name")]
        [Required]
        [StringLength(100)]
        public string LastName { get; set; }

        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Phone")]
        [StringLength(50)]
        public string Phone { get; set; }

        public string RoleName { get; set; }

        public string OrganizationName { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Display(Name = "Current password")]
        [Required]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; }

        [Display(Name = "New password")]
        [Required]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; }

        [Display(Name = "Confirm new password")]
        [Required]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; }
    }
}
