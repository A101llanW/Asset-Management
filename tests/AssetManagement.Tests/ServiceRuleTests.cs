using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests
{
    [TestFixture]
    public class ServiceRuleTests
    {
        [Test]
        public void AssignmentService_TemporaryAssignmentRequiresExpectedReturnDate()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Asset
            {
                Id = 1,
                AssetTag = "AST-001",
                AssetName = "Laptop",
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                DepartmentId = 1,
                Currency = "USD",
                AcquisitionCost = 1000,
                CurrentStatus = AssetStatus.InStore,
                PurchaseDate = DateTime.UtcNow,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = DateTime.UtcNow,
                UsefulLifeMonths = 36,
                CreatedAt = DateTime.UtcNow
            });

            var service = TestServiceFactory.CreateAssignmentService(unitOfWork);

            Assert.Throws<BusinessException>(() => service.Assign(new AssetAssignmentVm
            {
                AssetId = 1,
                AssignmentType = "Temporary",
                ToUserId = "user-1",
                AssignedDate = DateTime.UtcNow
            }));
        }

        [Test]
        public void UsefulLifeResolver_PrefersAssetTypeOverrideOverCategoryDefault()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new AssetCategory
            {
                Id = 1,
                Name = "Furniture",
                DefaultUsefulLifeMonths = 120,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            unitOfWork.Seed(new AssetType
            {
                Id = 1,
                AssetCategoryId = 1,
                Name = "Laptop",
                UsefulLifeMonths = 36,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });

            Assert.AreEqual(36, UsefulLifeResolver.Resolve(unitOfWork, 1, 1));
            Assert.AreEqual("asset type", UsefulLifeResolver.DescribeSource(unitOfWork, 1, 1));
        }

        [Test]
        public void UsefulLifeResolver_ReturnsNullWhenCategoryAndTypeAreNotConfigured()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new AssetCategory
            {
                Id = 2,
                Name = "General",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            unitOfWork.Seed(new AssetType
            {
                Id = 2,
                AssetCategoryId = 2,
                Name = "Misc",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });

            Assert.IsNull(UsefulLifeResolver.Resolve(unitOfWork, 2, 2));
            Assert.AreEqual("not configured", UsefulLifeResolver.DescribeSource(unitOfWork, 2, 2));
        }

        [Test]
        public void AssetService_CreateGeneratesAssetTagWhenOmitted()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department
            {
                Id = 1,
                Name = "Information Technology",
                Code = "IT",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            unitOfWork.Seed(new AssetType
            {
                Id = 1,
                Name = "Laptop",
                AssetCategoryId = 1,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            unitOfWork.Seed(new Asset
            {
                Id = 3,
                AssetTag = "IT-LTP-001",
                AssetName = "Existing Laptop",
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                DepartmentId = 1,
                Currency = "USD",
                AcquisitionCost = 700,
                CurrentStatus = AssetStatus.InStore,
                PurchaseDate = DateTime.UtcNow,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = DateTime.UtcNow,
                UsefulLifeMonths = 36,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });

            var service = TestServiceFactory.CreateAssetService(unitOfWork);
            var assetId = service.Create(new AssetCreateVm
            {
                AssetName = "New Laptop",
                CategoryId = 1,
                AssetTypeId = 1,
                Brand = "Dell",
                Model = "Latitude",
                PurchaseDate = DateTime.UtcNow,
                AcquisitionCost = 800,
                Currency = "USD",
                SupplierId = 1,
                DepartmentId = 1,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = DateTime.UtcNow
            });

            var created = unitOfWork.Repository<Asset>().GetById(assetId);
            Assert.AreEqual("IT-LTP-002", created.AssetTag);
            Assert.IsNull(created.UsefulLifeMonths);
        }

        [Test]
        public void AssetService_CreateAllowsOptionalDepartmentAndSupplier()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new AssetCategory
            {
                Id = 1,
                Name = "IT Equipment",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            unitOfWork.Seed(new AssetType
            {
                Id = 1,
                Name = "Laptop",
                AssetCategoryId = 1,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });

            var service = TestServiceFactory.CreateAssetService(unitOfWork);
            var assetId = service.Create(new AssetCreateVm
            {
                AssetName = "Organization Custody Asset",
                AssetTag = "ORG-001",
                CategoryId = 1,
                AssetTypeId = 1,
                Brand = "Generic",
                Model = "Model",
                PurchaseDate = DateTime.UtcNow,
                AcquisitionCost = 500,
                Currency = "USD",
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = DateTime.UtcNow
            });

            var created = unitOfWork.Repository<Asset>().GetById(assetId);
            Assert.IsNull(created.DepartmentId);
            Assert.IsNull(created.SupplierId);
        }

        [Test]
        public void AssetService_CreateRejectsDuplicateAssetTag()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Asset
            {
                Id = 3,
                AssetTag = "AST-100",
                AssetName = "Router",
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                DepartmentId = 1,
                Currency = "USD",
                AcquisitionCost = 700,
                CurrentStatus = AssetStatus.InStore,
                PurchaseDate = DateTime.UtcNow,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = DateTime.UtcNow,
                UsefulLifeMonths = 36,
                CreatedAt = DateTime.UtcNow
            });

            var service = TestServiceFactory.CreateAssetService(unitOfWork);
            Assert.Throws<BusinessException>(() => service.Create(new AssetCreateVm
            {
                AssetName = "Router 2",
                AssetTag = "AST-100",
                CategoryId = 1,
                AssetTypeId = 1,
                Brand = "Cisco",
                Model = "RV",
                PurchaseDate = DateTime.UtcNow,
                AcquisitionCost = 800,
                Currency = "USD",
                SupplierId = 1,
                DepartmentId = 1,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = DateTime.UtcNow
            }));
        }

        [Test]
        public void AssignmentService_RequiresTargetUserOrDepartment()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 10, assetTag: "AST-010", status: AssetStatus.InStore));

            var service = TestServiceFactory.CreateAssignmentService(unitOfWork);
            Assert.Throws<BusinessException>(() => service.Assign(new AssetAssignmentVm
            {
                AssetId = 10,
                AssignmentType = "Permanent",
                AssignedDate = DateTime.UtcNow
            }));
        }

        [Test]
        public void AssignmentService_RejectsWhenAlreadyAssigned()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 12, assetTag: "AST-012", status: AssetStatus.Assigned, custodianId: "user-a", departmentId: 1));
            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "user-b", DepartmentId = 1, IsActive = true, FirstName = "B", LastName = "User" });

            var service = TestServiceFactory.CreateAssignmentService(unitOfWork, users);
            var ex = Assert.Throws<BusinessException>(() => service.Assign(new AssetAssignmentVm
            {
                AssetId = 12,
                ToUserId = "user-b",
                ToDepartmentId = 1,
                AssignmentType = "Permanent",
                AssignedDate = DateTime.UtcNow
            }));

            StringAssert.Contains("already assigned", ex.Message);
            StringAssert.Contains("Transfer", ex.Message);
            StringAssert.Contains("Return", ex.Message);
        }

        [Test]
        public void AssignmentService_CustodyEventIncludesPreviousCustodian()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 11, assetTag: "AST-011", status: AssetStatus.InStore, custodianId: "user-old", departmentId: 2));
            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "user-new", DepartmentId = 4, IsActive = true, FirstName = "New", LastName = "Custodian" });

            var service = TestServiceFactory.CreateAssignmentService(unitOfWork, users);
            service.Assign(new AssetAssignmentVm
            {
                AssetId = 11,
                ToUserId = "user-new",
                ToDepartmentId = 4,
                AssignmentType = "Permanent",
                AssignedDate = DateTime.UtcNow
            });

            var custodyEvent = unitOfWork.Repository<AssetCustodyEvent>().Find(x => x.AssetId == 11).Single();
            Assert.AreEqual("user-old", custodyEvent.FromUserId);
            Assert.AreEqual("user-new", custodyEvent.ToUserId);
            Assert.AreEqual(2, custodyEvent.FromDepartmentId);
            Assert.AreEqual(4, custodyEvent.ToDepartmentId);
        }

        [Test]
        public void TransferService_RejectsTransferWhenAssetIsNotAssigned()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 20, assetTag: "AST-020", status: AssetStatus.InStore));

            var service = TestServiceFactory.CreateTransferService(unitOfWork);
            Assert.Throws<BusinessException>(() => service.Transfer(new AssetTransferVm
            {
                AssetId = 20,
                ToUserId = "user-2",
                ToDepartmentId = 2
            }, "requester-1"));
        }

        [Test]
        public void TransferService_RejectsMismatchedFromUser()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 21, assetTag: "AST-021", status: AssetStatus.Assigned, custodianId: "user-a"));

            var service = TestServiceFactory.CreateTransferService(unitOfWork);
            Assert.Throws<BusinessException>(() => service.Transfer(new AssetTransferVm
            {
                AssetId = 21,
                FromUserId = "user-wrong",
                ToUserId = "user-b"
            }, "requester-1"));
        }

        [Test]
        public void ReturnService_RejectsReturnWhenAssetIsNotAssigned()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 30, assetTag: "AST-030", status: AssetStatus.InStore));

            var service = TestServiceFactory.CreateReturnService(unitOfWork);
            Assert.Throws<BusinessException>(() => service.ReturnAsset(new AssetReturnVm
            {
                AssetId = 30,
                ReturnedById = "user-1",
                ReceivedById = "receiver-1",
                ReturnDate = DateTime.UtcNow
            }));
        }

        [Test]
        public void ReturnService_UsesCurrentCustodianWhenReturnedByNotProvided()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 31, assetTag: "AST-031", status: AssetStatus.Assigned, custodianId: "custodian-1"));
            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "receiver-1", DepartmentId = 1, IsActive = true, FirstName = "Receiver", LastName = "One" });

            var service = TestServiceFactory.CreateReturnService(unitOfWork, users);
            service.ReturnAsset(new AssetReturnVm
            {
                AssetId = 31,
                ReceivedById = "receiver-1",
                ReturnDate = DateTime.UtcNow,
                ReturnCondition = "Good"
            });

            var returnRecord = unitOfWork.Repository<AssetReturn>().Find(x => x.AssetId == 31).Single();
            var custodyEvent = unitOfWork.Repository<AssetCustodyEvent>().Find(x => x.AssetId == 31).Single();

            Assert.AreEqual("custodian-1", returnRecord.ReturnedById);
            Assert.AreEqual("custodian-1", custodyEvent.FromUserId);
        }

        [Test]
        public void MaintenanceService_RejectsWhenOpenTicketExists()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 40, assetTag: "AST-040", status: AssetStatus.Assigned));
            unitOfWork.Seed(new AssetMaintenanceRecord
            {
                AssetId = 40,
                MaintenanceTicketNumber = "MT-OPEN-1",
                ReportedIssue = "Battery issue",
                MaintenanceType = MaintenanceType.Corrective,
                ServiceDate = DateTime.UtcNow.AddDays(-1),
                Status = MaintenanceStatus.Open,
                CreatedAt = DateTime.UtcNow
            });

            var service = new MaintenanceService(unitOfWork);
            Assert.Throws<BusinessException>(() => service.Create(new AssetMaintenanceVm
            {
                AssetId = 40,
                MaintenanceType = MaintenanceType.Corrective.ToString(),
                ReportedIssue = "Another issue"
            }));
        }

        [Test]
        public void MaintenanceService_Complete_KeepInStoreClosesTicketAndReturnsAssetToStore()
        {
            var unitOfWork = new FakeUnitOfWork();
            const int ticketId = 1;
            unitOfWork.Seed(BuildAsset(id: 41, assetTag: "AST-041", status: AssetStatus.UnderMaintenance, custodianId: "user-1"));
            unitOfWork.Seed(new AssetMaintenanceRecord
            {
                Id = ticketId,
                AssetId = 41,
                MaintenanceTicketNumber = "MT-CLOSE-1",
                ReportedIssue = "Screen repair",
                MaintenanceType = MaintenanceType.Corrective,
                ServiceDate = DateTime.UtcNow.AddDays(-3),
                Status = MaintenanceStatus.InProgress,
                CreatedAt = DateTime.UtcNow
            });

            var service = TestServiceFactory.CreateMaintenanceService(unitOfWork);
            service.Complete(new MaintenanceCompleteVm
            {
                Id = ticketId,
                AssetId = 41,
                CompletionDate = DateTime.UtcNow.Date,
                Outcome = "Screen replaced",
                Disposition = MaintenanceDisposition.KeepInStore.ToString(),
                ConditionAfter = AssetCondition.Good.ToString()
            });

            var asset = unitOfWork.Repository<Asset>().GetById(41);
            var ticket = unitOfWork.Repository<AssetMaintenanceRecord>().GetById(ticketId);
            var custodyEvents = unitOfWork.Repository<AssetCustodyEvent>().Find(x => x.AssetId == 41).ToList();

            Assert.AreEqual(AssetStatus.InStore, asset.CurrentStatus);
            Assert.IsNull(asset.CurrentCustodianId);
            Assert.AreEqual(MaintenanceStatus.Completed, ticket.Status);
            Assert.IsNotNull(ticket.CompletionDate);
            Assert.AreEqual("Screen replaced", ticket.Outcome);
            Assert.AreEqual(1, custodyEvents.Count);
            Assert.AreEqual(CustodyActionType.Recovered, custodyEvents[0].ActionType);
        }

        [Test]
        public void MaintenanceService_Complete_ReturnToPreviousOwnerReassignsAsset()
        {
            var unitOfWork = new FakeUnitOfWork();
            var users = new FakeUserService();
            users.Seed(new UserVm
            {
                Id = "user-prev",
                FirstName = "Pat",
                LastName = "Owner",
                Email = "pat@test",
                DepartmentId = 1,
                IsActive = true
            });

            const int ticketId = 2;
            unitOfWork.Seed(BuildAsset(id: 42, assetTag: "AST-042", status: AssetStatus.UnderMaintenance, custodianId: "user-prev", departmentId: 1));
            unitOfWork.Seed(new AssetMaintenanceRecord
            {
                Id = ticketId,
                AssetId = 42,
                MaintenanceTicketNumber = "MT-CLOSE-2",
                ReportedIssue = "Keyboard repair",
                MaintenanceType = MaintenanceType.Corrective,
                ServiceDate = DateTime.UtcNow.AddDays(-2),
                Status = MaintenanceStatus.Open,
                CreatedAt = DateTime.UtcNow
            });

            var service = TestServiceFactory.CreateMaintenanceService(unitOfWork, users);
            service.Complete(new MaintenanceCompleteVm
            {
                Id = ticketId,
                AssetId = 42,
                CompletionDate = DateTime.UtcNow.Date,
                Outcome = "Keyboard replaced",
                Disposition = MaintenanceDisposition.ReturnToPreviousOwner.ToString(),
                ConditionAfter = AssetCondition.Good.ToString()
            });

            var asset = unitOfWork.Repository<Asset>().GetById(42);
            var ticket = unitOfWork.Repository<AssetMaintenanceRecord>().GetById(ticketId);
            var assignments = unitOfWork.Repository<AssetAssignment>().Find(x => x.AssetId == 42).ToList();

            Assert.AreEqual(AssetStatus.Assigned, asset.CurrentStatus);
            Assert.AreEqual("user-prev", asset.CurrentCustodianId);
            Assert.AreEqual(MaintenanceStatus.Completed, ticket.Status);
            Assert.AreEqual(1, assignments.Count);
            Assert.AreEqual("user-prev", assignments[0].ToUserId);
        }

        [Test]
        public void IncidentService_LostIncidentMarksAssetAsLost()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 50, assetTag: "AST-050", status: AssetStatus.Assigned, custodianId: "user-1"));

            var service = new IncidentService(unitOfWork);
            service.Create(new AssetIncidentVm
            {
                AssetId = 50,
                IncidentType = IncidentType.Lost.ToString(),
                Severity = IncidentSeverity.High.ToString(),
                IncidentDate = DateTime.UtcNow.AddHours(-1),
                Description = "Lost while in transit"
            });

            var asset = unitOfWork.Repository<Asset>().GetById(50);
            Assert.AreEqual(AssetStatus.Lost, asset.CurrentStatus);
        }

        [Test]
        public void AssetCustodyRules_AssignAndTransferVisibility()
        {
            Assert.IsTrue(AssetCustodyRules.CanAssign(AssetStatus.InStore));
            Assert.IsTrue(AssetCustodyRules.CanAssign(AssetStatus.Returned));
            Assert.IsFalse(AssetCustodyRules.CanAssign(AssetStatus.Assigned));
            Assert.IsFalse(AssetCustodyRules.CanAssign(AssetStatus.Lost));
            Assert.IsFalse(AssetCustodyRules.CanAssign(AssetStatus.Damaged));
            Assert.IsFalse(AssetCustodyRules.CanAssign(AssetStatus.UnderMaintenance));

            Assert.IsTrue(AssetCustodyRules.CanTransfer(AssetStatus.Assigned));
            Assert.IsFalse(AssetCustodyRules.CanTransfer(AssetStatus.InStore));
            Assert.IsFalse(AssetCustodyRules.CanTransfer(AssetStatus.Lost));
        }

        [Test]
        public void AssignmentService_RejectsLostAssetAssignment()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 90, assetTag: "AST-090", status: AssetStatus.Lost));

            var service = TestServiceFactory.CreateAssignmentService(unitOfWork);
            var ex = Assert.Throws<BusinessException>(() => service.Assign(new AssetAssignmentVm
            {
                AssetId = 90,
                ToUserId = "user-1",
                ToDepartmentId = 1,
                AssignmentType = AssignmentType.Permanent.ToString(),
                AssignedDate = DateTime.UtcNow,
                ConditionBeforeHandover = AssetCondition.Good.ToString()
            }));

            StringAssert.Contains("cannot be assigned", ex.Message);
        }

        [Test]
        public void AuditDisplayLabelHelper_FormatsCommonAuditActionsForUsers()
        {
            Assert.AreEqual("Incident reported", AuditDisplayLabelHelper.FormatAction("Incidents.Create"));
            Assert.AreEqual("Maintenance ticket opened", AuditDisplayLabelHelper.FormatAction("Maintenance.Create"));
            Assert.AreEqual("Insurance claim filed", AuditDisplayLabelHelper.FormatAction("Claims.Create"));
            Assert.AreEqual("Incident", AuditDisplayLabelHelper.FormatEntityType("AssetIncident"));
        }

        [Test]
        public void IncidentService_Create_RejectsDuplicateSubmissionWithinWindow()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 50, assetTag: "AST-050", status: AssetStatus.Assigned, custodianId: "user-1"));
            unitOfWork.Seed(new AssetIncident
            {
                Id = 1999,
                AssetId = 50,
                IncidentNumber = "INC-1999",
                IncidentType = IncidentType.Damaged,
                IncidentDate = DateTime.UtcNow.AddHours(-1),
                Description = "Cracked screen",
                Severity = IncidentSeverity.Medium,
                ResolutionStatus = "Open",
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
                IsActive = true
            });

            var service = new IncidentService(unitOfWork);
            Assert.Throws<BusinessException>(() => service.Create(new AssetIncidentVm
            {
                AssetId = 50,
                IncidentType = IncidentType.Damaged.ToString(),
                Severity = IncidentSeverity.Medium.ToString(),
                IncidentDate = DateTime.UtcNow.AddHours(-1),
                Description = "Cracked screen"
            }));
        }

        [Test]
        public void IncidentService_UpdateResolutionStatus_RejectsInvalidValue()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 50, assetTag: "AST-050", status: AssetStatus.Damaged));
            unitOfWork.Seed(new AssetIncident
            {
                Id = 2000,
                AssetId = 50,
                IncidentNumber = "INC-2000",
                IncidentType = IncidentType.Damaged,
                IncidentDate = DateTime.UtcNow.AddDays(-1),
                Description = "Cracked screen",
                Severity = IncidentSeverity.Medium,
                ResolutionStatus = "Open",
                CreatedAt = DateTime.UtcNow
            });

            var service = new IncidentService(unitOfWork);
            Assert.Throws<BusinessException>(() => service.UpdateResolutionStatus(2000, "In progress"));
        }

        [Test]
        public void IncidentService_UpdateResolutionStatus_NormalizesSelectedOption()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 50, assetTag: "AST-050", status: AssetStatus.Damaged));
            unitOfWork.Seed(new AssetIncident
            {
                Id = 2001,
                AssetId = 50,
                IncidentNumber = "INC-2001",
                IncidentType = IncidentType.Damaged,
                IncidentDate = DateTime.UtcNow.AddDays(-1),
                Description = "Cracked screen",
                Severity = IncidentSeverity.Medium,
                ResolutionStatus = "Open",
                CreatedAt = DateTime.UtcNow
            });

            var service = new IncidentService(unitOfWork);
            service.UpdateResolutionStatus(2001, "under review");

            var incident = unitOfWork.Repository<AssetIncident>().GetById(2001);
            Assert.AreEqual("Under review", incident.ResolutionStatus);
        }

        [Test]
        public void IncidentService_ClosingIncident_BlocksWhenActiveClaimExists()
        {
            var unitOfWork = new FakeUnitOfWork();
            var asset = BuildAsset(id: 70, assetTag: "AST-070", status: AssetStatus.Damaged);
            unitOfWork.Seed(asset);
            unitOfWork.Seed(new AssetIncident
            {
                Id = 3000,
                AssetId = 70,
                IncidentNumber = "INC-3000",
                IncidentType = IncidentType.Damaged,
                IncidentDate = DateTime.UtcNow.AddDays(-1),
                Description = "Screen cracked",
                Severity = IncidentSeverity.Medium,
                ResolutionStatus = IncidentResolutionStatusHelper.UnderReview,
                CreatedAt = DateTime.UtcNow
            });
            unitOfWork.Seed(new InsuranceClaim
            {
                Id = 4000,
                AssetId = 70,
                IncidentId = 3000,
                ClaimNumber = "CLM-4000",
                ClaimStatus = ClaimStatus.UnderReview,
                ClaimDate = DateTime.UtcNow,
                ClaimType = "Damage",
                Insurer = "Acme Insurance",
                CreatedAt = DateTime.UtcNow
            });

            var service = new IncidentService(unitOfWork);
            Assert.Throws<BusinessException>(() => service.UpdateResolutionStatus(3000, IncidentResolutionStatusHelper.Closed));
        }

        [Test]
        public void ClaimService_SettlingClaim_ClosesLinkedIncident()
        {
            var unitOfWork = new FakeUnitOfWork();
            var asset = BuildAsset(id: 71, assetTag: "AST-071", status: AssetStatus.Damaged);
            unitOfWork.Seed(asset);
            unitOfWork.Seed(new AssetIncident
            {
                Id = 3001,
                AssetId = 71,
                IncidentNumber = "INC-3001",
                IncidentType = IncidentType.Damaged,
                IncidentDate = DateTime.UtcNow.AddDays(-1),
                Description = "Keyboard failure",
                Severity = IncidentSeverity.Medium,
                ResolutionStatus = IncidentResolutionStatusHelper.UnderReview,
                CreatedAt = DateTime.UtcNow
            });
            unitOfWork.Seed(new InsuranceClaim
            {
                Id = 4001,
                AssetId = 71,
                IncidentId = 3001,
                ClaimNumber = "CLM-4001",
                ClaimStatus = ClaimStatus.Approved,
                ClaimDate = DateTime.UtcNow,
                ClaimType = "Damage",
                Insurer = "Acme Insurance",
                CreatedAt = DateTime.UtcNow
            });

            var service = new ClaimService(unitOfWork);
            service.UpdateStatus(4001, ClaimStatus.Settled, 500m, "Paid in full");

            var incident = unitOfWork.Repository<AssetIncident>().GetById(3001);
            Assert.AreEqual(IncidentResolutionStatusHelper.Closed, incident.ResolutionStatus);
            Assert.AreEqual(AssetStatus.InStore, unitOfWork.Repository<Asset>().GetById(71).CurrentStatus);
        }

        [Test]
        public void ClaimService_RejectsIncidentForDifferentAsset()
        {
            var unitOfWork = new FakeUnitOfWork();
            var firstAsset = BuildAsset(id: 60, assetTag: "AST-060", status: AssetStatus.Assigned);
            firstAsset.IsInsured = true;
            var secondAsset = BuildAsset(id: 61, assetTag: "AST-061", status: AssetStatus.Assigned);
            secondAsset.IsInsured = true;
            unitOfWork.Seed(firstAsset);
            unitOfWork.Seed(secondAsset);

            unitOfWork.Seed(new AssetIncident
            {
                Id = 1000,
                AssetId = 61,
                IncidentNumber = "INC-1000",
                IncidentType = IncidentType.Damaged,
                IncidentDate = DateTime.UtcNow.AddDays(-2),
                Description = "Display damage",
                Severity = IncidentSeverity.Medium,
                ResolutionStatus = "Open",
                CreatedAt = DateTime.UtcNow
            });

            var service = new ClaimService(unitOfWork);
            Assert.Throws<BusinessException>(() => service.Create(new InsuranceClaimVm
            {
                AssetId = 60,
                IncidentId = 1000,
                ClaimDate = DateTime.UtcNow.AddDays(-1),
                ClaimType = "Damage",
                Insurer = "Global Insurance"
            }));
        }

        [Test]
        public void ClaimService_Create_RejectsUnresolvedOtherClaimType()
        {
            var unitOfWork = new FakeUnitOfWork();
            var asset = BuildAsset(id: 62, assetTag: "AST-062", status: AssetStatus.Damaged);
            asset.IsInsured = true;
            unitOfWork.Seed(asset);

            var service = new ClaimService(unitOfWork);
            Assert.Throws<BusinessException>(() => service.Create(new InsuranceClaimVm
            {
                AssetId = 62,
                ClaimDate = DateTime.UtcNow,
                ClaimType = "Other",
                Insurer = "Global Insurance"
            }));
        }

        [Test]
        public void ClaimService_Create_AcceptsCustomClaimTypeText()
        {
            var unitOfWork = new FakeUnitOfWork();
            var asset = BuildAsset(id: 63, assetTag: "AST-063", status: AssetStatus.Damaged);
            asset.IsInsured = true;
            unitOfWork.Seed(asset);

            var service = new ClaimService(unitOfWork);
            service.Create(new InsuranceClaimVm
            {
                AssetId = 63,
                ClaimDate = DateTime.UtcNow,
                ClaimType = "Vandalism",
                Insurer = "Global Insurance"
            });

            var claim = unitOfWork.Repository<InsuranceClaim>().GetAll().Single();
            Assert.AreEqual("Vandalism", claim.ClaimType);
        }

        [Test]
        public void NotificationService_DoesNotDuplicateWarrantyNotification()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 80, assetTag: "AST-080", status: AssetStatus.Assigned));
            var asset = unitOfWork.Repository<Asset>().GetById(80);
            asset.WarrantyEndDate = DateTime.UtcNow.AddDays(10);
            unitOfWork.Repository<Asset>().Update(asset);

            unitOfWork.Seed(new Notification
            {
                Type = NotificationType.WarrantyExpiry,
                Subject = "Warranty expiring",
                Message = "Existing notification",
                Status = NotificationStatus.Unread,
                LinkUrl = "/Assets/Details/80",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            });

            var service = TestServiceFactory.CreateNotificationService(unitOfWork);
            service.GenerateSystemNotifications();

            var notifications = unitOfWork.Repository<Notification>().GetAll().ToList();
            Assert.AreEqual(1, notifications.Count(x => x.Type == NotificationType.WarrantyExpiry && x.LinkUrl == "/Assets/Details/80"));
        }

        [Test]
        public void AssetService_RequestDisposalSetsAssetAwaitingApproval()
        {
            var unitOfWork = new FakeUnitOfWork();
            SeedDisposalApprovalSettings(unitOfWork);
            var asset = BuildAsset(id: 90, assetTag: "AST-090", status: AssetStatus.InStore);
            asset.RequireDisposalApproval = true;
            asset.DisposalApprovalStageRoleIds = "1";
            unitOfWork.Seed(asset);

            var service = TestServiceFactory.CreateAssetService(unitOfWork);
            service.RequestDisposal(new AssetDisposalRequestVm
            {
                AssetId = 90,
                DisposalReason = "Device end of life",
                DisposalMethod = DisposalMethod.Retire,
                Notes = "Annual refresh cycle"
            }, "manager-1");

            var updatedAsset = unitOfWork.Repository<Asset>().GetById(90);
            var request = unitOfWork.Repository<DisposalRecord>().Find(x => x.AssetId == 90).Single();
            Assert.AreEqual(AssetStatus.AwaitingApproval, updatedAsset.CurrentStatus);
            Assert.AreEqual(ApprovalStatus.Pending, request.ApprovalStatus);
            Assert.AreEqual(DisposalMethod.Retire, request.DisposalMethod);
        }

        [Test]
        public void AssetService_RequestDisposalRejectsDisposedAsset()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 91, assetTag: "AST-091", status: AssetStatus.Disposed));

            var service = TestServiceFactory.CreateAssetService(unitOfWork);
            Assert.Throws<BusinessException>(() => service.RequestDisposal(new AssetDisposalRequestVm
            {
                AssetId = 91,
                DisposalReason = "Damaged beyond repair",
                DisposalMethod = DisposalMethod.WriteOff
            }, "manager-1"));
        }

        [Test]
        public void AssetService_ApproveDisposalMarksAssetDisposed()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 92, assetTag: "AST-092", status: AssetStatus.AwaitingApproval, custodianId: "user-old", departmentId: 3));
            unitOfWork.Seed(new DisposalRecord
            {
                AssetId = 92,
                DisposalRequestDate = DateTime.UtcNow.AddDays(-1),
                DisposalReason = "Unserviceable",
                DisposalMethod = DisposalMethod.Scrap,
                ApprovalStatus = ApprovalStatus.Pending,
                CurrentApprovalStage = 1,
                ApprovalStageRoleIds = "1",
                RequestedById = "manager-1",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            var service = TestServiceFactory.CreateAssetService(unitOfWork);
            service.ApproveDisposal(new AssetDisposalApprovalVm
            {
                AssetId = 92,
                DisposalAmount = 25m,
                Notes = "Approved by committee"
            }, "approver-1", 1, true);

            var asset = unitOfWork.Repository<Asset>().GetById(92);
            var disposal = unitOfWork.Repository<DisposalRecord>().Find(x => x.AssetId == 92).Single();
            var eventItem = unitOfWork.Repository<AssetCustodyEvent>().Find(x => x.AssetId == 92).Single();
            Assert.AreEqual(AssetStatus.Disposed, asset.CurrentStatus);
            Assert.IsNull(asset.CurrentCustodianId);
            Assert.AreEqual(ApprovalStatus.Approved, disposal.ApprovalStatus);
            Assert.AreEqual("approver-1", disposal.DisposalApprovedById);
            Assert.AreEqual(CustodyActionType.Disposed, eventItem.ActionType);
        }

        [Test]
        public void DepartmentService_CreateReturnsCreatedId()
        {
            var unitOfWork = new FakeUnitOfWork();
            var service = new DepartmentService(unitOfWork, new PermissiveDepartmentScopeService());

            var id = service.Create(new DepartmentVm
            {
                Name = "Finance",
                Code = "FIN",
                Description = "Finance department",
                IsActive = true
            });

            Assert.AreEqual(1, id);
            Assert.AreEqual("Finance", unitOfWork.Repository<Department>().GetById(id).Name);
        }

        [Test]
        public void SupplierService_CreateReturnsCreatedId()
        {
            var unitOfWork = new FakeUnitOfWork();
            var catalogService = new SupplierCatalogService(unitOfWork, new FakeOrganizationScopeService());
            var service = new SupplierService(unitOfWork, catalogService);

            var id = service.Create(new SupplierVm
            {
                SupplierName = "Acme Supplies",
                ContactPerson = "Mary",
                Email = "mary@acme.test",
                IsActive = true
            });

            Assert.AreEqual(1, id);
            Assert.AreEqual("Acme Supplies", unitOfWork.Repository<Supplier>().GetById(id).SupplierName);
        }

        [Test]
        public void TransferService_RejectsToUserOutsideTargetDepartment()
        {
            var unitOfWork = new FakeUnitOfWork();
            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "user-a", DepartmentId = 1, IsActive = true, FirstName = "A", LastName = "One" });
            users.Seed(new UserVm { Id = "user-b", DepartmentId = 2, IsActive = true, FirstName = "B", LastName = "Two" });
            unitOfWork.Seed(BuildAsset(id: 22, assetTag: "AST-022", status: AssetStatus.Assigned, custodianId: "user-a", departmentId: 1));

            var service = TestServiceFactory.CreateTransferService(unitOfWork, users);
            Assert.Throws<BusinessException>(() => service.Transfer(new AssetTransferVm
            {
                AssetId = 22,
                ToUserId = "user-b",
                ToDepartmentId = 1
            }, "requester-1"));
        }

        [Test]
        public void AssignmentService_RejectsToUserOutsideTargetDepartment()
        {
            var unitOfWork = new FakeUnitOfWork();
            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "user-x", DepartmentId = 5, IsActive = true, FirstName = "X", LastName = "User" });
            unitOfWork.Seed(BuildAsset(id: 12, assetTag: "AST-012", status: AssetStatus.InStore));

            var service = TestServiceFactory.CreateAssignmentService(unitOfWork, users);
            Assert.Throws<BusinessException>(() => service.Assign(new AssetAssignmentVm
            {
                AssetId = 12,
                ToUserId = "user-x",
                ToDepartmentId = 3,
                AssignmentType = "Permanent",
                AssignedDate = DateTime.UtcNow
            }));
        }

        [Test]
        public void ReceivingService_RejectsQuantityAboveRemaining()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Supplier { Id = 1, SupplierName = "Acme", CreatedAt = DateTime.UtcNow, IsActive = true });
            unitOfWork.Seed(new Asset
            {
                Id = 40,
                AssetTag = "AST-040",
                AssetName = "Monitor",
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                DepartmentId = 1,
                Currency = "USD",
                AcquisitionCost = 400,
                CurrentStatus = AssetStatus.InStore,
                PurchaseDate = DateTime.UtcNow,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = DateTime.UtcNow,
                UsefulLifeMonths = 36,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            unitOfWork.Seed(new PurchaseRecord
            {
                Id = 10,
                PurchaseOrderNumber = "PO-10",
                SupplierId = 1,
                Quantity = 2,
                UnitCost = 200,
                TotalCost = 400,
                PurchaseDate = DateTime.UtcNow,
                Currency = "USD",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            unitOfWork.Seed(new AssetReceiving
            {
                PurchaseRecordId = 10,
                AssetId = 40,
                ReceivedDate = DateTime.UtcNow,
                QuantityReceived = 1,
                ReceivedById = "receiver-1",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });

            var service = new ReceivingService(unitOfWork);
            Assert.Throws<BusinessException>(() => service.Receive(new AssetReceiveVm
            {
                PurchaseRecordId = 10,
                AssetId = 40,
                ReceivedDate = DateTime.UtcNow,
                QuantityReceived = 2
            }, "receiver-1"));
        }

        [Test]
        public void ReceivingService_GetReceiveAssetLookup_SelectsPreferredAssetId()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Supplier { Id = 1, SupplierName = "Acme", CreatedAt = DateTime.UtcNow, IsActive = true });
            unitOfWork.Seed(new Asset
            {
                Id = 41,
                AssetTag = "AST-041",
                AssetName = "Chair",
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                DepartmentId = 1,
                Currency = "USD",
                AcquisitionCost = 100,
                CurrentStatus = AssetStatus.InStore,
                PurchaseDate = DateTime.UtcNow,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = DateTime.UtcNow,
                UsefulLifeMonths = 36,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            unitOfWork.Seed(new Asset
            {
                Id = 42,
                AssetTag = "AST-042",
                AssetName = "Desk",
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 2,
                DepartmentId = 1,
                Currency = "USD",
                AcquisitionCost = 200,
                CurrentStatus = AssetStatus.InStore,
                PurchaseDate = DateTime.UtcNow,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = DateTime.UtcNow,
                UsefulLifeMonths = 36,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            unitOfWork.Seed(new PurchaseRecord
            {
                Id = 11,
                PurchaseOrderNumber = "PO-11",
                SupplierId = 1,
                Quantity = 1,
                UnitCost = 100,
                TotalCost = 100,
                PurchaseDate = DateTime.UtcNow,
                Currency = "USD",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });

            var service = new ReceivingService(unitOfWork);
            var lookup = service.GetReceiveAssetLookup(11, 42);

            Assert.AreEqual(42, lookup.SelectedAssetId);
            Assert.IsTrue(lookup.Assets.Any(x => x.Id == 41));
            Assert.IsTrue(lookup.Assets.Any(x => x.Id == 42));
        }

        [Test]
        public void ReceivingService_GetReceiveAssetLookup_AutoSelectsSingleSupplierMatch()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Supplier { Id = 1, SupplierName = "Acme", CreatedAt = DateTime.UtcNow, IsActive = true });
            unitOfWork.Seed(new Asset
            {
                Id = 43,
                AssetTag = "AST-043",
                AssetName = "Monitor",
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                DepartmentId = 1,
                Currency = "USD",
                AcquisitionCost = 400,
                CurrentStatus = AssetStatus.InStore,
                PurchaseDate = DateTime.UtcNow,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = DateTime.UtcNow,
                UsefulLifeMonths = 36,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            unitOfWork.Seed(new Asset
            {
                Id = 44,
                AssetTag = "AST-044",
                AssetName = "Printer",
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 2,
                DepartmentId = 1,
                Currency = "USD",
                AcquisitionCost = 500,
                CurrentStatus = AssetStatus.InStore,
                PurchaseDate = DateTime.UtcNow,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = DateTime.UtcNow,
                UsefulLifeMonths = 36,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            unitOfWork.Seed(new PurchaseRecord
            {
                Id = 12,
                PurchaseOrderNumber = "PO-12",
                SupplierId = 1,
                Quantity = 1,
                UnitCost = 400,
                TotalCost = 400,
                PurchaseDate = DateTime.UtcNow,
                Currency = "USD",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });

            var service = new ReceivingService(unitOfWork);
            var lookup = service.GetReceiveAssetLookup(12, null);

            Assert.AreEqual(43, lookup.SelectedAssetId);
            Assert.AreEqual(1, lookup.Assets.Count);
            Assert.AreEqual("AST-043 - Monitor", lookup.Assets[0].Label);
        }

        [Test]
        public void ReportService_ExportDepartmentSummaryCsvIncludesDepartmentTotals()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department { Id = 1, Name = "IT", Code = "IT", CreatedAt = DateTime.UtcNow, IsActive = true });
            unitOfWork.Seed(BuildAsset(id: 41, assetTag: "AST-041", status: AssetStatus.InStore, departmentId: 1));
            var asset = unitOfWork.Repository<Asset>().GetById(41);
            asset.AcquisitionCost = 500;
            asset.CurrentBookValue = 450;
            asset.AccumulatedDepreciation = 50;
            unitOfWork.Repository<Asset>().Update(asset);

            var service = TestServiceFactory.CreateReportService(unitOfWork);
            var csv = System.Text.Encoding.UTF8.GetString(service.ExportDepartmentSummaryCsv());

            StringAssert.Contains("DepartmentName", csv);
            StringAssert.Contains("IT", csv);
            StringAssert.Contains("500.00", csv);
        }

        [Test]
        public void PurchaseService_GetByIdReturnsSupplierName()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Supplier
            {
                Id = 5,
                SupplierName = "Northwind",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });

            var service = TestServiceFactory.CreatePurchaseService(unitOfWork);
            var purchaseId = service.Create(new PurchaseRecordVm
            {
                PurchaseOrderNumber = "PO-500",
                SupplierId = 5,
                InvoiceNumber = "INV-500",
                PurchaseDate = DateTime.UtcNow.Date,
                Quantity = 2,
                UnitCost = 300m,
                Currency = "USD"
            });

            var purchase = service.GetById(purchaseId);
            Assert.IsNotNull(purchase);
            Assert.AreEqual("Northwind", purchase.SupplierName);
            Assert.AreEqual(600m, purchase.TotalCost);
        }

        private static void SeedDisposalApprovalSettings(FakeUnitOfWork unitOfWork)
        {
            unitOfWork.Seed(new SystemSetting
            {
                SettingKey = ApprovalProcessCodes.GetLegacyRequireSettingKey(ApprovalProcessCodes.Disposal),
                SettingValue = "true",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            unitOfWork.Seed(new SystemSetting
            {
                SettingKey = ApprovalProcessCodes.GetStageRoleIdsSettingKey(ApprovalProcessCodes.Disposal),
                SettingValue = "1",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
        }

        private static Asset BuildAsset(int id, string assetTag, AssetStatus status, string custodianId = null, int departmentId = 1)
        {
            return new Asset
            {
                Id = id,
                AssetTag = assetTag,
                AssetName = "Asset " + id,
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                DepartmentId = departmentId,
                CurrentCustodianId = custodianId,
                Currency = "USD",
                AcquisitionCost = 1000,
                CurrentStatus = status,
                PurchaseDate = DateTime.UtcNow,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = DateTime.UtcNow,
                UsefulLifeMonths = 36,
                OrganizationId = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}

