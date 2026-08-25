using System;

namespace AssetManagement.Application.Helpers
{
    public static class ReportPeriodHelper
    {
        public static DateRange ResolveRange(string periodPreset, DateTime? fromDate, DateTime? toDate, DateTime? referenceUtc = null)
        {
            var now = (referenceUtc ?? DateTime.UtcNow).Date;
            var preset = (periodPreset ?? string.Empty).Trim().ToLowerInvariant();

            switch (preset)
            {
                case "next-30-days":
                    return new DateRange(now, now.AddDays(30));
                case "next-90-days":
                    return new DateRange(now, now.AddDays(90));
                case "this-week":
                    var weekStart = now.AddDays(-(int)now.DayOfWeek);
                    return new DateRange(weekStart, weekStart.AddDays(6));
                case "last-week":
                    var lastWeekEnd = now.AddDays(-(int)now.DayOfWeek - 1);
                    return new DateRange(lastWeekEnd.AddDays(-6), lastWeekEnd);
                case "this-month":
                    return new DateRange(new DateTime(now.Year, now.Month, 1), now);
                case "last-month":
                    var firstThisMonth = new DateTime(now.Year, now.Month, 1);
                    var lastMonthEnd = firstThisMonth.AddDays(-1);
                    return new DateRange(new DateTime(lastMonthEnd.Year, lastMonthEnd.Month, 1), lastMonthEnd);
                case "this-year":
                    return new DateRange(new DateTime(now.Year, 1, 1), now);
                case "last-3-months":
                    return new DateRange(now.AddMonths(-3), now);
                case "custom":
                    if (fromDate.HasValue && toDate.HasValue)
                    {
                        return new DateRange(fromDate.Value.Date, toDate.Value.Date);
                    }
                    break;
            }

            if (fromDate.HasValue || toDate.HasValue)
            {
                return new DateRange(fromDate?.Date ?? now.AddMonths(-3), toDate?.Date ?? now);
            }

            return new DateRange(now.AddMonths(-3), now);
        }

        public struct DateRange
        {
            public DateRange(DateTime from, DateTime to)
            {
                From = from.Date;
                To = to.Date;
            }

            public DateTime From { get; }

            public DateTime To { get; }

            public string Label
            {
                get { return From.ToString("dd MMM yyyy") + " – " + To.ToString("dd MMM yyyy"); }
            }
        }
    }
}
