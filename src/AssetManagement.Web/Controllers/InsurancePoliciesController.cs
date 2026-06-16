using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers
{
    public class InsurancePoliciesController : BaseController
    {
        private readonly IInsurancePolicyService _insurancePolicyService;

        public InsurancePoliciesController()
        {
            _insurancePolicyService = DependencyResolver.Current.GetService<IInsurancePolicyService>();
        }

        [PermissionAuthorize("Insurance.Manage")]
        public ActionResult Create(int assetId)
        {
            return View(new InsurancePolicyEditVm
            {
                AssetId = assetId,
                PolicyStartDate = System.DateTime.UtcNow.Date,
                PolicyEndDate = System.DateTime.UtcNow.Date.AddYears(1),
                ClaimEligibility = true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Insurance.Manage")]
        public ActionResult Create(InsurancePolicyEditVm model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            _insurancePolicyService.Create(model);
            TempData["Message"] = "Insurance policy created.";
            return RedirectToAction("Details", "Assets", new { id = model.AssetId });
        }

        [PermissionAuthorize("Insurance.Manage")]
        public ActionResult Edit(int id)
        {
            var model = _insurancePolicyService.GetForEdit(id);
            return model == null ? (ActionResult)HttpNotFound() : View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Insurance.Manage")]
        public ActionResult Edit(InsurancePolicyEditVm model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            _insurancePolicyService.Update(model);
            TempData["Message"] = "Insurance policy updated.";
            return RedirectToAction("Details", "Assets", new { id = model.AssetId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Insurance.Manage")]
        public ActionResult Delete(int id, int assetId)
        {
            _insurancePolicyService.Delete(id);
            TempData["Message"] = "Insurance policy removed.";
            return RedirectToAction("Details", "Assets", new { id = assetId });
        }
    }
}
