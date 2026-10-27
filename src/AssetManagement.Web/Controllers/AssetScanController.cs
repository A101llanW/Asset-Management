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

        /// <summary>
        /// Candidate assets for label printing (Scan &amp; Search print queue and AssetLabels/BulkPrint).
        /// Query params are validated by <see cref="PrintCandidateQuery"/>: sort in (tag, created, assigned, activity),
        /// direction asc|desc, createdFrom/createdTo/assignedFrom/assignedTo as yyyy-MM-dd (EAT days), take 1..100.
        /// Invalid input returns HTTP 400 with { Error }.
        /// Bulk Label Print also sends categoryId, assetTypeId, assetSubTypeId (cascading pickers) and
        /// includeSubDepartments (default true: the chosen department/class/room plus every node below it).
        /// All filters combine with AND; the asset query stays tenant-scoped, so another tenant's ids match nothing.
        /// </summary>
        [HttpGet]
        [Authorize]
        [TenantAuthorize]
        [PermissionAuthorize("Assets.View")]
        public JsonResult PrintCandidates(
            int? departmentId,
            AssetStatus? status,
            string search,
            string sort = null,
            string direction = null,
            string createdFrom = null,
            string createdTo = null,
            string assignedFrom = null,
            string assignedTo = null,
            int take = PrintCandidateQuery.DefaultTake,
            int? purchaseId = null,
            int? categoryId = null,
            int? assetTypeId = null,
            int? assetSubTypeId = null,
            bool includeSubDepartments = true)
        {
            var query = PrintCandidateQuery.Parse(sort, direction, createdFrom, createdTo, assignedFrom, assignedTo, take);
            if (!query.IsValid)
            {
                Response.StatusCode = 400;
                Response.TrySkipIisCustomErrors = true;
                return Json(new { Error = query.Error }, JsonRequestBehavior.AllowGet);
            }

            var departmentIds = PrintCandidateQuery.ResolveDepartmentIds(
                PrintCandidateQuery.NormalizeId(departmentId),
                includeSubDepartments,
                departmentId.HasValue && includeSubDepartments && _departmentService != null ? _departmentService.GetAll() : null);
            var filter = new AssetFilterVm
            {
                // One node: exact match (as before); a subtree: IN (...) over the node and its descendants.
                DepartmentId = departmentIds != null && departmentIds.Count == 1 ? departmentIds[0] : (int?)null,
                DepartmentIds = departmentIds != null && departmentIds.Count > 1 ? departmentIds : null,
                CategoryId = PrintCandidateQuery.NormalizeId(categoryId),
                AssetTypeId = PrintCandidateQuery.NormalizeId(assetTypeId),
                AssetSubTypeId = PrintCandidateQuery.NormalizeId(assetSubTypeId),
                Status = status,
                Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                // Review fix S9: printable assets follow the user's department scope (Assets.ViewAll = whole organization).
                OrganizationWide = false,
                CreatedFromUtc = query.CreatedFromUtc,
                CreatedToUtcExclusive = query.CreatedToUtcExclusive,
                AssignedFromUtc = query.AssignedFromUtc,
                AssignedToUtcExclusive = query.AssignedToUtcExclusive,
                IncludeActivityDates = true,
                // rel1014a: PO filter. The EXISTS clause also matches the asset's tenant, so another tenant's id returns nothing.
                PurchaseRecordId = purchaseId.HasValue && purchaseId.Value > 0 ? purchaseId : null
            };

            var page = _assetService.GetAssetListPage(filter, query.Sort, query.Direction, 1, query.Take);
            return Json(new
            {
                TotalCount = page.TotalCount,
                Sort = query.Sort,
                Direction = query.Direction,
                Take = query.Take,
                Assets = page.Items.Select(asset => new
                {
                    asset.Id,
                    asset.AssetTag,
                    asset.AssetName,
                    asset.SerialNumber,
                    asset.DepartmentName,
                    Status = asset.CurrentStatus.ToString(),
                    EnteredAt = FormatEat(asset.CreatedAt),
                    EntryDay = PrintCandidateQuery.EntryDayKey(asset.CreatedAt),
                    EntryDayLabel = FormatEntryDayLabel(asset.CreatedAt),
                    LastAssignedAt = FormatEat(asset.LastAssignedAt),
                    LastActivityAt = FormatEat(asset.LastActivityAt),
                    LabelZplUrl = TenantUrlHelper.TenantRouteUrl(Url, "LabelZpl", "Assets", new { id = asset.Id }),
                    DetailsUrl = TenantUrlHelper.TenantRouteUrl(Url, "Details", "Assets", new { id = asset.Id })
                }).ToArray()
            }, JsonRequestBehavior.AllowGet);
        }

        private static string FormatEntryDayLabel(DateTime? utc)
        {
            return utc.HasValue
                ? KenyaTimeHelper.FormatUtc(utc.Value, "ddd d MMM yyyy")
                : "Entry date unknown";
        }

        private static string FormatEat(DateTime? utc)
        {
            return utc.HasValue ? KenyaTimeHelper.FormatUtc(utc.Value, KenyaTimeHelper.ShortTimestampFormat) : null;
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

            // rel1027: a signed-in user whose department scope hides this asset still gets the PUBLIC card
            // (same as a signed-out viewer: no names, e-mails, cost or supplier, no actions) instead of "not found".
            var outsideScope = false;
            if (!lookup.Found && canViewDetails && !string.IsNullOrWhiteSpace(term)
                && (searchResults == null || searchResults.TotalCount == 0))
            {
                var unscoped = TryUnscopedScanLookup(term);
                if (unscoped.Found)
                {
                    lookup = unscoped;
                    searchResults = null;
                    outsideScope = true;
                }
            }

            var pageModel = new AssetScanLookupPageVm
            {
                Lookup = lookup,
                OutsideYourDepartments = outsideScope,
                SearchResults = searchResults,
                IsPublicScan = !canViewDetails,
                CanViewAssetDetails = lookup.Found && canViewDetails && !outsideScope,
                CanOpenQuickActions = lookup.Found && !outsideScope
                    && AssetCustodyRules.HasAnyQuickAction(
                        lookup.CurrentStatus,
                        canAssign,
                        canTransfer,
                        canReturn,
                        canReportIncident),
                InitialCode = term,
                LookupJsonUrl = TenantUrlHelper.TenantRouteUrl(Url, "LookupJson", "AssetScan"),
                BulkPrintUrl = canViewDetails
                    ? TenantUrlHelper.TenantRouteUrl(Url, "BulkPrint", "AssetLabels")
                    : null,
            };

            if (lookup.Found)
            {
                // Review fix S6: signed out, signed in without Assets.View, or outside scope
                // all get the public card; only Assets.View in scope sees the custodian, cost (Financials.View) and supplier.
                var viewer = AssetScanViewer.Resolve(
                    isAuthenticated,
                    canViewDetails,
                    outsideScope,
                    isAuthenticated && _authorizationService != null && _authorizationService.HasPermission(userId, "Financials.View"),
                    isAuthenticated && _authorizationService != null && _authorizationService.HasPermission(userId, "Suppliers.View"),
                    HideSerialFromPublic());
                pageModel.Card = AssetScanCardBuilder.Build(lookup, viewer);
                pageModel.StatusBadgeClass = StatusHtmlHelpers.ToBadgeClass(lookup.CurrentStatus);
                if (pageModel.Card != null && pageModel.Card.HasPhoto)
                {
                    pageModel.PhotoUrl = TenantUrlHelper.TenantRouteUrl(Url, "Photo", "AssetScan", new { code = lookup.AssetTag });
                }
            }

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

        /// <summary>
        /// Review S8 switch: Web.config appSetting "AssetScan:HideSerialFromPublic" = "true" hides the serial number from
        /// viewers without Assets.View. Missing or anything else = shown (current behaviour).
        /// </summary>
        public const string HideSerialFromPublicSetting = "AssetScan:HideSerialFromPublic";

        private static bool HideSerialFromPublic()
        {
            bool value;
            var raw = System.Configuration.ConfigurationManager.AppSettings[HideSerialFromPublicSetting];
            return bool.TryParse((raw ?? string.Empty).Trim(), out value) && value;
        }

        private AssetScanLookupVm TryUnscopedScanLookup(string term)
        {
            try
            {
                return _assetService.LookupByScanCode(term, applyDepartmentScope: false, includeDetails: true);
            }
            catch (BusinessException ex)
            {
                return new AssetScanLookupVm { Found = false, Message = ex.Message };
            }
        }

        private AssetScanLookupVm TryScanLookup(string term, bool canViewDetails)
        {
            try
            {
                // rel1027: details for every viewer (the QR scan page is public); AssetScanCardBuilder filters what is shown.
                // Department scope still applies to signed-in users with Assets.View, as before.
                return _assetService.LookupByScanCode(
                    term,
                    applyDepartmentScope: canViewDetails,
                    includeDetails: true);
            }
            catch (BusinessException ex)
            {
                return new AssetScanLookupVm { Found = false, Message = ex.Message };
            }
        }

        private object ToJsonPayload(AssetScanLookupPageVm page)
        {
            var lookup = page.Lookup;
            if (page.IsPublicScan || page.OutsideYourDepartments)
            {
                // rel1027: the viewer-filtered card only (no e-mail, cost, supplier or ids for signed-out viewers).
                return new
                {
                    Found = lookup.Found,
                    Message = lookup.Message,
                    Card = ToCardPayload(page.Card),
                    StatusBadgeClass = page.Card == null ? null : page.StatusBadgeClass,
                    PhotoUrl = page.PhotoUrl,
                    CanViewAssetDetails = false,
                    CanOpenQuickActions = false
                };
            }

            if (page.SearchResults != null && page.SearchResults.Assets != null && page.SearchResults.Assets.Any())
            {
                return new
                {
                    Found = false,
                    Message = (string)null,
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
                            DetailsUrl = TenantUrlHelper.TenantRouteUrl(Url, "Details", "Assets", new { id = hit.AssetId })
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
                DetailsUrl = page.DetailsUrl,
                QuickActionsUrl = page.QuickActionsUrl,
                EmptyDisplay = DisplayText.Empty,
                Card = ToCardPayload(page.Card),
                PhotoUrl = page.PhotoUrl
            };
        }

        private static object ToCardPayload(AssetScanCardVm card)
        {
            if (card == null)
            {
                return null;
            }

            return new
            {
                card.AssetName,
                card.AssetTag,
                card.StatusText,
                card.CustodianText,
                Rows = card.Rows.Select(r => new { r.Key, r.Label, r.Value }).ToArray(),
                RestrictedRows = card.RestrictedRows.Select(r => new { r.Key, r.Label, r.Value }).ToArray()
            };
        }

        /// <summary>
        /// rel1027: the asset photo shown on the scan card (same lookup and scope as the page; images only).
        /// 404 when there is no photo. Public like Lookup, and rate-limited the same way.
        /// </summary>
        [HttpGet]
        public ActionResult Photo(string code)
        {
            var term = ResolveLookupTerm(code, null);
            if (string.IsNullOrWhiteSpace(term))
            {
                return HttpNotFound();
            }

            if (!TryAcquireScanLookup())
            {
                return new HttpStatusCodeResult(429, ScanRateLimitMessage);
            }

            var canViewDetails = User.Identity.IsAuthenticated
                && _authorizationService != null
                && _authorizationService.HasPermission(User.GetUserId(), "Assets.View");
            var lookup = TryScanLookup(term, canViewDetails);
            var mayViewAnyPhotoType = canViewDetails;
            if ((lookup == null || !lookup.Found) && canViewDetails)
            {
                // Same as the page: outside the user's departments = the public card, so only the public photo type.
                lookup = TryUnscopedScanLookup(term);
                mayViewAnyPhotoType = false;
            }

            // Review fix S7: without Assets.View only "Asset photo..." documents are served (no serial-plate or location photos).
            if (lookup == null || !lookup.Found || string.IsNullOrWhiteSpace(lookup.PhotoFilePath)
                || !AssetScanPhotoRules.IsAllowedContentType(lookup.PhotoContentType)
                || !AssetScanPhotoRules.MayServe(lookup.PhotoDocumentType, mayViewAnyPhotoType))
            {
                return HttpNotFound();
            }

            var storage = DependencyResolver.Current.GetService<IFileStorageProvider>();
            System.IO.Stream content;
            try
            {
                content = storage == null ? null : storage.OpenRead(lookup.PhotoFilePath);
            }
            catch (Exception)
            {
                content = null;
            }

            if (content == null)
            {
                return HttpNotFound();
            }

            Response.Cache.SetCacheability(System.Web.HttpCacheability.Private);
            Response.Cache.SetMaxAge(TimeSpan.FromMinutes(10));
            Response.AddHeader("X-Content-Type-Options", "nosniff");
            return File(content, lookup.PhotoContentType);
        }

        /// <summary>Department filter options for the bulk label print page (AssetLabels/BulkPrint).</summary>
        public static System.Collections.Generic.IEnumerable<SelectListItem> BuildDepartmentOptions(IDepartmentService departmentService)
        {
            if (departmentService == null)
            {
                return new SelectListItem[0];
            }

            var departments = departmentService.GetAll()
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

        /// <summary>Status filter options for the bulk label print page (AssetLabels/BulkPrint).</summary>
        public static System.Collections.Generic.IEnumerable<SelectListItem> BuildStatusOptions()
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
