namespace AssetManagement.Application
{
    public static class ApplicationTimeDefaults
    {
        /// <summary>Windows timezone id for East Africa (Kenya).</summary>
        public const string TimeZoneId = "E. Africa Standard Time";

        /// <summary>IANA timezone id for Kenya.</summary>
        public const string IanaTimeZoneId = "Africa/Nairobi";

        public const int UtcOffsetHours = 3;

        public const string DisplayAbbreviation = "EAT";

        public const string DisplayLabel = "Kenya time (EAT)";

        public const string AppSettingsOffsetHoursKey = "ApplicationTimeZoneOffsetHours";

        public const string AppSettingsDisplayNameKey = "ApplicationTimeZoneDisplayName";
    }
}
