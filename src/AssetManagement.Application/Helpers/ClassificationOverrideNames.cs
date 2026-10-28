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
                string name;
                if (!names.TryGetValue(item.ChangedById, out name))
                {
                    name = Describe(SafeGet(userService, item.ChangedById));
                    names[item.ChangedById] = name;
                }

                item.ChangedByName = name;
            }
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