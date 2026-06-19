using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public partial class ReportService : IReportService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDepartmentScopeService _departmentScope;
        private readonly IMetricsService _metricsService;
        private readonly IDashboardQueryService _dashboardQueryService;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IAssetQueryService _assetQueryService;

        public ReportService(
            IUnitOfWork unitOfWork,
            IDepartmentScopeService departmentScope,
            IMetricsService metricsService,
            IDashboardQueryService dashboardQueryService,
            IOrganizationScopeService organizationScope,
            IAssetQueryService assetQueryService)
        {
            _unitOfWork = unitOfWork;
            _departmentScope = departmentScope;
            _metricsService = metricsService;
            _dashboardQueryService = dashboardQueryService;
            _organizationScope = organizationScope;
            _assetQueryService = assetQueryService;
        }

        public DashboardVm GetDashboard()
        {
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                throw new InvalidOperationException("Organization context is required for dashboard queries.");
            }

            var bypassDepartmentScope = _departmentScope.BypassesDepartmentScope;
            int? departmentId = null;
            var denyDepartmentScope = false;
            if (!bypassDepartmentScope)
            {
                departmentId = _departmentScope.ScopedDepartmentId;
                denyDepartmentScope = !departmentId.HasValue;
            }

            var kpis = _dashboardQueryService.GetKpis(
                organizationId.Value,
                departmentId,
                bypassDepartmentScope,
                denyDepartmentScope);
            var warrantyThresholdDays = _metricsService.GetWarrantyThresholdDays();
            var insuranceThresholdDays = _metricsService.GetInsuranceThresholdDays();
            var activeCount = kpis.TotalAssets;
            var lostDamageCount = kpis.LostDamagedStolenAssets;

            return new DashboardVm
            {
                TotalAssets = activeCount,
                AssignedAssets = kpis.AssignedAssets,
                UnassignedAssets = kpis.UnassignedAssets,
                AssetsUnderMaintenance = kpis.AssetsUnderMaintenance,
                LostDamagedStolenAssets = lostDamageCount,
                TotalAcquisitionValue = kpis.TotalAcquisitionValue,
                ExpiringWarrantyCount = _metricsService.CountExpiringWarranties(warrantyThresholdDays),
                ExpiringInsuranceCount = _metricsService.CountExpiringInsurance(insuranceThresholdDays),
                AssignmentsPerMonth = kpis.AssignmentsPerMonth,
                ApprovalBacklogCount = _metricsService.CountPendingApprovals(PendingApprovalCountMode.UserInboxTotal),
                TopDepartmentValues = kpis.TopDepartmentValues,
                LossDamageRatePercent = activeCount == 0 ? 0m : Math.Round((decimal)lostDamageCount * 100m / activeCount, 1),
                TotalCostOfOwnership = kpis.TotalCostOfOwnership,
                WarrantyThresholdDays = warrantyThresholdDays,
                InsuranceThresholdDays = insuranceThresholdDays,
                IsDepartmentScoped = !_departmentScope.BypassesDepartmentScope && _departmentScope.ScopedDepartmentId.HasValue
            };
        }

        public ReportsHubVm GetReportsHub()
        {
            var now = DateTime.UtcNow;
            var custodyFrom = now.AddMonths(-3).Date;
            var departments = _departmentScope.ApplyDepartmentScope(_unitOfWork.Repository<Department>().Query())
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new ReportLookupVm { Id = x.Id, Name = x.Name })
                .ToList();
            var categories = _unitOfWork.Repository<AssetCategory>().GetAll()
                .OrderBy(x => x.Name)
                .Select(x => new ReportLookupVm { Id = x.Id, Name = x.Name })
                .ToList();

            return new ReportsHubVm
            {
                Dashboard = GetDashboard(),
                AssetRegisterCount = _metricsService.CountAssets(null),
                CustodyMovementCount = _metricsService.CountCustodyMovements(custodyFrom, now.Date),
                DepartmentCount = _metricsService.CountDepartments(true),
                PendingApprovalCount = _metricsService.CountPendingApprovals(PendingApprovalCountMode.GlobalPending),
                ReportDefinitions = BuildReportDefinitions(),
                Departments = departments,
                Categories = categories
            };
        }

        private static IList<ReportDefinitionVm> BuildReportDefinitions()
        {
            return new List<ReportDefinitionVm>
            {
                new ReportDefinitionVm
                {
                    Key = "asset-register",
                    Title = "Asset Register",
                    Description = "Full inventory with category, department, custodian, and acquisition details.",
                    Section = "Asset & custody",
                    ThemeColor = "#0d6efd",
                    SupportsDepartmentFilter = true,
                    SupportsDateRange = true,
                    SupportsCategoryFilter = true,
                    SupportsStatusFilter = true,
                    DefaultPeriodPreset = "this-month"
                },
                new ReportDefinitionVm
                {
                    Key = "custody-movement",
                    Title = "Custody Movement",
                    Description = "Transfers and assignments — ideal for weekly department custody reviews.",
                    Section = "Asset & custody",
                    ThemeColor = "#198754",
                    SupportsDepartmentFilter = true,
                    SupportsDateRange = true,
                    DefaultPeriodPreset = "this-week"
                },
                new ReportDefinitionVm
                {
                    Key = "department-summary",
                    Title = "Department Summary",
                    Description = "Roll-up of active assets and acquisition totals by department.",
                    Section = "Asset & custody",
                    ThemeColor = "#6f42c1",
                    SupportsDepartmentFilter = true,
                    DefaultPeriodPreset = string.Empty
                },
                new ReportDefinitionVm
                {
                    Key = "general-ledger",
                    Title = "General Ledger Extract",
                    Description = "Capitalization postings for finance integration.",
                    Section = "Financial",
                    ThemeColor = "#20c997",
                    SupportsDepartmentFilter = true,
                    SupportsDateRange = true,
                    DefaultPeriodPreset = "this-year"
                },
                new ReportDefinitionVm
                {
                    Key = "pending-approvals",
                    Title = "Pending Approvals Aging",
                    Description = "Open workflow items with aging bands for operational follow-up.",
                    Section = "Workflow",
                    ThemeColor = "#fd7e14",
                    SupportsDepartmentFilter = true,
                    SupportsDateRange = true,
                    DefaultPeriodPreset = "last-3-months"
                },
                new ReportDefinitionVm
                {
                    Key = "warranty-expiry",
                    Title = "Warranty Expiry",
                    Description = "Assets with warranties expiring in the selected window — plan renewals before coverage lapses.",
                    Section = "Risk & compliance",
                    ThemeColor = "#dc3545",
                    SupportsDepartmentFilter = true,
                    SupportsDateRange = true,
                    UsesForwardPeriods = true,
                    DefaultPeriodPreset = "next-90-days"
                },
                new ReportDefinitionVm
                {
                    Key = "insurance-coverage",
                    Title = "Insurance Coverage",
                    Description = "Policies approaching renewal with insured values and eligibility flags.",
                    Section = "Risk & compliance",
                    ThemeColor = "#6610f2",
                    SupportsDepartmentFilter = true,
                    SupportsDateRange = true,
                    UsesForwardPeriods = true,
                    DefaultPeriodPreset = "next-90-days"
                }
            };
        }

        public byte[] ExportAssetRegisterCsv()
        {
            var rows = new List<string[]>
            {
                new[]
                {
                    "AssetTag", "AssetName", "Status", "Category", "Department", "Custodian",
                    "AcquisitionCost", "PurchaseDate", "SerialNumber"
                }
            };

            var exportResult = _assetQueryService.StreamExport(new AssetFilterVm(), "tag", "asc", asset =>
            {
                rows.Add(new[]
                {
                    asset.AssetTag,
                    asset.AssetName,
                    asset.CurrentStatus.ToString(),
                    asset.CategoryName ?? string.Empty,
                    asset.DepartmentName ?? string.Empty,
                    asset.CurrentCustodianId ?? string.Empty,
                    CurrencyFormatter.Format(asset.AcquisitionCost),
                    asset.PurchaseDate.ToString("yyyy-MM-dd"),
                    asset.SerialNumber ?? string.Empty
                });
            });

            if (exportResult.Truncated && !string.IsNullOrWhiteSpace(exportResult.WarningMessage))
            {
                rows.Add(new[] { exportResult.WarningMessage });
            }

            return CsvExportHelper.ToUtf8Bytes(rows);
        }

        public byte[] ExportCustodyMovementCsv(DateTime? fromDate, DateTime? toDate)
        {
            var from = fromDate?.Date ?? DateTime.UtcNow.AddMonths(-3).Date;
            var to = toDate?.Date ?? DateTime.UtcNow.Date;
            var assets = _departmentScope.ApplyAssetScope(_unitOfWork.Repository<Asset>().Query())
                .ToDictionary(x => x.Id, x => x);
            var assetIds = assets.Keys.ToList();
            var rows = new List<string[]>
            {
                new[] { "MovementType", "AssetTag", "AssetName", "EventDate", "FromParty", "ToParty", "Status", "Notes" }
            };

            var transfers = _unitOfWork.Repository<AssetTransfer>().GetAll()
                .Where(x => assetIds.Contains(x.AssetId))
                .Where(x => x.TransferDate.Date >= from && x.TransferDate.Date <= to)
                .OrderByDescending(x => x.TransferDate);
            foreach (var transfer in transfers)
            {
                Asset asset;
                assets.TryGetValue(transfer.AssetId, out asset);
                rows.Add(new[]
                {
                    "Transfer",
                    asset?.AssetTag ?? ("Asset#" + transfer.AssetId),
                    asset?.AssetName ?? string.Empty,
                    transfer.TransferDate.ToString("yyyy-MM-dd HH:mm"),
                    transfer.FromDepartmentId.ToString(),
                    transfer.ToDepartmentId.ToString(),
                    transfer.ApprovalStatus.ToString(),
                    transfer.Reason ?? string.Empty
                });
            }

            var assignments = _unitOfWork.Repository<AssetAssignment>().GetAll()
                .Where(x => assetIds.Contains(x.AssetId) && x.AssignedDate.Date >= from && x.AssignedDate.Date <= to)
                .OrderByDescending(x => x.AssignedDate);
            foreach (var assignment in assignments)
            {
                Asset asset;
                assets.TryGetValue(assignment.AssetId, out asset);
                rows.Add(new[]
                {
                    "Assignment",
                    asset?.AssetTag ?? ("Asset#" + assignment.AssetId),
                    asset?.AssetName ?? string.Empty,
                    assignment.AssignedDate.ToString("yyyy-MM-dd HH:mm"),
                    assignment.HandedOverById ?? string.Empty,
                    assignment.ToUserId ?? string.Empty,
                    assignment.IsActive ? "Active" : "Closed",
                    assignment.HandoverNotes ?? string.Empty
                });
            }

            return CsvExportHelper.ToUtf8Bytes(rows);
        }

        public byte[] ExportDepartmentSummaryCsv()
        {
            var departments = _departmentScope.ApplyDepartmentScope(_unitOfWork.Repository<Department>().Query())
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ToList();
            var assets = _departmentScope.ApplyAssetScope(_unitOfWork.Repository<Asset>().Query())
                .Where(x => x.IsActive)
                .ToList();
            var rows = new List<string[]>
            {
                new[]
                {
                    "DepartmentCode", "DepartmentName", "ActiveAssets", "AcquisitionTotal"
                }
            };

            foreach (var department in departments)
            {
                var deptAssets = assets.Where(x => x.DepartmentId == department.Id).ToList();
                rows.Add(new[]
                {
                    department.Code ?? string.Empty,
                    department.Name,
                    deptAssets.Count.ToString(),
                    CurrencyFormatter.Format(deptAssets.Sum(x => x.AcquisitionCost))
                });
            }

            var unassigned = assets.Where(x => !departments.Any(d => d.Id == x.DepartmentId)).ToList();
            if (unassigned.Any())
            {
                rows.Add(new[]
                {
                    string.Empty,
                    "Unassigned Department",
                    unassigned.Count.ToString(),
                    CurrencyFormatter.Format(unassigned.Sum(x => x.AcquisitionCost))
                });
            }

            return CsvExportHelper.ToUtf8Bytes(rows);
        }

        public byte[] ExportPendingApprovalsAgingCsv()
        {
            var now = DateTime.UtcNow;
            var visibleAssetIds = new HashSet<int>(_departmentScope.ApplyAssetScope(_unitOfWork.Repository<Asset>().Query()).Select(x => x.Id));
            var assets = _unitOfWork.Repository<Asset>().GetAll().ToDictionary(x => x.Id, x => x);
            var rows = new List<string[]>
            {
                new[]
                {
                    "Process", "RequestId", "AssetTag", "AssetName", "RequestedBy",
                    "SubmittedUtc", "AgeDays", "AgingBand", "ApprovalStatus", "CurrentStage"
                }
            };

            foreach (var transfer in _unitOfWork.Repository<AssetTransfer>()
                .Find(x => x.ApprovalStatus == ApprovalStatus.Pending && x.IsActive)
                .OrderByDescending(x => x.TransferDate))
            {
                if (!visibleAssetIds.Contains(transfer.AssetId))
                {
                    continue;
                }

                AppendPendingApprovalRow(rows, now, assets, "Asset Transfer", transfer.Id, transfer.AssetId,
                    transfer.RequestedById, transfer.TransferDate, transfer.ApprovalStatus.ToString(),
                    transfer.CurrentApprovalStage.ToString());
            }

            foreach (var disposal in _unitOfWork.Repository<DisposalRecord>()
                .Find(x => x.ApprovalStatus == ApprovalStatus.Pending && x.IsActive)
                .OrderByDescending(x => x.DisposalRequestDate))
            {
                if (!visibleAssetIds.Contains(disposal.AssetId))
                {
                    continue;
                }

                AppendPendingApprovalRow(rows, now, assets, "Asset Disposal", disposal.Id, disposal.AssetId,
                    disposal.RequestedById, disposal.DisposalRequestDate, disposal.ApprovalStatus.ToString(),
                    disposal.CurrentApprovalStage.ToString());
            }

            foreach (var purchaseRequest in _unitOfWork.Repository<PurchaseRequest>()
                .Find(x => x.ApprovalStatus == ApprovalStatus.Pending && x.IsActive)
                .OrderByDescending(x => x.CreatedAt))
            {
                if (!_departmentScope.BypassesDepartmentScope
                    && _departmentScope.ScopedDepartmentId.HasValue
                    && purchaseRequest.DepartmentId != _departmentScope.ScopedDepartmentId.Value)
                {
                    continue;
                }

                var deptLabel = ResolveDepartmentName(purchaseRequest.DepartmentId);
                var ageDays = Math.Max(0, (int)(now - purchaseRequest.CreatedAt).TotalDays);
                rows.Add(new[]
                {
                    "Purchase Request",
                    purchaseRequest.Id.ToString(),
                    purchaseRequest.RequestNumber ?? string.Empty,
                    deptLabel,
                    purchaseRequest.RequestedById ?? string.Empty,
                    purchaseRequest.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                    ageDays.ToString(),
                    ResolveAgingBand(ageDays),
                    purchaseRequest.ApprovalStatus.ToString(),
                    purchaseRequest.CurrentApprovalStage.ToString()
                });
            }

            foreach (var assetRequest in _unitOfWork.Repository<AssetRequest>()
                .Find(x => x.Status == AssetRequestStatus.Pending && x.IsActive)
                .OrderByDescending(x => x.CreatedAt))
            {
                if (!_departmentScope.BypassesDepartmentScope
                    && _departmentScope.ScopedDepartmentId.HasValue
                    && (!assetRequest.DepartmentId.HasValue || assetRequest.DepartmentId.Value != _departmentScope.ScopedDepartmentId.Value))
                {
                    continue;
                }

                var ageDays = Math.Max(0, (int)(now - assetRequest.CreatedAt).TotalDays);
                var requestedAsset = assetRequest.RequestedAssetId.HasValue
                    ? (assetRequest.RequestedAsset ?? _unitOfWork.Repository<Asset>().GetById(assetRequest.RequestedAssetId.Value))
                    : null;
                var requestedLabel = requestedAsset != null && !string.IsNullOrWhiteSpace(requestedAsset.AssetName)
                    ? requestedAsset.AssetName
                    : (string.IsNullOrWhiteSpace(assetRequest.RequestedAssetTag)
                        ? ("REQ-" + assetRequest.Id)
                        : assetRequest.RequestedAssetTag);
                rows.Add(new[]
                {
                    "Asset Request",
                    assetRequest.Id.ToString(),
                    requestedLabel,
                    requestedAsset != null && !string.IsNullOrWhiteSpace(requestedAsset.AssetName)
                        ? requestedAsset.AssetName
                        : "Employee asset request",
                    assetRequest.RequestedById ?? string.Empty,
                    assetRequest.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                    ageDays.ToString(),
                    ResolveAgingBand(ageDays),
                    assetRequest.Status.ToString(),
                    "1"
                });
            }

            return CsvExportHelper.ToUtf8Bytes(rows);
        }

        private string ResolveDepartmentName(int departmentId)
        {
            var department = _unitOfWork.Repository<Department>().GetById(departmentId);
            return department == null ? ("Dept #" + departmentId) : department.Name;
        }

        private static void AppendPendingApprovalRow(
            ICollection<string[]> rows,
            DateTime now,
            IDictionary<int, Asset> assets,
            string process,
            int requestId,
            int assetId,
            string requestedById,
            DateTime submittedUtc,
            string approvalStatus,
            string currentStage)
        {
            Asset asset;
            assets.TryGetValue(assetId, out asset);
            var ageDays = Math.Max(0, (int)(now - submittedUtc).TotalDays);
            rows.Add(new[]
            {
                process,
                requestId.ToString(),
                asset?.AssetTag ?? (assetId > 0 ? ("Asset#" + assetId) : string.Empty),
                asset?.AssetName ?? string.Empty,
                requestedById ?? string.Empty,
                submittedUtc.ToString("yyyy-MM-dd HH:mm"),
                ageDays.ToString(),
                ResolveAgingBand(ageDays),
                approvalStatus,
                currentStage
            });
        }

        private static string ResolveAgingBand(int ageDays)
        {
            if (ageDays >= 14)
            {
                return "Critical (14+ days)";
            }

            if (ageDays >= 7)
            {
                return "Warning (7-13 days)";
            }

            return "Current (0-6 days)";
        }

        public byte[] ExportGeneralLedgerCsv()
        {
            var departments = _unitOfWork.Repository<Department>().GetAll().ToDictionary(x => x.Id, x => x.Name);
            var rows = new List<string[]>
            {
                new[]
                {
                    "PostingDate", "AccountCode", "Department", "AssetTag", "Description",
                    "Debit", "Credit"
                }
            };

            foreach (var asset in _departmentScope.ApplyAssetScope(_unitOfWork.Repository<Asset>().Query())
                .Where(x => x.IsActive).OrderBy(x => x.AssetTag))
            {
                var dept = asset.DepartmentId.HasValue && departments.ContainsKey(asset.DepartmentId.Value)
                    ? departments[asset.DepartmentId.Value]
                    : string.Empty;
                rows.Add(new[]
                {
                    asset.PurchaseDate.ToString("yyyy-MM-dd"),
                    "1500-ASSET",
                    dept,
                    asset.AssetTag,
                    "Capitalize acquisition",
                    CurrencyFormatter.Format(asset.AcquisitionCost),
                    "0.00"
                });
            }

            return CsvExportHelper.ToUtf8Bytes(rows);
        }
    }
}
