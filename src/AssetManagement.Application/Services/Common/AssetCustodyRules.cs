using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public static class AssetCustodyRules
    {
        public const string AlreadyAssignedMessage =
            "This asset is already assigned. Use Transfer to move custody to another user or department, or Return to check it back in first.";

        public static bool CanAssign(AssetStatus status)
        {
            return status != AssetStatus.Assigned;
        }
    }
}
