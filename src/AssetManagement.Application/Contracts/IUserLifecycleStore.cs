using System.Collections.Generic;

namespace AssetManagement.Application.Contracts
{
    /// <summary>Data access for user deactivate / delete (rel1009g). Implemented with plain SQL in Infrastructure.</summary>
    public interface IUserLifecycleStore
    {
        /// <summary>(label, count) for every record that blocks a hard delete; empty when the user can be deleted.</summary>
        IList<KeyValuePair<string, int>> GetDeleteBlockers(string userId, int organizationId);

        /// <summary>Active users of the organization holding the system Company Admin role, excluding one user.</summary>
        int CountOtherActiveCompanyAdmins(int organizationId, string excludeUserId);

        /// <summary>True when the user's role is the system Company Admin role.</summary>
        bool IsCompanyAdmin(string userId, int organizationId);

        /// <summary>Deletes the user and the rows they own (notifications, identity links, reset tokens) in one transaction.</summary>
        void HardDelete(string userId, int organizationId);
    }
}
