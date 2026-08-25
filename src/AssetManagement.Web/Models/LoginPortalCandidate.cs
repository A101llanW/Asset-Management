namespace AssetManagement.Web.Models
{
    public class LoginPortalCandidate
    {
        public string UserId { get; set; }

        public string Email { get; set; }

        public int? OrganizationId { get; set; }

        public string OrganizationName { get; set; }

        public string OrganizationSlug { get; set; }
    }
}
