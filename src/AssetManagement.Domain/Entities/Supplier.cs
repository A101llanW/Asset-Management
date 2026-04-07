using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    public class Supplier : AuditableEntity
    {
        public int Id { get; set; }

        public string SupplierName { get; set; }

        public string ContactPerson { get; set; }

        public string Email { get; set; }

        public string Phone { get; set; }

        public string Address { get; set; }

        public string RegistrationNumber { get; set; }

        public string Notes { get; set; }
    }
}
