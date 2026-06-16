using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Web.Models
{
    public class LoginViewModel
    {
        [Required]
        [Display(Name = "Email")]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Invalid email address.")]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }

    public class ForgotPasswordViewModel
    {
        [Required]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Invalid email address.")]
        public string Email { get; set; }
    }

    public class RegisterViewModel
    {
        [Required]
        [StringLength(80)]
        [Display(Name = "First name")]
        public string FirstName { get; set; }

        [Required]
        [StringLength(80)]
        [Display(Name = "Last name")]
        public string LastName { get; set; }

        [Required]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Invalid email address.")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [StringLength(60)]
        [Display(Name = "Phone")]
        public string Phone { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [StringLength(100)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; }
    }

    public class ResetPasswordViewModel
    {
        [Required]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Invalid email address.")]
        public string Email { get; set; }

        [Required]
        [StringLength(100)]
        [RegularExpression(@".{6,}", ErrorMessage = "The password must be at least 6 characters long.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; }

        public string Code { get; set; }
    }
}
