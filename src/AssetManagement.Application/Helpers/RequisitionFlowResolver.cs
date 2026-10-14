using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    /// <summary>Effective approval path of one requisition line plus the fingerprint used to group lines on submit.</summary>
    public sealed class RequisitionFlowResolveResult
    {
        public ApprovalProcessConfiguration Configuration { get; set; }

        /// <summary>CatalogCustom | CatalogAutoApprove | CustomDepartment | Inherited | AutoApprove | OrganizationMatrix</summary>
        public string SourceKind { get; set; }

        /// <summary>Human label of the winning source, e.g. "Custom on Type: Laptops", "Inherited from ICT".</summary>
        public string SourceLabel { get; set; }

        /// <summary>Stable id of the winning source, e.g. "Catalog:Type:12", "Department:5", "OrganizationMatrix".</summary>
        public string SourceKey { get; set; }

        public CatalogRequisitionFlowResolver.CatalogHit CatalogHit { get; set; }

        public DepartmentRequisitionFlowResolver.ResolveResult DepartmentResult { get; set; }

        /// <summary>Lines with equal fingerprints share one requisition; different fingerprints split (or block).</summary>
        public string Fingerprint { get; set; }

        public bool IsCatalogSource
        {
            get { return CatalogHit != null; }
        }
    }

    /// <summary>
    /// Per-line requisition approval resolution: Sub-type > Type > Category (catalog overrides) >
    /// department inherit chain (<see cref="DepartmentRequisitionFlowResolver"/>, unchanged) > org Purchase matrix.
    /// The PO raiser travels with the configuration of whichever source wins.
    /// </summary>
    public static class RequisitionFlowResolver
    {
        public static RequisitionFlowResolveResult Resolve(
            Department department,
            RequisitionLineClassification classification,
            Func<int, Department> getDepartment,
            ApprovalProcessConfiguration orgDefault,
            Func<CatalogFlowScope, int, CatalogRequisitionFlow> getCatalogFlow,
            Func<CatalogFlowScope, int, string> getCatalogName)
        {
            var hit = CatalogRequisitionFlowResolver.Resolve(classification, getCatalogFlow, getCatalogName);
            if (hit != null)
            {
                // Owner decision: catalog paths ALWAYS apply, regardless of the org purchase-approval switch.
                return Finish(new RequisitionFlowResolveResult
                {
                    Configuration = hit.Configuration,
                    SourceKind = hit.SourceKind,
                    SourceLabel = hit.SourceLabel,
                    SourceKey = hit.SourceKey,
                    CatalogHit = hit
                });
            }

            if (department == null)
            {
                return Finish(new RequisitionFlowResolveResult
                {
                    Configuration = orgDefault ?? EmptyPurchaseConfiguration(),
                    SourceKind = "OrganizationMatrix",
                    SourceLabel = "Organization Approval Matrix",
                    SourceKey = "OrganizationMatrix"
                });
            }

            var detailed = DepartmentRequisitionFlowResolver.ResolveDetailed(department, getDepartment, orgDefault);
            var isMatrix = string.Equals(detailed.SourceKind, "OrganizationMatrix", StringComparison.OrdinalIgnoreCase);
            return Finish(new RequisitionFlowResolveResult
            {
                Configuration = detailed.Configuration,
                SourceKind = detailed.SourceKind,
                SourceLabel = detailed.SourceLabel,
                SourceKey = isMatrix || !detailed.SourceDepartmentId.HasValue
                    ? "OrganizationMatrix"
                    : "Department:" + detailed.SourceDepartmentId.Value,
                DepartmentResult = detailed
            });
        }

        /// <summary>Repository-backed entry point used by PurchaseRequestService and PurchaseOrderRaiserRules.</summary>
        public static RequisitionFlowResolveResult ResolveForLine(
            IUnitOfWork unitOfWork,
            Department department,
            RequisitionLineClassification classification)
        {
            var orgDefault = ApprovalWorkflowHelper.GetProcessConfiguration(unitOfWork, ApprovalProcessCodes.Purchase);
            var organizationId = department == null ? null : department.OrganizationId;
            var hasClassification = classification != null && classification.HasAny;
            return Resolve(
                department,
                classification,
                id => unitOfWork.Repository<Department>().GetById(id),
                orgDefault,
                // Only touch the catalog table when the line is classified (unclassified lines = legacy path).
                hasClassification ? (Func<CatalogFlowScope, int, CatalogRequisitionFlow>)((scope, id) => FindCatalogFlow(unitOfWork, scope, id, organizationId)) : null,
                (scope, id) => GetCatalogName(unitOfWork, scope, id));
        }

        public static CatalogRequisitionFlow FindCatalogFlow(IUnitOfWork unitOfWork, CatalogFlowScope scope, int scopedEntityId, int? organizationId)
        {
            if (unitOfWork == null)
            {
                return null;
            }

            return unitOfWork.Repository<CatalogRequisitionFlow>()
                .Find(x => x.Scope == scope && x.ScopedEntityId == scopedEntityId && x.IsActive)
                .Where(x => !organizationId.HasValue || !x.OrganizationId.HasValue || x.OrganizationId.Value == organizationId.Value)
                .OrderBy(x => x.Id)
                .FirstOrDefault();
        }

        public static string GetCatalogName(IUnitOfWork unitOfWork, CatalogFlowScope scope, int id)
        {
            if (unitOfWork == null)
            {
                return null;
            }

            switch (scope)
            {
                case CatalogFlowScope.SubType:
                    var subType = unitOfWork.Repository<AssetSubType>().GetById(id);
                    return subType == null ? null : subType.Name;
                case CatalogFlowScope.Type:
                    var type = unitOfWork.Repository<AssetType>().GetById(id);
                    return type == null ? null : type.Name;
                case CatalogFlowScope.Category:
                    var category = unitOfWork.Repository<AssetCategory>().GetById(id);
                    return category == null ? null : category.Name;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Stable hash of the effective path: source identity + ordered stage roles + stage users + auto-approve flag
        /// + PO raiser role/user. Order of stages matters; ids are used (not names) so renames never split a form.
        /// </summary>
        public static string ComputeFingerprint(string sourceKey, ApprovalProcessConfiguration configuration)
        {
            var canonical = BuildCanonical(sourceKey, configuration);
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                var builder = new StringBuilder(32);
                for (var i = 0; i < 16; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        public static string BuildCanonical(string sourceKey, ApprovalProcessConfiguration configuration)
        {
            var config = configuration ?? EmptyPurchaseConfiguration();
            var roles = config.StageRoleIds ?? new List<int>();
            var users = config.StageUserIds ?? new List<string>();
            var normalizedUsers = new List<string>();
            for (var i = 0; i < roles.Count; i++)
            {
                var user = i < users.Count ? users[i] : null;
                normalizedUsers.Add(string.IsNullOrWhiteSpace(user) ? string.Empty : user.Trim().ToLowerInvariant());
            }

            return "src=" + (sourceKey ?? string.Empty)
                + "|auto=" + (config.UsesApproval ? "0" : "1")
                + "|req=" + (config.RequiresApproval ? "1" : "0")
                + "|roles=" + string.Join(",", roles.Select(x => x.ToString()).ToArray())
                + "|users=" + string.Join(",", normalizedUsers.ToArray())
                + "|poRole=" + (config.HasPurchaseOrderRaiser ? config.PurchaseOrderRaiserRoleId.Value.ToString() : string.Empty)
                + "|poUser=" + (config.HasPurchaseOrderRaiser && !string.IsNullOrWhiteSpace(config.PurchaseOrderRaiserUserId)
                    ? config.PurchaseOrderRaiserUserId.Trim().ToLowerInvariant()
                    : string.Empty);
        }

        private static RequisitionFlowResolveResult Finish(RequisitionFlowResolveResult result)
        {
            // Owner decision (supersedes the PM merge decision): the fingerprint includes the winning source node,
            // so two catalog nodes with identical stages still produce SEPARATE requisitions.
            result.Fingerprint = ComputeFingerprint(result.SourceKey, result.Configuration);
            return result;
        }

        private static ApprovalProcessConfiguration EmptyPurchaseConfiguration()
        {
            return new ApprovalProcessConfiguration
            {
                ProcessCode = ApprovalProcessCodes.Purchase,
                DisplayName = ApprovalProcessCodes.GetDisplayName(ApprovalProcessCodes.Purchase),
                RequiresApproval = false
            };
        }
    }
}
