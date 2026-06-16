using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Assets.View")]
    public class CustodianController : BaseController
    {
        private readonly ICustodianService _custodianService;

        public CustodianController()
        {
            _custodianService = DependencyResolver.Current.GetService<ICustodianService>();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AcknowledgeReceipt(int assetId, string returnUrl)
        {
            try
            {
                _custodianService.AcknowledgeReceipt(assetId, User.GetUserId());
                TempData["Message"] = "Receipt acknowledged. Thank you.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToReturnUrl(returnUrl, "MyAssets", "Assets");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RequestReturn(int assetId, string notes, string returnUrl)
        {
            try
            {
                _custodianService.RequestReturn(assetId, User.GetUserId(), notes);
                TempData["Message"] = "Return request submitted. Asset operations will follow up.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToReturnUrl(returnUrl, "MyAssets", "Assets");
        }
    }
}
