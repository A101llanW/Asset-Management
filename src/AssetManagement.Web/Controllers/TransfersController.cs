using System;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Assets.Transfer")]
    public class TransfersController : BaseController
    {
        private readonly ITransferService _transferService;
        private readonly IUserService _userService;
        private readonly IDepartmentService _departmentService;

        public TransfersController()
        {
            _transferService = BuildTransferService();
            _userService = BuildUserService();
            _departmentService = BuildDepartmentService();
        }

        public ActionResult Create(int assetId)
        {
            var asset = UnitOfWork.Repository<Asset>().GetById(assetId);
            var model = new AssetTransferVm
            {
                AssetId = assetId,
                FromUserId = asset?.CurrentCustodianId,
                FromDepartmentId = asset != null ? (int?)asset.DepartmentId : null
            };

            PopulateLookups(model);
            ViewBag.AssetContext = BuildAssetWorkflowContext(assetId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(AssetTransferVm model)
        {
            PopulateLookups(model);
            ViewBag.AssetContext = BuildAssetWorkflowContext(model.AssetId);
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                _transferService.Transfer(model);
                TempData["Message"] = "Transfer recorded.";
                return RedirectToAction("Details", "Assets", new { id = model.AssetId });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        private void PopulateLookups(AssetTransferVm model)
        {
            var users = _userService.GetAll()
                .Where(x => x.IsActive)
                .Select(x => new
                {
                    x.Id,
                    Name = BuildUserLabel(x)
                })
                .OrderBy(x => x.Name)
                .ToList();
            ViewBag.Users = new SelectList(users, "Id", "Name");

            var departments = _departmentService.GetAll()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ToList();
            ViewBag.Departments = new SelectList(departments, "Id", "Name");
        }

        private static string BuildUserLabel(UserVm user)
        {
            var name = (user.FirstName + " " + user.LastName).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return user.Email ?? user.Id;
            }

            return string.IsNullOrWhiteSpace(user.Email) ? name : name + " (" + user.Email + ")";
        }
    }
}
