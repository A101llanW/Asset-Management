using AssetManagement.Application.DTOs;
using System;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Asset location moves (Allan, 2026-10-09; class to class added 2026-10-27, QA P3.6/X9.4). A class acts as a
    /// room: the class itself never moves, only the assets in it, to a room or to another classroom. Allowed: room to
    /// room, room to sub-department, sub-department to room, class to room, class to class. Everything else is refused. An asset with no department (company custody) is being placed
    /// for the first time, which is not a move.
    /// </summary>
    public static class AssetMoveRules
    {
        public const string RefusedMessage =
            "This move is not allowed. Assets can only be moved room to room, between a room and a sub-department, from a classroom to a room or another classroom, or from the Administration department itself (not a room or sub-department under it) to a room.";

        public static bool IsAllowed(DepartmentKind? fromKind, DepartmentKind toKind)
        {
            if (!fromKind.HasValue)
            {
                return true;
            }

            switch (fromKind.Value)
            {
                case DepartmentKind.Room:
                    return toKind == DepartmentKind.Room || toKind == DepartmentKind.SubDepartment;
                case DepartmentKind.SubDepartment:
                    return toKind == DepartmentKind.Room;
                case DepartmentKind.Class:
                    // Allan, 2026-10-27: classroom to classroom as well as classroom to room.
                    return toKind == DepartmentKind.Room || toKind == DepartmentKind.Class;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Allan, 2026-10-19 (narrowed): also allow a move into a room when the asset sits directly in the
        /// organisation's top-level Administration department (see IsAdministrationRoot). Other top-level
        /// departments (grades, academics, ...) cannot move assets into rooms. All other rules are unchanged.
        /// </summary>
        public static bool IsAllowed(DepartmentKind? fromKind, bool fromIsAdministrationRoot, DepartmentKind toKind)
        {
            if (IsAllowed(fromKind, toKind))
            {
                return true;
            }

            return fromKind.HasValue && fromIsAdministrationRoot && toKind == DepartmentKind.Room;
        }

        public static void Ensure(DepartmentKind? fromKind, bool fromIsAdministrationRoot, DepartmentKind toKind)
        {
            if (!IsAllowed(fromKind, fromIsAdministrationRoot, toKind))
            {
                throw new BusinessException(RefusedMessage);
            }
        }

        public static void Ensure(DepartmentKind? fromKind, DepartmentKind toKind)
        {
            if (!IsAllowed(fromKind, toKind))
            {
                throw new BusinessException(RefusedMessage);
            }
        }

        public const string AssignmentRefusedMessage =
            "This assignment is not allowed. Assets can be assigned into or out of a top-level department, or moved room to room, between a room and a sub-department, or from a classroom to a room or another classroom.";

        /// <summary>
        /// rel1009h (Allan, 2026-10-09): rule for assignment and transfer targets (Assign, Batch assign, Asset request
        /// fulfil, Transfer "Move"). Everything <see cref="IsAllowed"/> permits, plus:
        /// (a) a top-level department (no parent) may always be the target - assigning to a department is allowed;
        /// (b) a top-level department acts as the store, so an asset held there may be placed into any department
        ///     (this is what refused Administrative to Room pool assignments in rel1009g).
        /// Physical relocations (Move to room, bulk department move, asset edit) keep using <see cref="IsAllowed"/>.
        /// </summary>
        public static bool IsAllowedAssignmentTarget(DepartmentKind? fromKind, bool fromIsTopLevel, DepartmentKind toKind, bool toIsTopLevel)
        {
            if (IsAllowed(fromKind, toKind))
            {
                return true;
            }

            if (toIsTopLevel)
            {
                return true;
            }

            return fromKind.HasValue && fromIsTopLevel;
        }

        public static void EnsureAssignmentTarget(DepartmentKind? fromKind, bool fromIsTopLevel, DepartmentKind toKind, bool toIsTopLevel)
        {
            if (!IsAllowedAssignmentTarget(fromKind, fromIsTopLevel, toKind, toIsTopLevel))
            {
                throw new BusinessException(AssignmentRefusedMessage);
            }
        }

        /// <summary>True when assets in a department of this kind can be moved to a room (UI "Move to room").</summary>
        public static bool CanMoveToRoomFrom(DepartmentKind? fromKind)
        {
            return IsAllowed(fromKind, DepartmentKind.Room);
        }

        public static bool CanMoveToRoomFrom(DepartmentKind? fromKind, bool fromIsAdministrationRoot)
        {
            return IsAllowed(fromKind, fromIsAdministrationRoot, DepartmentKind.Room);
        }

        /// <summary>True when assets in a department of this kind can be moved to a classroom (UI "Move" / "Move group").</summary>
        public static bool CanMoveToClassFrom(DepartmentKind? fromKind)
        {
            return IsAllowed(fromKind, DepartmentKind.Class);
        }

        public const string AdministrationDepartmentName = "Administration";

        /// <summary>
        /// True for an organisation's top-level Administration department: no parent, name "Administration"
        /// (trimmed, case-insensitive). Matched by name, not Id, so it works in every organisation and database
        /// (live NIS Id 1493 is not hard-coded). Rooms/sub-departments under it are NOT matched.
        /// </summary>
        public static bool IsAdministrationRoot(Department department)
        {
            return department != null
                && !department.ParentDepartmentId.HasValue
                && department.Name != null
                && string.Equals(department.Name.Trim(), AdministrationDepartmentName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
