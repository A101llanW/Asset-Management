using System;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Controllers
{
    [RequireTenantRoute]
    public class AssetScanController : Controller
    {
        private const string ScanRateLimitMessage = "Too many scan requests. Please wait and try again.";
        private const int MaxSearchResults = 50;

        private const int MaxPrintCandidates = 100;

        private readonly IAssetService _assetService;
        private readonly IAuthorizationService _authorizationService;
        private readonly ISearchService _searchService;
        private readonly IDistributedRateLimiter _rateLimiter;
        private readonly IDepartmentService _departmentService;

        public AssetScanController()
        {
            _assetService = DependencyResolver.Current.GetService<IAssetService>();
            _authorizationService = DependencyResolver.Current.GetService<IAuthorizationService>();
            _searchService = DependencyResolver.Current.GetService<ISearchService>();
            _rateLimiter = DependencyResolver.Current.GetService<IDistributedRateLimiter>();
            _departmentService = DependencyResolver.Current.GetService<IDepartmentService>();
        }

        public ActionResult Lookup(string code, string q)
        {
            var term = ResolveLookupTerm(code, q);
            if (!TryAcquireScanLookup())
            {
                return View(CreateRateLimitedPageModel(term));
            }

            return View(BuildPageModel(term));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LookupPost(string code)
        {
            if (!TryAcquireScanLookup())
            {
                return View("Lookup", CreateRateLimitedPageModel(code));
            }

            return View("Lookup", BuildPageModel(code));
        }

        [HttpGet]
        public JsonResult LookupJson(string code, string q)
        {
            var term = ResolveLookupTerm(code, q);
            if (!TryAcquireScanLookup())
            {
                return Json(new { Found = false, Message = ScanRateLimitMessage }, JsonRequestBehavior.AllowGet);
            }

            var pageModel = BuildPageModel(term);
            return Json(ToJsonPayload(pageModel), JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [Authorize]
        [TenantAuthorize]
        [PermissionAuthorize("Assets.View")]
        public JsonResult PrintCandidates(int? departmentId, AssetStatus? status, string search, int take = 50)
        {
            var pageSize = Math.Min(Math.Max(take, 1), MaxPrintCandidates);
            var filter = new AssetFilterVm
            {
                DepartmentId = departmentId,
                Status = status,
                Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                OrganizationWide = true
            };

            var page = _assetService.GetAssetListPage(filter, "tag", "asc", 1, pageSize);
            return Json(new
            {
                TotalCount = page.TotalCount,
                Assets = page.Items.Select(asset => new
                {
                    asset.Id,
                    asset.AssetTag,
                    asset.AssetName,
                    asset.SerialNumber,
                    asset.DepartmentName,
                    Status = asset.CurrentStatus.ToString(),
                    LabelZplUrl = TenantUrlHelper.TenantRouteUrl(Url, "LabelZpl", "Assets", new { id = asset.Id }),
                    DetailsUrl = TenantUrlHelper.TenantRouteUrl(Url, "Details", "Assets", new { id = asset.Id })
                }).ToArray()
            }, JsonRequestBehavior.AllowGet);
        }

        [Authorize]
        [TenantAuthorize]
        [PermissionAuthorize("Assets.View")]
        public ActionResult QuickActions(string id)
        {
            int assetId;
            if (!TryResolveAssetId(id, out assetId))
            {
                return HttpNotFound();
            }

            var asset = _assetService.GetById(assetId);
            if (asset == null)
            {
                return HttpNotFound();
            }

            var userId = User.GetUserId();
            var canAssign = _authorizationService.HasPermission(userId, "Assets.Assign")
                && AssetCustodyRules.CanAssign(asset.CurrentStatus);
            var canTransfer = _authorizationService.HasPermission(userId, "Assets.Transfer")
                && AssetCustodyRules.CanTransfer(asset.CurrentStatus);
            var canReturn = _authorizationService.HasPermission(userId, "Assets.Return");
            var canReportIncident = _authorizationService.HasPermission(userId, "Incidents.Create");
            if (!AssetCustodyRules.HasAnyQuickAction(
                asset.CurrentStatus,
                _authorizationService.HasPermission(userId, "Assets.Assign"),
                _authorizationService.HasPermission(userId, "Assets.Transfer"),
                canReturn,
                canReportIncident))
            {
                return new HttpStatusCodeResult(403, "You do not have permission to perform quick actions on this asset.");
            }

            var model = new AssetQuickActionsVm
            {
                AssetId = asset.Id,
                AssetTag = asset.AssetTag,
                AssetName = asset.AssetName,
                CurrentStatus = asset.CurrentStatus,
                DepartmentName = asset.DepartmentName,
                CanAssign = canAssign,
                CanTransfer = canTransfer,
                CanReturn = canReturn,
                CanReportIncident = canReportIncident,
                CanViewAssetDetails = _authorizationService.HasPermission(userId, "Assets.View")
            };

            return View(model);
        }

        private static string ResolveLookupTerm(string code, string q)
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                return code.Trim();
            }

            return string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        }

        private bool TryResolveAssetId(string id, out int assetId)
        {
            assetId = 0;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            if (int.TryParse(id, out assetId) && assetId > 0)
            {
                return true;
            }

            var lookup = _assetService.LookupByScanCode(id, true, false);
            if (lookup == null || !lookup.Found || !lookup.AssetId.HasValue || lookup.AssetId.Value <= 0)
            {
                return false;
            }

            assetId = lookup.AssetId.Value;
            return true;
        }

        private bool TryAcquireScanLookup()
        {
            if (ScanLookupRateLimiter.TryAcquire(HttpContext, _rateLimiter))
            {
                return true;
            }

            Response.StatusCode = 429;
            Response.TrySkipIisCustomErrors = true;
            return false;
        }

        private AssetScanLookupPageVm CreateRateLimitedPageModel(string code)
        {
            return new AssetScanLookupPageVm
            {
                Lookup = new AssetScanLookupVm
                {
                    Found = false,
                    Message = ScanRateLimitMessage
                },
                IsPublicScan = User == null || User.Identity == null || !User.Identity.IsAuthenticated,
                CanViewAssetDetails = false,
                CanOpenQuickActions = false,
                InitialCode = code,
                LookupJsonUrl = TenantUrlHelper.TenantRouteUrl(Url, "LookupJson", "AssetScan")
            };
        }

        private AssetScanLookupPageVm BuildPageModel(string term)
        {
            var isAuthenticated = User.Identity.IsAuthenticated;
            var userId = User.GetUserId();
            var canViewDetails = isAuthenticated
                && _authorizationService != null
                && _authorizationService.HasPermission(userId, "Assets.View");
            var canAssign = isAuthenticated
                && _authorizationService != null
                && _authorizationService.HasPermission(userId, "Assets.Assign");
            var canTransfer = isAuthenticated
                && _authorizationService != null
                && _authorizationService.HasPermission(userId, "Assets.Transfer");
            var canReturn = isAuthenticated
                && _authorizationService != null
                && _authorizationService.HasPermission(userId, "Assets.Return");
            var canReportIncident = isAuthenticated
                && _authorizationService != null
                && _authorizationService.HasPermission(userId, "Incidents.Create");

            AssetScanLookupVm lookup;
            GlobalSearchResultVm searchResults = null;

            if (string.IsNullOrWhiteSpace(term))
            {
                lookup = new AssetScanLookupVm
                {
                    Found = false,
                    Message = "Enter or scan an asset tag, barcode, serial number, custodian, or department."
                };
            }
            else
            {
                lookup = TryScanLookup(term, canViewDetails);

                if (!lookup.Found && canViewDetails)
                {
                    searchResults = _searchService.Search(term, MaxSearchResults);
                    if (searchResults.TotalCount == 1)
                    {
                        var promoted = TryScanLookup(searchResults.Assets[0].AssetTag, canViewDetails);
                        if (promoted.Found)
                        {
                            lookup = promoted;
                            searchResults = null;
                        }
                    }
                    else if (searchResults.TotalCount == 0)
                    {
                        lookup.Message = "No assets matched \"" + term + "\".";
                    }
                    else
                    {
                        lookup = new AssetScanLookupVm { Found = false };
                    }
                }
            }

            var pageModel = new AssetScanLookupPageVm
            {
                Lookup = lookup,
                SearchResults = searchResults,
                IsPublicScan = !canViewDetails,
                CanViewAssetDetails = lookup.Found && canViewDetails,
                CanOpenQuickActions = lookup.Found
                    && AssetCustodyRules.HasAnyQuickAction(
                        lookup.CurrentStatus,
                        canAssign,
                        canTransfer,
                        canReturn,
                        canReportIncident),
                CanBatchPrintLabels = canViewDetails,
                InitialCode = term,
                LookupJsonUrl = TenantUrlHelper.TenantRouteUrl(Url, "LookupJson", "AssetScan"),
                PrintCandidatesUrl = canViewDetails
                    ? TenantUrlHelper.TenantRouteUrl(Url, "PrintCandidates", "AssetScan")
                    : null,
                LabelPrintConfigUrl = canViewDetails
                    ? TenantUrlHelper.TenantRouteUrl(Url, "LabelPrintConfig", "Assets", new { id = "__id__" })
                    : null,
                LabelZplUrlTemplate = canViewDetails
                    ? TenantUrlHelper.TenantRouteUrl(Url, "LabelZpl", "Assets", new { id = "__id__" })
                    : null,
                DepartmentOptions = canViewDetails ? BuildDepartmentOptions() : null,
                StatusOptions = canViewDetails ? BuildStatusOptions() : null
            };

            if (pageModel.CanViewAssetDetails)
            {
                pageModel.StatusBadgeClass = StatusHtmlHelpers.ToBadgeClass(lookup.CurrentStatus);
                pageModel.BrandModelDisplay = BuildBrandModelDisplay(lookup);
                pageModel.DetailsUrl = TenantUrlHelper.TenantRouteUrl(Url, "Details", "Assets", new { id = lookup.AssetId });
            }

            if (pageModel.CanOpenQuickActions)
            {
                pageModel.QuickActionsUrl = TenantUrlHelper.TenantRouteUrl(Url, "QuickActions", "AssetScan", new { id = lookup.AssetId });
            }

            return pageModel;
        }

        private AssetScanLookupVm TryScanLookup(string term, bool canViewDetails)
        {
            try
            {
                return _assetService.LookupByScanCode(
                    term,
                    applyDepartmentScope: canViewDetails,
                    includeDetails: canViewDetails);
            }
            catch (BusinessException ex)
            {
                return new AssetScanLookupVm { Found = false, Message = ex.Message };
            }
        }

        private object ToJsonPayload(AssetScanLookupPageVm page)
        {
            var lookup = page.Lookup;
            if (page.IsPublicScan)
            {
                return new
                {
                    Found = lookup.Found,
                    Message = lookup.Message
                };
            }

            if (page.SearchResults != null && page.SearchResults.Assets != null && page.SearchResults.Assets.Any())
            {
                return new
                {
                    Found = false,
                    Message = (string)null,
                    CanBatchPrintLabels = page.CanBatchPrintLabels,
                    SearchResults = new
                    {
                        Query = page.SearchResults.Query,
                        TotalCount = page.SearchResults.TotalCount,
                        Assets = page.SearchResults.Assets.Select(hit => new
                        {
                            hit.AssetId,
                            hit.AssetTag,
                            hit.AssetName,
                            hit.SerialNumber,
                            hit.DepartmentName,
                            hit.CustodianName,
                            hit.Status,
                            hit.MatchReason,
                            DetailsUrl = TenantUrlHelper.TenantRouteUrl(Url, "Details", "Assets", new { id = hit.AssetId }),
                            LabelZplUrl = TenantUrlHelper.TenantRouteUrl(Url, "LabelZpl", "Assets", new { id = hit.AssetId })
                        }).ToArray()
                    },
                    EmptyDisplay = DisplayText.Empty
                };
            }

            return new
            {
                Found = lookup.Found,
                Message = lookup.Message,
                AssetId = lookup.AssetId,
                AssetTag = lookup.AssetTag,
                AssetName = lookup.AssetName,
                DepartmentName = lookup.DepartmentName,
                CurrentStatus = lookup.Found ? lookup.CurrentStatus.ToString() : null,
                StatusBadgeClass = page.StatusBadgeClass,
                SerialNumber = lookup.SerialNumber,
                Brand = lookup.Brand,
                Model = lookup.Model,
                BrandModelDisplay = page.BrandModelDisplay,
                CategoryName = lookup.CategoryName,
                CustodianName = lookup.CustodianName,
                CanViewAssetDetails = page.CanViewAssetDetails,
                CanOpenQuickActions = page.CanOpenQuickActions,
                CanBatchPrintLabels = page.CanBatchPrintLabels,
                LabelZplUrl = lookup.Found && lookup.AssetId.HasValue
                    ? TenantUrlHelper.TenantRouteUrl(Url, "LabelZpl", "Assets", new { id = lookup.AssetId.Value })
                    : null,
                DetailsUrl = page.DetailsUrl,
                QuickActionsUrl = page.QuickActionsUrl,
                EmptyDisplay = DisplayText.Empty
            };
        }

        private System.Collections.Generic.IEnumerable<SelectListItem> BuildDepartmentOptions()
        {
            if (_departmentService == null)
            {
                return new SelectListItem[0];
            }

            var departments = _departmentService.GetAll()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = x.Name
                })
                .ToList();

            departments.Insert(0, new SelectListItem { Value = string.Empty, Text = "All departments" });
            return departments;
        }

        private static System.Collections.Generic.IEnumerable<SelectListItem> BuildStatusOptions()
        {
            var statuses = System.Enum.GetValues(typeof(AssetStatus))
                .Cast<AssetStatus>()
                .Select(status => new SelectListItem
                {
                    Value = status.ToString(),
                    Text = status.ToString()
                })
                .ToList();

            statuses.Insert(0, new SelectListItem { Value = string.Empty, Text = "Any status" });
            return statuses;
        }

        private static string BuildBrandModelDisplay(AssetScanLookupVm lookup)
        {
            if (!string.IsNullOrWhiteSpace(lookup.Brand) && !string.IsNullOrWhiteSpace(lookup.Model))
            {
                return lookup.Brand + " / " + lookup.Model;
            }

            if (!string.IsNullOrWhiteSpace(lookup.Brand))
            {
                return lookup.Brand;
            }

            return lookup.Model;
        }
    }
}
