using AssetManagement.Application.ViewModels.Organizations;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Contracts.Organizations
{
    public interface IOrganizationLicenseService
    {
        LicenseListPageVm GetLicenseListPage(LicenseListFilterVm filter, string sort, string direction, int page, int pageSize);

        OrganizationLicenseDetailVm GetByOrganizationId(int organizationId);

        LicenseOperationResult Renew(RenewLicenseRequest request, string performedBy);

        LicenseOperationResult Pause(PauseLicenseRequest request, string performedBy);

        LicenseOperationResult Resume(ResumeLicenseRequest request, string performedBy);

        LicenseOperationResult UpdatePlan(UpdatePlanRequest request, string performedBy);

        LicenseStatus GetEffectiveStatus(OrganizationLicense license);

        OrganizationLicense GetLicenseForOrganization(int organizationId);

        int ProcessExpiredLicenses();
    }
}
