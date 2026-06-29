using System.Web.Mvc;
using AssetManagement.Application;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
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

        private readonly IAssetService _assetService;
        private readonly IAuthorizationService _authorizationService;

        public AssetScanController()
        {
            _assetService = DependencyResolver.Current.GetService<IAssetService>();
            _authorizationService = DependencyResolver.Current.GetService<IAuthorizationService>();
        }

        public ActionResult Lookup(string code)
        {
            if (!TryAcquireScanLookup())
            {
                return View(CreateRateLimitedPageModel(code));
            }

            return View(BuildPageModel(code));
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
        public JsonResult LookupJson(string code)
        {
            if (!TryAcquireScanLookup())
            {
                return Json(new { Found = false, Message = ScanRateLimitMessage }, JsonRequestBehavior.AllowGet);
            }

            var pageModel = BuildPageModel(code);
            return Json(ToJsonPayload(pageModel), JsonRequestBehavior.AllowGet);
        }

        [Authorize]
        [TenantAuthorize]
        [PermissionAuthorize("Assets.View")]
        public ActionResult QuickActions(int id)
        {
            var asset = _assetService.GetById(id);
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

        private bool TryAcquireScanLookup()
        {
            if (ScanLookupRateLimiter.TryAcquire(HttpContext))
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

        private AssetScanLookupPageVm BuildPageModel(string code)
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
            if (string.IsNullOrWhiteSpace(code))
            {
                lookup = new AssetScanLookupVm
                {
                    Found = false,
                    Message = "Enter or scan an asset tag, barcode, or serial number."
                };
            }
            else
            {
                try
                {
                    lookup = _assetService.LookupByScanCode(
                        code,
                        applyDepartmentScope: canViewDetails,
                        includeDetails: canViewDetails);
                }
                catch (BusinessException ex)
                {
                    lookup = new AssetScanLookupVm { Found = false, Message = ex.Message };
                }
            }

            var pageModel = new AssetScanLookupPageVm
            {
                Lookup = lookup,
                IsPublicScan = !canViewDetails,
                CanViewAssetDetails = lookup.Found && canViewDetails,
                CanOpenQuickActions = lookup.Found
                    && AssetCustodyRules.HasAnyQuickAction(
                        lookup.CurrentStatus,
                        canAssign,
                        canTransfer,
                        canReturn,
                        canReportIncident),
                InitialCode = code,
                LookupJsonUrl = TenantUrlHelper.TenantRouteUrl(Url, "LookupJson", "AssetScan")
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

        private static object ToJsonPayload(AssetScanLookupPageVm page)
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
                EmptyDisplay = DisplayText.Empty
            };
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
