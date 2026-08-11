using System.IO;

namespace AssetManagement.Application.Contracts.Organizations
{
    public class SchoolOrganizationBootstrapRequest
    {
        public string Name { get; set; }

        public string Slug { get; set; }

        public string AdminEmail { get; set; }

        public string RoleTemplateOrganizationSlug { get; set; }

        public string TemplatePath { get; set; }
    }

    public class SchoolOrganizationBootstrapResult
    {
        public bool Succeeded { get; set; }

        public int OrganizationId { get; set; }

        public string Slug { get; set; }

        public string AdminEmail { get; set; }

        public string ProvisionalPassword { get; set; }

        public int ImportedCount { get; set; }

        public int SkippedCount { get; set; }

        public System.Collections.Generic.IList<string> ImportMessages { get; set; }

        public string Message { get; set; }
    }

    public interface ISchoolOrganizationBootstrapService
    {
        SchoolOrganizationBootstrapResult Bootstrap(SchoolOrganizationBootstrapRequest request);
    }
}
