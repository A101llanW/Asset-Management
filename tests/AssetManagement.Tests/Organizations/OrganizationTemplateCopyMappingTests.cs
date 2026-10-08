using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Organizations;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services.Organizations;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Tests.Helpers;
using AssetManagement.Tests.Security;
using Moq;
using NUnit.Framework;

namespace AssetManagement.Tests.Organizations
{
    /// <summary>
    /// New company from a template org: approval stage roles are mapped to the new company's copies of the template
    /// roles; unmapped ids are dropped and reported; persons are never copied.
    /// </summary>
    [TestFixture]
    public class OrganizationTemplateCopyMappingTests
    {
        private const int TemplateOrgId = 11;

        [Test]
        public void CreateOrganization_MapsTemplateStageRoles_DropsUnmapped_AndReports()
        {
            var unitOfWork = new FakeUnitOfWork();
            var audit = new OrgReferenceValidatorTests.RecordingAuditWriter();
            unitOfWork.Seed(new Organization { Id = TemplateOrgId, Name = "Template 3", Slug = "template-3", IsActive = true });
            unitOfWork.Seed(new Role { Id = 41, Name = OrganizationApprovalDefaults.CompanyAdminRoleName, OrganizationId = TemplateOrgId, IsActive = true });
            unitOfWork.Seed(new Role { Id = 45, Name = OrganizationApprovalDefaults.DepartmentHeadRoleName, OrganizationId = TemplateOrgId, IsActive = true });
            unitOfWork.Seed(new Role { Id = 50, Name = "Retired Approver", OrganizationId = TemplateOrgId, IsActive = false });
            SeedSetting(unitOfWork, ApprovalProcessCodes.GetStageRoleIdsSettingKey(ApprovalProcessCodes.Transfer), "45,50");
            SeedSetting(unitOfWork, ApprovalProcessCodes.GetStageUserIdsSettingKey(ApprovalProcessCodes.Transfer), "template-user,");
            SeedSetting(unitOfWork, ApprovalProcessCodes.GetStageRoleIdsSettingKey(ApprovalProcessCodes.Disposal), "41,45");
            SeedSetting(unitOfWork, "Finance.DefaultCurrency", "KES");

            var accounts = new Mock<IUserAccountService>();
            accounts.Setup(x => x.CreateUser(It.IsAny<UserAccountCreateRequest>(), It.IsAny<string>()))
                .Returns(new UserAccountCreateResult { Succeeded = true, UserId = "new-admin" });
            var service = new OrganizationService(unitOfWork, new FakeOrganizationScopeService(TemplateOrgId, platformAdmin: true), accounts.Object, audit);

            var result = service.CreateOrganization(new OrganizationCreateRequest
            {
                Name = "Copy Co",
                Slug = "copy-co",
                SkipPlatformAdminCheck = true,
                SkipCoreReferenceSeed = true,
                RoleTemplateOrganizationSlug = "template-3"
            });

            Assert.IsTrue(result.Succeeded, result.Message);
            var newOrgId = result.Organization.Id;
            var newRoles = unitOfWork.Repository<Role>().Find(r => r.OrganizationId == newOrgId).ToList();
            var newHead = newRoles.Single(r => r.Name == OrganizationApprovalDefaults.DepartmentHeadRoleName).Id;
            var newAdmin = newRoles.Single(r => r.Name == OrganizationApprovalDefaults.CompanyAdminRoleName).Id;
            var newRoleIds = new HashSet<int>(newRoles.Select(r => r.Id));

            var transfer = Setting(unitOfWork, newOrgId, ApprovalProcessCodes.GetStageRoleIdsSettingKey(ApprovalProcessCodes.Transfer));
            var disposal = Setting(unitOfWork, newOrgId, ApprovalProcessCodes.GetStageRoleIdsSettingKey(ApprovalProcessCodes.Disposal));
            CollectionAssert.AreEqual(new[] { newHead }, ApprovalWorkflowSettingsHelper.ParseStageRoleIds(transfer));
            CollectionAssert.AreEqual(new[] { newAdmin, newHead }, ApprovalWorkflowSettingsHelper.ParseStageRoleIds(disposal));
            Assert.AreEqual(string.Empty, Setting(unitOfWork, newOrgId, ApprovalProcessCodes.GetStageUserIdsSettingKey(ApprovalProcessCodes.Transfer)) ?? string.Empty);
            Assert.AreEqual("KES", Setting(unitOfWork, newOrgId, "Finance.DefaultCurrency"));

            // No setting of the new company points at a role outside it.
            foreach (var setting in unitOfWork.Repository<SystemSetting>().Find(s => s.OrganizationId == newOrgId)
                .Where(s => s.SettingKey.EndsWith(".StageRoleIds", StringComparison.OrdinalIgnoreCase)))
            {
                Assert.IsTrue(ApprovalWorkflowSettingsHelper.ParseStageRoleIds(setting.SettingValue).All(newRoleIds.Contains), setting.SettingKey);
            }

            StringAssert.Contains("role #50", result.Message);
            StringAssert.Contains("person template-user", result.Message);
            Assert.AreEqual(2, audit.Entries.Count(e => e.Action == OrgReferenceGuard.AuditAction));
        }

        [Test]
        public void CreateOrganization_CleanTemplate_NoWarning()
        {
            var unitOfWork = new FakeUnitOfWork();
            var audit = new OrgReferenceValidatorTests.RecordingAuditWriter();
            unitOfWork.Seed(new Organization { Id = TemplateOrgId, Name = "Template 3", Slug = "template-3", IsActive = true });
            unitOfWork.Seed(new Role { Id = 45, Name = OrganizationApprovalDefaults.DepartmentHeadRoleName, OrganizationId = TemplateOrgId, IsActive = true });
            SeedSetting(unitOfWork, ApprovalProcessCodes.GetStageRoleIdsSettingKey(ApprovalProcessCodes.Transfer), "45");

            var accounts = new Mock<IUserAccountService>();
            accounts.Setup(x => x.CreateUser(It.IsAny<UserAccountCreateRequest>(), It.IsAny<string>()))
                .Returns(new UserAccountCreateResult { Succeeded = true, UserId = "new-admin" });
            var service = new OrganizationService(unitOfWork, new FakeOrganizationScopeService(TemplateOrgId, platformAdmin: true), accounts.Object, audit);

            var result = service.CreateOrganization(new OrganizationCreateRequest
            {
                Name = "Clean Co",
                Slug = "clean-co",
                SkipPlatformAdminCheck = true,
                SkipCoreReferenceSeed = true,
                RoleTemplateOrganizationSlug = "template-3"
            });

            Assert.IsTrue(result.Succeeded, result.Message);
            Assert.AreEqual("Organization created successfully.", result.Message);
            Assert.AreEqual(0, audit.Entries.Count(e => e.Action == OrgReferenceGuard.AuditAction));
        }

        private static void SeedSetting(FakeUnitOfWork unitOfWork, string key, string value)
        {
            unitOfWork.Seed(new SystemSetting { SettingKey = key, SettingValue = value, OrganizationId = TemplateOrgId, IsActive = true, CreatedAt = DateTime.UtcNow });
        }

        private static string Setting(FakeUnitOfWork unitOfWork, int organizationId, string key)
        {
            var setting = unitOfWork.Repository<SystemSetting>().Find(s => s.OrganizationId == organizationId && s.SettingKey == key).FirstOrDefault();
            return setting == null ? null : setting.SettingValue;
        }
    }
}
