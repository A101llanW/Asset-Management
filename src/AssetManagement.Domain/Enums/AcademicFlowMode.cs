namespace AssetManagement.Domain.Enums
{
    /// <summary>
    /// How Class/Grade locations resolve purchase requisition approval stages.
    /// </summary>
    public enum AcademicFlowMode
    {
        /// <summary>Always use the organization Purchase matrix (ignore Class/Grade Custom).</summary>
        UseOrganizationMatrix = 0,
        /// <summary>Prefer Custom/AutoApprove on the Class node, then Grade, then org matrix.</summary>
        PreferClassCustom = 1,
        /// <summary>Prefer Custom/AutoApprove on the Grade ancestor (or Grade itself), else org matrix.</summary>
        PreferGradeCustom = 2
    }
}
