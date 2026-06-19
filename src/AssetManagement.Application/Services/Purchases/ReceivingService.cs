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

    public class ReceivingService : IReceivingService

    {

        private readonly IUnitOfWork _unitOfWork;



        public ReceivingService(IUnitOfWork unitOfWork)

        {

            _unitOfWork = unitOfWork;

        }



        public PurchaseReceiveDetailVm GetReceiveDetail(int purchaseRecordId)

        {

            var purchase = _unitOfWork.Repository<PurchaseRecord>().GetById(purchaseRecordId);

            if (purchase == null)

            {

                return null;

            }



            var supplierName = _unitOfWork.Repository<Supplier>().GetById(purchase.SupplierId)?.SupplierName;

            var receivings = GetReceivingsForPurchase(purchaseRecordId).ToList();

            var quantityReceived = receivings.Sum(x => x.QuantityReceived);



            return new PurchaseReceiveDetailVm

            {

                PurchaseRecordId = purchase.Id,

                PurchaseOrderNumber = purchase.PurchaseOrderNumber,

                SupplierName = supplierName,

                PurchaseQuantity = purchase.Quantity,

                QuantityReceived = quantityReceived,

                RemainingQuantity = Math.Max(0, purchase.Quantity - quantityReceived),

                Receivings = receivings

            };

        }



        public IEnumerable<AssetReceivingListVm> GetReceivingsForPurchase(int purchaseRecordId)

        {

            var receivings = _unitOfWork.Repository<AssetReceiving>()

                .Find(x => x.PurchaseRecordId == purchaseRecordId && x.IsActive)

                .OrderByDescending(x => x.ReceivedDate)

                .ToList();



            if (!receivings.Any())

            {

                return Enumerable.Empty<AssetReceivingListVm>();

            }



            var assetIds = receivings.Select(x => x.AssetId).Distinct().ToList();

            var assets = _unitOfWork.Repository<Asset>().GetAll()

                .Where(x => assetIds.Contains(x.Id))

                .ToDictionary(x => x.Id, x => x);



            return receivings.Select(x =>

            {

                Asset asset;

                assets.TryGetValue(x.AssetId, out asset);

                return new AssetReceivingListVm

                {

                    Id = x.Id,

                    AssetId = x.AssetId,

                    AssetTag = asset?.AssetTag,

                    AssetName = asset?.AssetName,

                    ReceivedDate = x.ReceivedDate,

                    QuantityReceived = x.QuantityReceived,

                    ConditionOnReceipt = x.ConditionOnReceipt,

                    ReceivedById = x.ReceivedById,

                    Notes = x.Notes

                };

            }).ToList();

        }



        public int Receive(AssetReceiveVm model, string receivedById)

        {

            if (model == null)

            {

                throw new BusinessException("Receive details are required.");

            }



            var purchase = _unitOfWork.Repository<PurchaseRecord>().GetById(model.PurchaseRecordId);

            if (purchase == null)

            {

                throw new BusinessException("Purchase record not found.");

            }



            if (model.QuantityReceived < 1)

            {

                throw new BusinessException("Quantity received must be at least 1.");

            }



            var asset = _unitOfWork.Repository<Asset>().GetById(model.AssetId);

            if (asset == null || !asset.IsActive)

            {

                throw new BusinessException("Asset not found.");

            }



            if (asset.CurrentStatus != AssetStatus.InStore && asset.CurrentStatus != AssetStatus.Returned)

            {

                throw new BusinessException("Only in-store or returned assets can be received against a purchase.");

            }



            var alreadyLinked = _unitOfWork.Repository<AssetReceiving>()

                .Find(x => x.PurchaseRecordId == purchase.Id && x.AssetId == asset.Id && x.IsActive)

                .Any();

            if (alreadyLinked)

            {

                throw new BusinessException("This asset is already linked to the purchase receiving record.");

            }



            var receivingId = 0;

            _unitOfWork.ExecuteInTransaction(() =>

            {

                var remaining = _unitOfWork.GetRemainingPurchaseQuantity(purchase.Id);

                if (remaining <= 0)

                {

                    throw new BusinessException("This purchase is already fully received.");

                }



                if (model.QuantityReceived > remaining)

                {

                    throw new BusinessException("Quantity received must be between 1 and the remaining purchase quantity (" + remaining + ").");

                }



                var now = DateTime.UtcNow;

                var receiving = new AssetReceiving

                {

                    PurchaseRecordId = purchase.Id,

                    AssetId = asset.Id,

                    ReceivedDate = model.ReceivedDate,

                    ConditionOnReceipt = model.ConditionOnReceipt,

                    QuantityReceived = model.QuantityReceived,

                    ReceivedById = receivedById,

                    Notes = model.Notes,

                    CreatedAt = now,

                    IsActive = true

                };



                if (!string.IsNullOrWhiteSpace(model.ConditionOnReceipt))

                {

                    asset.ConditionOnReceipt = model.ConditionOnReceipt;

                }



                if (purchase.SupplierId > 0 && (!asset.SupplierId.HasValue || asset.SupplierId.Value <= 0))

                {

                    asset.SupplierId = purchase.SupplierId;

                }



                asset.UpdatedAt = now;

                _unitOfWork.Repository<Asset>().Update(asset);

                _unitOfWork.Repository<AssetReceiving>().Add(receiving);

                _unitOfWork.SaveChanges();

                receivingId = receiving.Id;

            });



            return receivingId;

        }

    }

}


