using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using AssetManagement.Domain.Entities;
using AssetManagement.Web.Filters;
using Microsoft.AspNet.Identity;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Assets.View")]
    public class AssetsController : BaseController
    {
        private readonly IAssetService _assetService;

        public AssetsController()
        {
            _assetService = BuildAssetService();
        }

        public ActionResult Index(AssetFilterVm filter)
        {
            var model = _assetService.GetAssets(filter);
            return View(model);
        }

        [PermissionAuthorize("Assets.View")]
        public ActionResult Details(int id)
        {
            var model = _assetService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            return View(model);
        }

        [PermissionAuthorize("Assets.Create")]
        public ActionResult Create()
        {
            var model = new AssetCreateVm
            {
                Currency = "USD",
                UsefulLifeMonths = 36,
                CurrentStatus = AssetManagement.Domain.Enums.AssetStatus.InStore,
                DepreciationMethod = AssetManagement.Domain.Enums.DepreciationMethod.StraightLine
            };

            PopulateLookups(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Create")]
        public ActionResult Create(AssetCreateVm model)
        {
            PopulateLookups(model);
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var assetId = _assetService.Create(model);
                TempData["Message"] = "Asset created successfully.";
                TempData["Guidance"] = "Next step: review the asset details, then assign it, transfer it, or add maintenance and insurance information.";
                return RedirectToAction("Details", new { id = assetId });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        [PermissionAuthorize("Assets.Edit")]
        public ActionResult Edit(int id)
        {
            var item = _assetService.GetById(id);
            var entity = UnitOfWork.Repository<Asset>().GetById(id);
            if (item == null)
            {
                return HttpNotFound();
            }

            var model = new AssetEditVm
            {
                Id = item.Id,
                AssetName = item.AssetName,
                AssetTag = item.AssetTag,
                SerialNumber = item.SerialNumber,
                Brand = item.Brand,
                Model = item.Model,
                CategoryId = entity.CategoryId,
                AssetTypeId = entity.AssetTypeId,
                DepartmentId = entity.DepartmentId,
                SupplierId = entity.SupplierId,
                PurchaseDate = entity.PurchaseDate,
                AcquisitionCost = item.AcquisitionCost,
                Currency = entity.Currency,
                CurrentStatus = item.CurrentStatus,
                UsefulLifeMonths = entity.UsefulLifeMonths,
                SalvageValue = entity.SalvageValue,
                TaxAmount = entity.TaxAmount,
                ConditionOnReceipt = entity.ConditionOnReceipt,
                DepreciationMethod = entity.DepreciationMethod,
                DepreciationStartDate = entity.DepreciationStartDate,
                ReplacementValue = entity.ReplacementValue,
                IsInsured = entity.IsInsured,
                InsuredValue = entity.InsuredValue,
                WarrantyStartDate = entity.WarrantyStartDate,
                WarrantyEndDate = entity.WarrantyEndDate,
                Description = entity.Description
            };

            PopulateLookups(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Edit")]
        public ActionResult Edit(AssetEditVm model)
        {
            PopulateLookups(model);
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                _assetService.Update(model);
                TempData["Message"] = "Asset updated successfully.";
                return RedirectToAction("Details", new { id = model.Id });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Delete")]
        public ActionResult Delete(int id)
        {
            _assetService.Delete(id);
            TempData["Message"] = "Asset archived.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Dispose")]
        public ActionResult RequestDisposal(int id, string disposalReason, DisposalMethod disposalMethod, string notes)
        {
            try
            {
                _assetService.RequestDisposal(new AssetDisposalRequestVm
                {
                    AssetId = id,
                    DisposalReason = disposalReason,
                    DisposalMethod = disposalMethod,
                    Notes = notes
                }, User.Identity.GetUserId());
                TempData["Message"] = "Disposal request submitted.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.ApproveDisposal")]
        public ActionResult ApproveDisposal(int id, decimal? disposalAmount, string notes)
        {
            try
            {
                _assetService.ApproveDisposal(new AssetDisposalApprovalVm
                {
                    AssetId = id,
                    DisposalAmount = disposalAmount,
                    Notes = notes
                }, User.Identity.GetUserId());
                TempData["Message"] = "Disposal approved and asset marked as disposed.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", new { id });
        }

        private void PopulateLookups(AssetCreateVm model)
        {
            var categories = UnitOfWork.Repository<AssetCategory>().GetAll().OrderBy(x => x.Name).ToList();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", model?.CategoryId);

            var assetTypes = UnitOfWork.Repository<AssetType>().GetAll()
                .OrderBy(x => x.Name)
                .ToList();
            ViewBag.AssetTypeOptions = assetTypes;

            var departments = UnitOfWork.Repository<Department>().GetAll().OrderBy(x => x.Name).ToList();
            ViewBag.Departments = new SelectList(departments, "Id", "Name", model?.DepartmentId);

            var suppliers = UnitOfWork.Repository<Supplier>().GetAll().OrderBy(x => x.SupplierName).ToList();
            ViewBag.Suppliers = new SelectList(suppliers, "Id", "SupplierName", model?.SupplierId);
        }
    }
}
