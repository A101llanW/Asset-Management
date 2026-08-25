using System;
using System.IO;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Organizations;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Services.Organizations
{
    public class SchoolOrganizationBootstrapService : ISchoolOrganizationBootstrapService
    {
        private readonly IOrganizationService _organizationService;
        private readonly IAssetImportService _assetImportService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IReferenceDataCache _referenceDataCache;

        public SchoolOrganizationBootstrapService(
            IOrganizationService organizationService,
            IAssetImportService assetImportService,
            IUnitOfWork unitOfWork,
            IOrganizationScopeService organizationScope,
            IReferenceDataCache referenceDataCache)
        {
            _organizationService = organizationService;
            _assetImportService = assetImportService;
            _unitOfWork = unitOfWork;
            _organizationScope = organizationScope;
            _referenceDataCache = referenceDataCache;
        }

        public SchoolOrganizationBootstrapResult Bootstrap(SchoolOrganizationBootstrapRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                return Fail("Organization name is required.");
            }

            if (string.IsNullOrWhiteSpace(request.TemplatePath) || !File.Exists(request.TemplatePath))
            {
                return Fail("Template file was not found: " + (request.TemplatePath ?? "(null)"));
            }

            var slug = string.IsNullOrWhiteSpace(request.Slug)
                ? request.Name.Trim().ToLowerInvariant()
                : request.Slug.Trim().ToLowerInvariant();

            if (_unitOfWork.Repository<Organization>().Query().Any(o => o.Slug == slug))
            {
                return Fail("Organization slug '" + slug + "' is already in use.");
            }

            var roleTemplateSlug = string.IsNullOrWhiteSpace(request.RoleTemplateOrganizationSlug)
                ? "nanosoft"
                : request.RoleTemplateOrganizationSlug.Trim().ToLowerInvariant();

            var adminEmail = string.IsNullOrWhiteSpace(request.AdminEmail)
                ? slug + "@asset.local"
                : request.AdminEmail.Trim();

            _organizationScope.SetPlatformAdminOverride(true);
            _organizationScope.SetCompanyAdminOverride(true);
            try
            {
                var createResult = _organizationService.CreateOrganization(new OrganizationCreateRequest
                {
                    Name = request.Name.Trim(),
                    Slug = slug,
                    AdminEmail = adminEmail,
                    SkipCoreReferenceSeed = true,
                    RoleTemplateOrganizationSlug = roleTemplateSlug,
                    SkipPlatformAdminCheck = true
                });

                if (!createResult.Succeeded || createResult.Organization == null)
                {
                    return Fail(createResult.Message ?? "Organization creation failed.");
                }

                var organizationId = createResult.Organization.Id;
                _organizationScope.SetOrganizationFilterOverride(organizationId);
                _unitOfWork.ClearTracking();

                // Departments (admin, sub-units, grade/class leaves), categories, types,
                // suppliers, and assets are provisioned solely from the Excel template.
                AssetImportResultVm importResult;
                using (var stream = File.OpenRead(request.TemplatePath))
                {
                    importResult = _assetImportService.Import(
                        stream,
                        Path.GetFileName(request.TemplatePath),
                        createResult.CompanyAdminUserId ?? "system");
                }

                return new SchoolOrganizationBootstrapResult
                {
                    Succeeded = true,
                    OrganizationId = organizationId,
                    Slug = slug,
                    AdminEmail = createResult.AdminEmail ?? adminEmail,
                    ProvisionalPassword = createResult.ProvisionalPassword,
                    ImportedCount = importResult.ImportedCount,
                    SkippedCount = importResult.SkippedCount,
                    ImportMessages = importResult.Messages,
                    Message = "Organization '" + request.Name.Trim() + "' created and template imported."
                };
            }
            finally
            {
                _organizationScope.SetOrganizationFilterOverride(null);
                _organizationScope.SetCompanyAdminOverride(false);
                _organizationScope.SetPlatformAdminOverride(false);
            }
        }

        private static SchoolOrganizationBootstrapResult Fail(string message)
        {
            return new SchoolOrganizationBootstrapResult
            {
                Succeeded = false,
                Message = message
            };
        }
    }
}
