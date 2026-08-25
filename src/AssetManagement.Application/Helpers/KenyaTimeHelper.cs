using System;
using System.Configuration;
using System.Globalization;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Converts UTC timestamps to Kenya time (East Africa Time, UTC+3) for user-facing audit trails and logs.
    /// Storage remains UTC; display and date filters use EAT.
    /// </summary>
    public static class KenyaTimeHelper
    {
        public const string DefaultTimestampFormat = "yyyy-MM-dd HH:mm:ss";
        public const string ShortTimestampFormat = "yyyy-MM-dd HH:mm";
        public const string LogTimestampFormat = "yyyy-MM-dd HH:mm:ss";

        private static readonly TimeSpan Offset = TimeSpan.FromHours(ResolveOffsetHours());

        public static string DisplayAbbreviation
        {
            get { return ResolveDisplayAbbreviation(); }
        }

        public static string DisplayLabel
        {
            get { return ApplicationTimeDefaults.DisplayLabel; }
        }

        public static DateTime FromUtc(DateTime utc)
        {
            var normalized = NormalizeUtc(utc);
            return DateTime.SpecifyKind(normalized.Add(Offset), DateTimeKind.Unspecified);
        }

        public static DateTime ToUtc(DateTime local)
        {
            var normalized = local.Kind == DateTimeKind.Utc
                ? local
                : DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            return DateTime.SpecifyKind(normalized.Subtract(Offset), DateTimeKind.Utc);
        }

        public static string FormatUtc(DateTime utc)
        {
            return FormatUtc(utc, DefaultTimestampFormat);
        }

        public static string FormatUtc(DateTime utc, string format)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                format = DefaultTimestampFormat;
            }

            return FromUtc(utc).ToString(format, CultureInfo.InvariantCulture);
        }

        public static string FormatLogTimestamp(DateTime utc)
        {
            return FormatUtc(utc, LogTimestampFormat) + " " + DisplayAbbreviation;
        }

        public static string LocalDateStamp(DateTime utc)
        {
            return FromUtc(utc).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        public static DateTime? StartOfLocalDayToUtc(DateTime? localDate)
        {
            if (!localDate.HasValue)
            {
                return null;
            }

            var localStart = localDate.Value.Date;
            return ToUtc(localStart);
        }

        public static DateTime? ExclusiveEndOfLocalDayToUtc(DateTime? localDate)
        {
            if (!localDate.HasValue)
            {
                return null;
            }

            var localEndExclusive = localDate.Value.Date.AddDays(1);
            return ToUtc(localEndExclusive);
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Local)
            {
                return value.ToUniversalTime();
            }

            if (value.Kind == DateTimeKind.Unspecified)
            {
                return DateTime.SpecifyKind(value, DateTimeKind.Utc);
            }

            return value;
        }

        private static int ResolveOffsetHours()
        {
            var setting = ConfigurationManager.AppSettings[ApplicationTimeDefaults.AppSettingsOffsetHoursKey];
            int hours;
            if (int.TryParse(setting, out hours) && hours >= -12 && hours <= 14)
            {
                return hours;
            }

            return ApplicationTimeDefaults.UtcOffsetHours;
        }

        private static string ResolveDisplayAbbreviation()
        {
            var setting = ConfigurationManager.AppSettings[ApplicationTimeDefaults.AppSettingsDisplayNameKey];
            return string.IsNullOrWhiteSpace(setting)
                ? ApplicationTimeDefaults.DisplayAbbreviation
                : setting.Trim();
        }
    }
}
