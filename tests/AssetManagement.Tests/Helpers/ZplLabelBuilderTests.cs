using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class ZplLabelBuilderTests
    {
        [Test]
        public void Build_IncludesLabelDimensionsAndQrCommand()
        {
            var data = new ZplLabelData
            {
                AssetTag = "AST-100",
                AssetName = "Laptop",
                ScanUrl = "https://assets.example.com/AssetScan/Lookup?code=AST-100"
            };
            var settings = new LabelPrinterSettingsVm
            {
                WidthMm = 100,
                HeightMm = 50,
                QrMagnification = 5,
                LayoutPreset = LabelPrinterSettingsHelper.LayoutQrWithMeta
            };

            var zpl = ZplLabelBuilder.Build(data, settings);

            Assert.IsTrue(zpl.Contains("^PW800"));
            Assert.IsTrue(zpl.Contains("^LL400"));
            Assert.IsTrue(zpl.Contains("^BQN,2,5"));
            Assert.IsTrue(zpl.Contains("AST-100"));
            Assert.IsTrue(zpl.Contains("https://assets.example.com/AssetScan/Lookup?code=AST-100"));
        }

        [Test]
        public void EscapeZplField_EncodesSpecialCharacters()
        {
            var escaped = ZplLabelBuilder.EscapeZplField("Tag^1~test\\end");

            Assert.IsTrue(escaped.Contains("\\5E"));
            Assert.IsTrue(escaped.Contains("\\7E"));
            Assert.IsTrue(escaped.Contains("\\\\"));
        }

        [Test]
        public void Build_QrOnlyLayoutCentersQr()
        {
            var data = new ZplLabelData
            {
                AssetTag = "AST-200",
                ScanUrl = "https://assets.example.com/AssetScan/Lookup?code=AST-200"
            };
            var settings = new LabelPrinterSettingsVm
            {
                WidthMm = 50,
                HeightMm = 25,
                QrMagnification = 3,
                LayoutPreset = LabelPrinterSettingsHelper.LayoutQrOnly
            };

            var zpl = ZplLabelBuilder.Build(data, settings);

            Assert.IsTrue(zpl.Contains("^PW400"));
            Assert.IsTrue(zpl.Contains("^BQN,2,3"));
            Assert.IsFalse(zpl.Contains("^A0N"));
        }
    }
}
