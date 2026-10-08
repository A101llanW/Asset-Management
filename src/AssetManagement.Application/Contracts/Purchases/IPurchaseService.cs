using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IPurchaseService
    {
        IEnumerable<PurchaseRecordVm> GetAll();

        PagedListVm<PurchaseRecordVm> GetListPage(
            string search,
            int? supplierId,
            string sort,
            string direction,
            int page,
            int pageSize);

        PurchaseRecordVm GetById(int id);

        int Create(PurchaseRecordVm model);

        /// <summary>
        /// Create with the acting user: when linked to a requisition whose flow has a "Purchase order raised by"
        /// configured, only that person / role (or Company Admin break-glass, audited) may create it.
        /// </summary>
        int Create(PurchaseRecordVm model, string actingUserId, int? actingRoleId, bool bypassesRaiserCheck);
    }
}
