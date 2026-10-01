using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Departments
{
    [TestFixture]
    public class DepartmentRequisitionApprovalSettingsHelperTests
    {
        [Test]
        public void GetDepartmentRequisitionConfiguration_UsesCustomStagesWhenEnabled()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new SystemSetting
            {
                SettingKey = ApprovalProcessCodes.GetEnabledSettingKey(ApprovalProcessCodes.Purchase),
                SettingValue = "true",
                IsActive = true
            });
            unitOfWork.Seed(new SystemSetting
            {
                SettingKey = ApprovalProcessCodes.GetStageRoleIdsSettingKey(ApprovalProcessCodes.Purchase),
                SettingValue = "9,10",
                IsActive = true
            });

            var department = new Department
            {
                Id = 3,
                UseCustomRequisitionApproval = true,
                RequisitionApprovalStageRoleIds = "5,6,7"
            };

            var config = ApprovalWorkflowHelper.GetDepartmentRequisitionConfiguration(unitOfWork, department);

            Assert.IsTrue(config.RequiresApproval);
            Assert.AreEqual(new[] { 5, 6, 7 }, config.StageRoleIds.ToArray());
        }

        [Test]
        public void GetDepartmentRequisitionConfiguration_FallsBackToOrgDefaultsWhenCustomDisabled()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new SystemSetting
            {
                SettingKey = ApprovalProcessCodes.GetEnabledSettingKey(ApprovalProcessCodes.Purchase),
                SettingValue = "true",
                IsActive = true
            });
            unitOfWork.Seed(new SystemSetting
            {
                SettingKey = ApprovalProcessCodes.GetStageRoleIdsSettingKey(ApprovalProcessCodes.Purchase),
                SettingValue = "9,10",
                IsActive = true
            });

            var department = new Department
            {
                Id = 3,
                UseCustomRequisitionApproval = false,
                RequisitionApprovalStageRoleIds = "5,6,7"
            };

            var config = ApprovalWorkflowHelper.GetDepartmentRequisitionConfiguration(unitOfWork, department);

            Assert.AreEqual(new[] { 9, 10 }, config.StageRoleIds.ToArray());
        }

        [Test]
        public void ApplyToDepartment_PersistsCustomStageRoleIds()
        {
            var entity = new Department { Id = 1, DepartmentKind = DepartmentKind.Room };
            var model = new DepartmentVm
            {
                UseCustomRequisitionApproval = true,
                RequisitionApprovalProcesses = new List<ApprovalProcessSettingsVm>
                {
                    new ApprovalProcessSettingsVm
                    {
                        ProcessCode = ApprovalProcessCodes.Purchase,
                        RequiresApproval = true,
                        Stages = new List<ApprovalStageSettingsVm>
                        {
                            new ApprovalStageSettingsVm { RoleId = 4 },
                            new ApprovalStageSettingsVm { RoleId = 8 }
                        }
                    }
                }
            };

            DepartmentRequisitionApprovalSettingsHelper.ApplyToDepartment(entity, model);

            Assert.IsTrue(entity.UseCustomRequisitionApproval);
            Assert.AreEqual("4,8", entity.RequisitionApprovalStageRoleIds);
        }

        [Test]
        public void ValidateCustomApproval_RequiresRoleWhenCustomEnabled()
        {
            var model = new DepartmentVm
            {
                UseCustomRequisitionApproval = true,
                RequisitionApprovalProcesses = new List<ApprovalProcessSettingsVm>
                {
                    new ApprovalProcessSettingsVm
                    {
                        ProcessCode = ApprovalProcessCodes.Purchase,
                        RequiresApproval = true,
                        Stages = new List<ApprovalStageSettingsVm>
                        {
                            new ApprovalStageSettingsVm()
                        }
                    }
                }
            };

            var errors = new Dictionary<string, string>();
            DepartmentRequisitionApprovalSettingsHelper.ValidateCustomApproval(
                model,
                (key, message) => errors[key] = message);

            Assert.IsTrue(errors.Count > 0);
        }
    }
}
