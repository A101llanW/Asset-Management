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

        /// <summary>
        /// When true, skips generic CoreReferenceCatalog departments/categories/types (use template import instead).
        /// </summary>
        public bool SkipCoreReferenceSeed { get; set; }

        /// <summary>
        /// Slug of an existing organization whose roles, permissions, and settings are cloned (e.g. nanosoft).
        /// </summary>
        public string RoleTemplateOrganizationSlug { get; set; }

        /// <summary>
        /// Allows trusted bootstrap tools (Runner) to create organizations without a platform admin session.
        /// </summary>
        public bool SkipPlatformAdminCheck { get; set; }
    }

    public class OrganizationCreateResult
    {
        public bool Succeeded { get; set; }

        public Organization Organization { get; set; }

        public string CompanyAdminUserId { get; set; }

        public string AdminEmail { get; set; }

        public string ProvisionalPassword { get; set; }

        public string Message { get; set; }
    }

    public interface IOrganizationService
    {
        OrganizationCreateResult CreateOrganization(OrganizationCreateRequest request);

        /// <summary>
        /// Ensures the organization has tenant roles (cloned from a template org when missing).
        /// </summary>
        void EnsureTenantRoles(int organizationId);

        void EnsureTenantRoles(int organizationId, string roleTemplateOrganizationSlug);
    }
}
