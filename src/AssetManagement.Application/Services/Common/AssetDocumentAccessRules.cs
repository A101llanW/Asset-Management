using System;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Services
{
    public static class AssetDocumentAccessRules
    {
        public static bool IsCurrentCustodian(Asset asset, string userId)
        {
            return asset != null
                && IsCurrentCustodian(asset.CurrentCustodianId, userId);
        }

        public static bool IsCurrentCustodian(string assetCustodianId, string userId)
        {
            return !string.IsNullOrWhiteSpace(userId)
                && !string.IsNullOrWhiteSpace(assetCustodianId)
                && string.Equals(assetCustodianId, userId, StringComparison.OrdinalIgnoreCase);
        }
    }
}
