using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    public class Supplier : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public string SupplierName { get; set; }

        public string ContactPerson { get; set; }

        public string Email { get; set; }

        public string Phone { get; set; }

        public string Address { get; set; }

        public string RegistrationNumber { get; set; }

        public string TaxId { get; set; }

        public string PaymentTerms { get; set; }

        public int? DefaultLeadTimeDays { get; set; }

        public string Website { get; set; }

        public bool IsPreferred { get; set; }

        public string Country { get; set; }

        public string PaymentInstructions { get; set; }

        public string Notes { get; set; }
    }
}
