using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public class AssetService : IAssetService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditWriter _auditWriter;

        public AssetService(IUnitOfWork unitOfWork, IAuditWriter auditWriter)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
        }

        public IEnumerable<AssetListVm> GetAssets(AssetFilterVm filter)
        {
            var assets = _unitOfWork.Repository<Asset>().GetAll();
            if (filter != null)
            {
                if (!string.IsNullOrWhiteSpace(filter.Search))
                {
                    var search = filter.Search.Trim().ToLowerInvariant();
                    assets = assets.Where(x => (x.AssetTag ?? string.Empty).ToLower().Contains(search)
                                            || (x.AssetName ?? string.Empty).ToLower().Contains(search)
                                            || (x.SerialNumber ?? string.Empty).ToLower().Contains(search));
                }

                if (filter.DepartmentId.HasValue)
                {
                    assets = assets.Where(x => x.DepartmentId == filter.DepartmentId.Value);
                }

                if (filter.CategoryId.HasValue)
                {
                    assets = assets.Where(x => x.CategoryId == filter.CategoryId.Value);
                }

                if (filter.Status.HasValue)
                {
                    assets = assets.Where(x => x.CurrentStatus == filter.Status.Value);
                }
            }

            var categoryLookup = _unitOfWork.Repository<AssetCategory>().GetAll().ToDictionary(x => x.Id, x => x.Name);
            var departmentLookup = _unitOfWork.Repository<Department>().GetAll().ToDictionary(x => x.Id, x => x.Name);

            return assets
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new AssetListVm
                {
                    Id = x.Id,
                    AssetTag = x.AssetTag,
                    AssetName = x.AssetName,
                    CategoryName = categoryLookup.ContainsKey(x.CategoryId) ? categoryLookup[x.CategoryId] : string.Empty,
                    DepartmentName = departmentLookup.ContainsKey(x.DepartmentId) ? departmentLookup[x.DepartmentId] : string.Empty,
                    CurrentCustodianId = x.CurrentCustodianId,
                    CurrentStatus = x.CurrentStatus,
                    CurrentBookValue = x.CurrentBookValue
                })
                .ToList();
        }

        public AssetDetailsVm GetById(int id)
        {
            var asset = _unitOfWork.Repository<Asset>().GetById(id);
            if (asset == null)
            {
                return null;
            }

            var timeline = _unitOfWork.Repository<AssetCustodyEvent>()
                .Find(x => x.AssetId == id)
                .OrderByDescending(x => x.ActionDate)
                .Select(x => new AssetCustodyTimelineVm
                {
                    ActionDate = x.ActionDate,
                    ActionType = x.ActionType.ToString(),
                    FromEntity = x.FromUserId ?? x.FromDepartmentId?.ToString(),
                    ToEntity = x.ToUserId ?? x.ToDepartmentId?.ToString(),
                    ConditionBefore = x.ConditionBefore,
                    ConditionAfter = x.ConditionAfter,
                    Reason = x.Reason,
                    ApprovedById = x.ApprovedById,
                    Notes = x.Notes
                })
                .ToList();

            return new AssetDetailsVm
            {
                Id = asset.Id,
                AssetTag = asset.AssetTag,
                AssetName = asset.AssetName,
                SerialNumber = asset.SerialNumber,
                Brand = asset.Brand,
                Model = asset.Model,
                DepartmentName = _unitOfWork.Repository<Department>().GetById(asset.DepartmentId)?.Name,
                CategoryName = _unitOfWork.Repository<AssetCategory>().GetById(asset.CategoryId)?.Name,
                SupplierName = _unitOfWork.Repository<Supplier>().GetById(asset.SupplierId)?.SupplierName,
                CurrentStatus = asset.CurrentStatus,
                AcquisitionCost = asset.AcquisitionCost,
                CurrentBookValue = asset.CurrentBookValue,
                AccumulatedDepreciation = asset.AccumulatedDepreciation,
                ReplacementValue = asset.ReplacementValue,
                PolicyReference = asset.PolicyReference,
                WarrantyEndDate = asset.WarrantyEndDate,
                CustodyHistory = timeline
            };
        }

        public int Create(AssetCreateVm model)
        {
            ValidateUniqueness(model.AssetTag, model.SerialNumber, null);

            var entity = new Asset
            {
                AssetName = model.AssetName,
                AssetTag = model.AssetTag,
                CategoryId = model.CategoryId,
                AssetTypeId = model.AssetTypeId,
                Brand = model.Brand,
                Model = model.Model,
                SerialNumber = model.SerialNumber,
                Description = model.Description,
                PurchaseDate = model.PurchaseDate,
                AcquisitionCost = model.AcquisitionCost,
                TaxAmount = model.TaxAmount,
                Currency = model.Currency,
                SupplierId = model.SupplierId,
                DepartmentId = model.DepartmentId,
                ConditionOnReceipt = model.ConditionOnReceipt,
                UsefulLifeMonths = model.UsefulLifeMonths > 0 ? model.UsefulLifeMonths : 36,
                SalvageValue = model.SalvageValue,
                DepreciationMethod = model.DepreciationMethod,
                DepreciationStartDate = model.DepreciationStartDate == default(DateTime) ? model.PurchaseDate : model.DepreciationStartDate,
                CurrentBookValue = model.AcquisitionCost,
                AccumulatedDepreciation = 0,
                ReplacementValue = model.ReplacementValue,
                IsInsured = model.IsInsured,
                InsuredValue = model.InsuredValue,
                WarrantyStartDate = model.WarrantyStartDate,
                WarrantyEndDate = model.WarrantyEndDate,
                CurrentStatus = model.CurrentStatus == 0 ? AssetStatus.InStore : model.CurrentStatus,
                Condition = AssetCondition.New,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _unitOfWork.Repository<Asset>().Add(entity);
            _unitOfWork.SaveChanges();
            _auditWriter.Write("Assets.Create", nameof(Asset), entity.Id.ToString(), null, entity.AssetTag);
            return entity.Id;
        }

        public void Update(AssetEditVm model)
        {
            ValidateUniqueness(model.AssetTag, model.SerialNumber, model.Id);

            var entity = _unitOfWork.Repository<Asset>().GetById(model.Id);
            if (entity == null)
            {
                throw new BusinessException("Asset not found.");
            }

            if (entity.CurrentStatus == AssetStatus.Disposed)
            {
                throw new BusinessException("Disposed assets cannot be modified.");
            }

            var oldSnapshot = entity.AssetName + "|" + entity.AssetTag + "|" + entity.CurrentStatus;

            entity.AssetName = model.AssetName;
            entity.AssetTag = model.AssetTag;
            entity.CategoryId = model.CategoryId;
            entity.AssetTypeId = model.AssetTypeId;
            entity.Brand = model.Brand;
            entity.Model = model.Model;
            entity.SerialNumber = model.SerialNumber;
            entity.Description = model.Description;
            entity.PurchaseDate = model.PurchaseDate;
            entity.AcquisitionCost = model.AcquisitionCost;
            entity.TaxAmount = model.TaxAmount;
            entity.Currency = model.Currency;
            entity.SupplierId = model.SupplierId;
            entity.DepartmentId = model.DepartmentId;
            entity.ConditionOnReceipt = model.ConditionOnReceipt;
            entity.UsefulLifeMonths = model.UsefulLifeMonths;
            entity.SalvageValue = model.SalvageValue;
            entity.DepreciationMethod = model.DepreciationMethod;
            entity.DepreciationStartDate = model.DepreciationStartDate;
            entity.ReplacementValue = model.ReplacementValue;
            entity.IsInsured = model.IsInsured;
            entity.InsuredValue = model.InsuredValue;
            entity.WarrantyStartDate = model.WarrantyStartDate;
            entity.WarrantyEndDate = model.WarrantyEndDate;
            entity.CurrentStatus = model.CurrentStatus;
            entity.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Repository<Asset>().Update(entity);
            _unitOfWork.SaveChanges();
            _auditWriter.Write("Assets.Edit", nameof(Asset), entity.Id.ToString(), oldSnapshot, entity.AssetName + "|" + entity.AssetTag + "|" + entity.CurrentStatus);
        }

        public void Delete(int id)
        {
            var entity = _unitOfWork.Repository<Asset>().GetById(id);
            if (entity == null)
            {
                return;
            }

            entity.IsActive = false;
            entity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Asset>().Update(entity);
            _unitOfWork.SaveChanges();
            _auditWriter.Write("Assets.Delete", nameof(Asset), entity.Id.ToString(), entity.AssetTag, "SoftDeleted");
        }

        public void RequestDisposal(AssetDisposalRequestVm model, string requestedByUserId)
        {
            EnsureDisposalRequestInput(model, requestedByUserId);
            var asset = _unitOfWork.Repository<Asset>().GetById(model.AssetId);
            if (asset == null)
            {
                throw new BusinessException("Asset not found.");
            }

            EnsureCanRequestDisposal(asset, model.AssetId);

            var disposal = new DisposalRecord
            {
                AssetId = model.AssetId,
                DisposalRequestDate = DateTime.UtcNow,
                DisposalReason = model.DisposalReason?.Trim(),
                DisposalMethod = model.DisposalMethod,
                ApprovalStatus = ApprovalStatus.Pending,
                Notes = model.Notes,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            var oldStatus = asset.CurrentStatus;
            asset.CurrentStatus = AssetStatus.AwaitingApproval;
            asset.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Repository<DisposalRecord>().Add(disposal);
            _unitOfWork.Repository<Asset>().Update(asset);
            _unitOfWork.SaveChanges();

            _auditWriter.Write(
                "Assets.RequestDisposal",
                nameof(Asset),
                asset.Id.ToString(),
                oldStatus.ToString(),
                AssetStatus.AwaitingApproval + "|" + model.DisposalMethod);
        }

        public void ApproveDisposal(AssetDisposalApprovalVm model, string approvedByUserId)
        {
            EnsureDisposalApprovalInput(model, approvedByUserId);
            var asset = _unitOfWork.Repository<Asset>().GetById(model.AssetId);
            if (asset == null)
            {
                throw new BusinessException("Asset not found.");
            }

            if (asset.CurrentStatus != AssetStatus.AwaitingApproval)
            {
                throw new BusinessException("Asset is not in awaiting approval state for disposal.");
            }

            var pendingRequest = GetPendingDisposalRequest(model.AssetId);

            pendingRequest.ApprovalStatus = ApprovalStatus.Approved;
            pendingRequest.DisposalApprovedById = approvedByUserId;
            pendingRequest.DisposalDate = model.DisposalDate ?? DateTime.UtcNow;
            pendingRequest.DisposalAmount = model.DisposalAmount;
            ApplyApprovalNotes(pendingRequest, model.Notes);
            pendingRequest.UpdatedAt = DateTime.UtcNow;

            var fromUserId = asset.CurrentCustodianId;
            var fromDepartmentId = asset.DepartmentId;
            var oldStatus = asset.CurrentStatus;

            asset.CurrentStatus = AssetStatus.Disposed;
            asset.CurrentCustodianId = null;
            asset.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Repository<DisposalRecord>().Update(pendingRequest);
            _unitOfWork.Repository<Asset>().Update(asset);
            AddDisposalCustodyEvent(asset, pendingRequest, approvedByUserId, fromUserId, fromDepartmentId);

            _unitOfWork.SaveChanges();

            _auditWriter.Write(
                "Assets.ApproveDisposal",
                nameof(Asset),
                asset.Id.ToString(),
                oldStatus.ToString(),
                "Disposed");
        }

        private static void ApplyApprovalNotes(DisposalRecord pendingRequest, string notes)
        {
            pendingRequest.Notes = string.IsNullOrWhiteSpace(notes)
                ? pendingRequest.Notes
                : notes.Trim();
        }

        private void AddDisposalCustodyEvent(Asset asset, DisposalRecord pendingRequest, string approvedByUserId, string fromUserId, int fromDepartmentId)
        {
            _unitOfWork.Repository<AssetCustodyEvent>().Add(new AssetCustodyEvent
            {
                AssetId = asset.Id,
                ActionType = CustodyActionType.Disposed,
                ActionDate = pendingRequest.DisposalDate.Value,
                FromUserId = fromUserId,
                FromDepartmentId = fromDepartmentId,
                Reason = pendingRequest.DisposalReason,
                ApprovedById = approvedByUserId,
                Notes = pendingRequest.Notes,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
        }

        private void EnsureDisposalRequestInput(AssetDisposalRequestVm model, string requestedByUserId)
        {
            if (model == null)
            {
                throw new BusinessException("Disposal request payload is required.");
            }

            if (string.IsNullOrWhiteSpace(requestedByUserId))
            {
                throw new BusinessException("Current user is required for disposal request.");
            }
        }

        private void EnsureDisposalApprovalInput(AssetDisposalApprovalVm model, string approvedByUserId)
        {
            if (model == null)
            {
                throw new BusinessException("Disposal approval payload is required.");
            }

            if (string.IsNullOrWhiteSpace(approvedByUserId))
            {
                throw new BusinessException("Approver user is required.");
            }
        }

        private void EnsureCanRequestDisposal(Asset asset, int assetId)
        {
            if (asset.CurrentStatus == AssetStatus.Disposed || asset.CurrentStatus == AssetStatus.Retired)
            {
                throw new BusinessException("Disposed or retired assets cannot be submitted for disposal.");
            }

            if (asset.CurrentStatus == AssetStatus.AwaitingApproval)
            {
                throw new BusinessException("A disposal request is already pending approval for this asset.");
            }

            if (asset.CurrentStatus == AssetStatus.Assigned)
            {
                throw new BusinessException("Return or transfer the assigned asset before requesting disposal.");
            }

            var hasPending = _unitOfWork.Repository<DisposalRecord>()
                .Find(x => x.AssetId == assetId && x.ApprovalStatus == ApprovalStatus.Pending && x.IsActive)
                .Any();
            if (hasPending)
            {
                throw new BusinessException("A pending disposal request already exists for this asset.");
            }
        }

        private DisposalRecord GetPendingDisposalRequest(int assetId)
        {
            var pendingRequest = _unitOfWork.Repository<DisposalRecord>()
                .Find(x => x.AssetId == assetId && x.ApprovalStatus == ApprovalStatus.Pending && x.IsActive)
                .OrderByDescending(x => x.DisposalRequestDate)
                .FirstOrDefault();
            if (pendingRequest == null)
            {
                throw new BusinessException("No pending disposal request found for this asset.");
            }

            return pendingRequest;
        }

        private void ValidateUniqueness(string assetTag, string serialNumber, int? ignoreId)
        {
            var assets = _unitOfWork.Repository<Asset>().GetAll();
            var duplicateTag = assets.Any(x => x.AssetTag == assetTag && (!ignoreId.HasValue || x.Id != ignoreId.Value));
            if (duplicateTag)
            {
                throw new BusinessException("AssetTag must be unique.");
            }

            if (!string.IsNullOrWhiteSpace(serialNumber))
            {
                var duplicateSerial = assets.Any(x => x.SerialNumber == serialNumber && (!ignoreId.HasValue || x.Id != ignoreId.Value));
                if (duplicateSerial)
                {
                    throw new BusinessException("SerialNumber must be unique when provided.");
                }
            }
        }
    }
}
