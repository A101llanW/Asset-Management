using System.Collections.Generic;
using System.Linq;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Helpers
{
    public static class NotificationSettingsHelper
    {
        public const string WarrantyThresholdKey = "Notification.WarrantyThresholdDays";
        public const string InsuranceThresholdKey = "Notification.InsuranceThresholdDays";
        public const string MaintenanceThresholdKey = "Notification.MaintenanceThresholdDays";

        public static int GetWarrantyThresholdDays(IEnumerable<SystemSetting> settings)
        {
            return GetInt(settings, WarrantyThresholdKey, 30);
        }

        public static int GetInsuranceThresholdDays(IEnumerable<SystemSetting> settings)
        {
            return GetInt(settings, InsuranceThresholdKey, 30);
        }

        public static int GetMaintenanceThresholdDays(IEnumerable<SystemSetting> settings)
        {
            return GetInt(settings, MaintenanceThresholdKey, 14);
        }

        private static int GetInt(IEnumerable<SystemSetting> settings, string key, int fallback)
        {
            var setting = (settings ?? Enumerable.Empty<SystemSetting>())
                .FirstOrDefault(x => string.Equals(x.SettingKey, key, System.StringComparison.OrdinalIgnoreCase));
            int parsed;
            if (setting != null && int.TryParse(setting.SettingValue, out parsed))
            {
                return parsed;
            }

            return fallback;
        }
    }
}
