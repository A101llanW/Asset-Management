using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Web.Controllers
{
    public partial class SettingsController
    {
        private void PopulateSectionHeadOptions(SettingsVm model)
        {
            model = model ?? new SettingsVm();
            ViewBag.KgSectionHeadUsers = BuildActiveUserSelectList(model.KgHeadUserId);
            ViewBag.PrepSectionHeadUsers = BuildActiveUserSelectList(model.PrepHeadUserId);
            ViewBag.SeniorSectionHeadUsers = BuildActiveUserSelectList(model.SeniorHeadUserId);
        }

        private void ApplySectionHeadsToModel(SettingsVm model)
        {
            if (model == null)
            {
                return;
            }

            var map = TryLoadOrgSectionHeadMap();
            model.KgHeadUserId = OrganizationSectionHeads.Get(map, DepartmentSection.KG);
            model.PrepHeadUserId = OrganizationSectionHeads.Get(map, DepartmentSection.Prep);
            model.SeniorHeadUserId = OrganizationSectionHeads.Get(map, DepartmentSection.SeniorSchool);
        }

        private void SaveSectionHeads(SettingsVm model)
        {
            if (model == null)
            {
                return;
            }

            var orgId = ResolveCurrentOrganizationId();
            if (!orgId.HasValue || orgId.Value <= 0)
            {
                return;
            }

            IList<OrganizationSectionHead> existing;
            try
            {
                existing = UnitOfWork.Repository<OrganizationSectionHead>().GetAll().ToList();
            }
            catch (Exception ex)
            {
                if (IsMissingOrganizationSectionHeadTable(ex))
                {
                    ModelState.AddModelError(string.Empty, "Section heads cannot be saved until database migration 112 is applied.");
                    return;
                }

                throw;
            }

            var rows = OrganizationSectionHeads.EnsureRows(
                existing,
                orgId.Value,
                row => UnitOfWork.Repository<OrganizationSectionHead>().Add(row));

            // Reject users outside this company (tenant-scoped active list).
            var allowed = new HashSet<string>(
                GetActiveUsers().Select(x => x.Id),
                StringComparer.OrdinalIgnoreCase);
            if (!IsAllowedOrBlank(model.KgHeadUserId, allowed)
                || !IsAllowedOrBlank(model.PrepHeadUserId, allowed)
                || !IsAllowedOrBlank(model.SeniorHeadUserId, allowed))
            {
                ModelState.AddModelError(string.Empty, "Section heads must be active users in this company.");
                return;
            }

            OrganizationSectionHeads.Apply(rows, model.KgHeadUserId, model.PrepHeadUserId, model.SeniorHeadUserId);
        }

        private IDictionary<DepartmentSection, string> TryLoadOrgSectionHeadMap()
        {
            try
            {
                return OrganizationSectionHeads.ToMap(UnitOfWork.Repository<OrganizationSectionHead>().GetAll());
            }
            catch (Exception ex)
            {
                if (IsMissingOrganizationSectionHeadTable(ex))
                {
                    return new Dictionary<DepartmentSection, string>();
                }

                throw;
            }
        }

        private static bool IsAllowedOrBlank(string userId, HashSet<string> allowed)
        {
            return string.IsNullOrWhiteSpace(userId) || allowed.Contains(userId.Trim());
        }

        private static bool IsMissingOrganizationSectionHeadTable(Exception ex)
        {
            var sql = ex as SqlException;
            if (sql != null && sql.Number == 208)
            {
                return true;
            }

            var inner = ex.InnerException as SqlException;
            return inner != null && inner.Number == 208;
        }
    }
}