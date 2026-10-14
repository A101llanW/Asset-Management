using System;
using System.Globalization;
using System.Linq;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Validated query for AssetScan/PrintCandidates (scan page print queue and Bulk Label Print).
    /// Sort keys are whitelisted; dates are yyyy-MM-dd calendar days in Kenya time (EAT) converted to the
    /// UTC range used by storage; take is capped at <see cref="MaxTake"/>.
    /// </summary>
    public sealed class PrintCandidateQuery
    {
        public const int MaxTake = 100;
        public const int DefaultTake = 50;
        public const string DateFormat = "yyyy-MM-dd";

        /// <summary>rel1014a: default sort is date of entry, newest first.</summary>
        public const string DefaultSort = "created";

        /// <summary>
        /// rel1014a: registration-day key (yyyy-MM-dd, Kenya time) for the collapsible day headers on
        /// Bulk Label Print. A missing entry date maps to "unknown".
        /// </summary>
        public static string EntryDayKey(DateTime? createdUtc)
        {
            if (!createdUtc.HasValue)
            {
                return "unknown";
            }

            var utc = createdUtc.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(createdUtc.Value, DateTimeKind.Utc)
                : createdUtc.Value.ToUniversalTime();
            return KenyaTimeHelper.FromUtc(utc).ToString(DateFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>tag = asset tag; created = date of entry (Asset.CreatedAt);
        /// assigned = latest AssetAssignment.AssignedDate; activity = latest of CreatedAt, UpdatedAt and AssetCustodyEvent.ActionDate.</summary>
        public static readonly string[] SortKeys = { "tag", "created", "assigned", "activity" };

        public string Sort { get; private set; }

        public string Direction { get; private set; }

        public DateTime? CreatedFromUtc { get; private set; }

        public DateTime? CreatedToUtcExclusive { get; private set; }

        public DateTime? AssignedFromUtc { get; private set; }

        public DateTime? AssignedToUtcExclusive { get; private set; }

        public int Take { get; private set; }

        public string Error { get; private set; }

        public bool IsValid
        {
            get { return Error == null; }
        }

        public static PrintCandidateQuery Parse(
            string sort,
            string direction,
            string createdFrom,
            string createdTo,
            string assignedFrom,
            string assignedTo,
            int? take)
        {
            var query = new PrintCandidateQuery();
            var sortKey = string.IsNullOrWhiteSpace(sort) ? DefaultSort : sort.Trim().ToLowerInvariant();
            if (!SortKeys.Contains(sortKey))
            {
                return Fail(query, "Unknown sort '" + sort + "'. Use one of: " + string.Join(", ", SortKeys) + ".");
            }

            query.Sort = sortKey;
            if (string.IsNullOrWhiteSpace(direction))
            {
                query.Direction = sortKey == "tag" ? "asc" : "desc";
            }
            else
            {
                var dir = direction.Trim().ToLowerInvariant();
                if (dir != "asc" && dir != "desc")
                {
                    return Fail(query, "Direction must be asc or desc.");
                }

                query.Direction = dir;
            }

            DateTime? cFrom, cTo, aFrom, aTo;
            string error;
            if (!TryParseDay(createdFrom, "createdFrom", out cFrom, out error)
                || !TryParseDay(createdTo, "createdTo", out cTo, out error)
                || !TryParseDay(assignedFrom, "assignedFrom", out aFrom, out error)
                || !TryParseDay(assignedTo, "assignedTo", out aTo, out error))
            {
                return Fail(query, error);
            }

            if (cFrom.HasValue && cTo.HasValue && cFrom.Value > cTo.Value)
            {
                return Fail(query, "Entry date 'from' must be on or before 'to'.");
            }

            if (aFrom.HasValue && aTo.HasValue && aFrom.Value > aTo.Value)
            {
                return Fail(query, "Assignment date 'from' must be on or before 'to'.");
            }

            query.CreatedFromUtc = KenyaTimeHelper.StartOfLocalDayToUtc(cFrom);
            query.CreatedToUtcExclusive = KenyaTimeHelper.ExclusiveEndOfLocalDayToUtc(cTo);
            query.AssignedFromUtc = KenyaTimeHelper.StartOfLocalDayToUtc(aFrom);
            query.AssignedToUtcExclusive = KenyaTimeHelper.ExclusiveEndOfLocalDayToUtc(aTo);
            query.Take = Math.Min(Math.Max(take ?? DefaultTake, 1), MaxTake);
            return query;
        }

        private static bool TryParseDay(string value, string name, out DateTime? day, out string error)
        {
            day = null;
            error = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            DateTime parsed;
            if (!DateTime.TryParseExact(value.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                error = name + " must be a date in " + DateFormat + " format.";
                return false;
            }

            day = parsed.Date;
            return true;
        }

        private static PrintCandidateQuery Fail(PrintCandidateQuery query, string error)
        {
            query.Error = error;
            query.Take = DefaultTake;
            return query;
        }
    }
}
