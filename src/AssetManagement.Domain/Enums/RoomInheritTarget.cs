namespace AssetManagement.Domain.Enums
{
    /// <summary>
    /// When a Room inherits requisition flow, which ancestor path to prefer.
    /// </summary>
    public enum RoomInheritTarget
    {
        /// <summary>Walk ParentDepartmentId chain (Room → Sub-department → Department → org).</summary>
        ParentChain = 0,
        /// <summary>Skip to first Sub-department ancestor when inheriting.</summary>
        PreferSubDepartment = 1,
        /// <summary>Skip to first Administrative (Department) ancestor when inheriting.</summary>
        PreferAdministrative = 2,
        /// <summary>Skip parents; use organization Purchase matrix only.</summary>
        OrganizationOnly = 3
    }
}
