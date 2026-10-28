using System.Linq;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Tests.Helpers
{
    /// <summary>
    /// Migration 108: every untagged requisition line now needs an item / asset name and a type + sub-type.
    /// Tests written before 108 exercise routing, splitting and notifications, not these rules, so they submit
    /// through this helper: a blank name becomes the description, a line with a type but no sub-type gets that
    /// type's first active sub-type (one is seeded when the type has none), and an unclassified line gets a
    /// "General" category / type / sub-type that no catalog flow matches (so it keeps the department path, as an
    /// unclassified line did before). Header-classified forms (RequiredHeader) only get the header sub-type.
    /// Tagged lines are left alone. The 108 rules themselves are covered in RequisitionLineAssetNameTests.
    /// </summary>
    internal static class RequisitionLineTestData
    {
        public const int GeneralCategoryId = 9001;
        public const int GeneralTypeId = 9001;
        public const int GeneralSubTypeId = 9001;

        public static int SubmitCompleted(this PurchaseRequestService service, FakeUnitOfWork uow, PurchaseRequestCreateVm form, string requesterId)
        {
            return service.Submit(Complete(uow, form), requesterId);
        }

        public static PurchaseRequestSubmitResult SubmitBatchCompleted(this PurchaseRequestService service, FakeUnitOfWork uow, PurchaseRequestCreateVm form, string requesterId)
        {
            return service.SubmitBatch(Complete(uow, form), requesterId);
        }

        public static PurchaseRequestCreateVm Complete(FakeUnitOfWork uow, PurchaseRequestCreateVm form)
        {
            var headerTagged = form.TargetAssetId.HasValue && form.TargetAssetId.Value > 0;
            var headerClassified = (form.HeaderAssetTypeId.HasValue && form.HeaderAssetTypeId.Value > 0)
                || (form.HeaderAssetSubTypeId.HasValue && form.HeaderAssetSubTypeId.Value > 0);
            if (headerClassified && !(form.HeaderAssetSubTypeId.HasValue && form.HeaderAssetSubTypeId.Value > 0))
            {
                form.HeaderAssetSubTypeId = EnsureSubType(uow, form.HeaderAssetTypeId.Value);
            }

            var lines = form.Lines == null ? null : form.Lines.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Description)).ToList();
            if (lines == null || lines.Count == 0)
            {
                // Legacy single-item post: the header fields are the one line.
                if (string.IsNullOrWhiteSpace(form.ItemName))
                {
                    form.ItemName = form.ItemDescription;
                }

                if (!headerTagged && !headerClassified)
                {
                    EnsureGeneral(uow);
                    form.HeaderAssetTypeId = GeneralTypeId;
                    form.HeaderAssetSubTypeId = GeneralSubTypeId;
                }

                return form;
            }

            var anyLineTagged = lines.Any(x => x.TargetAssetId.HasValue && x.TargetAssetId.Value > 0);
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line.ItemName))
                {
                    line.ItemName = line.Description;
                }

                var tagged = (line.TargetAssetId.HasValue && line.TargetAssetId.Value > 0) || (i == 0 && headerTagged && !anyLineTagged);
                if (tagged || (line.AssetSubTypeId.HasValue && line.AssetSubTypeId.Value > 0))
                {
                    continue;
                }

                if (line.AssetTypeId.HasValue && line.AssetTypeId.Value > 0)
                {
                    line.AssetSubTypeId = EnsureSubType(uow, line.AssetTypeId.Value);
                }
                else if (!headerClassified)
                {
                    EnsureGeneral(uow);
                    line.AssetCategoryId = null;
                    line.AssetTypeId = GeneralTypeId;
                    line.AssetSubTypeId = GeneralSubTypeId;
                }
            }

            return form;
        }

        private static void EnsureGeneral(FakeUnitOfWork uow)
        {
            if (uow.Repository<AssetType>().GetById(GeneralTypeId) != null)
            {
                return;
            }

            uow.Seed(new AssetCategory { Id = GeneralCategoryId, Name = "General", IsActive = true });
            uow.Seed(new AssetType { Id = GeneralTypeId, AssetCategoryId = GeneralCategoryId, Name = "General items", IsActive = true });
            uow.Seed(new AssetSubType { Id = GeneralSubTypeId, AssetTypeId = GeneralTypeId, Name = "Standard", IsActive = true });
        }

        private static int? EnsureSubType(FakeUnitOfWork uow, int typeId)
        {
            var existing = uow.Repository<AssetSubType>().GetAll().FirstOrDefault(x => x.AssetTypeId == typeId && x.IsActive);
            if (existing != null)
            {
                return existing.Id;
            }

            var type = uow.Repository<AssetType>().GetById(typeId);
            if (type == null)
            {
                return null; // e.g. a forged type: let the service reject it as before
            }

            var subType = new AssetSubType { Id = 9100 + typeId, OrganizationId = type.OrganizationId, AssetTypeId = typeId, Name = type.Name + " (standard)", IsActive = true };
            uow.Seed(subType);
            return subType.Id;
        }
    }
}
