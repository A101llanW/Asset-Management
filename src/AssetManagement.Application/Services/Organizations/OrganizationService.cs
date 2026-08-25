using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Organizations;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Security;
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
            if (request == null || !request.SkipPlatformAdminCheck)
            {
                if (!_organizationScope.IsActualPlatformAdmin())
                {
                    return new OrganizationCreateResult { Succeeded = false, Message = "Only platform administrators can create organizations." };
                }
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

            Organization organization = null;
            OrganizationCreateResult adminResult = null;
            var now = DateTime.UtcNow;

            try
            {
                _unitOfWork.ExecuteInTransaction(() =>
                {
                    organization = new Organization
                    {
                        Name = request.Name.Trim(),
                        Slug = slug,
                        Status = "Active",
                        Code = slug.ToUpperInvariant().Replace("-", string.Empty),
                        CurrencyCode = FinanceDefaults.DefaultCurrencyCode,
                        AccessToken = SecurePasswordGenerator.GenerateAccessToken().Substring(0, 8).ToUpperInvariant(),
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

                    EnsureTenantRoles(organization.Id, now, request.RoleTemplateOrganizationSlug);
                    if (!request.SkipCoreReferenceSeed)
                    {
                        SeedCoreReferenceData(organization.Id, now);
                    }
                    adminResult = CreateCompanyAdmin(organization, request);
                    if (!adminResult.Succeeded)
                    {
                        throw new InvalidOperationException(adminResult.Message);
                    }
                });
            }
            catch (InvalidOperationException ex)
            {
                return new OrganizationCreateResult
                {
                    Succeeded = false,
                    Organization = organization,
                    Message = ex.Message
                };
            }

            _auditWriter.Write("ORGANIZATION_CREATED", "Organization", organization.Id.ToString(), null,
                "{\"Name\":\"" + organization.Name + "\",\"Slug\":\"" + organization.Slug + "\"}");

            return new OrganizationCreateResult
            {
                Succeeded = true,
                Organization = organization,
                CompanyAdminUserId = adminResult.CompanyAdminUserId,
                AdminEmail = adminResult.AdminEmail,
                ProvisionalPassword = adminResult.ProvisionalPassword,
                Message = "Organization created successfully."
            };
        }

        public void EnsureTenantRoles(int organizationId)
        {
            EnsureTenantRoles(organizationId, null);
        }

        public void EnsureTenantRoles(int organizationId, string roleTemplateOrganizationSlug)
        {
            EnsureTenantRoles(organizationId, DateTime.UtcNow, roleTemplateOrganizationSlug);
            EnsureApprovalSettings(organizationId, DateTime.UtcNow, refreshInvalidStageRoleIds: true);
            _unitOfWork.SaveChanges();
        }

        private void EnsureTenantRoles(int organizationId, DateTime now, string roleTemplateOrganizationSlug)
        {
            if (organizationId <= 0)
            {
                return;
            }

            _organizationScope.SetOrganizationFilterOverride(organizationId);
            try
            {
                if (_unitOfWork.Repository<Role>().Query()
                    .Any(r => r.OrganizationId == organizationId && r.IsActive))
                {
                    return;
                }
            }
            finally
            {
                _organizationScope.SetOrganizationFilterOverride(null);
            }

            var templateOrgId = ResolveRoleTemplateOrganizationId(organizationId, roleTemplateOrganizationSlug);
            if (templateOrgId <= 0)
            {
                return;
            }

            _organizationScope.SetOrganizationFilterOverride(templateOrgId);
            try
            {
                var roleMap = new Dictionary<int, int>();
                foreach (var templateRole in _unitOfWork.Repository<Role>().Query()
                    .Where(r => r.OrganizationId == templateOrgId && r.IsActive)
                    .ToList())
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

                foreach (var rp in _unitOfWork.Repository<RolePermission>().Query()
                    .Where(x => x.OrganizationId == templateOrgId)
                    .ToList())
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

                var targetHasSettings = false;
                _organizationScope.SetOrganizationFilterOverride(organizationId);
                try
                {
                    targetHasSettings = _unitOfWork.Repository<SystemSetting>().Query()
                        .Any(x => x.OrganizationId == organizationId);
                }
                finally
                {
                    _organizationScope.SetOrganizationFilterOverride(templateOrgId);
                }

                if (!targetHasSettings)
                {
                    foreach (var setting in _unitOfWork.Repository<SystemSetting>().Query()
                        .Where(x => x.OrganizationId == templateOrgId)
                        .ToList())
                    {
                        if (OrganizationApprovalDefaults.IsApprovalSettingKey(setting.SettingKey))
                        {
                            continue;
                        }

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
                }

                EnsureApprovalSettings(organizationId, now, refreshInvalidStageRoleIds: true);
                _unitOfWork.SaveChanges();
            }
            finally
            {
                _organizationScope.SetOrganizationFilterOverride(null);
            }
        }

        private void EnsureApprovalSettings(int organizationId, DateTime now, bool refreshInvalidStageRoleIds)
        {
            _organizationScope.SetOrganizationFilterOverride(organizationId);
            try
            {
                var roles = _unitOfWork.Repository<Role>().Query()
                    .Where(r => r.OrganizationId == organizationId && r.IsActive)
                    .ToList();
                if (roles.Count == 0)
                {
                    return;
                }

                var settings = _unitOfWork.Repository<SystemSetting>().Query()
                    .Where(x => x.OrganizationId == organizationId)
                    .ToList();
                var settingRepo = _unitOfWork.Repository<SystemSetting>();

                OrganizationApprovalDefaults.EnsureApprovalSettings(
                    settings,
                    roles,
                    organizationId,
                    now,
                    settingRepo.Add,
                    settingRepo.Update,
                    refreshInvalidStageRoleIds);
            }
            finally
            {
                _organizationScope.SetOrganizationFilterOverride(null);
            }
        }

        private int ResolveRoleTemplateOrganizationId(int excludeOrganizationId, string templateOrganizationSlug = null)
        {
            _organizationScope.SetOrganizationFilterOverride(null);
            try
            {
                if (!string.IsNullOrWhiteSpace(templateOrganizationSlug))
                {
                    var slug = templateOrganizationSlug.Trim().ToLowerInvariant();
                    var templateBySlug = _unitOfWork.Repository<Organization>().Query()
                        .FirstOrDefault(o => o.Slug == slug && o.Id != excludeOrganizationId && o.IsActive);
                    if (templateBySlug != null)
                    {
                        return templateBySlug.Id;
                    }
                }

                // Prefer any org that already has tenant roles (covers renamed default/nanosoft slug).
                var templateFromRoles = _unitOfWork.Repository<Role>().Query()
                    .Where(r => r.OrganizationId.HasValue
                        && r.OrganizationId.Value > 0
                        && r.OrganizationId.Value != excludeOrganizationId
                        && r.IsActive)
                    .OrderBy(r => r.OrganizationId.Value)
                    .Select(r => r.OrganizationId.Value)
                    .FirstOrDefault();
                if (templateFromRoles > 0)
                {
                    return templateFromRoles;
                }

                return _unitOfWork.Repository<Organization>().Query()
                    .Where(o => o.Id != excludeOrganizationId && o.IsActive)
                    .OrderBy(o => o.Id)
                    .Select(o => o.Id)
                    .FirstOrDefault();
            }
            finally
            {
                _organizationScope.SetOrganizationFilterOverride(null);
            }
        }

        private void SeedCoreReferenceData(int organizationId, DateTime now)
        {
            _organizationScope.SetOrganizationFilterOverride(organizationId);
            try
            {
                var departmentRepo = _unitOfWork.Repository<Department>();
                foreach (var seed in CoreReferenceCatalog.Departments)
                {
                    if (departmentRepo.Query().Any(d => d.OrganizationId == organizationId && d.Code == seed.Code))
                    {
                        continue;
                    }

                    departmentRepo.Add(new Department
                    {
                        Name = seed.Name,
                        Code = seed.Code,
                        Description = seed.Description,
                        OrganizationId = organizationId,
                        CreatedAt = now,
                        IsActive = true
                    });
                }

                var categoryRepo = _unitOfWork.Repository<AssetCategory>();
                var categoryIdsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var existing in categoryRepo.Query().Where(c => c.OrganizationId == organizationId).ToList())
                {
                    categoryIdsByName[existing.Name] = existing.Id;
                }

                foreach (var seed in CoreReferenceCatalog.Categories)
                {
                    if (categoryIdsByName.ContainsKey(seed.Name))
                    {
                        var existingCategory = categoryRepo.Query()
                            .FirstOrDefault(c => c.OrganizationId == organizationId && c.Name == seed.Name);
                        if (existingCategory != null)
                        {
                            ApplyCategoryDepreciationDefaults(existingCategory, seed);
                            categoryRepo.Update(existingCategory);
                        }

                        continue;
                    }

                    var category = new AssetCategory
                    {
                        Name = seed.Name,
                        Description = seed.Description,
                        DefaultDepreciationLifeMonths = seed.DefaultDepreciationLifeMonths,
                        DefaultDepreciationRatePercent = seed.DefaultDepreciationRatePercent,
                        OrganizationId = organizationId,
                        CreatedAt = now,
                        IsActive = true
                    };
                    categoryRepo.Add(category);
                    _unitOfWork.SaveChanges();
                    categoryIdsByName[seed.Name] = category.Id;
                }

                _unitOfWork.SaveChanges();

                var typeRepo = _unitOfWork.Repository<AssetType>();
                foreach (var seed in CoreReferenceCatalog.AssetTypes)
                {
                    int categoryId;
                    if (!categoryIdsByName.TryGetValue(seed.CategoryName, out categoryId))
                    {
                        continue;
                    }

                    if (typeRepo.Query().Any(t => t.OrganizationId == organizationId && t.Name == seed.Name))
                    {
                        continue;
                    }

                    typeRepo.Add(new AssetType
                    {
                        AssetCategoryId = categoryId,
                        Name = seed.Name,
                        Description = seed.Description,
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

        private static void ApplyCategoryDepreciationDefaults(AssetCategory category, CoreReferenceCatalog.CategorySeed seed)
        {
            if (category == null || seed == null)
            {
                return;
            }

            if (!category.DefaultDepreciationLifeMonths.HasValue && seed.DefaultDepreciationLifeMonths.HasValue)
            {
                category.DefaultDepreciationLifeMonths = seed.DefaultDepreciationLifeMonths;
            }

            if (!category.DefaultDepreciationRatePercent.HasValue && seed.DefaultDepreciationRatePercent.HasValue)
            {
                category.DefaultDepreciationRatePercent = seed.DefaultDepreciationRatePercent;
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

                var provisionalPassword = SecurePasswordGenerator.Generate();
                var createResult = _userAccountService.CreateUser(new UserAccountCreateRequest
                {
                    Email = email,
                    FirstName = string.IsNullOrWhiteSpace(request.AdminFirstName) ? "Company" : request.AdminFirstName,
                    LastName = string.IsNullOrWhiteSpace(request.AdminLastName) ? "Admin" : request.AdminLastName,
                    RoleId = companyAdminRole.Id,
                    EmployeeNumber = "EMP-" + organization.Id.ToString("D4"),
                    OrganizationId = organization.Id,
                    RequirePasswordChange = true
                }, provisionalPassword);

                if (!createResult.Succeeded)
                {
                    return new OrganizationCreateResult { Succeeded = false, Message = string.Join("; ", createResult.Errors ?? new string[0]) };
                }

                return new OrganizationCreateResult
                {
                    Succeeded = true,
                    CompanyAdminUserId = createResult.UserId,
                    AdminEmail = email,
                    ProvisionalPassword = provisionalPassword
                };
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
