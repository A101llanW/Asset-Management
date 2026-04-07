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
    [PermissionAuthorize("Assets.Return")]
    public class ReturnsController : BaseController
    {
        private readonly IReturnService _returnService;
        private readonly IUserService _userService;

        public ReturnsController()
        {
            _returnService = BuildReturnService();
            _userService = BuildUserService();
        }

        public ActionResult Create(int assetId)
        {
            var asset = UnitOfWork.Repository<Asset>().GetById(assetId);
            var model = new AssetReturnVm
            {
                AssetId = assetId,
                ReturnedById = asset?.CurrentCustodianId,
                ReturnDate = DateTime.UtcNow
            };

            PopulateLookups(model);
            ViewBag.AssetContext = BuildAssetWorkflowContext(assetId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(AssetReturnVm model)
        {
            PopulateLookups(model);
            ViewBag.AssetContext = BuildAssetWorkflowContext(model.AssetId);
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                _returnService.ReturnAsset(model);
                TempData["Message"] = "Return logged.";
                return RedirectToAction("Details", "Assets", new { id = model.AssetId });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        private void PopulateLookups(AssetReturnVm model)
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
