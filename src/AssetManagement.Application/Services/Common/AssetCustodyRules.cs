using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public static class AssetCustodyRules
    {
        public const string AlreadyAssignedMessage =
            "This asset is already assigned. Use Transfer to move custody to another user or department, or Return to check it back in first.";

        public const string UnavailableForAssignmentMessage =
            "This asset cannot be assigned in its current status.";

        public static bool CanAssign(AssetStatus status)
        {
            return status == AssetStatus.InStore
                || status == AssetStatus.Returned
                || status == AssetStatus.Received;
        }

        public static bool CanTransfer(AssetStatus status)
        {
            return status == AssetStatus.Assigned;
        }

        public static bool BlocksCustodyChange(AssetStatus status)
        {
            return status == AssetStatus.Lost
                || status == AssetStatus.Stolen
                || status == AssetStatus.Damaged
                || status == AssetStatus.Disposed
                || status == AssetStatus.Retired;
        }

        public static string GetAssignBlockedMessage(AssetStatus status)
        {
            if (status == AssetStatus.Assigned)
            {
                return AlreadyAssignedMessage;
            }

            if (status == AssetStatus.UnderMaintenance)
            {
                return "This asset is under maintenance. Complete the open ticket before assigning it.";
            }

            if (status == AssetStatus.AwaitingApproval)
            {
                return "This asset has a pending approval. Resolve it before assigning custody.";
            }

            if (BlocksCustodyChange(status))
            {
                return UnavailableForAssignmentMessage;
            }

            return "This asset is not available for assignment in its current status.";
        }

        public static bool HasAnyQuickAction(
            AssetStatus status,
            bool canAssign,
            bool canTransfer,
            bool canReturn,
            bool canReportIncident)
        {
            return (canAssign && CanAssign(status))
                || (canTransfer && CanTransfer(status))
                || canReturn
                || canReportIncident;
        }
    }
}
