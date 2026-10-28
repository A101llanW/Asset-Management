using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Helpers
{
    /// <summary>C3 (migration 108): fills "who" on classification overrides with the user's name (id when unknown).</summary>
    public static class ClassificationOverrideNames
    {
        public static void Fill(IUserService userService, IEnumerable<ClassificationOverrideVm> overrides)
        {
            if (userService == null || overrides == null)
            {
                return;
            }

            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in overrides.Where(x => x != null && !string.IsNullOrWhiteSpace(x.ChangedById)))
            {
                item.ChangedByName = Lookup(userService, names, item.ChangedById);
            }
        }

        /// <summary>Fills "Received by" on receiving rows with the user's name (the view falls back to the id).</summary>
        public static void FillReceivedBy(IUserService userService, IEnumerable<AssetReceivingListVm> receivings)
        {
            if (userService == null || receivings == null)
            {
                return;
            }

            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in receivings.Where(x => x != null && !string.IsNullOrWhiteSpace(x.ReceivedById)))
            {
                item.ReceivedByName = Lookup(userService, names, item.ReceivedById);
            }
        }

        private static string Lookup(IUserService userService, IDictionary<string, string> cache, string id)
        {
            string name;
            if (!cache.TryGetValue(id, out name))
            {
                name = Describe(SafeGet(userService, id));
                cache[id] = name;
            }

            return name;
        }

        private static UserVm SafeGet(IUserService userService, string id)
        {
            try
            {
                return userService.GetById(id);
            }
            catch (Exception)
            {
                return null; // display only: never fail the page over a name
            }
        }

        private static string Describe(UserVm user)
        {
            if (user == null)
            {
                return null;
            }

            var full = ((user.FirstName ?? string.Empty) + " " + (user.LastName ?? string.Empty)).Trim();
            return full.Length > 0 ? full : user.Email;
        }
    }
}