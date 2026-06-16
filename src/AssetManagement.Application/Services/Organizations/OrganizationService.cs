using System;
using System.Linq;
using AssetManagement.Application;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Organizations;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Domain.Entities;
namespace AssetManagement.Application.Services.Organizations
{
    public class OrganizationService : IOrganizationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IUserAccountService _userAccountService;
        private readonly IAuditWriter _auditWriter;

        public OrganizationService(
            IUnitOfWork unitOfWork,
            IOrganizationScopeService organizationScope,
            IUserAccountService userAccountService,
            IAuditWriter auditWriter)
        {
            _unitOfWork = unitOfWork;
            _organizationScope = organizationScope;
            _userAccountService = userAccountService;
            _auditWriter = auditWriter;
        }

        public OrganizationCreateResult CreateOrganization(OrganizationCreateRequest request)
        {
            if (!_organizationScope.IsActualPlatformAdmin())
            {
                return new OrganizationCreateResult { Succeeded = false, Message = "Only platform administrators can create organizations." };
            }

            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                return new OrganizationCreateResult { Succeeded = false, Message = "Organization name is required." };
            }

            var slug = string.IsNullOrWhiteSpace(request.Slug)
                ? GenerateUniqueSlug()
                : request.Slug.Trim().ToLowerInvariant();

            if (_unitOfWork.Repository<Organization>().Query().Any(o => o.Slug == slug))
            {
                return new OrganizationCreateResult { Succeeded = false, Message = "Organization slug is already in use." };
            }

            var now = DateTime.UtcNow;
            var organization = new Organization
            {
                Name = request.Name.Trim(),
                Slug = slug,
                Status = "Active",
                Code = slug.ToUpperInvariant().Replace("-", string.Empty),
                CurrencyCode = FinanceDefaults.DefaultCurrencyCode,
                CreatedAt = now,
                IsActive = true
            };
            _unitOfWork.Repository<Organization>().Add(organization);
            _unitOfWork.SaveChanges();

            var license = OrganizationLicenseService.CreateDefaultLicense(organization.Id, now);
            _unitOfWork.Repository<OrganizationLicense>().Add(license);
            _unitOfWork.SaveChanges();
            _unitOfWork.Repository<OrganizationLicenseHistory>().Add(new OrganizationLicenseHistory
            {
                OrganizationLicenseId = license.Id,
                OrganizationId = organization.Id,
                Action = "Created",
                NewExpiryDate = license.ExpiryDate,
                NewStatus = license.Status,
                PerformedBy = "system",
                Reason = "Organization provisioning",
                CreatedAt = now
            });
            _unitOfWork.SaveChanges();

            CloneRolesAndSettings(organization.Id, now);
            var adminResult = CreateCompanyAdmin(organization, request);
            if (!adminResult.Succeeded)
            {
                return new OrganizationCreateResult
                {
                    Succeeded = false,
                    Organization = organization,
                    Message = adminResult.Message
                };
            }

            _auditWriter.Write("ORGANIZATION_CREATED", "Organization", organization.Id.ToString(), null,
                "{\"Name\":\"" + organization.Name + "\",\"Slug\":\"" + organization.Slug + "\"}");

            return new OrganizationCreateResult
            {
                Succeeded = true,
                Organization = organization,
                CompanyAdminUserId = adminResult.CompanyAdminUserId,
                Message = "Organization created successfully."
            };
        }

        private void CloneRolesAndSettings(int organizationId, DateTime now)
        {
            var templateOrgId = _unitOfWork.Repository<Organization>().Query()
                .OrderBy(o => o.Id)
                .Select(o => o.Id)
                .FirstOrDefault();

            if (templateOrgId <= 0)
            {
                return;
            }

            _organizationScope.SetOrganizationFilterOverride(templateOrgId);
            try
            {
                var roleMap = new System.Collections.Generic.Dictionary<int, int>();
                foreach (var templateRole in _unitOfWork.Repository<Role>().Query().Where(r => r.OrganizationId == templateOrgId).ToList())
                {
                    if (string.Equals(templateRole.Name, "Platform Admin", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var newRole = new Role
                    {
                        Name = templateRole.Name,
                        Description = templateRole.Description,
                        IsSystemRole = templateRole.IsSystemRole,
                        OrganizationId = organizationId,
                        CreatedAt = now,
                        IsActive = true
                    };
                    _unitOfWork.Repository<Role>().Add(newRole);
                    _unitOfWork.SaveChanges();
                    roleMap[templateRole.Id] = newRole.Id;
                }

                foreach (var rp in _unitOfWork.Repository<RolePermission>().Query().Where(x => x.OrganizationId == templateOrgId).ToList())
                {
                    int newRoleId;
                    if (!roleMap.TryGetValue(rp.RoleId, out newRoleId))
                    {
                        continue;
                    }

                    _unitOfWork.Repository<RolePermission>().Add(new RolePermission
                    {
                        RoleId = newRoleId,
                        PermissionId = rp.PermissionId,
                        OrganizationId = organizationId
                    });
                }

                foreach (var setting in _unitOfWork.Repository<SystemSetting>().Query().Where(x => x.OrganizationId == templateOrgId).ToList())
                {
                    _unitOfWork.Repository<SystemSetting>().Add(new SystemSetting
                    {
                        SettingKey = setting.SettingKey,
                        SettingValue = setting.SettingValue,
                        Description = setting.Description,
                        OrganizationId = organizationId,
                        CreatedAt = now,
                        IsActive = true
                    });
                }

                _unitOfWork.SaveChanges();
            }
            finally
            {
                _organizationScope.SetOrganizationFilterOverride(null);
            }
        }

        private OrganizationCreateResult CreateCompanyAdmin(Organization organization, OrganizationCreateRequest request)
        {
            _organizationScope.SetOrganizationFilterOverride(organization.Id);
            try
            {
                var companyAdminRole = _unitOfWork.Repository<Role>().Query()
                    .FirstOrDefault(r => r.OrganizationId == organization.Id && r.Name == "Company Admin");

                if (companyAdminRole == null)
                {
                    companyAdminRole = new Role
                    {
                        Name = "Company Admin",
                        Description = "Tenant-wide company administrator",
                        IsSystemRole = true,
                        OrganizationId = organization.Id,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    _unitOfWork.Repository<Role>().Add(companyAdminRole);
                    _unitOfWork.SaveChanges();

                    foreach (var permission in _unitOfWork.Repository<Permission>().Query().ToList())
                    {
                        _unitOfWork.Repository<RolePermission>().Add(new RolePermission
                        {
                            RoleId = companyAdminRole.Id,
                            PermissionId = permission.Id,
                            OrganizationId = organization.Id
                        });
                    }
                    _unitOfWork.SaveChanges();
                }

                var email = string.IsNullOrWhiteSpace(request.AdminEmail)
                    ? "admin@" + organization.Slug + ".asset.local"
                    : request.AdminEmail.Trim();

                var createResult = _userAccountService.CreateUser(new UserAccountCreateRequest
                {
                    Email = email,
                    FirstName = string.IsNullOrWhiteSpace(request.AdminFirstName) ? "Company" : request.AdminFirstName,
                    LastName = string.IsNullOrWhiteSpace(request.AdminLastName) ? "Admin" : request.AdminLastName,
                    RoleId = companyAdminRole.Id,
                    EmployeeNumber = "EMP-" + organization.Id.ToString("D4"),
                    OrganizationId = organization.Id
                }, "P@ssw0rd!");

                if (!createResult.Succeeded)
                {
                    return new OrganizationCreateResult { Succeeded = false, Message = string.Join("; ", createResult.Errors ?? new string[0]) };
                }

                return new OrganizationCreateResult { Succeeded = true, CompanyAdminUserId = createResult.UserId };
            }
            finally
            {
                _organizationScope.SetOrganizationFilterOverride(null);
            }
        }

        private string GenerateUniqueSlug()
        {
            var slug = GenerateRandomSlugToken();
            while (_unitOfWork.Repository<Organization>().Query().Any(o => o.Slug == slug))
            {
                slug = GenerateRandomSlugToken();
            }

            return slug;
        }

        private static string GenerateRandomSlugToken()
        {
            var bytes = new byte[9];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string numbers = "0123456789";

            var slug = letters[bytes[0] % letters.Length].ToString();
            for (var i = 1; i < 9; i++)
            {
                slug += numbers[bytes[i] % numbers.Length].ToString();
            }

            return slug;
        }
    }
}
