using System.Collections.Generic;
using AssetManagement.Application.ViewModels.Organizations;

namespace AssetManagement.Application.Contracts.Organizations
{
    public interface IOrganizationLicenseQueryRepository
    {
        int CountLicenses(LicenseListFilterVm filter);

        IList<LicenseListItemVm> GetLicensePage(LicenseListFilterVm filter, string sort, string direction, int skip, int take);

        IList<LicenseHistoryItemVm> GetHistoryForOrganization(int organizationId);

        IList<LicenseExpiryCandidateVm> GetLicensesDueForExpiry();
    }

    public class LicenseExpiryCandidateVm
    {
        public int LicenseId { get; set; }

        public int OrganizationId { get; set; }

        public string Status { get; set; }

        public System.DateTime ExpiryDate { get; set; }
    }
}
