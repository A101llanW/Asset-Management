using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Helpers
{
    /// <summary>Shared by manual create, receiving (via AssetService.Create) and import.</summary>
    public static class AssetTagPrefixGenerator
    {
        private const int MaxAttempts = 25;

        /// <summary>
        /// Returns a structured tag (PREFIX-NNN) for a new asset, or null when the tenant turned structured tags
        /// off (callers then keep the legacy random tag). Uniqueness: the counter never repeats a number, every
        /// candidate is checked against all assets of the tenant (active or not, matching the unique index
        /// IX_Asset_Org_AssetTag) and against tags reserved earlier in the same batch.
        /// </summary>
        public static string TryGenerate(
            IUnitOfWork unitOfWork,
            IAssetTagSequenceStore store,
            int organizationId,
            int? departmentId,
            ISet<string> reservedTags)
        {
            var settings = unitOfWork.Repository<SystemSetting>().GetAll()
                .Where(x => x.SettingKey != null)
                .GroupBy(x => x.SettingKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().SettingValue, StringComparer.OrdinalIgnoreCase);
            if (!AssetTagPrefixResolver.IsEnabled(settings))
            {
                return null;
            }

            var departments = unitOfWork.Repository<Department>().GetAll().ToDictionary(x => x.Id);
            // Section prefix (Prep / Senior school, migration 106) and the per-level segment switches (migration 104) both come from GetOptions.
            var resolution = AssetTagPrefixResolver.ResolveWithOptions(departmentId, departments, AssetTagPrefixResolver.GetOptions(settings));
            var repository = unitOfWork.Repository<Asset>();
            int? fallbackNext = null;

            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                int sequence;
                if (store != null)
                {
                    sequence = store.AllocateNext(organizationId, resolution.Prefix);
                }
                else
                {
                    // Unit tests / no SQL store: compute from the repository (not concurrency-safe, test only).
                    if (!fallbackNext.HasValue)
                    {
                        fallbackNext = AssetTagHelper.GetNextSequence(repository.Query(), resolution.Prefix + "-");
                    }

                    sequence = fallbackNext.Value;
                    fallbackNext = sequence + 1;
                }

                var candidate = AssetTagPrefixResolver.FormatTag(resolution.Prefix, sequence);
                if (reservedTags != null && reservedTags.Contains(candidate))
                {
                    continue;
                }

                var taken = candidate;
                if (repository.Count(x => x.AssetTag == taken) == 0)
                {
                    if (reservedTags != null)
                    {
                        reservedTags.Add(candidate);
                    }

                    return candidate;
                }
            }

            throw new BusinessException("Unable to generate a unique asset tag for prefix " + resolution.Prefix + ". Enter one manually.");
        }
    }
}