using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Web.ViewModels
{
    public class PlatformUserCreateViewModel : UserCreateViewModel
    {
        /// <summary>
        /// Null selects a system (platform) user; otherwise the target organization.
        /// </summary>
        public int? OrganizationId { get; set; }
    }
}
