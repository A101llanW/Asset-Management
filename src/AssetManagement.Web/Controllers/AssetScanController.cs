using System.Web.Mvc;
using AssetManagement.Application;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Controllers
{
    public class AssetScanController : Controller
    {
        private readonly IAssetService _assetService;
        private readonly IAuthorizationService _authorizationService;

        public AssetScanController()
        {
            _assetService = DependencyResolver.Current.GetService<IAssetService>();
            _authorizationService = DependencyResolver.Current.GetService<IAuthorizationService>();
        }

        public ActionResult Lookup(string code)
        {
            var pageModel = BuildPageModel(code);
            return View(pageModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LookupPost(string code)
        {
            return RedirectToAction("Lookup", new { code });
        }

        [HttpGet]
        public JsonResult LookupJson(string code)
        {
            if (!ScanLookupRateLimiter.TryAcquire(HttpContext))
            {
                Response.StatusCode = 429;
                return Json(new { Found = false, Message = "Too many scan requests. Please wait and try again." }, JsonRequestBehavior.AllowGet);
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
            var model = new AssetQuickActionsVm
            {
                AssetId = asset.Id,
                AssetTag = asset.AssetTag,
                AssetName = asset.AssetName,
                CurrentStatus = asset.CurrentStatus,
                DepartmentName = asset.DepartmentName,
                CanAssign = _authorizationService.HasPermission(userId, "Assets.Assign")
                    && AssetCustodyRules.CanAssign(asset.CurrentStatus),
                CanTransfer = _authorizationService.HasPermission(userId, "Assets.Transfer"),
                CanReturn = _authorizationService.HasPermission(userId, "Assets.Return"),
                CanReportIncident = _authorizationService.HasPermission(userId, "Incidents.Create")
            };

            return View(model);
        }

        private AssetScanLookupPageVm BuildPageModel(string code)
        {
            if (!ScanLookupRateLimiter.TryAcquire(HttpContext))
            {
                return new AssetScanLookupPageVm
                {
                    Lookup = new AssetScanLookupVm
                    {
                        Found = false,
                        Message = "Too many scan requests. Please wait and try again."
                    },
                    IsPublicScan = !User.Identity.IsAuthenticated,
                    CanManageAsset = false,
                    InitialCode = code,
                    LookupJsonUrl = Url.Action("LookupJson", "AssetScan")
                };
            }

            var isAuthenticated = User.Identity.IsAuthenticated;
            var canViewDetails = isAuthenticated
                && _authorizationService != null
                && _authorizationService.HasPermission(User.GetUserId(), "Assets.View");

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
                CanManageAsset = canViewDetails,
                InitialCode = code,
                LookupJsonUrl = Url.Action("LookupJson", "AssetScan")
            };

            if (lookup.Found && canViewDetails)
            {
                pageModel.StatusBadgeClass = StatusHtmlHelpers.ToBadgeClass(lookup.CurrentStatus);
                pageModel.BrandModelDisplay = BuildBrandModelDisplay(lookup);
                pageModel.DetailsUrl = Url.RouteUrl("Default", new { controller = "Assets", action = "Details", id = lookup.AssetId });
                pageModel.QuickActionsUrl = Url.Action("QuickActions", "AssetScan", new { id = lookup.AssetId });
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
                CanManageAsset = page.CanManageAsset,
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
