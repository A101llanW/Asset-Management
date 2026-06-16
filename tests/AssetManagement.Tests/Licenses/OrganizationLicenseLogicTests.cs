using System;
using AssetManagement.Application.ViewModels.Organizations;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Licenses
{
    [TestFixture]
    public class OrganizationLicenseLogicTests
    {
        [Test]
        public void GetEffectiveStatus_ReturnsPaused_WhenStatusIsPaused()
        {
            var service = TestServiceFactory.CreateOrganizationLicenseService(new FakeUnitOfWork());
            var status = service.GetEffectiveStatus(new OrganizationLicense
            {
                Status = LicenseStatus.Paused.ToString(),
                ExpiryDate = DateTime.UtcNow.AddMonths(6)
            });

            Assert.AreEqual(LicenseStatus.Paused, status);
        }

        [Test]
        public void GetEffectiveStatus_ReturnsExpired_WhenPastExpiryDate()
        {
            var service = TestServiceFactory.CreateOrganizationLicenseService(new FakeUnitOfWork());
            var status = service.GetEffectiveStatus(new OrganizationLicense
            {
                Status = LicenseStatus.Active.ToString(),
                ExpiryDate = DateTime.UtcNow.AddDays(-1)
            });

            Assert.AreEqual(LicenseStatus.Expired, status);
        }

        [Test]
        public void GetEffectiveStatus_ReturnsPendingRenewal_WhenExpiringWithinWindow()
        {
            var service = TestServiceFactory.CreateOrganizationLicenseService(new FakeUnitOfWork());
            var status = service.GetEffectiveStatus(new OrganizationLicense
            {
                Status = LicenseStatus.Active.ToString(),
                ExpiryDate = DateTime.UtcNow.AddDays(15)
            });

            Assert.AreEqual(LicenseStatus.PendingRenewal, status);
        }

        [Test]
        public void GetEffectiveStatus_ReturnsExpired_WhenLicenseIsNull()
        {
            var service = TestServiceFactory.CreateOrganizationLicenseService(new FakeUnitOfWork());
            Assert.AreEqual(LicenseStatus.Expired, service.GetEffectiveStatus(null));
        }

        [Test]
        public void Renew_ExtendsExpiryAndSetsActiveStatus()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Organization { Id = 2, Name = "Demo B", Slug = "demo-b", IsActive = true, CreatedAt = DateTime.UtcNow });
            unitOfWork.Seed(new OrganizationLicense
            {
                Id = 10,
                OrganizationId = 2,
                PlanCode = "Standard",
                PlanName = "Standard",
                Status = LicenseStatus.Active.ToString(),
                StartDate = DateTime.UtcNow.AddMonths(-6),
                ExpiryDate = DateTime.UtcNow.AddDays(10),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            var service = TestServiceFactory.CreateOrganizationLicenseService(unitOfWork);
            var newExpiry = DateTime.UtcNow.AddYears(1).Date;
            var result = service.Renew(new RenewLicenseRequest
            {
                OrganizationId = 2,
                NewExpiryDate = newExpiry,
                Notes = "Annual renewal"
            }, "platform-admin");

            Assert.IsTrue(result.Succeeded);
            var license = unitOfWork.Repository<OrganizationLicense>().GetById(10);
            Assert.AreEqual(newExpiry, license.ExpiryDate.Date);
            Assert.AreEqual(LicenseStatus.Active.ToString(), license.Status);
        }
    }
}
