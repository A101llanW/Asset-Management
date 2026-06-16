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
    public partial class ReportService
    {
        public ReportDocumentResultVm GenerateReportDocument(ReportExportRequestVm request, string generatedBy)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            var reportType = (request.ReportType ?? string.Empty).Trim().ToLowerInvariant();
            var period = request.SupportsDateRange()
                ? ReportPeriodHelper.ResolveRange(request.PeriodPreset, request.FromDate, request.ToDate)
                : default(ReportPeriodHelper.DateRange);

            ReportBuildContext context;
            switch (reportType)
            {
                case "asset-register":
                    context = BuildAssetRegisterContext(request, period);
                    break;
                case "custody-movement":
                    context = BuildCustodyMovementContext(request, period);
                    break;
                case "department-summary":
                    context = BuildDepartmentSummaryContext(request);
                    break;
                case "pending-approvals":
                    context = BuildPendingApprovalsContext(request, period);
                    break;
                case "general-ledger":
                    context = BuildGeneralLedgerContext(request, period);
                    break;
                case "warranty-expiry":
                    context = BuildWarrantyExpiryContext(request, period);
                    break;
                case "insurance-coverage":
                    context = BuildInsuranceCoverageContext(request, period);
                    break;
                default:
                    throw new InvalidOperationException("Unknown report type: " + reportType);
            }

            var html = ReportHtmlBuilder.BuildReport(
                context.Title,
                context.Subtitle,
                context.ThemeColor,
                generatedBy,
                context.ReportCode,
                context.PeriodLabel,
                context.FilterSummary,
                context.Stats,
                context.Headers,
                context.Rows,
                context.FooterNote);

            return new ReportDocumentResultVm
            {
                Html = html,
                CsvBytes = CsvExportHelper.ToUtf8Bytes(context.CsvRows),
                RowCount = Math.Max(0, context.CsvRows.Count - 1),
                FileName = context.FileStem + "-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".csv",
                Title = context.Title
            };
        }

        private ReportBuildContext BuildAssetRegisterContext(ReportExportRequestVm request, ReportPeriodHelper.DateRange period)
        {
            var filter = new AssetFilterVm
            {
                DepartmentId = request.DepartmentId,
                CategoryId = request.CategoryId,
                Status = request.Status
            };

            var sortBy = string.IsNullOrWhiteSpace(request.SortBy) ? "tag" : request.SortBy;
            var sortDirection = string.IsNullOrWhiteSpace(request.SortDirection) ? "asc" : request.SortDirection;
            var csvRows = new List<string[]>
            {
                new[]
                {
                    "AssetTag", "AssetName", "Status", "Category", "Department", "Custodian",
                    "AcquisitionCost", "PurchaseDate", "SerialNumber"
                }
            };
            var displayRows = new List<IList<string>>();
            decimal totalValue = 0m;
            var statusCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var exportResult = _assetQueryService.StreamExport(filter, sortBy, sortDirection, asset =>
            {
                if (asset.PurchaseDate.Date < period.From || asset.PurchaseDate.Date > period.To)
                {
                    return;
                }

                totalValue += asset.AcquisitionCost;
                var statusKey = asset.CurrentStatus.ToString();
                if (!statusCounts.ContainsKey(statusKey))
                {
                    statusCounts[statusKey] = 0;
                }
                statusCounts[statusKey]++;

                var row = new[]
                {
                    asset.AssetTag,
                    asset.AssetName,
                    statusKey,
                    asset.CategoryName ?? string.Empty,
                    asset.DepartmentName ?? string.Empty,
                    asset.CurrentCustodianId ?? string.Empty,
                    CurrencyFormatter.Format(asset.AcquisitionCost),
                    asset.PurchaseDate.ToString("yyyy-MM-dd"),
                    asset.SerialNumber ?? string.Empty
                };
                csvRows.Add(row);
                displayRows.Add(row);
            });

            if (exportResult.Truncated && !string.IsNullOrWhiteSpace(exportResult.WarningMessage))
            {
                csvRows.Add(new[] { exportResult.WarningMessage });
            }

            return new ReportBuildContext
            {
                Title = "Asset Register",
                Subtitle = "Detailed asset inventory with acquisition and custody attributes",
                ThemeColor = "#0d6efd",
                ReportCode = "AM-RPT-REG",
                FileStem = "asset-register",
                PeriodLabel = period.Label,
                FilterSummary = BuildFilterSummary(request),
                FooterNote = exportResult.Truncated ? exportResult.WarningMessage : "Purchase date used for period filtering.",
                Headers = new List<string> { "Tag", "Name", "Status", "Category", "Department", "Custodian", "Cost", "Purchased", "Serial" },
                Rows = displayRows,
                CsvRows = csvRows,
                Stats = new List<ReportStatCard>
                {
                    new ReportStatCard { Label = "Assets", Value = displayRows.Count.ToString() },
                    new ReportStatCard { Label = "Total Value", Value = CurrencyFormatter.Format(totalValue) },
                    new ReportStatCard { Label = "Statuses", Value = statusCounts.Count.ToString() }
                }
            };
        }

        private ReportBuildContext BuildCustodyMovementContext(ReportExportRequestVm request, ReportPeriodHelper.DateRange period)
        {
            var from = period.From;
            var to = period.To;
            var departments = _unitOfWork.Repository<Department>().GetAll().ToDictionary(x => x.Id, x => x.Name);
            var assetsQuery = _departmentScope.ApplyAssetScope(_unitOfWork.Repository<Asset>().Query());
            if (request.DepartmentId.HasValue)
            {
                assetsQuery = assetsQuery.Where(x => x.DepartmentId == request.DepartmentId.Value);
            }

            var assets = assetsQuery.ToDictionary(x => x.Id, x => x);
            var assetIds = assets.Keys.ToList();
            var csvRows = new List<string[]>
            {
                new[] { "MovementType", "AssetTag", "AssetName", "EventDate", "FromParty", "ToParty", "Status", "Notes" }
            };
            var displayRows = new List<IList<string>>();
            var transferCount = 0;
            var assignmentCount = 0;

            foreach (var transfer in _unitOfWork.Repository<AssetTransfer>().GetAll()
                .Where(x => assetIds.Contains(x.AssetId))
                .Where(x => x.TransferDate.Date >= from && x.TransferDate.Date <= to)
                .OrderByDescending(x => x.TransferDate))
            {
                Asset asset;
                assets.TryGetValue(transfer.AssetId, out asset);
                var fromParty = ResolveDepartmentLabel(departments, transfer.FromDepartmentId);
                var toParty = ResolveDepartmentLabel(departments, transfer.ToDepartmentId);
                var row = new[]
                {
                    "Transfer",
                    asset?.AssetTag ?? ("Asset#" + transfer.AssetId),
                    asset?.AssetName ?? string.Empty,
                    transfer.TransferDate.ToString("yyyy-MM-dd HH:mm"),
                    fromParty,
                    toParty,
                    transfer.ApprovalStatus.ToString(),
                    transfer.Reason ?? string.Empty
                };
                csvRows.Add(row);
                displayRows.Add(row);
                transferCount++;
            }

            foreach (var assignment in _unitOfWork.Repository<AssetAssignment>().GetAll()
                .Where(x => assetIds.Contains(x.AssetId) && x.AssignedDate.Date >= from && x.AssignedDate.Date <= to)
                .OrderByDescending(x => x.AssignedDate))
            {
                Asset asset;
                assets.TryGetValue(assignment.AssetId, out asset);
                var row = new[]
                {
                    "Assignment",
                    asset?.AssetTag ?? ("Asset#" + assignment.AssetId),
                    asset?.AssetName ?? string.Empty,
                    assignment.AssignedDate.ToString("yyyy-MM-dd HH:mm"),
                    assignment.HandedOverById ?? string.Empty,
                    assignment.ToUserId ?? string.Empty,
                    assignment.IsActive ? "Active" : "Closed",
                    assignment.HandoverNotes ?? string.Empty
                };
                csvRows.Add(row);
                displayRows.Add(row);
                assignmentCount++;
            }

            return new ReportBuildContext
            {
                Title = "Custody Movement",
                Subtitle = "Transfers and assignments within the selected period",
                ThemeColor = "#198754",
                ReportCode = "AM-RPT-CUS",
                FileStem = "custody-movement",
                PeriodLabel = period.Label,
                FilterSummary = BuildFilterSummary(request),
                FooterNote = "Includes department-scoped assets only.",
                Headers = new List<string> { "Type", "Tag", "Asset", "Date", "From", "To", "Status", "Notes" },
                Rows = displayRows,
                CsvRows = csvRows,
                Stats = new List<ReportStatCard>
                {
                    new ReportStatCard { Label = "Movements", Value = displayRows.Count.ToString() },
                    new ReportStatCard { Label = "Transfers", Value = transferCount.ToString() },
                    new ReportStatCard { Label = "Assignments", Value = assignmentCount.ToString() }
                }
            };
        }

        private ReportBuildContext BuildDepartmentSummaryContext(ReportExportRequestVm request)
        {
            var departments = _departmentScope.ApplyDepartmentScope(_unitOfWork.Repository<Department>().Query())
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ToList();
            if (request.DepartmentId.HasValue)
            {
                departments = departments.Where(x => x.Id == request.DepartmentId.Value).ToList();
            }

            var assets = _departmentScope.ApplyAssetScope(_unitOfWork.Repository<Asset>().Query())
                .Where(x => x.IsActive)
                .ToList();
            var csvRows = new List<string[]>
            {
                new[] { "DepartmentCode", "DepartmentName", "ActiveAssets", "AcquisitionTotal" }
            };
            var displayRows = new List<IList<string>>();
            var totalAssets = 0;
            decimal totalValue = 0m;

            foreach (var department in departments)
            {
                var deptAssets = assets.Where(x => x.DepartmentId == department.Id).ToList();
                var deptValue = deptAssets.Sum(x => x.AcquisitionCost);
                totalAssets += deptAssets.Count;
                totalValue += deptValue;
                var row = new[]
                {
                    department.Code ?? string.Empty,
                    department.Name,
                    deptAssets.Count.ToString(),
                    CurrencyFormatter.Format(deptValue)
                };
                csvRows.Add(row);
                displayRows.Add(row);
            }

            if (!request.DepartmentId.HasValue)
            {
                var unassigned = assets.Where(x => !departments.Any(d => d.Id == x.DepartmentId)).ToList();
                if (unassigned.Any())
                {
                    var unassignedValue = unassigned.Sum(x => x.AcquisitionCost);
                    totalAssets += unassigned.Count;
                    totalValue += unassignedValue;
                    var row = new[]
                    {
                        string.Empty,
                        "Unassigned Department",
                        unassigned.Count.ToString(),
                        CurrencyFormatter.Format(unassignedValue)
                    };
                    csvRows.Add(row);
                    displayRows.Add(row);
                }
            }

            return new ReportBuildContext
            {
                Title = "Department Summary",
                Subtitle = "Active asset counts and acquisition totals by department",
                ThemeColor = "#6f42c1",
                ReportCode = "AM-RPT-DEPT",
                FileStem = "department-summary",
                FilterSummary = BuildFilterSummary(request),
                FooterNote = "Totals reflect active assets in your scope.",
                Headers = new List<string> { "Code", "Department", "Assets", "Acquisition Total" },
                Rows = displayRows,
                CsvRows = csvRows,
                Stats = new List<ReportStatCard>
                {
                    new ReportStatCard { Label = "Departments", Value = displayRows.Count.ToString() },
                    new ReportStatCard { Label = "Assets", Value = totalAssets.ToString() },
                    new ReportStatCard { Label = "Portfolio Value", Value = CurrencyFormatter.Format(totalValue) }
                }
            };
        }

        private ReportBuildContext BuildPendingApprovalsContext(ReportExportRequestVm request, ReportPeriodHelper.DateRange period)
        {
            var now = DateTime.UtcNow;
            var visibleAssetIds = new HashSet<int>(_departmentScope.ApplyAssetScope(_unitOfWork.Repository<Asset>().Query()).Select(x => x.Id));
            var assets = _unitOfWork.Repository<Asset>().GetAll().ToDictionary(x => x.Id, x => x);
            var csvRows = new List<string[]>
            {
                new[]
                {
                    "Process", "RequestId", "AssetTag", "AssetName", "RequestedBy",
                    "SubmittedUtc", "AgeDays", "AgingBand", "ApprovalStatus", "CurrentStage"
                }
            };
            var displayRows = new List<IList<string>>();
            var criticalCount = 0;
            var warningCount = 0;

            Action<string[], int> addRow = (row, ageDays) =>
            {
                if (ageDays >= 14)
                {
                    criticalCount++;
                }
                else if (ageDays >= 7)
                {
                    warningCount++;
                }

                csvRows.Add(row);
                displayRows.Add(row);
            };

            foreach (var transfer in _unitOfWork.Repository<AssetTransfer>()
                .Find(x => x.ApprovalStatus == ApprovalStatus.Pending && x.IsActive)
                .OrderByDescending(x => x.TransferDate))
            {
                if (!visibleAssetIds.Contains(transfer.AssetId))
                {
                    continue;
                }

                if (!IsWithinPeriod(transfer.TransferDate, period))
                {
                    continue;
                }

                if (request.DepartmentId.HasValue
                    && transfer.ToDepartmentId != request.DepartmentId.Value
                    && transfer.FromDepartmentId != request.DepartmentId.Value)
                {
                    continue;
                }

                var row = BuildPendingApprovalRow(now, assets, "Asset Transfer", transfer.Id, transfer.AssetId,
                    transfer.RequestedById, transfer.TransferDate, transfer.ApprovalStatus.ToString(),
                    transfer.CurrentApprovalStage.ToString());
                addRow(row, Math.Max(0, (int)(now - transfer.TransferDate).TotalDays));
            }

            foreach (var disposal in _unitOfWork.Repository<DisposalRecord>()
                .Find(x => x.ApprovalStatus == ApprovalStatus.Pending && x.IsActive)
                .OrderByDescending(x => x.DisposalRequestDate))
            {
                if (!visibleAssetIds.Contains(disposal.AssetId))
                {
                    continue;
                }

                if (!IsWithinPeriod(disposal.DisposalRequestDate, period))
                {
                    continue;
                }

                Asset asset;
                assets.TryGetValue(disposal.AssetId, out asset);
                if (request.DepartmentId.HasValue && asset != null && asset.DepartmentId != request.DepartmentId.Value)
                {
                    continue;
                }

                var row = BuildPendingApprovalRow(now, assets, "Asset Disposal", disposal.Id, disposal.AssetId,
                    disposal.RequestedById, disposal.DisposalRequestDate, disposal.ApprovalStatus.ToString(),
                    disposal.CurrentApprovalStage.ToString());
                addRow(row, Math.Max(0, (int)(now - disposal.DisposalRequestDate).TotalDays));
            }

            foreach (var purchaseRequest in _unitOfWork.Repository<PurchaseRequest>()
                .Find(x => x.ApprovalStatus == ApprovalStatus.Pending && x.IsActive)
                .OrderByDescending(x => x.CreatedAt))
            {
                if (!IsWithinPeriod(purchaseRequest.CreatedAt, period))
                {
                    continue;
                }

                if (request.DepartmentId.HasValue && purchaseRequest.DepartmentId != request.DepartmentId.Value)
                {
                    continue;
                }

                if (!_departmentScope.BypassesDepartmentScope
                    && _departmentScope.ScopedDepartmentId.HasValue
                    && purchaseRequest.DepartmentId != _departmentScope.ScopedDepartmentId.Value)
                {
                    continue;
                }

                var deptLabel = ResolveDepartmentName(purchaseRequest.DepartmentId);
                var ageDays = Math.Max(0, (int)(now - purchaseRequest.CreatedAt).TotalDays);
                var row = new[]
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
                };
                addRow(row, ageDays);
            }

            foreach (var assetRequest in _unitOfWork.Repository<AssetRequest>()
                .Find(x => x.Status == AssetRequestStatus.Pending && x.IsActive)
                .OrderByDescending(x => x.CreatedAt))
            {
                if (!IsWithinPeriod(assetRequest.CreatedAt, period))
                {
                    continue;
                }

                if (request.DepartmentId.HasValue
                    && (!assetRequest.DepartmentId.HasValue || assetRequest.DepartmentId.Value != request.DepartmentId.Value))
                {
                    continue;
                }

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
                var row = new[]
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
                };
                addRow(row, ageDays);
            }

            return new ReportBuildContext
            {
                Title = "Pending Approvals Aging",
                Subtitle = "Open workflow items grouped by aging bands",
                ThemeColor = "#fd7e14",
                ReportCode = "AM-RPT-APP",
                FileStem = "pending-approvals-aging",
                PeriodLabel = period.Label,
                FilterSummary = BuildFilterSummary(request),
                FooterNote = "Submitted date used for period filtering.",
                Headers = new List<string> { "Process", "Request", "Tag", "Subject", "Requested By", "Submitted", "Age", "Band", "Status", "Stage" },
                Rows = displayRows,
                CsvRows = csvRows,
                Stats = new List<ReportStatCard>
                {
                    new ReportStatCard { Label = "Pending", Value = displayRows.Count.ToString() },
                    new ReportStatCard { Label = "Warning (7+ d)", Value = warningCount.ToString() },
                    new ReportStatCard { Label = "Critical (14+ d)", Value = criticalCount.ToString() }
                }
            };
        }

        private ReportBuildContext BuildGeneralLedgerContext(ReportExportRequestVm request, ReportPeriodHelper.DateRange period)
        {
            var departments = _unitOfWork.Repository<Department>().GetAll().ToDictionary(x => x.Id, x => x.Name);
            var csvRows = new List<string[]>
            {
                new[] { "PostingDate", "AccountCode", "Department", "AssetTag", "Description", "Debit", "Credit" }
            };
            var displayRows = new List<IList<string>>();
            decimal totalDebit = 0m;

            foreach (var asset in _departmentScope.ApplyAssetScope(_unitOfWork.Repository<Asset>().Query())
                .Where(x => x.IsActive)
                .Where(x => !request.DepartmentId.HasValue || x.DepartmentId == request.DepartmentId.Value)
                .Where(x => x.PurchaseDate.Date >= period.From && x.PurchaseDate.Date <= period.To)
                .OrderBy(x => x.AssetTag))
            {
                var dept = departments.ContainsKey(asset.DepartmentId) ? departments[asset.DepartmentId] : string.Empty;
                totalDebit += asset.AcquisitionCost;
                var row = new[]
                {
                    asset.PurchaseDate.ToString("yyyy-MM-dd"),
                    "1500-ASSET",
                    dept,
                    asset.AssetTag,
                    "Capitalize acquisition",
                    CurrencyFormatter.Format(asset.AcquisitionCost),
                    "0.00"
                };
                csvRows.Add(row);
                displayRows.Add(row);
            }

            return new ReportBuildContext
            {
                Title = "General Ledger Extract",
                Subtitle = "Capitalization postings for active assets",
                ThemeColor = "#20c997",
                ReportCode = "AM-RPT-GL",
                FileStem = "general-ledger",
                PeriodLabel = period.Label,
                FilterSummary = BuildFilterSummary(request),
                FooterNote = "Debit entries use account 1500-ASSET.",
                Headers = new List<string> { "Posting Date", "Account", "Department", "Asset Tag", "Description", "Debit", "Credit" },
                Rows = displayRows,
                CsvRows = csvRows,
                Stats = new List<ReportStatCard>
                {
                    new ReportStatCard { Label = "Postings", Value = displayRows.Count.ToString() },
                    new ReportStatCard { Label = "Total Debit", Value = CurrencyFormatter.Format(totalDebit) },
                    new ReportStatCard { Label = "Account", Value = "1500-ASSET" }
                }
            };
        }

        private ReportBuildContext BuildWarrantyExpiryContext(ReportExportRequestVm request, ReportPeriodHelper.DateRange period)
        {
            var departments = _unitOfWork.Repository<Department>().GetAll().ToDictionary(x => x.Id, x => x.Name);
            var today = DateTime.UtcNow.Date;
            var csvRows = new List<string[]>
            {
                new[] { "AssetTag", "AssetName", "Department", "WarrantyStart", "WarrantyEnd", "DaysRemaining", "RiskBand", "AcquisitionCost" }
            };
            var displayRows = new List<IList<string>>();
            var expiredCount = 0;
            var criticalCount = 0;

            foreach (var asset in _departmentScope.ApplyAssetScope(_unitOfWork.Repository<Asset>().Query())
                .Where(x => x.IsActive && x.WarrantyEndDate.HasValue)
                .Where(x => !request.DepartmentId.HasValue || x.DepartmentId == request.DepartmentId.Value)
                .Where(x => x.WarrantyEndDate.Value.Date >= period.From && x.WarrantyEndDate.Value.Date <= period.To)
                .OrderBy(x => x.WarrantyEndDate))
            {
                var daysRemaining = (int)(asset.WarrantyEndDate.Value.Date - today).TotalDays;
                var band = ResolveExpiryBand(daysRemaining);
                if (daysRemaining < 0)
                {
                    expiredCount++;
                }
                else if (daysRemaining <= 30)
                {
                    criticalCount++;
                }

                var dept = departments.ContainsKey(asset.DepartmentId) ? departments[asset.DepartmentId] : string.Empty;
                var row = new[]
                {
                    asset.AssetTag,
                    asset.AssetName,
                    dept,
                    asset.WarrantyStartDate.HasValue ? asset.WarrantyStartDate.Value.ToString("yyyy-MM-dd") : string.Empty,
                    asset.WarrantyEndDate.Value.ToString("yyyy-MM-dd"),
                    daysRemaining.ToString(),
                    band,
                    CurrencyFormatter.Format(asset.AcquisitionCost)
                };
                csvRows.Add(row);
                displayRows.Add(row);
            }

            return new ReportBuildContext
            {
                Title = "Warranty Expiry",
                Subtitle = "Assets with warranty coverage ending in the selected window",
                ThemeColor = "#dc3545",
                ReportCode = "AM-RPT-WAR",
                FileStem = "warranty-expiry",
                PeriodLabel = period.Label,
                FilterSummary = BuildFilterSummary(request),
                FooterNote = "Days remaining calculated from UTC today.",
                Headers = new List<string> { "Tag", "Asset", "Department", "Start", "End", "Days Left", "Band", "Cost" },
                Rows = displayRows,
                CsvRows = csvRows,
                Stats = new List<ReportStatCard>
                {
                    new ReportStatCard { Label = "In window", Value = displayRows.Count.ToString() },
                    new ReportStatCard { Label = "Expired", Value = expiredCount.ToString() },
                    new ReportStatCard { Label = "Critical (30d)", Value = criticalCount.ToString() }
                }
            };
        }

        private ReportBuildContext BuildInsuranceCoverageContext(ReportExportRequestVm request, ReportPeriodHelper.DateRange period)
        {
            var departments = _unitOfWork.Repository<Department>().GetAll().ToDictionary(x => x.Id, x => x.Name);
            var scopedAssets = _departmentScope.ApplyAssetScope(_unitOfWork.Repository<Asset>().Query())
                .Where(x => x.IsActive)
                .Where(x => !request.DepartmentId.HasValue || x.DepartmentId == request.DepartmentId.Value)
                .ToDictionary(x => x.Id, x => x);
            var today = DateTime.UtcNow.Date;
            var csvRows = new List<string[]>
            {
                new[] { "PolicyNumber", "Insurer", "AssetTag", "AssetName", "Department", "PolicyStart", "PolicyEnd", "DaysRemaining", "InsuredValue", "ClaimEligible" }
            };
            var displayRows = new List<IList<string>>();
            decimal totalInsured = 0m;
            var expiringCount = 0;

            foreach (var policy in _unitOfWork.Repository<InsurancePolicy>().GetAll()
                .Where(x => scopedAssets.ContainsKey(x.AssetId))
                .Where(x => x.PolicyEndDate.Date >= period.From && x.PolicyEndDate.Date <= period.To)
                .OrderBy(x => x.PolicyEndDate))
            {
                Asset asset;
                scopedAssets.TryGetValue(policy.AssetId, out asset);
                var daysRemaining = (int)(policy.PolicyEndDate.Date - today).TotalDays;
                if (daysRemaining <= 90)
                {
                    expiringCount++;
                }

                totalInsured += policy.InsuredValue;
                var dept = asset != null && departments.ContainsKey(asset.DepartmentId)
                    ? departments[asset.DepartmentId]
                    : string.Empty;
                var row = new[]
                {
                    policy.PolicyNumber ?? string.Empty,
                    policy.InsurerName ?? string.Empty,
                    asset?.AssetTag ?? ("Asset#" + policy.AssetId),
                    asset?.AssetName ?? string.Empty,
                    dept,
                    policy.PolicyStartDate.ToString("yyyy-MM-dd"),
                    policy.PolicyEndDate.ToString("yyyy-MM-dd"),
                    daysRemaining.ToString(),
                    CurrencyFormatter.Format(policy.InsuredValue),
                    policy.ClaimEligibility ? "Yes" : "No"
                };
                csvRows.Add(row);
                displayRows.Add(row);
            }

            return new ReportBuildContext
            {
                Title = "Insurance Coverage",
                Subtitle = "Policies renewing within the selected window",
                ThemeColor = "#6610f2",
                ReportCode = "AM-RPT-INS",
                FileStem = "insurance-coverage",
                PeriodLabel = period.Label,
                FilterSummary = BuildFilterSummary(request),
                FooterNote = "Includes active assets in your department scope.",
                Headers = new List<string> { "Policy", "Insurer", "Tag", "Asset", "Department", "Start", "End", "Days Left", "Insured", "Eligible" },
                Rows = displayRows,
                CsvRows = csvRows,
                Stats = new List<ReportStatCard>
                {
                    new ReportStatCard { Label = "Policies", Value = displayRows.Count.ToString() },
                    new ReportStatCard { Label = "Expiring (90d)", Value = expiringCount.ToString() },
                    new ReportStatCard { Label = "Total insured", Value = CurrencyFormatter.Format(totalInsured) }
                }
            };
        }

        private static string ResolveExpiryBand(int daysRemaining)
        {
            if (daysRemaining < 0)
            {
                return "Expired";
            }

            if (daysRemaining <= 30)
            {
                return "Critical (0-30 days)";
            }

            if (daysRemaining <= 90)
            {
                return "Warning (31-90 days)";
            }

            return "Upcoming (90+ days)";
        }

        private static string[] BuildPendingApprovalRow(
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
            return new[]
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
            };
        }

        private string BuildFilterSummary(ReportExportRequestVm request)
        {
            var parts = new List<string>();
            if (request.DepartmentId.HasValue)
            {
                parts.Add("Department: " + ResolveDepartmentName(request.DepartmentId.Value));
            }

            if (request.CategoryId.HasValue)
            {
                var category = _unitOfWork.Repository<AssetCategory>().GetById(request.CategoryId.Value);
                parts.Add("Category: " + (category == null ? request.CategoryId.Value.ToString() : category.Name));
            }

            if (request.Status.HasValue)
            {
                parts.Add("Status: " + request.Status.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.SortBy))
            {
                parts.Add("Sort: " + request.SortBy + " " + (request.SortDirection ?? "asc"));
            }

            return parts.Count == 0 ? "All records in scope" : string.Join("; ", parts);
        }

        private static bool IsWithinPeriod(DateTime value, ReportPeriodHelper.DateRange period)
        {
            if (period.From == default(DateTime) && period.To == default(DateTime))
            {
                return true;
            }

            var date = value.Date;
            return date >= period.From && date <= period.To;
        }

        private static string ResolveDepartmentLabel(IDictionary<int, string> departments, int? departmentId)
        {
            if (!departmentId.HasValue)
            {
                return string.Empty;
            }

            string name;
            return departments.TryGetValue(departmentId.Value, out name) ? name : ("Dept #" + departmentId.Value);
        }

        private class ReportBuildContext
        {
            public string Title { get; set; }

            public string Subtitle { get; set; }

            public string ThemeColor { get; set; }

            public string ReportCode { get; set; }

            public string FileStem { get; set; }

            public string PeriodLabel { get; set; }

            public string FilterSummary { get; set; }

            public string FooterNote { get; set; }

            public IList<string> Headers { get; set; }

            public IList<IList<string>> Rows { get; set; }

            public IList<string[]> CsvRows { get; set; }

            public IList<ReportStatCard> Stats { get; set; }
        }
    }

    internal static class ReportExportRequestExtensions
    {
        public static bool SupportsDateRange(this ReportExportRequestVm request)
        {
            var reportType = (request.ReportType ?? string.Empty).Trim().ToLowerInvariant();
            return reportType != "department-summary";
        }
    }
}
