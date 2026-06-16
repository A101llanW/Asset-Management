using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Helpers;
using AssetManagement.Domain.Entities;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class NotificationSettingsHelperTests
    {
        [Test]
        public void GetWarrantyThresholdDays_UsesConfiguredSetting()
        {
            var settings = new List<SystemSetting>
            {
                new SystemSetting { SettingKey = NotificationSettingsHelper.WarrantyThresholdKey, SettingValue = "45" }
            };

            Assert.AreEqual(45, NotificationSettingsHelper.GetWarrantyThresholdDays(settings));
        }

        [Test]
        public void GetInsuranceThresholdDays_FallsBackToDefault()
        {
            Assert.AreEqual(30, NotificationSettingsHelper.GetInsuranceThresholdDays(new List<SystemSetting>()));
        }

        [Test]
        public void GetMaintenanceThresholdDays_UsesConfiguredSetting()
        {
            var settings = new List<SystemSetting>
            {
                new SystemSetting { SettingKey = NotificationSettingsHelper.MaintenanceThresholdKey, SettingValue = "7" }
            };

            Assert.AreEqual(7, NotificationSettingsHelper.GetMaintenanceThresholdDays(settings));
        }
    }
}
