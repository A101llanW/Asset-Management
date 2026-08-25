using AssetManagement.Domain.Enums;

namespace AssetManagement.Web.Helpers
{
    public static class StatusHtmlHelpers
    {
        public static string ToBadgeClass(AssetStatus status)
        {
            switch (status)
            {
                case AssetStatus.Assigned:
                    return "success";
                case AssetStatus.UnderMaintenance:
                case AssetStatus.AwaitingApproval:
                    return "warning";
                case AssetStatus.Lost:
                case AssetStatus.Stolen:
                case AssetStatus.Disposed:
                    return "danger";
                default:
                    return "secondary";
            }
        }

        public static string ToBadgeClass(bool isActive)
        {
            return isActive ? "success" : "secondary";
        }
    }
}
