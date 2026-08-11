using System;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Helpers
{
    public static class ReportBrandingHelper
    {
        public static ReportBrandingVm Resolve(
            IUnitOfWork unitOfWork,
            IOrganizationScopeService organizationScope,
            string applicationBaseUrl = null)
        {
            if (unitOfWork == null || organizationScope == null)
            {
                return null;
            }

            var organizationId = organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return null;
            }

            var organization = unitOfWork.Repository<Organization>().GetById(organizationId.Value);
            if (organization == null)
            {
                return null;
            }

            return new ReportBrandingVm
            {
                OrganizationName = organization.Name,
                OrganizationLogoUrl = ToAbsoluteLogoUrl(NormalizeLogoUrl(organization.LogoPath), applicationBaseUrl)
            };
        }

        public static string ToAbsoluteLogoUrl(string logoUrl, string applicationBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(logoUrl))
            {
                return null;
            }

            if (logoUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || logoUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return logoUrl;
            }

            if (string.IsNullOrWhiteSpace(applicationBaseUrl))
            {
                return logoUrl;
            }

            return applicationBaseUrl.TrimEnd('/') + (logoUrl.StartsWith("/") ? logoUrl : "/" + logoUrl);
        }

        private static string NormalizeLogoUrl(string logoPath)
        {
            if (string.IsNullOrWhiteSpace(logoPath))
            {
                return null;
            }

            var trimmed = logoPath.Trim();
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            if (trimmed.StartsWith("/"))
            {
                return trimmed;
            }

            return "/" + trimmed.TrimStart('~', '/');
        }
    }
}
