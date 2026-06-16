using System;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Organizations;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels.Organizations;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services.Organizations
{
    public class OrganizationLicenseService : IOrganizationLicenseService
    {
        private const int PendingRenewalWindowDays = 30;

        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrganizationLicenseQueryRepository _queryRepository;
        private readonly IAuditWriter _auditWriter;
        private readonly IOrganizationScopeService _organizationScope;

        public OrganizationLicenseService(
            IUnitOfWork unitOfWork,
            IOrganizationLicenseQueryRepository queryRepository,
            IAuditWriter auditWriter,
            IOrganizationScopeService organizationScope)
        {
            _unitOfWork = unitOfWork;
            _queryRepository = queryRepository;
            _auditWriter = auditWriter;
            _organizationScope = organizationScope;
        }

        public LicenseListPageVm GetLicenseListPage(LicenseListFilterVm filter, string sort, string direction, int page, int pageSize)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 10 : (pageSize > 100 ? 100 : pageSize);
            var normalizedFilter = filter ?? new LicenseListFilterVm();
            var totalCount = _queryRepository.CountLicenses(normalizedFilter);
            var skip = (page - 1) * pageSize;

            return new LicenseListPageVm
            {
                Items = _queryRepository.GetLicensePage(normalizedFilter, sort, direction, skip, pageSize),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                Sort = sort,
                Direction = direction,
                Filter = normalizedFilter
            };
        }

        public OrganizationLicenseDetailVm GetByOrganizationId(int organizationId)
        {
            var license = GetLicenseForOrganization(organizationId);
            if (license == null)
            {
                return null;
            }

            var organization = _unitOfWork.Repository<Organization>().GetById(organizationId);
            if (organization == null)
            {
                return null;
            }

            var effectiveStatus = GetEffectiveStatus(license);
            return new OrganizationLicenseDetailVm
            {
                LicenseId = license.Id,
                OrganizationId = organizationId,
                OrganizationName = organization.Name,
                OrganizationSlug = organization.Slug,
                PlanCode = license.PlanCode,
                PlanName = license.PlanName,
                Status = license.Status,
                EffectiveStatus = effectiveStatus,
                StartDate = license.StartDate,
                ExpiryDate = license.ExpiryDate,
                DaysRemaining = ComputeDaysRemaining(license.ExpiryDate),
                MaxUsers = license.MaxUsers,
                PausedAt = license.PausedAt,
                PausedBy = license.PausedBy,
                PauseReason = license.PauseReason,
                Notes = license.Notes,
                History = _queryRepository.GetHistoryForOrganization(organizationId)
            };
        }

        public LicenseOperationResult Renew(RenewLicenseRequest request, string performedBy)
        {
            EnsureManageAccess();
            if (request == null || request.OrganizationId <= 0)
            {
                return Failure("Organization is required.");
            }

            if (request.NewExpiryDate.Date <= DateTime.UtcNow.Date)
            {
                return Failure("New expiry date must be after today.");
            }

            var license = RequireLicense(request.OrganizationId);
            var previousExpiry = license.ExpiryDate;
            var previousStatus = license.Status;

            license.ExpiryDate = request.NewExpiryDate;
            license.Status = LicenseStatus.Active.ToString();
            license.PausedAt = null;
            license.PausedBy = null;
            license.PauseReason = null;
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                license.Notes = request.Notes.Trim();
            }

            license.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<OrganizationLicense>().Update(license);
            AppendHistory(license, "Renewed", previousStatus, license.Status, previousExpiry, license.ExpiryDate, performedBy, request.Notes);
            _auditWriter.Write(
                "LICENSE_RENEWED",
                "OrganizationLicense",
                license.Id.ToString(),
                "{\"ExpiryDate\":\"" + previousExpiry.ToString("o") + "\"}",
                "{\"ExpiryDate\":\"" + license.ExpiryDate.ToString("o") + "\"}");
            _unitOfWork.SaveChanges();

            return Success("License renewed successfully.");
        }

        public LicenseOperationResult Pause(PauseLicenseRequest request, string performedBy)
        {
            EnsureManageAccess();
            if (request == null || request.OrganizationId <= 0)
            {
                return Failure("Organization is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return Failure("A pause reason is required.");
            }

            var license = RequireLicense(request.OrganizationId);
            if (string.Equals(license.Status, LicenseStatus.Paused.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return Failure("License is already paused.");
            }

            var previousStatus = license.Status;
            license.Status = LicenseStatus.Paused.ToString();
            license.PausedAt = DateTime.UtcNow;
            license.PausedBy = performedBy;
            license.PauseReason = request.Reason.Trim();
            license.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Repository<OrganizationLicense>().Update(license);
            AppendHistory(license, "Paused", previousStatus, license.Status, license.ExpiryDate, license.ExpiryDate, performedBy, request.Reason);
            _auditWriter.Write(
                "LICENSE_PAUSED",
                "OrganizationLicense",
                license.Id.ToString(),
                "{\"Status\":\"" + previousStatus + "\"}",
                "{\"Status\":\"" + license.Status + "\",\"Reason\":\"" + EscapeJson(license.PauseReason) + "\"}");
            _unitOfWork.SaveChanges();

            return Success("License paused. Tenant portal access is suspended.");
        }

        public LicenseOperationResult Resume(ResumeLicenseRequest request, string performedBy)
        {
            EnsureManageAccess();
            if (request == null || request.OrganizationId <= 0)
            {
                return Failure("Organization is required.");
            }

            var license = RequireLicense(request.OrganizationId);
            if (!string.Equals(license.Status, LicenseStatus.Paused.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return Failure("Only paused licenses can be resumed.");
            }

            if (license.ExpiryDate < DateTime.UtcNow)
            {
                return Failure("License has expired. Renew the license before resuming.");
            }

            var previousStatus = license.Status;
            license.Status = LicenseStatus.Active.ToString();
            license.PausedAt = null;
            license.PausedBy = null;
            license.PauseReason = null;
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                license.Notes = request.Notes.Trim();
            }

            license.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<OrganizationLicense>().Update(license);
            AppendHistory(license, "Resumed", previousStatus, license.Status, license.ExpiryDate, license.ExpiryDate, performedBy, request.Notes);
            _auditWriter.Write(
                "LICENSE_RESUMED",
                "OrganizationLicense",
                license.Id.ToString(),
                "{\"Status\":\"" + previousStatus + "\"}",
                "{\"Status\":\"" + license.Status + "\"}");
            _unitOfWork.SaveChanges();

            return Success("License resumed. Tenant portal access restored.");
        }

        public LicenseOperationResult UpdatePlan(UpdatePlanRequest request, string performedBy)
        {
            EnsureManageAccess();
            if (request == null || request.OrganizationId <= 0)
            {
                return Failure("Organization is required.");
            }

            if (string.IsNullOrWhiteSpace(request.PlanCode) || string.IsNullOrWhiteSpace(request.PlanName))
            {
                return Failure("Plan code and name are required.");
            }

            var license = RequireLicense(request.OrganizationId);
            var previousPlan = license.PlanCode + "/" + license.PlanName;
            license.PlanCode = request.PlanCode.Trim();
            license.PlanName = request.PlanName.Trim();
            license.MaxUsers = request.MaxUsers;
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                license.Notes = request.Notes.Trim();
            }

            license.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<OrganizationLicense>().Update(license);
            AppendHistory(license, "PlanChanged", license.Status, license.Status, license.ExpiryDate, license.ExpiryDate, performedBy, request.Notes);
            _auditWriter.Write(
                "LICENSE_PLAN_CHANGED",
                "OrganizationLicense",
                license.Id.ToString(),
                "{\"Plan\":\"" + EscapeJson(previousPlan) + "\"}",
                "{\"Plan\":\"" + EscapeJson(license.PlanCode + "/" + license.PlanName) + "\"}");
            _unitOfWork.SaveChanges();

            return Success("License plan updated.");
        }

        public LicenseStatus GetEffectiveStatus(OrganizationLicense license)
        {
            if (license == null)
            {
                return LicenseStatus.Expired;
            }

            if (string.Equals(license.Status, LicenseStatus.Paused.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return LicenseStatus.Paused;
            }

            if (string.Equals(license.Status, LicenseStatus.Expired.ToString(), StringComparison.OrdinalIgnoreCase)
                || license.ExpiryDate < DateTime.UtcNow)
            {
                return LicenseStatus.Expired;
            }

            if (string.Equals(license.Status, LicenseStatus.PendingRenewal.ToString(), StringComparison.OrdinalIgnoreCase)
                || license.ExpiryDate <= DateTime.UtcNow.AddDays(PendingRenewalWindowDays))
            {
                return LicenseStatus.PendingRenewal;
            }

            return LicenseStatus.Active;
        }

        public OrganizationLicense GetLicenseForOrganization(int organizationId)
        {
            if (organizationId <= 0)
            {
                return null;
            }

            return _unitOfWork.Repository<OrganizationLicense>().Query()
                .FirstOrDefault(l => l.IsActive && l.OrganizationId == organizationId);
        }

        public int ProcessExpiredLicenses()
        {
            var now = DateTime.UtcNow;
            var candidates = _queryRepository.GetLicensesDueForExpiry();
            var processed = 0;

            foreach (var candidate in candidates)
            {
                var license = _unitOfWork.Repository<OrganizationLicense>().GetById(candidate.LicenseId);
                if (license == null || !license.IsActive)
                {
                    continue;
                }

                var previousStatus = license.Status;
                license.Status = LicenseStatus.Expired.ToString();
                license.UpdatedAt = now;
                _unitOfWork.Repository<OrganizationLicense>().Update(license);
                AppendHistory(license, "Expired", previousStatus, license.Status, license.ExpiryDate, license.ExpiryDate, "system", "Automatic expiry");
                _auditWriter.Write(
                    "LICENSE_EXPIRED",
                    "OrganizationLicense",
                    license.Id.ToString(),
                    "{\"Status\":\"" + previousStatus + "\"}",
                    "{\"Status\":\"" + license.Status + "\"}");
                processed++;
            }

            if (processed > 0)
            {
                _unitOfWork.SaveChanges();
            }

            return processed;
        }

        public static OrganizationLicense CreateDefaultLicense(int organizationId, DateTime? startDate = null)
        {
            var start = startDate ?? DateTime.UtcNow;
            return new OrganizationLicense
            {
                OrganizationId = organizationId,
                PlanCode = "Standard",
                PlanName = "Standard",
                Status = LicenseStatus.Active.ToString(),
                StartDate = start,
                ExpiryDate = start.AddMonths(12),
                CreatedAt = start,
                IsActive = true
            };
        }

        private OrganizationLicense RequireLicense(int organizationId)
        {
            var license = GetLicenseForOrganization(organizationId);
            if (license == null)
            {
                throw new BusinessException("No license record exists for this organization.");
            }

            return license;
        }

        private void AppendHistory(
            OrganizationLicense license,
            string action,
            string previousStatus,
            string newStatus,
            DateTime? previousExpiry,
            DateTime? newExpiry,
            string performedBy,
            string reason)
        {
            _unitOfWork.Repository<OrganizationLicenseHistory>().Add(new OrganizationLicenseHistory
            {
                OrganizationLicenseId = license.Id,
                OrganizationId = license.OrganizationId,
                Action = action,
                PreviousExpiryDate = previousExpiry,
                NewExpiryDate = newExpiry,
                PreviousStatus = previousStatus,
                NewStatus = newStatus,
                PerformedBy = string.IsNullOrWhiteSpace(performedBy) ? "system" : performedBy,
                Reason = reason,
                CreatedAt = DateTime.UtcNow
            });
        }

        private void EnsureManageAccess()
        {
            if (_organizationScope == null || !_organizationScope.IsActualPlatformAdmin())
            {
                throw new BusinessException("Platform administrator access is required.");
            }
        }

        private static int ComputeDaysRemaining(DateTime expiryDate)
        {
            return (int)Math.Ceiling((expiryDate - DateTime.UtcNow).TotalDays);
        }

        private static LicenseOperationResult Success(string message)
        {
            return new LicenseOperationResult { Succeeded = true, Message = message };
        }

        private static LicenseOperationResult Failure(string message)
        {
            return new LicenseOperationResult { Succeeded = false, Message = message };
        }

        private static string EscapeJson(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
