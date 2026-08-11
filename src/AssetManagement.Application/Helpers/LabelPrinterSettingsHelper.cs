using System.Collections.Generic;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Helpers
{
    public static class LabelPrinterSettingsHelper
    {
        public const string EnabledKey = "Label.Printer.Enabled";
        public const string ModelKey = "Label.Printer.Model";
        public const string ModeKey = "Label.Printer.Mode";
        public const string DeviceNameKey = "Label.Printer.DeviceName";
        public const string WidthMmKey = "Label.Size.WidthMm";
        public const string HeightMmKey = "Label.Size.HeightMm";
        public const string QrMagnificationKey = "Label.Qr.Magnification";
        public const string LayoutPresetKey = "Label.Layout.Preset";

        public const string DefaultModel = "ZD4A042-30EM00EZ";
        public const string ModeZebraBrowserPrint = "ZebraBrowserPrint";
        public const string ModeBrowser = "Browser";

        public const string LayoutQrWithMeta = "QrWithMeta";
        public const string LayoutQrCompact = "QrCompact";
        public const string LayoutQrOnly = "QrOnly";

        public const int DefaultWidthMm = 100;
        public const int DefaultHeightMm = 50;
        public const int DefaultQrMagnification = 5;

        public static LabelPrinterSettingsVm FromDictionary(IDictionary<string, SystemSetting> settings)
        {
            settings = settings ?? new Dictionary<string, SystemSetting>();
            return new LabelPrinterSettingsVm
            {
                Enabled = ApprovalWorkflowSettingsHelper.GetBool(settings, EnabledKey, false),
                Model = ApprovalWorkflowSettingsHelper.GetString(settings, ModelKey, DefaultModel),
                Mode = ApprovalWorkflowSettingsHelper.GetString(settings, ModeKey, ModeZebraBrowserPrint),
                DeviceName = ApprovalWorkflowSettingsHelper.GetString(settings, DeviceNameKey, string.Empty),
                WidthMm = GetInt(settings, WidthMmKey, DefaultWidthMm),
                HeightMm = GetInt(settings, HeightMmKey, DefaultHeightMm),
                QrMagnification = GetInt(settings, QrMagnificationKey, DefaultQrMagnification),
                LayoutPreset = ApprovalWorkflowSettingsHelper.GetString(settings, LayoutPresetKey, LayoutQrWithMeta)
            };
        }

        public static LabelPrinterSettingsVm FromSettings(IEnumerable<SystemSetting> settings)
        {
            return FromDictionary(ApprovalWorkflowSettingsHelper.ToDictionary(settings));
        }

        private static int GetInt(IDictionary<string, SystemSetting> settings, string key, int fallback)
        {
            SystemSetting setting;
            int parsed;
            if (settings.TryGetValue(key, out setting) && int.TryParse(setting.SettingValue, out parsed))
            {
                return parsed;
            }

            return fallback;
        }
    }
}
