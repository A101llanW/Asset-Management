using System;
using System.Collections.Generic;
using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    public class PurchaseRecord : AuditableEntity
    {
        public int Id { get; set; }

        public int? PurchaseRequestId { get; set; }

        public string PurchaseOrderNumber { get; set; }

        public int SupplierId { get; set; }

        public string InvoiceNumber { get; set; }

        public string ReceiptNumber { get; set; }

        public DateTime PurchaseDate { get; set; }

        public DateTime? DeliveryDate { get; set; }

        public DateTime? ReceivedDate { get; set; }

        public int Quantity { get; set; }

        public decimal UnitCost { get; set; }

        public decimal TotalCost { get; set; }

        public string Currency { get; set; }

        public decimal TaxAmount { get; set; }

        public string FundingSource { get; set; }

        public string BudgetCode { get; set; }

        public int UsefulLifeMonths { get; set; }

        public DateTime? WarrantyStartDate { get; set; }

        public DateTime? WarrantyEndDate { get; set; }

        public string Notes { get; set; }

        public byte[] RowVersion { get; set; }

        public virtual Supplier Supplier { get; set; }

        public virtual PurchaseRequest PurchaseRequest { get; set; }

        public virtual ICollection<AssetReceiving> Receivings { get; set; } = new HashSet<AssetReceiving>();
    }
}
