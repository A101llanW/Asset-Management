using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Purchases.View")]
    public class PurchaseRequestsController : BaseController
    {
        private readonly IPurchaseRequestService _purchaseRequestService;

        public PurchaseRequestsController()
        {
            _purchaseRequestService = BuildPurchaseRequestService();
        }

        public ActionResult Index()
        {
            return View(_purchaseRequestService.GetAll());
        }

        [PermissionAuthorize("Purchases.Create")]
        public ActionResult Create(string returnUrl = null)
        {
            var model = new PurchaseRequestCreateVm
            {
                Currency = GetDefaultCurrencyCode(),
                Quantity = 1
            };

            ApplyLockedUserDepartment(GetCurrentUserDepartmentId(), deptId =>
            {
                if (deptId.HasValue)
                {
                    model.DepartmentId = deptId.Value;
                }
            });

            PopulateDepartmentLookups(model);
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.PurchaseApprovalSummary = BuildApprovalProcessSummary(ApprovalProcessCodes.Purchase);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Purchases.Create")]
        public ActionResult Create(PurchaseRequestCreateVm model, string returnUrl = null)
        {
            ApplyLockedUserDepartment(GetCurrentUserDepartmentId(), deptId =>
            {
                if (deptId.HasValue)
                {
                    model.DepartmentId = deptId.Value;
                }
            });

            PopulateDepartmentLookups(model);
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.PurchaseApprovalSummary = BuildApprovalProcessSummary(ApprovalProcessCodes.Purchase);
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var id = _purchaseRequestService.Submit(model, User.GetUserId());
                TempData["Message"] = "Purchase request submitted.";
                return RedirectToAction("Details", new { id, returnUrl = ViewBag.ReturnUrl });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        public ActionResult Details(int id, string returnUrl = null)
        {
            var model = _purchaseRequestService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            var currentRoleId = GetCurrentUserRoleId();
            var isSuperAdmin = IsCurrentUserSuperAdmin();
            model.CanCurrentUserApprove = model.IsPending
                && (isSuperAdmin || (currentRoleId.HasValue && model.CurrentStageRoleId == currentRoleId.Value));

            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.PurchaseApprovalSummary = BuildApprovalProcessSummary(ApprovalProcessCodes.Purchase);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Purchases.Approve")]
        public ActionResult Approve(int id, string notes, string returnUrl = null)
        {
            try
            {
                _purchaseRequestService.Approve(new PurchaseRequestApprovalVm
                {
                    PurchaseRequestId = id,
                    Notes = notes
                }, User.GetUserId(), GetCurrentUserRoleId(), IsCurrentUserSuperAdmin());
                TempData["Message"] = "Purchase request approval recorded.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", new { id, returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Purchases.Approve")]
        public ActionResult Reject(int id, string notes, string returnUrl = null)
        {
            try
            {
                _purchaseRequestService.Reject(new PurchaseRequestApprovalVm
                {
                    PurchaseRequestId = id,
                    Notes = notes
                }, User.GetUserId(), GetCurrentUserRoleId(), IsCurrentUserSuperAdmin());
                TempData["Message"] = "Purchase request rejected.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", new { id, returnUrl });
        }

        private void PopulateDepartmentLookups(PurchaseRequestCreateVm model)
        {
            var lockDepartment = !IsCurrentUserSuperAdmin() && GetCurrentUserDepartmentId().HasValue;
            int? departmentId = model == null ? GetCurrentUserDepartmentId() : (int?)model.DepartmentId;
            ViewBag.LockDepartment = lockDepartment;
            ViewBag.DepartmentName = DepartmentUserWorkflowHelper.ResolveDepartmentDisplayName(
                departmentId,
                BuildDepartmentService().GetAll().Where(x => x.IsActive).ToList());
            ViewBag.Departments = BuildDepartmentSelectList(departmentId);
        }
    }
}
