using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Web.Helpers
{
    /// <summary>
    /// Label print helpers shared by Assets (single-asset label) and AssetLabels (Bulk Label Print):
    /// the read-only saved-template picker and the organisation name for the Company name element.
    /// </summary>
    public static class LabelPrintSupport
    {
        public const string ActiveLayoutText = "Active layout (Label designer)";

        /// <summary>Template picker options: "" = active layout, then every saved template (id + name + size).</summary>
        public static IList<SelectListItem> BuildTemplateOptions(IUnitOfWork unitOfWork)
        {
            var options = new List<SelectListItem>
            {
                new SelectListItem { Value = string.Empty, Text = ActiveLayoutText, Selected = true }
            };

            if (unitOfWork == null)
            {
                return options;
            }

            var settings = ApprovalWorkflowSettingsHelper.ToDictionary(unitOfWork.Repository<SystemSetting>().GetAll());
            foreach (var template in LabelLayoutTemplateHelper.ListTemplates(settings))
            {
                options.Add(new SelectListItem
                {
                    Value = template.Id,
                    Text = template.Name + " (" + template.WidthMm + " x " + template.HeightMm + " mm)"
                });
            }

            return options;
        }

        /// <summary>Category / type / sub-type options for the Bulk Label Print cascading filters (current org, active only).</summary>
        public static ClassificationCatalog BuildClassificationCatalog(IUnitOfWork unitOfWork)
        {
            if (unitOfWork == null)
            {
                return new ClassificationCatalog();
            }

            var scope = DependencyResolver.Current.GetService<IOrganizationScopeService>();
            var orgId = scope == null ? null : scope.GetCurrentOrganizationId();
            return BuildClassificationCatalog(
                orgId,
                unitOfWork.Repository<AssetCategory>().GetAll(),
                unitOfWork.Repository<AssetType>().GetAll(),
                unitOfWork.Repository<AssetSubType>().GetAll());
        }

        /// <summary>
        /// Org-scoped (when orgId is known), active, name-ordered; a type is kept only under a kept category and a
        /// sub-type only under a kept type, so the cascade can never offer a child of a hidden parent.
        /// </summary>
        public static ClassificationCatalog BuildClassificationCatalog(
            int? orgId,
            IEnumerable<AssetCategory> categories,
            IEnumerable<AssetType> types,
            IEnumerable<AssetSubType> subTypes)
        {
            var catalog = new ClassificationCatalog();
            catalog.Categories = (categories ?? Enumerable.Empty<AssetCategory>())
                .Where(x => x != null && x.IsActive && (!orgId.HasValue || x.OrganizationId == orgId))
                .OrderBy(x => x.Name)
                .Select(x => new ClassificationOption { Id = x.Id, Name = x.Name })
                .ToList();
            var categoryIds = new HashSet<int>(catalog.Categories.Select(x => x.Id));
            catalog.Types = (types ?? Enumerable.Empty<AssetType>())
                .Where(x => x != null && x.IsActive && categoryIds.Contains(x.AssetCategoryId) && (!orgId.HasValue || x.OrganizationId == orgId))
                .OrderBy(x => x.Name)
                .Select(x => new ClassificationOption { Id = x.Id, Name = x.Name, ParentId = x.AssetCategoryId })
                .ToList();
            var typeIds = new HashSet<int>(catalog.Types.Select(x => x.Id));
            catalog.SubTypes = (subTypes ?? Enumerable.Empty<AssetSubType>())
                .Where(x => x != null && x.IsActive && typeIds.Contains(x.AssetTypeId) && (!orgId.HasValue || x.OrganizationId == orgId))
                .OrderBy(x => x.Name)
                .Select(x => new ClassificationOption { Id = x.Id, Name = x.Name, ParentId = x.AssetTypeId })
                .ToList();
            return catalog;
        }

        /// <summary>Organization.Name of the current tenant (IOrganizationScopeService), else the fallback.</summary>
        public static string GetCurrentOrganizationName(IUnitOfWork unitOfWork, string fallback = null)
        {
            try
            {
                var scope = DependencyResolver.Current.GetService<IOrganizationScopeService>();
                var orgId = scope == null ? null : scope.GetCurrentOrganizationId();
                if (unitOfWork != null && orgId.HasValue)
                {
                    var organization = unitOfWork.Repository<Organization>().GetById(orgId.Value);
                    if (organization != null && !string.IsNullOrWhiteSpace(organization.Name))
                    {
                        return organization.Name.Trim();
                    }
                }
            }
            catch (System.InvalidOperationException)
            {
            }

            return fallback;
        }
    }

    public class ClassificationOption
    {
        public int Id { get; set; }

        public string Name { get; set; }

        /// <summary>Category id for a type, type id for a sub-type, null for a category.</summary>
        public int? ParentId { get; set; }
    }

    public class ClassificationCatalog
    {
        public IList<ClassificationOption> Categories { get; set; } = new List<ClassificationOption>();

        public IList<ClassificationOption> Types { get; set; } = new List<ClassificationOption>();

        public IList<ClassificationOption> SubTypes { get; set; } = new List<ClassificationOption>();
    }
}
