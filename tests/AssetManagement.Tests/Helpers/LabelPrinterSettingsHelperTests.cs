using System.Collections.Generic;
using AssetManagement.Application.Helpers;
using AssetManagement.Domain.Entities;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class LabelPrinterSettingsHelperTests
    {
        [Test]
        public void FromDictionary_UsesConfiguredValues()
        {
            var settings = new Dictionary<string, SystemSetting>
            {
                { LabelPrinterSettingsHelper.EnabledKey, new SystemSetting { SettingKey = LabelPrinterSettingsHelper.EnabledKey, SettingValue = "true" } },
                { LabelPrinterSettingsHelper.WidthMmKey, new SystemSetting { SettingKey = LabelPrinterSettingsHelper.WidthMmKey, SettingValue = "75" } },
                { LabelPrinterSettingsHelper.LayoutPresetKey, new SystemSetting { SettingKey = LabelPrinterSettingsHelper.LayoutPresetKey, SettingValue = LabelPrinterSettingsHelper.LayoutQrOnly } }
            };

            var result = LabelPrinterSettingsHelper.FromDictionary(settings);

            Assert.IsTrue(result.Enabled);
            Assert.AreEqual(75, result.WidthMm);
            Assert.AreEqual(LabelPrinterSettingsHelper.LayoutQrOnly, result.LayoutPreset);
        }

        [Test]
        public void FromDictionary_FallsBackToDefaults()
        {
            var result = LabelPrinterSettingsHelper.FromDictionary(new Dictionary<string, SystemSetting>());

            Assert.IsFalse(result.Enabled);
            Assert.AreEqual(LabelPrinterSettingsHelper.DefaultModel, result.Model);
            Assert.AreEqual(LabelPrinterSettingsHelper.DefaultWidthMm, result.WidthMm);
            Assert.AreEqual(LabelPrinterSettingsHelper.ModeZebraBrowserPrint, result.Mode);
        }
    }
}
