using System;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Services
{
    public class SegregationOfDutiesService : ISegregationOfDutiesService
    {
        public void EnsureActorIsNotRequester(string requesterUserId, string actorUserId, string processCode)
        {
            if (!WouldViolateSegregation(requesterUserId, actorUserId))
            {
                return;
            }

            throw new BusinessException(GetViolationMessage(processCode));
        }

        public bool WouldViolateSegregation(string requesterUserId, string actorUserId)
        {
            var requester = NormalizeUserId(requesterUserId);
            var actor = NormalizeUserId(actorUserId);
            return !string.IsNullOrWhiteSpace(requester)
                && !string.IsNullOrWhiteSpace(actor)
                && string.Equals(requester, actor, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeUserId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string GetViolationMessage(string processCode)
        {
            switch (processCode)
            {
                case ApprovalProcessCodes.Transfer:
                    return "The requester cannot approve their own transfer request.";
                case ApprovalProcessCodes.Disposal:
                    return "The requester cannot approve their own disposal request.";
                case ApprovalProcessCodes.Purchase:
                    return "The requester cannot approve their own purchase request.";
                case ApprovalProcessCodes.AssetRequest:
                    return "You cannot approve or fulfill your own asset request.";
                default:
                    return "The requester cannot approve their own request.";
            }
        }
    }
}
