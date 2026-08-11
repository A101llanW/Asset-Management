using System;

namespace AssetManagement.Application.Contracts.Organizations
{
    public class OrganizationDeleteResult
    {
        public bool Succeeded { get; set; }

        public string OrganizationName { get; set; }

        public string Message { get; set; }
    }

    /// <summary>
    /// Permanently removes an organization and all tenant-scoped rows. Intended for test/demo use only.
    /// </summary>
    public interface IOrganizationPurgeService
    {
        OrganizationDeleteResult DeleteOrganizationAndData(int organizationId);
    }
}
