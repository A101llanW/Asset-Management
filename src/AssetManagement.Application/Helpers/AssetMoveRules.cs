using AssetManagement.Application.DTOs;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Asset location moves (Allan, 2026-10-09). A class acts as a room: the class itself never moves, only the
    /// assets in it, and only to a room. Allowed: room to room, room to sub-department, sub-department to room,
    /// class to room. Everything else is refused. An asset with no department (company custody) is being placed
    /// for the first time, which is not a move.
    /// </summary>
    public static class AssetMoveRules
    {
        public const string RefusedMessage =
            "This move is not allowed. Assets can only be moved room to room, between a room and a sub-department, or from a class to a room.";

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
                case DepartmentKind.Class:
                    return toKind == DepartmentKind.Room;
                default:
                    return false;
            }
        }

        public static void Ensure(DepartmentKind? fromKind, DepartmentKind toKind)
        {
            if (!IsAllowed(fromKind, toKind))
            {
                throw new BusinessException(RefusedMessage);
            }
        }

        /// <summary>True when assets in a department of this kind can be moved to a room (UI "Move to room").</summary>
        public static bool CanMoveToRoomFrom(DepartmentKind? fromKind)
        {
            return IsAllowed(fromKind, DepartmentKind.Room);
        }
    }
}
