using AssetManagement.Application.DTOs;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Parent/kind rules for the shared Department table (org vs school domains).
    /// </summary>
    public static class DepartmentHierarchyRules
    {
        public const string DomainOrg = "org";
        public const string DomainClasses = "classes";

        public static bool IsOrganizational(DepartmentKind kind)
        {
            return kind == DepartmentKind.Administrative || kind == DepartmentKind.SubDepartment;
        }

        public static bool IsAcademic(DepartmentKind kind)
        {
            return kind == DepartmentKind.Grade || kind == DepartmentKind.Class;
        }

        public static bool BelongsToDomain(DepartmentKind kind, string domain)
        {
            var normalized = NormalizeDomain(domain);
            return normalized == DomainClasses ? IsAcademic(kind) : IsOrganizational(kind);
        }

        public static string NormalizeDomain(string domain)
        {
            if (string.Equals(domain, DomainClasses, System.StringComparison.OrdinalIgnoreCase))
            {
                return DomainClasses;
            }

            return DomainOrg;
        }

        public static string DisplayLabel(DepartmentKind kind)
        {
            switch (kind)
            {
                case DepartmentKind.SubDepartment:
                    return "Sub-unit";
                case DepartmentKind.Administrative:
                    return "Admin";
                case DepartmentKind.Grade:
                    return "Grade";
                case DepartmentKind.Class:
                    return "Stream";
                default:
                    return kind.ToString();
            }
        }

        public static void AssertKindUnchanged(DepartmentKind existingKind, DepartmentKind requestedKind)
        {
            if (existingKind != requestedKind)
            {
                throw new BusinessException("Department kind cannot be changed after create.");
            }
        }

        /// <summary>
        /// Validates kind/parent pairing. Pass null parent when the department is a root.
        /// </summary>
        public static void AssertValidHierarchy(DepartmentKind kind, Department parent)
        {
            switch (kind)
            {
                case DepartmentKind.Administrative:
                case DepartmentKind.Grade:
                    if (parent != null)
                    {
                        throw new BusinessException(DisplayLabel(kind) + " departments cannot have a parent.");
                    }
                    break;

                case DepartmentKind.SubDepartment:
                    if (parent == null)
                    {
                        throw new BusinessException("Sub-units must sit under a top-level administrative department.");
                    }

                    if (parent.DepartmentKind != DepartmentKind.Administrative || parent.ParentDepartmentId.HasValue)
                    {
                        throw new BusinessException("Sub-units can only sit under top-level administrative departments, not classes.");
                    }
                    break;

                case DepartmentKind.Class:
                    if (parent == null)
                    {
                        throw new BusinessException("Streams must sit under a grade.");
                    }

                    if (parent.DepartmentKind != DepartmentKind.Grade)
                    {
                        throw new BusinessException("Streams must have a grade parent.");
                    }
                    break;

                default:
                    throw new BusinessException("Unsupported department kind.");
            }
        }
    }
}
