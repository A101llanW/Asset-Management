using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Services
{
    /// <summary>
    /// "Purchase order raised by" enforcement. Configuration comes from the requisition flow resolver
    /// (the unit the flow is effectively taken from). Unconfigured = legacy behaviour (anyone who already has
    /// the Purchases permission). Configured person = only that person; role only = any active role holder.
    /// There is no Company Admin exemption ("only the configured role/person"); admins can change the setting.
    /// </summary>
    public static class PurchaseOrderRaiserRules
    {
        public static bool IsConfigured(ApprovalProcessConfiguration config)
        {
            return config != null && config.HasPurchaseOrderRaiser;
        }

        public static bool IsConfiguredRaiser(ApprovalProcessConfiguration config, string userId, int? roleId, StageApproverScope raiserScope = null)
        {
            if (!IsConfigured(config))
            {
                return true;
            }

            var actor = string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();
            if (actor == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(config.PurchaseOrderRaiserUserId))
            {
                return string.Equals(actor, config.PurchaseOrderRaiserUserId.Trim(), StringComparison.OrdinalIgnoreCase);
            }

            return roleId.HasValue && roleId.Value == config.PurchaseOrderRaiserRoleId.Value
                && ApprovalWorkflowHelper.IsInApproverScope(raiserScope, actor);
        }

        public static bool CanRaise(ApprovalProcessConfiguration config, string userId, int? roleId, StageApproverScope raiserScope = null)
        {
            return !IsConfigured(config) || IsConfiguredRaiser(config, userId, roleId, raiserScope);
        }

        public static void EnsureCanRaise(
            ApprovalProcessConfiguration config,
            string userId,
            int? roleId,
            string raiserLabel,
            StageApproverScope raiserScope = null)
        {
            if (CanRaise(config, userId, roleId, raiserScope))
            {
                return;
            }

            throw new BusinessException(
                "Only " + (string.IsNullOrWhiteSpace(raiserLabel) ? "the configured purchase order raiser" : raiserLabel)
                + " can raise the purchase order for this requisition.");
        }

        /// <summary>Users who get the "ready for purchase order" notification when configured.</summary>
        public static IList<string> ResolveNotifyUserIds(ApprovalProcessConfiguration config, IEnumerable<ApproverUserRecord> organizationUsers)
        {
            if (!IsConfigured(config))
            {
                return new List<string>();
            }

            if (!string.IsNullOrWhiteSpace(config.PurchaseOrderRaiserUserId))
            {
                return new List<string> { config.PurchaseOrderRaiserUserId.Trim() };
            }

            return (organizationUsers ?? Enumerable.Empty<ApproverUserRecord>())
                .Where(x => x != null && x.IsActive && x.RoleId == config.PurchaseOrderRaiserRoleId && !string.IsNullOrWhiteSpace(x.Id))
                .Select(x => x.Id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// rel1009e: the PO raisers for a requisition via the shared StageApproverScopeResolver (named person; else
        /// holders of the raiser role in the requisition's department, nearest ancestor, no-department holders,
        /// then org-wide). Null when no raiser is configured (legacy: anyone with the Purchases permission).
        /// </summary>
        public static StageApproverScope ResolveRaiserScope(
            IUnitOfWork unitOfWork,
            IUserService userService,
            ApprovalProcessConfiguration config,
            int? requestDepartmentId)
        {
            if (!IsConfigured(config) || userService == null)
            {
                return null;
            }

            return StageApproverScopeResolver.ResolveFor(
                unitOfWork,
                userService,
                config.PurchaseOrderRaiserRoleId,
                config.PurchaseOrderRaiserUserId,
                requestDepartmentId);
        }

        /// <summary>
        /// Live resolution for a requisition: the PO raiser of the unit its flow is effectively taken from
        /// (Custom unit via Inherit / RequisitionInheritFrom, same resolver as the approval stages).
        /// </summary>
        public static ApprovalProcessConfiguration ResolveForRequest(IUnitOfWork unitOfWork, PurchaseRequest request)
        {
            if (unitOfWork == null || request == null)
            {
                return null;
            }

            var department = unitOfWork.Repository<Department>().GetById(request.DepartmentId);
            if (department == null)
            {
                return null;
            }

            // Catalog flows (migration 100): the PO raiser follows the source that won for this requisition's lines.
            // Every line of one requisition shares the same approval fingerprint (split on submit), so the first
            // classified line is representative; unclassified requisitions resolve exactly as before.
            var classification = FindRepresentativeClassification(unitOfWork, request.Id);
            return classification == null
                ? ApprovalWorkflowHelper.GetDepartmentRequisitionConfiguration(unitOfWork, department)
                : Helpers.RequisitionFlowResolver.ResolveForLine(unitOfWork, department, classification).Configuration;
        }

        private static Helpers.RequisitionLineClassification FindRepresentativeClassification(IUnitOfWork unitOfWork, int purchaseRequestId)
        {
            if (purchaseRequestId <= 0)
            {
                return null;
            }

            var line = unitOfWork.Repository<PurchaseRequestLine>()
                .Find(x => x.PurchaseRequestId == purchaseRequestId && x.IsActive)
                .Where(x => x.AssetCategoryId.HasValue || x.AssetTypeId.HasValue || x.AssetSubTypeId.HasValue)
                .OrderBy(x => x.LineNumber)
                .FirstOrDefault();
            return line == null
                ? null
                : Helpers.RequisitionLineClassification.Create(line.AssetCategoryId, line.AssetTypeId, line.AssetSubTypeId);
        }

        /// <summary>Org-scoped user lookup (IUserService is tenant-filtered) mapped to an approver record.</summary>
        public static ApproverUserRecord ToRecord(UserVm user, int? organizationId)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.Id))
            {
                return null;
            }

            return new ApproverUserRecord
            {
                Id = user.Id,
                OrganizationId = organizationId,
                IsActive = user.IsActive,
                RoleId = user.RoleId,
                Label = StageApproverRules.BuildPersonLabel(user.Email, user.FirstName, user.LastName, user.Id)
            };
        }

        public static string Describe(IUnitOfWork unitOfWork, IUserService userService, ApprovalProcessConfiguration config)
        {
            if (!IsConfigured(config))
            {
                return null;
            }

            var role = unitOfWork == null ? null : unitOfWork.Repository<Role>().GetById(config.PurchaseOrderRaiserRoleId.Value);
            var roleLookup = new Dictionary<int, string>();
            if (role != null)
            {
                roleLookup[role.Id] = role.Name;
            }

            return BuildLabel(config, roleLookup, id =>
            {
                var user = userService == null ? null : userService.GetById(id);
                return user == null ? null : StageApproverRules.BuildPersonLabel(user.Email, user.FirstName, user.LastName, user.Id);
            });
        }

        /// <summary>"Procurement Officer" / "Procurement Officer — x@org (Name)" style label (same as stage display).</summary>
        public static string BuildLabel(ApprovalProcessConfiguration config, IDictionary<int, string> roleLookup, Func<string, string> personLabel)
        {
            if (!IsConfigured(config))
            {
                return null;
            }

            var roleName = ApprovalWorkflowSettingsHelper.ResolveRoleName(roleLookup, config.PurchaseOrderRaiserRoleId);
            if (string.IsNullOrWhiteSpace(config.PurchaseOrderRaiserUserId))
            {
                return roleName;
            }

            var person = personLabel == null ? null : personLabel(config.PurchaseOrderRaiserUserId);
            return roleName + " \u2014 " + (string.IsNullOrWhiteSpace(person) ? config.PurchaseOrderRaiserUserId : person);
        }
    }
}
