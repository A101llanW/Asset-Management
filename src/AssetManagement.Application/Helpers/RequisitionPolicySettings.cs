using System;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Org-scoped requisition policies (SystemSetting rows). Missing or unrecognised values fall back to the
    /// backward-compatible defaults: ClassificationMode = Optional, MixedFlowPolicy = AutoSplit.
    /// </summary>
    public static class RequisitionPolicySettings
    {
        public const string ClassificationModeKey = "Purchases.Requisition.ClassificationMode";

        public const string MixedFlowPolicyKey = "Purchases.Requisition.MixedFlowPolicy";

        public static RequisitionClassificationMode GetClassificationMode(IUnitOfWork unitOfWork)
        {
            return ParseClassificationMode(GetValue(unitOfWork, ClassificationModeKey));
        }

        public static RequisitionMixedFlowPolicy GetMixedFlowPolicy(IUnitOfWork unitOfWork)
        {
            return ParseMixedFlowPolicy(GetValue(unitOfWork, MixedFlowPolicyKey));
        }

        public static RequisitionClassificationMode ParseClassificationMode(string value)
        {
            var text = (value ?? string.Empty).Trim();
            if (text.Equals("RequiredPerLine", StringComparison.OrdinalIgnoreCase))
            {
                return RequisitionClassificationMode.RequiredPerLine;
            }

            if (text.Equals("RequiredHeader", StringComparison.OrdinalIgnoreCase))
            {
                return RequisitionClassificationMode.RequiredHeader;
            }

            return RequisitionClassificationMode.Optional;
        }

        public static RequisitionMixedFlowPolicy ParseMixedFlowPolicy(string value)
        {
            return (value ?? string.Empty).Trim().Equals("Block", StringComparison.OrdinalIgnoreCase)
                ? RequisitionMixedFlowPolicy.Block
                : RequisitionMixedFlowPolicy.AutoSplit;
        }

        private static string GetValue(IUnitOfWork unitOfWork, string key)
        {
            if (unitOfWork == null)
            {
                return null;
            }

#pragma warning disable 618 // same settings read as ApprovalWorkflowHelper (tenant-filtered repository)
            var settings = ApprovalWorkflowSettingsHelper.ToDictionary(unitOfWork.Repository<SystemSetting>().GetAll());
#pragma warning restore 618
            return ApprovalWorkflowSettingsHelper.GetString(settings, key);
        }
    }
}
