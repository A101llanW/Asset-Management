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
    }

    public static class ZplLabelBuilder
    {
        private const int DotsPerMm = 8;

        public static string Build(ZplLabelData data, LabelPrinterSettingsVm settings)
        {
            if (data == null || settings == null || string.IsNullOrWhiteSpace(data.ScanUrl))
            {
                return string.Empty;
            }

            var widthDots = MmToDots(settings.WidthMm);
            var heightDots = MmToDots(settings.HeightMm);
            var magnification = Clamp(settings.QrMagnification, 1, 10);
            var preset = string.IsNullOrWhiteSpace(settings.LayoutPreset)
                ? LabelPrinterSettingsHelper.LayoutQrWithMeta
                : settings.LayoutPreset;

            var sb = new StringBuilder();
            sb.AppendLine("^XA");
            sb.AppendLine("^CI28");
            sb.AppendLine("^PW" + widthDots);
            sb.AppendLine("^LL" + heightDots);

            if (string.Equals(preset, LabelPrinterSettingsHelper.LayoutQrOnly, StringComparison.OrdinalIgnoreCase))
            {
                AppendQrOnly(sb, data, widthDots, heightDots, magnification);
            }
            else if (string.Equals(preset, LabelPrinterSettingsHelper.LayoutQrCompact, StringComparison.OrdinalIgnoreCase))
            {
                AppendQrCompact(sb, data, widthDots, heightDots, magnification);
            }
            else
            {
                AppendQrWithMeta(sb, data, widthDots, heightDots, magnification);
            }

            sb.AppendLine("^XZ");
            return sb.ToString();
        }

        private static void AppendQrOnly(StringBuilder sb, ZplLabelData data, int widthDots, int heightDots, int magnification)
        {
            var qrSize = EstimateQrSizeDots(magnification);
            var x = Math.Max(10, (widthDots - qrSize) / 2);
            var y = Math.Max(10, (heightDots - qrSize) / 2);
            AppendQr(sb, x, y, data.ScanUrl, magnification);
        }

        private static void AppendQrCompact(StringBuilder sb, ZplLabelData data, int widthDots, int heightDots, int magnification)
        {
            var mag = Clamp(magnification, 1, 8);
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

        private static void AppendQrWithMeta(StringBuilder sb, ZplLabelData data, int widthDots, int heightDots, int magnification)
        {
            var qrX = 15;
            var qrY = 15;
            var qrSize = EstimateQrSizeDots(magnification);
            AppendQr(sb, qrX, qrY, data.ScanUrl, magnification);

            var textX = qrX + qrSize + 20;
            var textY = qrY;
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

        private static void AppendQr(StringBuilder sb, int x, int y, string payload, int magnification)
        {
            sb.AppendLine("^FO" + x + "," + y + "^BQN,2," + magnification + "^FDMA," + EscapeZplField(payload) + "^FS");
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

        private static int EstimateQrSizeDots(int magnification)
        {
            return 25 * magnification + 30;
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
