using System.Linq;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class LabelLayoutDesignHelperTests
    {
        [Test]
        public void Deserialize_EmptyJson_ReturnsDefaultElements()
        {
            var design = LabelLayoutDesignHelper.Deserialize(string.Empty, 76, 37);

            Assert.AreEqual(5, design.Elements.Count);
            Assert.IsTrue(design.Elements.Any(x => x.Id == LabelLayoutDesignHelper.ScanCodeId && x.Enabled));
        }

        [Test]
        public void Serialize_NormalizesElementOrder()
        {
            var design = new LabelLayoutDesign();
            design.Elements.Add(new LabelLayoutElement { Id = LabelLayoutDesignHelper.AssetNameId, Enabled = true, XMm = 10, YMm = 10 });
            design.Elements.Add(new LabelLayoutElement { Id = LabelLayoutDesignHelper.ScanCodeId, Enabled = true, XMm = 2, YMm = 2 });

            var json = LabelLayoutDesignHelper.Serialize(design, 76, 37);
            var parsed = LabelLayoutDesignHelper.Deserialize(json, 76, 37);

            Assert.AreEqual(LabelLayoutDesignHelper.ScanCodeId, parsed.Elements[0].Id);
            Assert.AreEqual(LabelLayoutDesignHelper.AssetNameId, parsed.Elements[2].Id);
        }

        [Test]
        public void CreateDefault_UsesEvenMarginAndQrAwareTextColumn()
        {
            var design = LabelLayoutDesignHelper.CreateDefault(76, 37);
            var scanCode = design.Elements.First(x => x.Id == LabelLayoutDesignHelper.ScanCodeId);
            var assetTag = design.Elements.First(x => x.Id == LabelLayoutDesignHelper.AssetTagId);

            Assert.AreEqual(LabelLayoutDesignHelper.DefaultMarginMm, scanCode.XMm);
            Assert.AreEqual(LabelLayoutDesignHelper.DefaultMarginMm, scanCode.YMm);
            Assert.Greater(assetTag.XMm, scanCode.XMm);
            Assert.AreEqual(LabelLayoutDesignHelper.DefaultMarginMm, assetTag.YMm);
        }

        [Test]
        public void EstimateQrSizeMm_MatchesZebraMagnificationFormula()
        {
            var size = LabelLayoutDesignHelper.EstimateQrSizeMm(3);
            Assert.AreEqual(13.125d, size, 0.001d);
        }
    }

    [TestFixture]
    public class ZplLabelBuilderCustomLayoutTests
    {
        [Test]
        public void BuildCustomLayout_RendersEnabledFields()
        {
            var design = LabelLayoutDesignHelper.CreateDefault(76, 37);
            var settings = new LabelPrinterSettingsVm
            {
                WidthMm = 76,
                HeightMm = 37,
                QrMagnification = 3,
                LayoutPreset = LabelPrinterSettingsHelper.LayoutCustom
            };
            var data = new ZplLabelData
            {
                AssetTag = "AST-100",
                AssetName = "Demo Laptop",
                DepartmentName = "IT",
                SerialNumber = "SN-001",
                ScanUrl = "https://assets.example.com/AssetScan/Lookup?code=AST-100",
                BarcodePayload = "AST-100"
            };

            var zpl = ZplLabelBuilder.BuildCustomLayout(data, settings, design, LabelPrinterSettingsHelper.CodeTypeQr);

            Assert.IsTrue(zpl.Contains("^BQN"));
            Assert.IsTrue(zpl.Contains("AST-100"));
            Assert.IsTrue(zpl.Contains("Demo Laptop"));
            Assert.IsTrue(zpl.Contains("^A0N"));
        }

        [Test]
        public void Build_UsesCustomLayoutWhenPresetIsCustom()
        {
            var settings = new LabelPrinterSettingsVm
            {
                WidthMm = 76,
                HeightMm = 37,
                QrMagnification = 3,
                LayoutPreset = LabelPrinterSettingsHelper.LayoutCustom,
                LayoutDesignJson = LabelLayoutDesignHelper.Serialize(LabelLayoutDesignHelper.CreateDefault(76, 37), 76, 37)
            };
            var data = new ZplLabelData
            {
                AssetTag = "AST-100",
                ScanUrl = "https://assets.example.com/AssetScan/Lookup?code=AST-100"
            };

            var zpl = ZplLabelBuilder.Build(data, settings);

            Assert.IsTrue(zpl.Contains("^BQN"));
            Assert.IsTrue(zpl.Contains("AST-100"));
        }
    }
}
