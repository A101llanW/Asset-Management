using System;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Custody timeline ordering (QA2 T4-timeline-order). Older assignment/return events stored the picked
    /// calendar day as ActionDate (midnight), while relocations/transfers store the real UTC time, so a
    /// same-day assignment sorted below the relocations that happened before it. For a date-only ActionDate
    /// recorded on that same Kenya day, the recording time (CreatedAt) is the real moment of the action.
    /// </summary>
    public static class CustodyTimelineOrdering
    {
        public static DateTime EffectiveActionDate(DateTime actionDate, DateTime createdAt)
        {
            if (createdAt == default(DateTime) || !IsDateOnly(actionDate))
            {
                return actionDate;
            }

            return KenyaTimeHelper.FromUtc(actionDate).Date == KenyaTimeHelper.FromUtc(createdAt).Date
                ? createdAt
                : actionDate;
        }

        /// <summary>
        /// True for values a date-only input produced: raw midnight (legacy) or the start of a Kenya day
        /// in UTC (KenyaTimeHelper.LocalDateInputToUtc for a day other than today).
        /// </summary>
        public static bool IsDateOnly(DateTime value)
        {
            var time = value.TimeOfDay;
            if (time == TimeSpan.Zero)
            {
                return true;
            }

            var kenyaMidnightUtc = KenyaTimeHelper.ToUtc(value.Date.AddDays(1)).TimeOfDay;
            return time == kenyaMidnightUtc;
        }
    }
}
