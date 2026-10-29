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

        /// <summary>
        /// Same rule, knowing whether the asset's room is in the rooms-only list (QA B on 65d06e1): a PrefillRoom whose
        /// room cannot be selected (not a room, not a requisition target, inactive, or filtered out), or an asset with no
        /// room while no target is chosen, becomes NotSelectableNotice: nothing is selected and the page says where the
        /// asset sits (TaggedAssetNotSelectableNotice). Never a silent no-op.
        /// </summary>
        public static TaggedAssetTargetDecision DecideTargetOnTag(
            string currentKind,
            int? currentDepartmentId,
            string currentUserId,
            int? assetDepartmentId,
            bool departmentLocked,
            bool assetRoomSelectable)
        {
            var decision = DecideTargetOnTag(currentKind, currentDepartmentId, currentUserId, assetDepartmentId, departmentLocked);
            if (decision == TaggedAssetTargetDecision.PrefillRoom && !assetRoomSelectable)
            {
                return TaggedAssetTargetDecision.NotSelectableNotice;
            }

            if (decision == TaggedAssetTargetDecision.Keep && !(assetDepartmentId.HasValue && assetDepartmentId.Value > 0))
            {
                var kind = NormalizeKind(currentKind);
                var chosen = departmentLocked
                    || (kind == KindPerson && !string.IsNullOrWhiteSpace(currentUserId))
                    || (kind == KindRoom && currentDepartmentId.HasValue && currentDepartmentId.Value > 0);
                return chosen ? decision : TaggedAssetTargetDecision.NotSelectableNotice;
            }

            return decision;
        }

        /// <summary>Notice when the tagged asset's room cannot be pre-selected (same text in purchase-request-create.js).</summary>
        public static string TaggedAssetNotSelectableNotice(string assetLocation)
        {
            return TaggedAssetNotSelectableNotice(assetLocation, false);
        }

        /// <summary>
        /// Notice when the tagged asset's room cannot be the target (same text in JS). <paramref name="roomInactive"/>:
        /// the asset sits in an inactive room (e.g. Test-WF Music Room 268, IsActive = 0), which is never listed.
        /// </summary>
        public static string TaggedAssetNotSelectableNotice(string assetLocation, bool roomInactive)
        {
            if (string.IsNullOrWhiteSpace(assetLocation))
            {
                return "The tagged asset is not in a room (company custody). Choose a room or a person as the requisition target.";
            }

            return "The tagged asset sits in " + assetLocation.Trim()
                + (roomInactive
                    ? ", which is inactive, so it can't be the requisition target. Choose a room or a person."
                    : ", which is not a room you can select as the requisition target. Choose a room or a person.");
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

        /// <summary>Last optgroup of the room list: valid rooms / classes that have no parent department.</summary>
        public const string NoDepartmentGroupLabel = "No department";

        /// <summary>
        /// A department id that may be posted as a ROOM requisition target: an active Room or Class that is marked as a
        /// requisition target and belongs to the user's organisation (null org on either side = not org-restricted).
        /// Departments and sub-departments are never room targets (Allan 2026-10-29 07:48 EAT: rooms only).
        /// </summary>
        public static bool IsValidRoomTarget(Department department, int? organizationId)
        {
            return department != null
                && department.IsActive
                && department.IsRequisitionTarget
                && IsRoomLike(department.DepartmentKind)
                && (!organizationId.HasValue || !department.OrganizationId.HasValue || department.OrganizationId.Value == organizationId.Value);
        }

        public static bool IsValidRoomTarget(DepartmentVm department)
        {
            return department != null && department.IsActive && department.IsRequisitionTarget && IsRoomLike(department.DepartmentKind);
        }

        /// <summary>Server check for a Room target (the UI list is not the only check).</summary>
        public static void EnsureValidRoomTarget(Department department, int? organizationId)
        {
            if (!IsValidRoomTarget(department, organizationId))
            {
                var name = department == null || string.IsNullOrWhiteSpace(department.Name) ? "The selected target" : department.Name.Trim();
                throw new BusinessException(name + " is not a room or class that can receive requisitions. Choose a room from the list.");
            }
        }

        /// <summary>
        /// The "A room" requisition target list, grouped for optgroups (QA A3 on 65d06e1; rooms only, Allan 2026-10-29).
        /// Only valid room targets are options (IsValidRoomTarget). Departments / sub-departments are group headings
        /// only: a room sits under its parent path ("Department &gt; Sub-department", any depth). Valid rooms with no
        /// (active) parent go in the last group, "No department". Groups with no valid room do not exist. Groups sort by
        /// department, then sub-department; rooms by name (natural, case-insensitive). Cycle-safe.
        /// </summary>
        /// <param name="departments">The active departments of the user's organisation (headings and rooms).</param>
        public static IList<RequisitionRoomGroup> GroupRoomTargets(IEnumerable<DepartmentVm> departments)
        {
            var lookup = new Dictionary<int, DepartmentVm>();
            foreach (var d in departments ?? Enumerable.Empty<DepartmentVm>())
            {
                if (d != null && d.IsActive && !lookup.ContainsKey(d.Id))
                {
                    lookup[d.Id] = d;
                }
            }

            var groupsById = new Dictionary<int, RequisitionRoomGroup>();
            RequisitionRoomGroup noDepartment = null;
            foreach (var d in lookup.Values)
            {
                if (!IsValidRoomTarget(d))
                {
                    continue;
                }

                int? groupId = d.ParentDepartmentId.HasValue && d.ParentDepartmentId.Value != d.Id && lookup.ContainsKey(d.ParentDepartmentId.Value)
                    ? d.ParentDepartmentId
                    : null;

                RequisitionRoomGroup group;
                if (!groupId.HasValue)
                {
                    if (noDepartment == null)
                    {
                        noDepartment = new RequisitionRoomGroup { Label = NoDepartmentGroupLabel, IsNoDepartment = true };
                    }

                    group = noDepartment;
                }
                else if (!groupsById.TryGetValue(groupId.Value, out group))
                {
                    var path = BuildDepartmentPath(groupId.Value, lookup);
                    group = new RequisitionRoomGroup
                    {
                        DepartmentId = groupId.Value,
                        PathParts = path,
                        Label = string.Join(DepartmentTreeHelper.PathSeparator, path.ToArray())
                    };
                    groupsById[groupId.Value] = group;
                }

                group.Options.Add(new RequisitionRoomOption
                {
                    DepartmentId = d.Id,
                    Name = (d.Name ?? string.Empty).Trim(),
                    DepartmentKind = d.DepartmentKind
                });
            }

            var ordered = groupsById.Values.ToList();
            ordered.Sort(CompareGroups);
            if (noDepartment != null)
            {
                ordered.Add(noDepartment);
            }

            foreach (var group in ordered)
            {
                var options = group.Options.ToList();
                options.Sort(CompareOptions);
                group.Options = options;
            }

            return ordered;
        }

        /// <summary>Case-insensitive compare where digit runs compare as numbers ("Year 2" &lt; "Year 10").</summary>
        public static int NaturalCompare(string left, string right)
        {
            left = left ?? string.Empty;
            right = right ?? string.Empty;
            int i = 0, j = 0;
            while (i < left.Length && j < right.Length)
            {
                if (char.IsDigit(left[i]) && char.IsDigit(right[j]))
                {
                    var si = i;
                    var sj = j;
                    while (i < left.Length && char.IsDigit(left[i])) i++;
                    while (j < right.Length && char.IsDigit(right[j])) j++;
                    var a = left.Substring(si, i - si).TrimStart('0');
                    var b = right.Substring(sj, j - sj).TrimStart('0');
                    if (a.Length != b.Length)
                    {
                        return a.Length < b.Length ? -1 : 1;
                    }

                    var c = string.CompareOrdinal(a, b);
                    if (c != 0)
                    {
                        return c;
                    }

                    continue;
                }

                var ci = string.Compare(left[i].ToString(), right[j].ToString(), StringComparison.OrdinalIgnoreCase);
                if (ci != 0)
                {
                    return ci;
                }

                i++;
                j++;
            }

            var rest = (left.Length - i).CompareTo(right.Length - j);
            return rest != 0 ? rest : string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static List<string> BuildDepartmentPath(int departmentId, IDictionary<int, DepartmentVm> lookup)
        {
            var names = new List<string>();
            var seen = new HashSet<int>();
            int? current = departmentId;
            while (current.HasValue && seen.Add(current.Value) && names.Count < 32)
            {
                DepartmentVm node;
                if (!lookup.TryGetValue(current.Value, out node) || node == null)
                {
                    break;
                }

                var name = (node.Name ?? string.Empty).Trim();
                if (name.Length > 0)
                {
                    names.Insert(0, name);
                }

                current = node.ParentDepartmentId;
            }

            return names;
        }

        private static int CompareGroups(RequisitionRoomGroup left, RequisitionRoomGroup right)
        {
            var a = left.PathParts;
            var b = right.PathParts;
            for (var k = 0; k < a.Count && k < b.Count; k++)
            {
                var c = NaturalCompare(a[k], b[k]);
                if (c != 0)
                {
                    return c;
                }
            }

            var len = a.Count.CompareTo(b.Count);
            return len != 0 ? len : left.DepartmentId.CompareTo(right.DepartmentId);
        }

        private static int CompareOptions(RequisitionRoomOption left, RequisitionRoomOption right)
        {
            var c = NaturalCompare(left.Name, right.Name);
            return c != 0 ? c : left.DepartmentId.CompareTo(right.DepartmentId);
        }

        private static bool SameUser(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left)
                && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>One optgroup of the requisition room list (see RequisitionTargetRules.GroupRoomTargets).</summary>
    public sealed class RequisitionRoomGroup
    {
        public RequisitionRoomGroup()
        {
            PathParts = new List<string>();
            Options = new List<RequisitionRoomOption>();
        }

        /// <summary>"Department &gt; Sub-department" or "No department".</summary>
        public string Label { get; set; }

        /// <summary>The department heading the group (0 for "No department").</summary>
        public int DepartmentId { get; set; }

        public bool IsNoDepartment { get; set; }

        public IList<string> PathParts { get; set; }

        public IList<RequisitionRoomOption> Options { get; set; }
    }

    public sealed class RequisitionRoomOption
    {
        public int DepartmentId { get; set; }

        public string Name { get; set; }

        public DepartmentKind DepartmentKind { get; set; }
    }

    public enum TaggedAssetTargetDecision
    {
        /// <summary>Nothing to do (no target chosen and the asset has no department, or the target already matches).</summary>
        Keep = 0,

        /// <summary>No target chosen yet: set the target to Room = the tagged asset's room / department.</summary>
        PrefillRoom = 1,

        /// <summary>A different room / a person was already chosen: keep it and show TaggedAssetKeptNotice.</summary>
        KeepWithNotice = 2,

        /// <summary>No target chosen, but the asset's room is not a selectable room: select nothing, show TaggedAssetNotSelectableNotice.</summary>
        NotSelectableNotice = 3
    }
}
