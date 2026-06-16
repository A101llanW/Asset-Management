using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Contracts.Organizations
{
    public class OrganizationCreateRequest
    {
        public string Name { get; set; }

        public string Slug { get; set; }

        public string AdminEmail { get; set; }

        public string AdminFirstName { get; set; }

        public string AdminLastName { get; set; }
    }

    public class OrganizationCreateResult
    {
        public bool Succeeded { get; set; }

        public Organization Organization { get; set; }

        public string CompanyAdminUserId { get; set; }

        public string Message { get; set; }
    }

    public interface IOrganizationService
    {
        OrganizationCreateResult CreateOrganization(OrganizationCreateRequest request);
    }
}
