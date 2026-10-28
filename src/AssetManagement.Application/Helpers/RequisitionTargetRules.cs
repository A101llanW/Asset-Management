using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Requisition target "room or person" (migration 110). The one place that decides:
    /// - which room / class a person is assigned to (FindAssignedRoom),
    /// - where a person's assets go (ResolvePlacement: in their room, or company custody held by them),
    /// - which department routes approval for a person target (ResolveRoutingDepartment),
    /// - what tagging an asset does to the target (DecideTargetOnTag; mirrored in purchase-request-create.js).
    /// PurchaseRequest.DepartmentId stays the approval / scope department in every mode, so approval routing,
    /// department scope and the reports keep working unchanged. Requisitions saved before 110 have a NULL kind and
    /// behave exactly as before (their department is the target).
    /// </summary>
    public static class RequisitionTargetRules
    {
        public const string KindRoom = "Room";
        public const string KindPerson = "Person";

        /// <summary>The asset is recorded in the person's room / class.</summary>
        public const string PlacementPersonRoom = "PersonRoom";

        /// <summary>The asset stays in company custody (no department) and the person is its custodian.</summary>
        public const string PlacementCompanyCustody = "CompanyCustody";

        /// <summary>"Room" / "Person" (case-insensitive) or null (not chosen / legacy).</summary>
        public static string NormalizeKind(string kind)
        {
            if (string.IsNullOrWhiteSpace(kind))
            {
                return null;
            }

            var value = kind.Trim();
            if (string.Equals(value, KindPerson, StringComparison.OrdinalIgnoreCase))
            {
                return KindPerson;
            }

            return string.Equals(value, KindRoom, StringComparison.OrdinalIgnoreCase) ? KindRoom : null;
        }

        public static bool IsPerson(string kind)
        {
            return NormalizeKind(kind) == KindPerson;
        }

        public static string NormalizePlacement(string placement)
        {
            if (string.IsNullOrWhiteSpace(placement))
            {
                return null;
            }

            var value = placement.Trim();
            if (string.Equals(value, PlacementPersonRoom, StringComparison.OrdinalIgnoreCase))
            {
                return PlacementPersonRoom;
            }

            return string.Equals(value, PlacementCompanyCustody, StringComparison.OrdinalIgnoreCase) ? PlacementCompanyCustody : null;
        }

        /// <summary>
        /// The room / class a person is assigned to: an active Class whose class teacher is the person, or an active
        /// Room whose person in charge is the person (lowest sort order, then name); otherwise the person's profile
        /// department when that is an active Room or Class. Null = no room.
        /// </summary>
        public static Department FindAssignedRoom(string userId, int? profileDepartmentId, IEnumerable<Department> departments)
        {
            if (string.IsNullOrWhiteSpace(userId) || departments == null)
            {
                return null;
            }

            var list = departments.Where(x => x != null && x.IsActive).ToList();
            var assigned = list
                .Where(x => (x.DepartmentKind == DepartmentKind.Class && SameUser(x.ClassTeacherUserId, userId))
                    || (x.DepartmentKind == DepartmentKind.Room && SameUser(x.RoomCustodianUserId, userId)))
                .OrderBy(x => x.SortOrder ?? int.MaxValue)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Id)
                .FirstOrDefault();
            if (assigned != null)
            {
                return assigned;
            }

            if (!profileDepartmentId.HasValue)
            {
                return null;
            }

            var profile = list.FirstOrDefault(x => x.Id == profileDepartmentId.Value);
            return profile != null && IsRoomLike(profile.DepartmentKind) ? profile : null;
        }

        public static bool IsRoomLike(DepartmentKind kind)
        {
            return kind == DepartmentKind.Room || kind == DepartmentKind.Class;
        }

        /// <summary>
        /// Placement for a person target. With a room the requester must choose (PersonRoom or CompanyCustody);
        /// without one the asset stays in company custody held by that person, without asking (Allan 2026-10-28 15:03 EAT).
        /// </summary>
        public static string ResolvePlacement(bool personHasRoom, string postedPlacement, string personName = null)
        {
            var who = string.IsNullOrWhiteSpace(personName) ? "this person" : personName.Trim();
            if (!personHasRoom)
            {
                return PlacementCompanyCustody;
            }

            var placement = NormalizePlacement(postedPlacement);
            if (placement == null)
            {
                throw new BusinessException("Choose whether the asset is recorded in " + who + "'s room or kept in company custody held by " + who + ".");
            }

            return placement;
        }

        /// <summary>
        /// Approval / scope department for a person target (PurchaseRequest.DepartmentId): the requester's locked
        /// department when they are locked to one; otherwise the person's room, then the person's profile department,
        /// whichever is first active and marked as a requisition target. Null = none (the caller rejects the form).
        /// </summary>
        public static Department ResolveRoutingDepartment(Department lockedDepartment, Department personRoom, Department profileDepartment)
        {
            if (lockedDepartment != null)
            {
                return lockedDepartment;
            }

            foreach (var candidate in new[] { personRoom, profileDepartment })
            {
                if (candidate != null && candidate.IsActive && candidate.IsRequisitionTarget)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Tagging rule (Debugging bot 2026-10-28): tagging an asset pre-fills the target as a ROOM (the tagged asset's
        /// room / department) only while no target has been chosen yet; a room or person the requester already picked
        /// is kept and the page shows TaggedAssetKeptNotice instead. A locked requester's department is never changed.
        /// </summary>
        public static TaggedAssetTargetDecision DecideTargetOnTag(
            string currentKind,
            int? currentDepartmentId,
            string currentUserId,
            int? assetDepartmentId,
            bool departmentLocked)
        {
            var kind = NormalizeKind(currentKind);
            var chosen = departmentLocked
                || (kind == KindPerson && !string.IsNullOrWhiteSpace(currentUserId))
                || (kind == KindRoom && currentDepartmentId.HasValue && currentDepartmentId.Value > 0);
            if (chosen)
            {
                return assetDepartmentId.HasValue && (kind == KindPerson || currentDepartmentId != assetDepartmentId)
                    ? TaggedAssetTargetDecision.KeepWithNotice
                    : TaggedAssetTargetDecision.Keep;
            }

            return assetDepartmentId.HasValue && assetDepartmentId.Value > 0
                ? TaggedAssetTargetDecision.PrefillRoom
                : TaggedAssetTargetDecision.Keep;
        }

        /// <summary>The inline notice shown when a tag does not change an already chosen target (same text in JS).</summary>
        public static string TaggedAssetKeptNotice(string assetLocation)
        {
            return "The tagged asset sits in " + (string.IsNullOrWhiteSpace(assetLocation) ? "another room" : assetLocation.Trim())
                + "; the requisition target you chose was kept.";
        }

        /// <summary>
        /// Display text for Details / receiving. Legacy (NULL kind) and Room: "Room: X". Person: "Person: Name (in X)"
        /// or "Person: Name (company custody, held by Name)".
        /// </summary>
        public static string Describe(string kind, string departmentName, string personName, string placement, string personRoomName)
        {
            if (!IsPerson(kind))
            {
                return string.IsNullOrWhiteSpace(departmentName) ? null : (NormalizeKind(kind) == KindRoom ? "Room: " : string.Empty) + departmentName;
            }

            var who = string.IsNullOrWhiteSpace(personName) ? "(unknown person)" : personName.Trim();
            return NormalizePlacement(placement) == PlacementPersonRoom
                ? "Person: " + who + " (recorded in " + (string.IsNullOrWhiteSpace(personRoomName) ? "their room" : personRoomName) + ")"
                : "Person: " + who + " (company custody, held by " + who + ")";
        }

        /// <summary>"First Last (EmployeeNumber)", else the e-mail / id.</summary>
        public static string PersonDisplayName(UserVm user)
        {
            if (user == null)
            {
                return null;
            }

            var name = ((user.FirstName ?? string.Empty) + " " + (user.LastName ?? string.Empty)).Trim();
            if (string.IsNullOrEmpty(name))
            {
                name = user.Email ?? user.Id;
            }

            return string.IsNullOrWhiteSpace(user.EmployeeNumber) ? name : name + " (" + user.EmployeeNumber.Trim() + ")";
        }

        /// <summary>"Parent › Child › Room" walking ParentDepartmentId (max 6 levels).</summary>
        public static string PathLabel(Department department, IDictionary<int, Department> byId)
        {
            if (department == null)
            {
                return null;
            }

            var parts = new List<string> { department.Name };
            var current = department;
            for (var i = 0; i < 6 && current.ParentDepartmentId.HasValue; i++)
            {
                Department parent;
                if (byId == null || !byId.TryGetValue(current.ParentDepartmentId.Value, out parent) || parent == null)
                {
                    break;
                }

                parts.Insert(0, parent.Name);
                current = parent;
            }

            return string.Join(" \u203A ", parts.ToArray());
        }

        private static bool SameUser(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left)
                && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    public enum TaggedAssetTargetDecision
    {
        /// <summary>Nothing to do (no target chosen and the asset has no department, or the target already matches).</summary>
        Keep = 0,

        /// <summary>No target chosen yet: set the target to Room = the tagged asset's room / department.</summary>
        PrefillRoom = 1,

        /// <summary>A different room / a person was already chosen: keep it and show TaggedAssetKeptNotice.</summary>
        KeepWithNotice = 2
    }
}
