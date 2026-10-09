using System;
using System.Text;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// rel1009i: real QR footprint and the largest magnification that keeps the QR inside the label.
    /// ZPL ^BQN,2,m prints m dots per module with no quiet zone (the 2 mm margin is the quiet zone);
    /// the data is "MA," = error correction M, automatic mode. Worst case is byte mode, so the QR version is the
    /// smallest one whose byte capacity at level M holds the payload; modules per side = 17 + 4 x version.
    /// maxMag = floor((min(width, height) - 2 x margin) x dotsPerMm / modules), clamped to 1..10.
    /// </summary>
    public static class LabelQrSizing
    {
        public const int DotsPerMm = 8;              // ZD421 / ZD4A042 = 203 dpi
        public const int MarginMm = 2;               // LabelLayoutDesignHelper.DefaultMarginMm
        public const int MinMagnification = 1;
        public const int MaxMagnification = 10;      // ZPL ^BQ limit
        /// <summary>Lower bound for the worst-case scan URL length (bytes).</summary>
        public const int DefaultWorstCasePayloadBytes = 100;
        /// <summary>Head-room for the asset tag inside the scan URL (URL-encoded characters).</summary>
        public const int MaxAssetTagPayloadChars = 40;

        /// <summary>Byte-mode capacity per QR version 1..40 at error correction level M (ISO/IEC 18004).</summary>
        private static readonly int[] ByteCapacityM =
        {
            14, 26, 42, 62, 84, 106, 122, 152, 180, 213, 251, 287, 331, 362, 412, 450, 504, 560, 624, 666,
            711, 779, 857, 911, 997, 1059, 1125, 1190, 1264, 1370, 1452, 1538, 1628, 1722, 1809, 1911, 1989, 2099, 2213, 2331
        };

        public static int PayloadBytes(string payload)
        {
            return string.IsNullOrEmpty(payload) ? 0 : Encoding.UTF8.GetByteCount(payload);
        }

        public static int VersionForBytes(int payloadBytes)
        {
            for (var i = 0; i < ByteCapacityM.Length; i++)
            {
                if (payloadBytes <= ByteCapacityM[i])
                {
                    return i + 1;
                }
            }

            return ByteCapacityM.Length;
        }

        public static int ModulesForBytes(int payloadBytes)
        {
            return 17 + (4 * VersionForBytes(Math.Max(1, payloadBytes)));
        }

        /// <summary>
        /// Worst-case payload for this deployment: the sample scan URL with the sample tag replaced by
        /// <see cref="MaxAssetTagPayloadChars"/> characters, never less than <see cref="DefaultWorstCasePayloadBytes"/>.
        /// </summary>
        public static int WorstCasePayloadBytes(string sampleScanUrl, string sampleAssetTag)
        {
            var bytes = PayloadBytes(sampleScanUrl);
            if (bytes > 0)
            {
                bytes = bytes - PayloadBytes(sampleAssetTag ?? string.Empty) + MaxAssetTagPayloadChars;
            }

            return Math.Max(DefaultWorstCasePayloadBytes, bytes);
        }

        /// <summary>ZPL magnification actually printed for a stored value (rounded, 1..10).</summary>
        public static int ToZplMagnification(decimal magnification)
        {
            var rounded = (int)Math.Round(magnification <= 0 ? 1m : magnification, MidpointRounding.AwayFromZero);
            return Math.Max(MinMagnification, Math.Min(MaxMagnification, rounded));
        }

        public static int MaxMagnificationFor(int widthMm, int heightMm, int payloadBytes)
        {
            var usableDots = (Math.Min(widthMm, heightMm) - (2 * MarginMm)) * DotsPerMm;
            var max = usableDots / ModulesForBytes(payloadBytes);
            return Math.Max(MinMagnification, Math.Min(MaxMagnification, max));
        }

        /// <summary>True when even magnification 1 does not fit (label too small for this payload).</summary>
        public static bool FitsAtMinimum(int widthMm, int heightMm, int payloadBytes)
        {
            return ModulesForBytes(payloadBytes) <= (Math.Min(widthMm, heightMm) - (2 * MarginMm)) * DotsPerMm;
        }

        /// <summary>Caps a stored magnification to the label (values below 0.5 become 0.5; above the cap become the cap).</summary>
        public static decimal CapMagnification(decimal magnification, int widthMm, int heightMm, int payloadBytes)
        {
            var value = magnification <= 0 ? LabelLayoutDesignHelper.DefaultQrMagnification : magnification;
            if (value < LabelPrinterSettingsHelper.MinQrMagnification)
            {
                value = LabelPrinterSettingsHelper.MinQrMagnification;
            }

            var cap = MaxMagnificationFor(widthMm, heightMm, payloadBytes);
            return value > cap ? cap : value;
        }

        public static int QrSizeDots(decimal magnification, int payloadBytes)
        {
            return ModulesForBytes(payloadBytes) * ToZplMagnification(magnification);
        }

        public static double QrSizeMm(decimal magnification, int payloadBytes)
        {
            return QrSizeDots(magnification, payloadBytes) / (double)DotsPerMm;
        }
    }
}
