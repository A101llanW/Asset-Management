using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using AssetManagement.Application.Contracts;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    public static class DepreciationCalculator
    {
        /// <summary>How long a dashboard-triggered org refresh is reused within the same month.</summary>
        public static readonly TimeSpan DashboardRefreshInterval = TimeSpan.FromMinutes(10);

        private static readonly ConcurrentDictionary<int, RefreshStamp> LastDashboardRefresh =
            new ConcurrentDictionary<int, RefreshStamp>();

        private static readonly ConcurrentDictionary<int, byte> RefreshInFlight =
            new ConcurrentDictionary<int, byte>();

        /// <summary>
        /// Off-request org refresh (organizationId, asOfUtc), installed at app start by the web host.
        /// When set, <see cref="RefreshOrganizationIfStale"/> queues the refresh on a pool thread and
        /// returns immediately, so no Dashboard/Reports request pays for a full-org recompute. When
        /// null (tests, tools) the refresh runs inline as before. Must compute exactly what
        /// <see cref="RefreshOrganization"/> computes (same Compute/Resolve).
        /// </summary>
        public static Action<int, DateTime> BackgroundOrganizationRefresh { get; set; }

        public sealed class Position
        {
            public decimal AccumulatedDepreciation { get; set; }

            public decimal CurrentBookValue { get; set; }

            public int ElapsedMonths { get; set; }
        }

        public static Position Compute(Asset asset, DepreciationSettings settings, DateTime asOfUtc)
        {
            if (asset == null)
            {
                return new Position();
            }

            if (asset.CurrentStatus == AssetStatus.Disposed || asset.CurrentStatus == AssetStatus.Retired)
            {
                return new Position
                {
                    CurrentBookValue = asset.CurrentBookValue,
                    AccumulatedDepreciation = asset.AccumulatedDepreciation,
                    ElapsedMonths = 0
                };
            }

            // Unpriced asset ("Not set"): nothing to depreciate and no value to carry. Never fall back to
            // the sub-type/type price or crash on a null cost.
            if (!asset.AcquisitionCost.HasValue)
            {
                return new Position
                {
                    CurrentBookValue = 0m,
                    AccumulatedDepreciation = 0m,
                    ElapsedMonths = 0
                };
            }

            var cost = asset.AcquisitionCost.Value;

            if (settings == null || settings.LifeMonths <= 0 || settings.AnnualRatePercent <= 0)
            {
                return new Position
                {
                    CurrentBookValue = cost,
                    AccumulatedDepreciation = 0m,
                    ElapsedMonths = 0
                };
            }

            if (asOfUtc.Date < asset.DepreciationStartDate.Date)
            {
                return new Position
                {
                    CurrentBookValue = cost,
                    AccumulatedDepreciation = 0m,
                    ElapsedMonths = 0
                };
            }

            var elapsedMonths = CountElapsedMonths(asset.DepreciationStartDate, asOfUtc, settings.LifeMonths);
            if (elapsedMonths <= 0)
            {
                return new Position
                {
                    CurrentBookValue = cost,
                    AccumulatedDepreciation = 0m,
                    ElapsedMonths = 0
                };
            }

            if (asset.DepreciationMethod == DepreciationMethod.ReducingBalance)
            {
                return ComputeReducingBalance(asset, cost, settings, elapsedMonths);
            }

            return ComputeStraightLine(asset, cost, settings, elapsedMonths);
        }

        public static void ApplyToAsset(Asset asset, DepreciationSettings settings, DateTime asOfUtc)
        {
            if (asset == null)
            {
                return;
            }

            var position = Compute(asset, settings, asOfUtc);
            asset.CurrentBookValue = position.CurrentBookValue;
            asset.AccumulatedDepreciation = position.AccumulatedDepreciation;
        }

        /// <summary>
        /// Runs <see cref="RefreshOrganization"/> unless this org was already refreshed by this
        /// method within <paramref name="maxAge"/> for the same as-of month. Positions are
        /// month-granular and asset create/edit recompute their own row, so a short reuse window
        /// only delays picking up type/category default changes. Any direct RefreshOrganization
        /// call (e.g. a report run for another period) clears the stamp.
        /// </summary>
        public static bool RefreshOrganizationIfStale(IUnitOfWork unitOfWork, int organizationId, DateTime asOfUtc, TimeSpan maxAge)
        {
            if (unitOfWork == null)
            {
                return false;
            }

            var monthKey = asOfUtc.Year * 12 + asOfUtc.Month;
            var nowUtc = DateTime.UtcNow;
            RefreshStamp stamp;
            if (LastDashboardRefresh.TryGetValue(organizationId, out stamp)
                && stamp.MonthKey == monthKey
                && nowUtc - stamp.RefreshedAtUtc < maxAge
                && nowUtc >= stamp.RefreshedAtUtc)
            {
                return false;
            }

            var background = BackgroundOrganizationRefresh;
            if (background == null)
            {
                RefreshOrganization(unitOfWork, organizationId, asOfUtc);
                LastDashboardRefresh[organizationId] = new RefreshStamp(monthKey, nowUtc);
                return true;
            }

            // One background refresh per org at a time; callers serve the stored values meanwhile.
            if (!RefreshInFlight.TryAdd(organizationId, 0))
            {
                return false;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    background(organizationId, asOfUtc);
                    LastDashboardRefresh[organizationId] = new RefreshStamp(monthKey, DateTime.UtcNow);
                }
                catch (Exception ex)
                {
                    // Retry on a later request (about a minute), never on every hit.
                    System.Diagnostics.Trace.TraceWarning(
                        "Depreciation background refresh failed for org {0}: {1}", organizationId, ex.Message);
                    LastDashboardRefresh[organizationId] = new RefreshStamp(
                        monthKey,
                        DateTime.UtcNow - maxAge + TimeSpan.FromMinutes(1));
                }
                finally
                {
                    byte ignored;
                    RefreshInFlight.TryRemove(organizationId, out ignored);
                }
            });
            return true;
        }

        public static void RefreshOrganization(IUnitOfWork unitOfWork, int organizationId, DateTime asOfUtc)
        {
            if (unitOfWork == null)
            {
                return;
            }

            RefreshStamp removed;
            LastDashboardRefresh.TryRemove(organizationId, out removed);

            var assets = unitOfWork.Repository<Asset>()
                .Find(x => x.IsActive && x.OrganizationId == organizationId)
                .ToList();
            if (assets.Count == 0)
            {
                return;
            }

            var types = unitOfWork.Repository<AssetType>().GetAll().ToDictionary(x => x.Id);
            var categories = unitOfWork.Repository<AssetCategory>().GetAll().ToDictionary(x => x.Id);
            var changed = false;

            foreach (var asset in assets)
            {
                AssetType assetType;
                types.TryGetValue(asset.AssetTypeId, out assetType);
                AssetCategory category;
                categories.TryGetValue(asset.CategoryId, out category);
                var settings = DepreciationSettingsResolver.Resolve(asset, assetType, category);
                var beforeBook = asset.CurrentBookValue;
                var beforeAccumulated = asset.AccumulatedDepreciation;
                ApplyToAsset(asset, settings, asOfUtc);
                if (asset.CurrentBookValue == beforeBook && asset.AccumulatedDepreciation == beforeAccumulated)
                {
                    continue;
                }

                asset.UpdatedAt = asOfUtc;
                unitOfWork.Repository<Asset>().Update(asset);
                changed = true;
            }

            if (changed)
            {
                unitOfWork.SaveChanges();
            }
        }

        private static int CountElapsedMonths(DateTime startDate, DateTime asOfUtc, int lifeMonths)
        {
            var start = new DateTime(startDate.Year, startDate.Month, 1);
            var asOf = new DateTime(asOfUtc.Year, asOfUtc.Month, 1);
            var months = (asOf.Year - start.Year) * 12 + (asOf.Month - start.Month);
            if (months < 0)
            {
                months = 0;
            }

            return Math.Min(months, lifeMonths);
        }

        private static Position ComputeStraightLine(Asset asset, decimal cost, DepreciationSettings settings, int elapsedMonths)
        {
            var depreciable = cost - asset.SalvageValue;
            if (depreciable <= 0m)
            {
                return new Position
                {
                    CurrentBookValue = cost,
                    AccumulatedDepreciation = 0m,
                    ElapsedMonths = elapsedMonths
                };
            }

            var monthly = System.Math.Round(depreciable * settings.AnnualRatePercent / 1200m, 2);
            var accumulated = System.Math.Min(monthly * elapsedMonths, depreciable);
            var bookValue = cost - accumulated;
            if (bookValue < asset.SalvageValue)
            {
                accumulated = cost - asset.SalvageValue;
                bookValue = asset.SalvageValue;
            }

            return new Position
            {
                AccumulatedDepreciation = accumulated,
                CurrentBookValue = bookValue,
                ElapsedMonths = elapsedMonths
            };
        }

        private static Position ComputeReducingBalance(Asset asset, decimal cost, DepreciationSettings settings, int elapsedMonths)
        {
            var bookValue = cost;
            var accumulated = 0m;
            var workingAsset = new Asset
            {
                DepreciationMethod = asset.DepreciationMethod,
                AcquisitionCost = cost,
                SalvageValue = asset.SalvageValue,
                CurrentBookValue = bookValue
            };

            for (var month = 0; month < elapsedMonths; month++)
            {
                workingAsset.CurrentBookValue = bookValue;
                var monthly = DepreciationSettingsResolver.CalculateMonthlyAmount(workingAsset, settings);
                if (monthly <= 0m)
                {
                    break;
                }

                if (bookValue - monthly < asset.SalvageValue)
                {
                    monthly = bookValue - asset.SalvageValue;
                    accumulated += monthly;
                    bookValue = asset.SalvageValue;
                    break;
                }

                bookValue -= monthly;
                accumulated += monthly;
            }

            return new Position
            {
                AccumulatedDepreciation = accumulated,
                CurrentBookValue = bookValue,
                ElapsedMonths = elapsedMonths
            };
        }

        private sealed class RefreshStamp
        {
            public RefreshStamp(int monthKey, DateTime refreshedAtUtc)
            {
                MonthKey = monthKey;
                RefreshedAtUtc = refreshedAtUtc;
            }

            public int MonthKey { get; private set; }

            public DateTime RefreshedAtUtc { get; private set; }
        }
    }
}
