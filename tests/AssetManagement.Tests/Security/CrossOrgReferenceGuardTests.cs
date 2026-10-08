using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    /// <summary>
    /// Pure same-org guard rules (OrgReferenceGuard) and the guarded asset serializer.
    /// Regression for 16 Sep 2026: Test-WF (org 13) assets stored org 11 role ids 45/41 in CSV stage columns.
    /// </summary>
    [TestFixture]
    public class CrossOrgReferenceGuardTests
    {
        private static readonly HashSet<int> Org13Roles = new HashSet<int> { 130, 131, 132 };
        private static readonly HashSet<string> Org13Users = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "u13-a", "u13-b" };

        private static bool RoleIn13(int id) { return Org13Roles.Contains(id); }

        private static bool UserIn13(string id) { return Org13Users.Contains(id); }

        private static IList<int> Roles(string csv) { return ApprovalWorkflowSettingsHelper.ParseStageRoleIds(csv); }

        [Test]
        public void GuardStageCsv_DropsForeignRoles_FromIncident()
        {
            var result = OrgReferenceGuard.GuardStageCsv("45,41", null, RoleIn13, UserIn13);

            Assert.IsTrue(result.HasDrops);
            CollectionAssert.AreEqual(new[] { 45, 41 }, result.DroppedRoleIds);
            Assert.AreEqual(0, result.RoleIds.Count);
            Assert.IsTrue(string.IsNullOrEmpty(result.RoleCsv));
            StringAssert.Contains("roles 45,41", result.DescribeDrops());
        }

        [Test]
        public void GuardStageCsv_KeepsOwnRoles_AndDropsForeignStageWithItsPerson()
        {
            var result = OrgReferenceGuard.GuardStageCsv("130,45,131", "u13-a,u11-x,u13-b", RoleIn13, UserIn13);

            CollectionAssert.AreEqual(new[] { 130, 131 }, result.RoleIds);
            CollectionAssert.AreEqual(new[] { "u13-a", "u13-b" }, result.UserIds);
            CollectionAssert.AreEqual(new[] { 45 }, result.DroppedRoleIds);
            CollectionAssert.AreEqual(new[] { "u11-x" }, result.DroppedUserIds);
            CollectionAssert.AreEqual(new[] { 130, 131 }, Roles(result.RoleCsv));
        }

        [Test]
        public void GuardStageCsv_ForeignPersonOnOwnRole_BecomesAnyUserInRole()
        {
            var result = OrgReferenceGuard.GuardStageCsv("130,131", "u11-x,u13-b", RoleIn13, UserIn13);

            CollectionAssert.AreEqual(new[] { 130, 131 }, result.RoleIds);
            Assert.IsNull(result.UserIds[0]);
            Assert.AreEqual("u13-b", result.UserIds[1]);
            CollectionAssert.AreEqual(new[] { "u11-x" }, result.DroppedUserIds);
            Assert.AreEqual(0, result.DroppedRoleIds.Count);
        }

        [Test]
        public void GuardStageCsv_CleanValues_HaveNoDrops()
        {
            var result = OrgReferenceGuard.GuardStageCsv("130,131", ",u13-b", RoleIn13, UserIn13);

            Assert.IsFalse(result.HasDrops);
            CollectionAssert.AreEqual(new[] { 130, 131 }, result.RoleIds);
            Assert.AreEqual(string.Empty, result.DescribeDrops());
        }

        [Test]
        public void FindForeignStages_ReportsEachBadStageWithProcessAndStageNumber()
        {
            var processes = new List<ApprovalProcessSettingsVm>
            {
                Process(ApprovalProcessCodes.Transfer, "Transfer", Stage(130, null), Stage(45, null)),
                Process(ApprovalProcessCodes.Disposal, "Disposal", Stage(131, "u11-x"))
            };

            var messages = OrgReferenceGuard.FindForeignStages(processes, RoleIn13, UserIn13);

            Assert.AreEqual(2, messages.Count);
            StringAssert.StartsWith("Transfer Stage 2:", messages[0]);
            StringAssert.Contains("role #45", messages[0]);
            StringAssert.StartsWith("Disposal Stage 1:", messages[1]);
            StringAssert.Contains("not a user in this company", messages[1]);
        }

        [Test]
        public void FindForeignStages_NullUserCheck_OnlyChecksRoles()
        {
            var processes = new List<ApprovalProcessSettingsVm> { Process(ApprovalProcessCodes.Transfer, "Transfer", Stage(130, "u11-x")) };

            Assert.AreEqual(0, OrgReferenceGuard.FindForeignStages(processes, RoleIn13, null).Count);
        }

        [Test]
        public void DropForeignStages_RemovesForeignStages_AndClearsForeignPersons()
        {
            var transfer = Process(ApprovalProcessCodes.Transfer, "Transfer", Stage(45, "u11-x"), Stage(130, "u11-y"), Stage(131, "u13-a"));
            var disposal = Process(ApprovalProcessCodes.Disposal, "Disposal", Stage(132, null));

            var dropped = OrgReferenceGuard.DropForeignStages(new[] { transfer, disposal }, RoleIn13, UserIn13);

            Assert.AreEqual(1, dropped.Count);
            Assert.IsTrue(dropped.ContainsKey(ApprovalProcessCodes.Transfer));
            CollectionAssert.AreEqual(new[] { 45 }, dropped[ApprovalProcessCodes.Transfer].DroppedRoleIds);
            CollectionAssert.AreEquivalent(new[] { "u11-x", "u11-y" }, dropped[ApprovalProcessCodes.Transfer].DroppedUserIds);
            Assert.AreEqual(2, transfer.Stages.Count);
            Assert.AreEqual(130, transfer.Stages[0].RoleId);
            Assert.IsNull(transfer.Stages[0].UserId);
            Assert.AreEqual("u13-a", transfer.Stages[1].UserId);
            Assert.AreEqual(1, disposal.Stages.Count);
        }

        [Test]
        public void ApplyToAsset_NeverSerializesForeignRoleIds()
        {
            var asset = new Asset { OrganizationId = 13 };
            var processes = new List<ApprovalProcessSettingsVm>
            {
                Process(ApprovalProcessCodes.Transfer, "Transfer", Stage(45, null), Stage(130, null)),
                Process(ApprovalProcessCodes.Disposal, "Disposal", Stage(41, null))
            };

            var dropped = AssetApprovalSettingsHelper.ApplyToAsset(asset, processes, RoleIn13, UserIn13);

            CollectionAssert.AreEqual(new[] { 130 }, Roles(asset.TransferApprovalStageRoleIds));
            Assert.AreEqual(0, Roles(asset.DisposalApprovalStageRoleIds).Count);
            Assert.AreEqual(2, dropped.Count);
        }

        [Test]
        public void ApplyToAsset_RequiresARoleCheck()
        {
            var asset = new Asset();
            Assert.Throws<ArgumentNullException>(() =>
                AssetApprovalSettingsHelper.ApplyToAsset(asset, new List<ApprovalProcessSettingsVm>(), null, null));
        }

        [Test]
        public void MapStageCsv_MapsThroughRoleMap_DropsUnmapped_AndNeverCopiesPersons()
        {
            var map = new Dictionary<int, int> { { 45, 145 }, { 41, 141 } };

            var result = OrgReferenceGuard.MapStageCsv("45,99,41", "u11-a,,u11-b", map);

            CollectionAssert.AreEqual(new[] { 145, 141 }, result.RoleIds);
            CollectionAssert.AreEqual(new[] { 99 }, result.DroppedRoleIds);
            CollectionAssert.AreEquivalent(new[] { "u11-a", "u11-b" }, result.DroppedUserIds);
            Assert.IsTrue(result.UserIds.All(x => x == null));
        }

        [Test]
        public void MapCopiedApprovalSetting_MapsStageRoles_AndReportsUnmapped()
        {
            var map = new Dictionary<int, int> { { 45, 145 } };
            var dropped = new List<string>();

            var value = OrgReferenceGuard.MapCopiedApprovalSetting(
                ApprovalProcessCodes.GetStageRoleIdsSettingKey(ApprovalProcessCodes.Transfer), "45,41", map, dropped);

            CollectionAssert.AreEqual(new[] { 145 }, Roles(value));
            CollectionAssert.AreEqual(new[] { "role #41" }, dropped);
        }

        [Test]
        public void MapCopiedApprovalSetting_AllUnmapped_SkipsKeySoDefaultsApply()
        {
            var dropped = new List<string>();

            var value = OrgReferenceGuard.MapCopiedApprovalSetting(
                ApprovalProcessCodes.GetStageRoleIdsSettingKey(ApprovalProcessCodes.Disposal), "41", new Dictionary<int, int>(), dropped);

            Assert.IsNull(value);
            CollectionAssert.AreEqual(new[] { "role #41" }, dropped);
        }

        [Test]
        public void MapCopiedApprovalSetting_StagePersons_AreBlankedAndReported()
        {
            var dropped = new List<string>();

            var value = OrgReferenceGuard.MapCopiedApprovalSetting(
                ApprovalProcessCodes.GetStageUserIdsSettingKey(ApprovalProcessCodes.Purchase), "u11-a,,u11-b", null, dropped);

            Assert.AreEqual(string.Empty, value);
            CollectionAssert.AreEqual(new[] { "person u11-a", "person u11-b" }, dropped);
        }

        [Test]
        public void MapCopiedApprovalSetting_OtherApprovalKeys_AreLeftToDefaults()
        {
            Assert.IsNull(OrgReferenceGuard.MapCopiedApprovalSetting(
                ApprovalProcessCodes.GetEnabledSettingKey(ApprovalProcessCodes.Transfer), "true", new Dictionary<int, int>(), new List<string>()));
        }

        [Test]
        public void BuildRoleMapByName_MatchesCaseAndSpaceInsensitively_LowestTargetIdWins()
        {
            var source = new[] { Pair(45, "Department Head"), Pair(41, "Company Admin"), Pair(7, "Gone Role") };
            var target = new[] { Pair(200, "company  admin"), Pair(150, "DEPARTMENT HEAD"), Pair(120, "Company Admin") };

            var map = OrgReferenceGuard.BuildRoleMapByName(source, target);

            Assert.AreEqual(150, map[45]);
            Assert.AreEqual(120, map[41]);
            Assert.IsFalse(map.ContainsKey(7));
        }

        private static KeyValuePair<int, string> Pair(int id, string name)
        {
            return new KeyValuePair<int, string>(id, name);
        }

        private static ApprovalStageSettingsVm Stage(int roleId, string userId)
        {
            return new ApprovalStageSettingsVm { RoleId = roleId, UserId = userId };
        }

        private static ApprovalProcessSettingsVm Process(string code, string name, params ApprovalStageSettingsVm[] stages)
        {
            return new ApprovalProcessSettingsVm { ProcessCode = code, DisplayName = name, RequiresApproval = true, Stages = stages.ToList() };
        }
    }
}
