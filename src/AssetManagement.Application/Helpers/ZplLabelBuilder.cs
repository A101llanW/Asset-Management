using System;
using System.Text;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Helpers
{
    public class ZplLabelData
    {
        public string AssetTag { get; set; }

        public string AssetName { get; set; }

        public string DepartmentName { get; set; }

        public string SerialNumber { get; set; }

        public string ScanUrl { get; set; }

        public string BarcodePayload { get; set; }
    }

    public static class ZplLabelBuilder
    {
        private const int DotsPerMm = 8;
        private const int DefaultBarcodeHeightDots = 80;

        public static string Build(ZplLabelData data, LabelPrinterSettingsVm settings, string codeType = null)
        {
            if (data == null || settings == null)
            {
                return string.Empty;
            }

            if (LabelPrinterSettingsHelper.UsesCustomLayout(settings))
            {
                var design = LabelPrinterSettingsHelper.GetLayoutDesign(settings);
                return BuildCustomLayout(data, settings, design, codeType);
            }

            var resolvedCodeType = ResolveCodeType(codeType);
            if (string.Equals(resolvedCodeType, LabelPrinterSettingsHelper.CodeTypeBarcode, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(data.BarcodePayload))
                {
                    return string.Empty;
                }
            }
            else if (string.IsNullOrWhiteSpace(data.ScanUrl))
            {
                return string.Empty;
            }

            var widthDots = MmToDots(settings.WidthMm);
            var heightDots = MmToDots(settings.HeightMm);
            var magnification = ClampDecimal(
                settings.QrMagnification,
                LabelPrinterSettingsHelper.MinQrMagnification,
                LabelPrinterSettingsHelper.MaxQrMagnification);
            var preset = string.IsNullOrWhiteSpace(settings.LayoutPreset)
                ? LabelPrinterSettingsHelper.LayoutQrWithMeta
                : settings.LayoutPreset;
            var useBarcode = string.Equals(resolvedCodeType, LabelPrinterSettingsHelper.CodeTypeBarcode, StringComparison.OrdinalIgnoreCase);

            var sb = new StringBuilder();
            sb.AppendLine("^XA");
            sb.AppendLine("^CI28");
            sb.AppendLine("^PW" + widthDots);
            sb.AppendLine("^LL" + heightDots);

            if (string.Equals(preset, LabelPrinterSettingsHelper.LayoutQrOnly, StringComparison.OrdinalIgnoreCase))
            {
                if (useBarcode)
                {
                    AppendBarcodeOnly(sb, data, widthDots, heightDots);
                }
                else
                {
                    AppendQrOnly(sb, data, widthDots, heightDots, magnification);
                }
            }
            else if (string.Equals(preset, LabelPrinterSettingsHelper.LayoutQrCompact, StringComparison.OrdinalIgnoreCase))
            {
                if (useBarcode)
                {
                    AppendBarcodeCompact(sb, data, widthDots, heightDots);
                }
                else
                {
                    AppendQrCompact(sb, data, widthDots, heightDots, magnification);
                }
            }
            else
            {
                if (useBarcode)
                {
                    AppendBarcodeWithMeta(sb, data, widthDots, heightDots);
                }
                else
                {
                    AppendQrWithMeta(sb, data, widthDots, heightDots, magnification);
                }
            }

            sb.AppendLine("^XZ");
            return sb.ToString();
        }

        public static string BuildCustomLayout(ZplLabelData data, LabelPrinterSettingsVm settings, LabelLayoutDesign design, string codeType)
        {
            if (data == null || settings == null)
            {
                return string.Empty;
            }

            design = LabelLayoutDesignHelper.Normalize(design, settings.WidthMm, settings.HeightMm);
            var resolvedCodeType = ResolveCodeType(codeType);
            var useBarcode = string.Equals(resolvedCodeType, LabelPrinterSettingsHelper.CodeTypeBarcode, StringComparison.OrdinalIgnoreCase);

            if (useBarcode && string.IsNullOrWhiteSpace(data.BarcodePayload))
            {
                return string.Empty;
            }

            if (!useBarcode && string.IsNullOrWhiteSpace(data.ScanUrl))
            {
                return string.Empty;
            }

            var widthDots = MmToDots(settings.WidthMm);
            var heightDots = MmToDots(settings.HeightMm);
            var magnification = ClampDecimal(
                settings.QrMagnification,
                LabelPrinterSettingsHelper.MinQrMagnification,
                LabelPrinterSettingsHelper.MaxQrMagnification);
            var sb = new StringBuilder();
            sb.AppendLine("^XA");
            sb.AppendLine("^CI28");
            sb.AppendLine("^PW" + widthDots);
            sb.AppendLine("^LL" + heightDots);

            foreach (var element in design.Elements)
            {
                if (element == null || !element.Enabled)
                {
                    continue;
                }

                var x = MmToDots(ToMmInt(element.XMm));
                var y = MmToDots(ToMmInt(element.YMm));

                if (string.Equals(element.Id, LabelLayoutDesignHelper.ScanCodeId, StringComparison.OrdinalIgnoreCase))
                {
                    if (useBarcode)
                    {
                        var barcodeWidth = Math.Min(widthDots - x - 10, 320);
                        AppendBarcode(sb, x, y, data.BarcodePayload, barcodeWidth, DefaultBarcodeHeightDots);
                    }
                    else
                    {
                        AppendQr(sb, x, y, data.ScanUrl, magnification);
                    }

                    continue;
                }

                var text = GetTextForElement(element.Id, data);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var fontHeight = element.FontHeight > 0 ? element.FontHeight : GetDefaultFontHeight(element.Id);
                var fontWidth = Math.Max(12, (int)Math.Round(fontHeight * 0.85));
                AppendText(sb, x, y, text, fontHeight, fontWidth);
            }

            sb.AppendLine("^XZ");
            return sb.ToString();
        }

        private static string GetTextForElement(string elementId, ZplLabelData data)
        {
            if (string.Equals(elementId, LabelLayoutDesignHelper.AssetTagId, StringComparison.OrdinalIgnoreCase))
            {
                return data.AssetTag;
            }

            if (string.Equals(elementId, LabelLayoutDesignHelper.AssetNameId, StringComparison.OrdinalIgnoreCase))
            {
                return Truncate(data.AssetName, 28);
            }

            if (string.Equals(elementId, LabelLayoutDesignHelper.DepartmentNameId, StringComparison.OrdinalIgnoreCase))
            {
                return Truncate(data.DepartmentName, 28);
            }

            if (string.Equals(elementId, LabelLayoutDesignHelper.SerialNumberId, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(data.SerialNumber))
                {
                    return string.Empty;
                }

                return "S/N: " + Truncate(data.SerialNumber, 24);
            }

            return string.Empty;
        }

        private static int GetDefaultFontHeight(string elementId)
        {
            if (string.Equals(elementId, LabelLayoutDesignHelper.AssetTagId, StringComparison.OrdinalIgnoreCase))
            {
                return 24;
            }

            if (string.Equals(elementId, LabelLayoutDesignHelper.AssetNameId, StringComparison.OrdinalIgnoreCase))
            {
                return 18;
            }

            return 14;
        }

        private static int ToMmInt(double value)
        {
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        public static string ResolveCodeType(string codeType)
        {
            if (string.Equals(codeType, LabelPrinterSettingsHelper.CodeTypeBarcode, StringComparison.OrdinalIgnoreCase))
            {
                return LabelPrinterSettingsHelper.CodeTypeBarcode;
            }

            return LabelPrinterSettingsHelper.CodeTypeQr;
        }

        private static void AppendQrOnly(StringBuilder sb, ZplLabelData data, int widthDots, int heightDots, decimal magnification)
        {
            var qrSize = EstimateQrSizeDots(magnification);
            var x = Math.Max(10, (widthDots - qrSize) / 2);
            var y = Math.Max(10, (heightDots - qrSize) / 2);
            AppendQr(sb, x, y, data.ScanUrl, magnification);
        }

        private static void AppendBarcodeOnly(StringBuilder sb, ZplLabelData data, int widthDots, int heightDots)
        {
            var barcodeWidth = Math.Min(widthDots - 20, 520);
            var x = Math.Max(10, (widthDots - barcodeWidth) / 2);
            var y = Math.Max(10, (heightDots - DefaultBarcodeHeightDots) / 2);
            AppendBarcode(sb, x, y, data.BarcodePayload, barcodeWidth, DefaultBarcodeHeightDots);
        }

        private static void AppendQrCompact(StringBuilder sb, ZplLabelData data, int widthDots, int heightDots, decimal magnification)
        {
            var mag = ClampDecimal(magnification, LabelPrinterSettingsHelper.MinQrMagnification, 8m);
            var qrX = 10;
            var qrY = 8;
            var qrSize = EstimateQrSizeDots(mag);
            AppendQr(sb, qrX, qrY, data.ScanUrl, mag);

            var textX = qrX + qrSize + 12;
            if (textX + 40 > widthDots)
            {
                textX = 10;
                qrY = 8;
                AppendText(sb, 10, qrY + qrSize + 8, data.AssetTag, 24, 24);
                return;
            }

            AppendText(sb, textX, qrY, data.AssetTag, 24, 24);
            if (!string.IsNullOrWhiteSpace(data.AssetName))
            {
                AppendText(sb, textX, qrY + 30, Truncate(data.AssetName, 24), 18, 18);
            }
        }

        private static void AppendBarcodeCompact(StringBuilder sb, ZplLabelData data, int widthDots, int heightDots)
        {
            var barcodeX = 10;
            var barcodeY = 8;
            var barcodeWidth = Math.Min(widthDots - 20, 320);
            var barcodeHeight = 60;
            AppendBarcode(sb, barcodeX, barcodeY, data.BarcodePayload, barcodeWidth, barcodeHeight);

            var textX = barcodeX + barcodeWidth + 12;
            if (textX + 40 > widthDots)
            {
                AppendText(sb, 10, barcodeY + barcodeHeight + 8, data.AssetTag, 24, 24);
                return;
            }

            AppendText(sb, textX, barcodeY, data.AssetTag, 24, 24);
            if (!string.IsNullOrWhiteSpace(data.AssetName))
            {
                AppendText(sb, textX, barcodeY + 30, Truncate(data.AssetName, 24), 18, 18);
            }
        }

        private static void AppendQrWithMeta(StringBuilder sb, ZplLabelData data, int widthDots, int heightDots, decimal magnification)
        {
            var qrX = 15;
            var qrY = 15;
            var qrSize = EstimateQrSizeDots(magnification);
            AppendQr(sb, qrX, qrY, data.ScanUrl, magnification);

            AppendMetaText(sb, qrX + qrSize + 20, qrY, data);
        }

        private static void AppendBarcodeWithMeta(StringBuilder sb, ZplLabelData data, int widthDots, int heightDots)
        {
            var barcodeX = 15;
            var barcodeY = 15;
            var barcodeWidth = Math.Min(widthDots / 2, 280);
            var barcodeHeight = DefaultBarcodeHeightDots;
            AppendBarcode(sb, barcodeX, barcodeY, data.BarcodePayload, barcodeWidth, barcodeHeight);

            AppendMetaText(sb, barcodeX + barcodeWidth + 20, barcodeY, data);
        }

        private static void AppendMetaText(StringBuilder sb, int textX, int textY, ZplLabelData data)
        {
            AppendText(sb, textX, textY, data.AssetTag, 30, 30);
            textY += 36;

            if (!string.IsNullOrWhiteSpace(data.AssetName))
            {
                AppendText(sb, textX, textY, Truncate(data.AssetName, 28), 22, 22);
                textY += 28;
            }

            if (!string.IsNullOrWhiteSpace(data.DepartmentName))
            {
                AppendText(sb, textX, textY, Truncate(data.DepartmentName, 28), 18, 18);
                textY += 24;
            }

            if (!string.IsNullOrWhiteSpace(data.SerialNumber))
            {
                AppendText(sb, textX, textY, "S/N: " + Truncate(data.SerialNumber, 24), 18, 18);
            }
        }

        public static int ResolveZplQrMagnification(decimal magnification)
        {
            var clamped = ClampDecimal(
                magnification,
                LabelPrinterSettingsHelper.MinQrMagnification,
                LabelPrinterSettingsHelper.MaxQrMagnification);
            var rounded = (int)Math.Round(clamped, MidpointRounding.AwayFromZero);
            return Clamp(rounded, 1, 10);
        }

        public static int ResolveZplQrModel(decimal magnification)
        {
            return magnification < 1m ? 1 : 2;
        }

        private static void AppendQr(StringBuilder sb, int x, int y, string payload, decimal magnification)
        {
            var model = ResolveZplQrModel(magnification);
            var zplMagnification = ResolveZplQrMagnification(magnification);
            sb.AppendLine("^FO" + x + "," + y + "^BQN," + model + "," + zplMagnification + "^FDMA," + EscapeZplField(payload) + "^FS");
        }

        private static void AppendBarcode(StringBuilder sb, int x, int y, string payload, int widthDots, int heightDots)
        {
            sb.AppendLine("^FO" + x + "," + y + "^BY2,3," + Math.Max(40, heightDots) + "^BCN," + Math.Max(40, heightDots) + ",Y,N,N^FD" + EscapeZplField(payload) + "^FS");
        }

        private static void AppendText(StringBuilder sb, int x, int y, string text, int height, int width)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            sb.AppendLine("^FO" + x + "," + y + "^A0N," + height + "," + width + "^FH\\^FD" + EscapeZplField(text) + "^FS");
        }

        public static string EscapeZplField(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("^", "\\5E")
                .Replace("~", "\\7E");
        }

        private static int MmToDots(int mm)
        {
            return Math.Max(1, mm) * DotsPerMm;
        }

        private static int EstimateQrSizeDots(decimal magnification)
        {
            var effectiveMagnification = Math.Max(LabelPrinterSettingsHelper.MinQrMagnification, magnification);
            return (int)Math.Round(25m * effectiveMagnification + 30m, MidpointRounding.AwayFromZero);
        }

        private static decimal ClampDecimal(decimal value, decimal min, decimal max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, maxLength - 1) + "…";
        }
    }
}
