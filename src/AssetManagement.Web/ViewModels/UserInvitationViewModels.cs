using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Web.ViewModels
{
    public class UserInvitationCreateViewModel
    {
        [Required]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Invalid email address.")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Role")]
        public int? RoleId { get; set; }

        [Display(Name = "Department")]
        public int? DepartmentId { get; set; }
    }
}
