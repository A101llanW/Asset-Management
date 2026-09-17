using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;

namespace AssetManagement.Application.Helpers
{
    public class LabelLayoutElement
    {
        public string Id { get; set; }

        public bool Enabled { get; set; }

        public double XMm { get; set; }

        public double YMm { get; set; }

        public int FontHeight { get; set; }
    }

    public class LabelLayoutDesign
    {
        public IList<LabelLayoutElement> Elements { get; set; } = new List<LabelLayoutElement>();
    }

    public static class LabelLayoutDesignHelper
    {
        public const int DefaultMarginMm = 2;
        private const int DotsPerMm = 8;

        public const string ScanCodeId = "ScanCode";
        public const string AssetTagId = "AssetTag";
        public const string AssetNameId = "AssetName";
        public const string DepartmentNameId = "DepartmentName";
        public const string SerialNumberId = "SerialNumber";

        public static readonly string[] OrderedElementIds =
        {
            ScanCodeId,
            AssetTagId,
            AssetNameId,
            DepartmentNameId,
            SerialNumberId
        };

        public static LabelLayoutDesign CreateDefault(int widthMm, int heightMm)
        {
            var design = new LabelLayoutDesign();
            foreach (var id in OrderedElementIds)
            {
                design.Elements.Add(CreateDefaultElement(id, widthMm, heightMm));
            }

            return design;
        }

        public static LabelLayoutDesign Normalize(LabelLayoutDesign design, int widthMm, int heightMm)
        {
            design = design ?? new LabelLayoutDesign();
            var byId = design.Elements
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Id))
                .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

            var normalized = new LabelLayoutDesign();
            foreach (var id in OrderedElementIds)
            {
                LabelLayoutElement element;
                if (byId.TryGetValue(id, out element))
                {
                    normalized.Elements.Add(NormalizeElement(element, id, widthMm, heightMm));
                }
                else
                {
                    normalized.Elements.Add(CreateDefaultElement(id, widthMm, heightMm));
                }
            }

            return normalized;
        }

        public static string Serialize(LabelLayoutDesign design, int widthMm, int heightMm)
        {
            design = Normalize(design, widthMm, heightMm);
            return new JavaScriptSerializer().Serialize(design);
        }

        public static LabelLayoutDesign Deserialize(string json, int widthMm, int heightMm)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return CreateDefault(widthMm, heightMm);
            }

            try
            {
                var design = new JavaScriptSerializer().Deserialize<LabelLayoutDesign>(json);
                return Normalize(design, widthMm, heightMm);
            }
            catch (ArgumentException)
            {
                return CreateDefault(widthMm, heightMm);
            }
            catch (InvalidOperationException)
            {
                return CreateDefault(widthMm, heightMm);
            }
        }

        public static string GetDisplayName(string elementId)
        {
            if (string.Equals(elementId, ScanCodeId, StringComparison.OrdinalIgnoreCase))
            {
                return "Scan code (QR / barcode)";
            }

            if (string.Equals(elementId, AssetTagId, StringComparison.OrdinalIgnoreCase))
            {
                return "Asset tag";
            }

            if (string.Equals(elementId, AssetNameId, StringComparison.OrdinalIgnoreCase))
            {
                return "Asset name";
            }

            if (string.Equals(elementId, DepartmentNameId, StringComparison.OrdinalIgnoreCase))
            {
                return "Department";
            }

            if (string.Equals(elementId, SerialNumberId, StringComparison.OrdinalIgnoreCase))
            {
                return "Serial number";
            }

            return elementId ?? string.Empty;
        }

        public static LabelLayoutElement GetElement(LabelLayoutDesign design, string elementId)
        {
            if (design == null || design.Elements == null)
            {
                return null;
            }

            return design.Elements.FirstOrDefault(x => string.Equals(x.Id, elementId, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsEnabled(LabelLayoutDesign design, string elementId)
        {
            var element = GetElement(design, elementId);
            return element != null && element.Enabled;
        }

        public static double EstimateQrSizeMm(int qrMagnification)
        {
            var magnification = qrMagnification <= 0 ? 3 : qrMagnification;
            return (25 * magnification + 30d) / DotsPerMm;
        }

        private static double EstimateTextColumnX(int widthMm, int qrMagnification)
        {
            var qrSizeMm = EstimateQrSizeMm(qrMagnification);
            var textX = DefaultMarginMm + qrSizeMm + DefaultMarginMm;
            return Clamp(textX, DefaultMarginMm, Math.Max(DefaultMarginMm, widthMm - DefaultMarginMm));
        }

        private static LabelLayoutElement CreateDefaultElement(string id, int widthMm, int heightMm)
        {
            var element = new LabelLayoutElement
            {
                Id = id,
                Enabled = true,
                FontHeight = 0
            };

            var textX = EstimateTextColumnX(widthMm, 3);

            if (string.Equals(id, ScanCodeId, StringComparison.OrdinalIgnoreCase))
            {
                element.XMm = DefaultMarginMm;
                element.YMm = DefaultMarginMm;
                if (heightMm <= 30)
                {
                    element.Enabled = true;
                }
            }
            else if (string.Equals(id, AssetTagId, StringComparison.OrdinalIgnoreCase))
            {
                element.XMm = textX;
                element.YMm = DefaultMarginMm;
                element.FontHeight = 24;
            }
            else if (string.Equals(id, AssetNameId, StringComparison.OrdinalIgnoreCase))
            {
                element.XMm = textX;
                element.YMm = Math.Min(heightMm - DefaultMarginMm - 4, DefaultMarginMm + 8);
                element.FontHeight = 18;
            }
            else if (string.Equals(id, DepartmentNameId, StringComparison.OrdinalIgnoreCase))
            {
                element.XMm = textX;
                element.YMm = Math.Min(heightMm - DefaultMarginMm - 4, DefaultMarginMm + 16);
                element.FontHeight = 14;
                element.Enabled = heightMm >= 30;
            }
            else if (string.Equals(id, SerialNumberId, StringComparison.OrdinalIgnoreCase))
            {
                element.XMm = textX;
                element.YMm = Math.Min(heightMm - DefaultMarginMm - 4, DefaultMarginMm + 24);
                element.FontHeight = 14;
                element.Enabled = heightMm >= 34;
            }

            return ClampElement(element, widthMm, heightMm);
        }

        private static LabelLayoutElement NormalizeElement(LabelLayoutElement source, string id, int widthMm, int heightMm)
        {
            var defaults = CreateDefaultElement(id, widthMm, heightMm);
            var element = new LabelLayoutElement
            {
                Id = id,
                Enabled = source.Enabled,
                XMm = source.XMm,
                YMm = source.YMm,
                FontHeight = source.FontHeight > 0 ? source.FontHeight : defaults.FontHeight
            };

            return ClampElement(element, widthMm, heightMm);
        }

        private static LabelLayoutElement ClampElement(LabelLayoutElement element, int widthMm, int heightMm)
        {
            element.XMm = Clamp(element.XMm, DefaultMarginMm, Math.Max(DefaultMarginMm, widthMm - DefaultMarginMm));
            element.YMm = Clamp(element.YMm, DefaultMarginMm, Math.Max(DefaultMarginMm, heightMm - DefaultMarginMm));
            return element;
        }

        private static double Clamp(double value, double min, double max)
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
    }
}
