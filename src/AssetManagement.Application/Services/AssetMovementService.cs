using System;
using System.Collections.Generic;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Services
{
    /// <summary>
    /// Asset movement reads (Assets &amp; custody &gt; Movements, asset Details tab, CSV export).
    /// Filter dates are org-local calendar days, converted to UTC bounds; times are shown in the org time zone.
    /// </summary>
    public class AssetMovementService : IAssetMovementService
    {
        public const int MaxPageSize = 200;
        public const int MaxExportRows = 50000;

        private readonly IAssetMovementQueryRepository _repository;
        private readonly IDepartmentScopeService _departmentScope;
        private readonly IOrganizationScopeService _organizationScope;

        public AssetMovementService(
            IAssetMovementQueryRepository repository,
            IDepartmentScopeService departmentScope,
            IOrganizationScopeService organizationScope)
        {
            _repository = repository;
            _departmentScope = departmentScope;
            _organizationScope = organizationScope;
        }

        public AssetMovementPageVm GetPage(AssetMovementFilterVm filter)
        {
            filter = filter ?? new AssetMovementFilterVm();
            var pageSize = filter.PageSize <= 0 ? 50 : Math.Min(filter.PageSize, MaxPageSize);
            var page = filter.Page <= 0 ? 1 : filter.Page;
            filter.PageSize = pageSize;
            filter.Page = page;

            var vm = new AssetMovementPageVm { Filter = filter, Page = page, PageSize = pageSize };
            var result = Run(filter, (page - 1) * pageSize, pageSize, true);
            if (result == null)
            {
                return vm;
            }

            vm.Rows = result.Rows;
            vm.TotalCount = result.TotalCount;
            vm.Unavailable = result.Unavailable;
            vm.TotalPages = Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)pageSize));
            return vm;
        }

        public byte[] ExportCsv(AssetMovementFilterVm filter)
        {
            filter = filter ?? new AssetMovementFilterVm();
            var eat = KenyaTimeHelper.DisplayAbbreviation;
            var rows = new List<string[]>
            {
                new[] { "When (" + eat + ")", "Asset tag", "Asset", "Movement", "From department", "To department", "From custodian", "To custodian", "Performed by", "Batch", "Notes", "Source" }
            };

            var result = Run(filter, 0, MaxExportRows, false);
            if (result != null)
            {
                foreach (var r in result.Rows)
                {
                    rows.Add(new[]
                    {
                        KenyaTimeHelper.FormatUtc(r.OccurredAtUtc, KenyaTimeHelper.ShortTimestampFormat),
                        r.AssetTag ?? string.Empty,
                        r.AssetName ?? string.Empty,
                        r.MovementTypeLabel ?? string.Empty,
                        r.FromDepartmentName ?? string.Empty,
                        r.ToDepartmentName ?? string.Empty,
                        r.FromCustodianName ?? r.FromCustodianUserId ?? string.Empty,
                        r.ToCustodianName ?? r.ToCustodianUserId ?? string.Empty,
                        r.PerformedByName ?? r.PerformedByUserId ?? string.Empty,
                        r.BatchId ?? string.Empty,
                        r.Notes ?? string.Empty,
                        r.SourceRef ?? string.Empty
                    });
                }
            }

            return CsvExportHelper.ToUtf8Bytes(rows);
        }

        public AssetMovementQueryResult GetForAsset(int assetId, int max)
        {
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return new AssetMovementQueryResult();
            }

            // Access to the asset itself was checked by the caller (Assets/Details), so its whole history is shown.
            var result = _repository.Query(organizationId.Value, null, new AssetMovementFilterVm { AssetId = assetId }, null, null, 0, Math.Max(1, max), false);
            Label(result);
            return result;
        }

        private AssetMovementQueryResult Run(AssetMovementFilterVm filter, int skip, int take, bool includeTotal)
        {
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return null;
            }

            int? scopedDepartmentId = null;
            if (!_departmentScope.BypassesDepartmentScope)
            {
                scopedDepartmentId = _departmentScope.ScopedDepartmentId;
                if (!scopedDepartmentId.HasValue)
                {
                    return new AssetMovementQueryResult();
                }
            }

            var fromUtc = KenyaTimeHelper.StartOfLocalDayToUtc(filter.FromDate);
            var toUtc = KenyaTimeHelper.ExclusiveEndOfLocalDayToUtc(filter.ToDate);
            var result = _repository.Query(organizationId.Value, scopedDepartmentId, filter, fromUtc, toUtc, skip, take, includeTotal);
            Label(result);
            return result;
        }

        private static void Label(AssetMovementQueryResult result)
        {
            if (result == null)
            {
                return;
            }

            foreach (var row in result.Rows)
            {
                row.MovementTypeLabel = AssetMovementRecorder.Label(row.MovementType);
            }
        }
    }
}
