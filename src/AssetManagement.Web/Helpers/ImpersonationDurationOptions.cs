using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetManagement.Web.Helpers
{
    public sealed class ImpersonationDurationOption
    {
        public int Minutes { get; set; }

        public string Label { get; set; }

        public bool IsDefault { get; set; }
    }

    public static class ImpersonationDurationOptions
    {
        private static readonly ImpersonationDurationOption[] Options =
        {
            new ImpersonationDurationOption { Minutes = 2, Label = "2 minutes" },
            new ImpersonationDurationOption { Minutes = 5, Label = "5 minutes" },
            new ImpersonationDurationOption { Minutes = 10, Label = "10 minutes" },
            new ImpersonationDurationOption { Minutes = 15, Label = "15 minutes" },
            new ImpersonationDurationOption { Minutes = 30, Label = "30 minutes" },
            new ImpersonationDurationOption { Minutes = 45, Label = "45 minutes" },
            new ImpersonationDurationOption { Minutes = 60, Label = "1 hour", IsDefault = true },
            new ImpersonationDurationOption { Minutes = 90, Label = "1 hour 30 minutes" },
            new ImpersonationDurationOption { Minutes = 120, Label = "2 hours" },
            new ImpersonationDurationOption { Minutes = 180, Label = "3 hours" },
            new ImpersonationDurationOption { Minutes = 240, Label = "4 hours" },
            new ImpersonationDurationOption { Minutes = 360, Label = "6 hours" },
            new ImpersonationDurationOption { Minutes = 480, Label = "8 hours" },
            new ImpersonationDurationOption { Minutes = 720, Label = "12 hours" },
            new ImpersonationDurationOption { Minutes = 1440, Label = "24 hours" }
        };

        public static IEnumerable<ImpersonationDurationOption> All
        {
            get { return Options; }
        }

        public static int DefaultMinutes
        {
            get { return Options.First(x => x.IsDefault).Minutes; }
        }

        public static int ResolveMinutes(int? requestedMinutes)
        {
            if (!requestedMinutes.HasValue)
            {
                return DefaultMinutes;
            }

            var match = Options.FirstOrDefault(x => x.Minutes == requestedMinutes.Value);
            return match != null ? match.Minutes : DefaultMinutes;
        }

        public static string FormatMinutes(int minutes)
        {
            var match = Options.FirstOrDefault(x => x.Minutes == minutes);
            return match != null ? match.Label : minutes + " minutes";
        }
    }
}
