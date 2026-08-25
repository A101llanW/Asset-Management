using System;

namespace AssetManagement.Domain.Entities
{
    public class UserInvitation
    {
        public int Id { get; set; }

        public string TokenHash { get; set; }

        public int OrganizationId { get; set; }

        public string InvitedByUserId { get; set; }

        public string Email { get; set; }

        public int? RoleId { get; set; }

        public int? DepartmentId { get; set; }

        public DateTime ExpiresAtUtc { get; set; }

        public DateTime? UsedAtUtc { get; set; }

        public string UsedByUserId { get; set; }

        public DateTime CreatedAtUtc { get; set; }
    }
}
