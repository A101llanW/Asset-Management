using System;
using System.Collections.Generic;
using System.Globalization;
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
        public const string LayoutDesignKey = "Label.Layout.DesignJson";

        public const string DefaultModel = "ZD4A042-30EM00EZ";
        public const string ModeZebraBrowserPrint = "ZebraBrowserPrint";
        public const string ModeBrowser = "Browser";

        public const string LayoutCustom = "Custom";
        public const string LayoutQrWithMeta = "QrWithMeta";
        public const string LayoutQrCompact = "QrCompact";
        public const string LayoutQrOnly = "QrOnly";

        public const string CodeTypeQr = "Qr";
        public const string CodeTypeBarcode = "Barcode";

        public const int DefaultWidthMm = 76;
        public const int DefaultHeightMm = 37;
        public const decimal DefaultQrMagnification = 1m;
        public const decimal MinQrMagnification = 0.5m;
        public const decimal MaxQrMagnification = 10m;

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
                QrMagnification = GetDecimal(settings, QrMagnificationKey, DefaultQrMagnification),
                LayoutPreset = ApprovalWorkflowSettingsHelper.GetString(settings, LayoutPresetKey, LayoutCustom),
                LayoutDesignJson = ApprovalWorkflowSettingsHelper.GetString(settings, LayoutDesignKey, string.Empty)
            };
        }

        public static LabelPrinterSettingsVm FromSettings(IEnumerable<SystemSetting> settings)
        {
            return FromDictionary(ApprovalWorkflowSettingsHelper.ToDictionary(settings));
        }

        public static LabelLayoutDesign GetLayoutDesign(LabelPrinterSettingsVm settings)
        {
            settings = settings ?? new LabelPrinterSettingsVm();
            if (!UsesCustomLayout(settings))
            {
                return null;
            }

            return LabelLayoutDesignHelper.Deserialize(settings.LayoutDesignJson, settings.WidthMm, settings.HeightMm);
        }

        public static bool UsesCustomLayout(LabelPrinterSettingsVm settings)
        {
            return settings != null
                && string.Equals(settings.LayoutPreset, LayoutCustom, StringComparison.OrdinalIgnoreCase);
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

        private static decimal GetDecimal(IDictionary<string, SystemSetting> settings, string key, decimal fallback)
        {
            SystemSetting setting;
            decimal parsed;
            if (settings.TryGetValue(key, out setting)
                && decimal.TryParse(setting.SettingValue, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            return fallback;
        }
    }
}
