using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Web.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    /// <summary>rel1019a: print-time templates, CustomText, CompanyName and Bulk Label Print classification filters.</summary>
    [TestFixture]
    public class LabelPrintFeatureTests
    {
        private const string ScanUrl = "https://assets.example.com/AssetScan/Lookup?code=AST-100";

        private static LabelLayoutDesign DesignWith(string elementId, string text, double x = 2, double y = 20)
        {
            var design = LabelLayoutDesignHelper.CreateDefault(76, 37);
            var element = design.Elements.First(e => e.Id == elementId);
            element.Enabled = true;
            element.XMm = x;
            element.YMm = y;
            element.Text = text;
            return design;
        }

        private static LabelPrinterSettingsVm Settings(string designJson = null)
        {
            return new LabelPrinterSettingsVm
            {
                Enabled = true,
                Mode = LabelPrinterSettingsHelper.ModeZebraBrowserPrint,
                DeviceName = "ZD421",
                WidthMm = 76,
                HeightMm = 37,
                QrMagnification = 3,
                LayoutPreset = LabelPrinterSettingsHelper.LayoutCustom,
                LayoutDesignJson = designJson
            };
        }

        [Test]
        public void CustomText_SurvivesSerializeDeserializeAndTemplateSave()
        {
            var design = DesignWith(LabelLayoutDesignHelper.CustomTextId, "Property of NIS\r\nCall 0700 000 000");
            var json = LabelLayoutDesignHelper.Serialize(design, 76, 37);
            var parsed = LabelLayoutDesignHelper.Deserialize(json, 76, 37);
            var element = LabelLayoutDesignHelper.GetElement(parsed, LabelLayoutDesignHelper.CustomTextId);

            Assert.IsTrue(element.Enabled);
            Assert.AreEqual("Property of NIS\nCall 0700 000 000", element.Text);

            var collection = new LabelLayoutTemplateCollection();
            var template = LabelLayoutTemplateHelper.SaveTemplate(collection, "With text", json, 76, 37, 3m);
            var fromTemplate = LabelLayoutDesignHelper.Deserialize(template.LayoutDesignJson, 76, 37);
            Assert.AreEqual("Property of NIS\nCall 0700 000 000", LabelLayoutDesignHelper.GetElement(fromTemplate, LabelLayoutDesignHelper.CustomTextId).Text);
        }

        [Test]
        public void Text_IsDroppedForNonCustomElements_AndCappedForCustomText()
        {
            var design = DesignWith(LabelLayoutDesignHelper.CustomTextId, new string('a', LabelLayoutDesignHelper.MaxCustomTextLength + 50));
            design.Elements.First(e => e.Id == LabelLayoutDesignHelper.AssetTagId).Text = "should not persist";

            var normalized = LabelLayoutDesignHelper.Normalize(design, 76, 37);

            Assert.IsNull(LabelLayoutDesignHelper.GetElement(normalized, LabelLayoutDesignHelper.AssetTagId).Text);
            Assert.AreEqual(LabelLayoutDesignHelper.MaxCustomTextLength, LabelLayoutDesignHelper.GetElement(normalized, LabelLayoutDesignHelper.CustomTextId).Text.Length);
        }

        [Test]
        public void WrapCustomText_FitsLinesToLabel_AndFlagsOverflow()
        {
            var element = new LabelLayoutElement { Id = LabelLayoutDesignHelper.CustomTextId, Enabled = true, XMm = 2, YMm = 20, FontHeight = 14 };
            // 76 x 37 mm, X 2, Y 20, font 14: (72 mm x 8) / (14 x 0.55) = 74 chars per line; (15 mm x 8 + 2) / 16 = 7 lines.
            Assert.AreEqual(74, LabelLayoutDesignHelper.GetCustomTextCharsPerLine(element, 76));
            Assert.AreEqual(7, LabelLayoutDesignHelper.GetCustomTextMaxLines(element, 37));

            bool overflow;
            element.Text = "Short note";
            var lines = LabelLayoutDesignHelper.WrapCustomText(element, 76, 37, out overflow);
            Assert.AreEqual(1, lines.Count);
            Assert.IsFalse(overflow);

            element.Text = string.Join(" ", Enumerable.Repeat("Property of Nairobi International School. If found please call 0700 000 000.", 8));
            lines = LabelLayoutDesignHelper.WrapCustomText(element, 76, 37, out overflow);
            Assert.IsTrue(overflow);
            Assert.AreEqual(7, lines.Count);
            Assert.IsTrue(lines.All(l => l.Length <= 74));
            Assert.IsTrue(lines.Last().EndsWith("..."));
        }

        [Test]
        public void BuildCustomLayout_PrintsCustomTextAsFieldBlock()
        {
            var design = DesignWith(LabelLayoutDesignHelper.CustomTextId, "Line one\nUse ^caret_ and ~tilde");
            var data = new ZplLabelData { AssetTag = "AST-100", ScanUrl = ScanUrl };

            var zpl = ZplLabelBuilder.BuildCustomLayout(data, Settings(), design, LabelPrinterSettingsHelper.CodeTypeQr);

            StringAssert.Contains("^FB", zpl);
            StringAssert.Contains("^FH_^FDLine one\\&Use _5Ecaret_5F and _7Etilde^FS", zpl);
        }

        [Test]
        public void BuildCustomLayout_CustomTextAndCompanyNameOffByDefault()
        {
            var data = new ZplLabelData { AssetTag = "AST-100", ScanUrl = ScanUrl, CompanyName = "Nairobi International School" };

            var zpl = ZplLabelBuilder.BuildCustomLayout(data, Settings(), LabelLayoutDesignHelper.CreateDefault(76, 37), LabelPrinterSettingsHelper.CodeTypeQr);

            Assert.IsFalse(zpl.Contains("^FB"));
            Assert.IsFalse(zpl.Contains("Nairobi International School"));
        }

        [Test]
        public void BuildCustomLayout_PrintsCompanyNameWhenEnabled()
        {
            var design = DesignWith(LabelLayoutDesignHelper.CompanyNameId, null, 2, 30);
            var data = new ZplLabelData { AssetTag = "AST-100", ScanUrl = ScanUrl, CompanyName = "Nairobi International School" };

            var zpl = ZplLabelBuilder.BuildCustomLayout(data, Settings(), design, LabelPrinterSettingsHelper.CodeTypeQr);

            StringAssert.Contains("Nairobi International School", zpl);
        }

        [Test]
        public void ApplyToSettings_UsesTemplateLayoutAndSize_KeepsPrinter()
        {
            var settingsMap = new Dictionary<string, SystemSetting>();
            var collection = new LabelLayoutTemplateCollection();
            var templateDesign = LabelLayoutDesignHelper.Serialize(DesignWith(LabelLayoutDesignHelper.CustomTextId, "Template text"), 50, 25);
            var saved = LabelLayoutTemplateHelper.SaveTemplate(collection, "Small", templateDesign, 50, 25, 2m);
            settingsMap[LabelLayoutTemplateHelper.TemplatesKey] = new SystemSetting
            {
                SettingKey = LabelLayoutTemplateHelper.TemplatesKey,
                SettingValue = LabelLayoutTemplateHelper.SerializeCollection(collection)
            };

            var found = LabelLayoutTemplateHelper.FindTemplate(settingsMap, saved.Id.ToUpperInvariant());
            Assert.IsNotNull(found);
            Assert.IsNull(LabelLayoutTemplateHelper.FindTemplate(settingsMap, "missing"));
            Assert.IsNull(LabelLayoutTemplateHelper.FindTemplate(settingsMap, ""));

            var active = Settings();
            active.LayoutPreset = LabelPrinterSettingsHelper.LayoutQrWithMeta;
            var applied = LabelLayoutTemplateHelper.ApplyToSettings(active, found);

            Assert.AreEqual(50, applied.WidthMm);
            Assert.AreEqual(25, applied.HeightMm);
            Assert.AreEqual(2m, applied.QrMagnification);
            Assert.AreEqual(LabelPrinterSettingsHelper.LayoutCustom, applied.LayoutPreset);
            Assert.AreEqual("ZD421", applied.DeviceName);
            Assert.AreEqual(LabelPrinterSettingsHelper.LayoutQrWithMeta, active.LayoutPreset, "stored settings are not changed");

            var zpl = ZplLabelBuilder.Build(new ZplLabelData { AssetTag = "AST-100", ScanUrl = ScanUrl }, applied);
            StringAssert.Contains("^PW400", zpl);
            StringAssert.Contains("Template text", zpl);
            Assert.AreSame(active, LabelLayoutTemplateHelper.ApplyToSettings(active, null));
        }

        [Test]
        public void ResolveDepartmentIds_IncludesDescendantsWhenAsked()
        {
            var departments = new List<DepartmentVm>
            {
                new DepartmentVm { Id = 1, ParentDepartmentId = null },
                new DepartmentVm { Id = 2, ParentDepartmentId = 1 },
                new DepartmentVm { Id = 3, ParentDepartmentId = 2 },
                new DepartmentVm { Id = 4, ParentDepartmentId = null }
            };

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, PrintCandidateQuery.ResolveDepartmentIds(1, true, departments).ToArray());
            CollectionAssert.AreEqual(new[] { 2, 3 }, PrintCandidateQuery.ResolveDepartmentIds(2, true, departments).ToArray());
            CollectionAssert.AreEqual(new[] { 1 }, PrintCandidateQuery.ResolveDepartmentIds(1, false, departments).ToArray());
            Assert.IsNull(PrintCandidateQuery.ResolveDepartmentIds(null, true, departments));
            Assert.IsNull(PrintCandidateQuery.NormalizeId(0));
            Assert.AreEqual(5, PrintCandidateQuery.NormalizeId(5));
        }

        [Test]
        public void ClassificationCatalog_IsOrgScopedActiveAndCascadeSafe()
        {
            var categories = new[]
            {
                new AssetCategory { Id = 1, OrganizationId = 13, Name = "ICT", IsActive = true },
                new AssetCategory { Id = 2, OrganizationId = 13, Name = "Old", IsActive = false },
                new AssetCategory { Id = 3, OrganizationId = 99, Name = "Other org", IsActive = true }
            };
            var types = new[]
            {
                new AssetType { Id = 10, OrganizationId = 13, AssetCategoryId = 1, Name = "Laptop", IsActive = true },
                new AssetType { Id = 11, OrganizationId = 13, AssetCategoryId = 2, Name = "Under inactive", IsActive = true },
                new AssetType { Id = 12, OrganizationId = 99, AssetCategoryId = 3, Name = "Other org type", IsActive = true }
            };
            var subTypes = new[]
            {
                new AssetSubType { Id = 100, OrganizationId = 13, AssetTypeId = 10, Name = "Dell", IsActive = true },
                new AssetSubType { Id = 101, OrganizationId = 13, AssetTypeId = 11, Name = "Orphan", IsActive = true }
            };

            var catalog = LabelPrintSupport.BuildClassificationCatalog(13, categories, types, subTypes);

            CollectionAssert.AreEqual(new[] { 1 }, catalog.Categories.Select(x => x.Id).ToArray());
            CollectionAssert.AreEqual(new[] { 10 }, catalog.Types.Select(x => x.Id).ToArray());
            Assert.AreEqual(1, catalog.Types[0].ParentId);
            CollectionAssert.AreEqual(new[] { 100 }, catalog.SubTypes.Select(x => x.Id).ToArray());
            Assert.AreEqual(10, catalog.SubTypes[0].ParentId);
        }

        [TestCase(76, 37)]
        [TestCase(50, 30)]
        [TestCase(50, 25)]
        [TestCase(100, 50)]
        public void CustomText_DefaultPosition_SitsBelowSerialNumber(int widthMm, int heightMm)
        {
            var design = LabelLayoutDesignHelper.CreateDefault(widthMm, heightMm);
            var serial = LabelLayoutDesignHelper.GetElement(design, LabelLayoutDesignHelper.SerialNumberId);
            var custom = LabelLayoutDesignHelper.GetElement(design, LabelLayoutDesignHelper.CustomTextId);

            // SerialNumber is one 14-dot line (1.75 mm); CustomText must start below it, inside the bottom margin.
            Assert.GreaterOrEqual(custom.YMm, serial.YMm + serial.FontHeight / 8.0, "CustomText overlaps SerialNumber");
            if (heightMm - LabelLayoutDesignHelper.DefaultMarginMm - 2 >= LabelLayoutDesignHelper.DefaultMarginMm + 29)
            {
                // rel1019g (QA): when the label is tall enough, leave about 1 mm clear below SerialNumber's box.
                Assert.GreaterOrEqual(custom.YMm, serial.YMm + serial.FontHeight / 8.0 + 1.0, "CustomText touches SerialNumber");
            }
            Assert.LessOrEqual(custom.YMm, heightMm - LabelLayoutDesignHelper.DefaultMarginMm - 2);
            Assert.AreEqual(serial.XMm, custom.XMm);
            Assert.GreaterOrEqual(LabelLayoutDesignHelper.GetCustomTextMaxLines(custom, heightMm), 1);
        }
    }
}
