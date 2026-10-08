using System;
using System.Linq;
using AssetManagement.Application.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Assets
{
    [TestFixture]
    public class CustodyTimelineOrderingTests
    {
        private static DateTime Utc(int y, int m, int d, int h = 0, int min = 0)
        {
            return new DateTime(y, m, d, h, min, 0, DateTimeKind.Utc);
        }

        [Test]
        public void LegacyMidnightAssignment_RecordedSameKenyaDay_UsesRecordingTime()
        {
            var created = Utc(2026, 10, 8, 18, 2);
            Assert.AreEqual(created, CustodyTimelineOrdering.EffectiveActionDate(Utc(2026, 10, 8), created));
        }

        [Test]
        public void KenyaStartOfDay_RecordedSameKenyaDay_UsesRecordingTime()
        {
            // 2026-10-08 00:00 EAT = 2026-10-07 21:00 UTC
            var created = Utc(2026, 10, 8, 9, 30);
            Assert.AreEqual(created, CustodyTimelineOrdering.EffectiveActionDate(Utc(2026, 10, 7, 21), created));
        }

        [Test]
        public void BackdatedDay_KeepsPickedDay()
        {
            var action = Utc(2026, 10, 4, 21); // 2026-10-05 EAT
            Assert.AreEqual(action, CustodyTimelineOrdering.EffectiveActionDate(action, Utc(2026, 10, 8, 18)));
        }

        [Test]
        public void RealTimestamp_IsUnchanged()
        {
            var action = Utc(2026, 10, 8, 17, 58);
            Assert.AreEqual(action, CustodyTimelineOrdering.EffectiveActionDate(action, Utc(2026, 10, 8, 17, 58)));
        }

        [Test]
        public void SameDayRelocationsThenAssignment_AssignmentSortsNewest()
        {
            var events = new[]
            {
                new { Id = 40, Action = Utc(2026, 10, 8, 17, 58), Created = Utc(2026, 10, 8, 17, 58), Name = "relocate1" },
                new { Id = 41, Action = Utc(2026, 10, 8, 17, 59), Created = Utc(2026, 10, 8, 17, 59), Name = "relocate2" },
                new { Id = 42, Action = Utc(2026, 10, 8), Created = Utc(2026, 10, 8, 18, 1), Name = "assign" }
            };
            var ordered = events
                .OrderByDescending(x => CustodyTimelineOrdering.EffectiveActionDate(x.Action, x.Created))
                .ThenByDescending(x => x.Created)
                .ThenByDescending(x => x.Id)
                .Select(x => x.Name)
                .ToArray();
            CollectionAssert.AreEqual(new[] { "assign", "relocate2", "relocate1" }, ordered);
        }
    }
}
