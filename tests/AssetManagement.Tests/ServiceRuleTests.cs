using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetManagement.Tests
{
    [TestClass]
    public class ServiceRuleTests
    {
        [TestMethod]
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

            var service = new AssignmentService(unitOfWork, new NoOpAuditWriter());

            Assert.ThrowsException<BusinessException>(() => service.Assign(new AssetAssignmentVm
            {
                AssetId = 1,
                AssignmentType = "Temporary",
                ToUserId = "user-1",
                AssignedDate = DateTime.UtcNow
            }));
        }

        [TestMethod]
        public void DepreciationService_DoesNotDropBookValueBelowSalvage()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Asset
            {
                Id = 2,
                AssetTag = "AST-002",
                AssetName = "Desktop",
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                DepartmentId = 1,
                Currency = "USD",
                AcquisitionCost = 1200,
                CurrentBookValue = 105,
                SalvageValue = 100,
                AccumulatedDepreciation = 1095,
                CurrentStatus = AssetStatus.Assigned,
                PurchaseDate = DateTime.UtcNow.AddYears(-3),
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = DateTime.UtcNow.AddYears(-3),
                UsefulLifeMonths = 36,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            var service = new DepreciationService(unitOfWork);
            service.RunMonthlyDepreciation();

            var updated = unitOfWork.Repository<Asset>().GetById(2);
            Assert.AreEqual(100m, updated.CurrentBookValue);
            Assert.IsTrue(updated.AccumulatedDepreciation >= 1095m);
        }

        [TestMethod]
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

            var service = new AssetService(unitOfWork, new NoOpAuditWriter());
            Assert.ThrowsException<BusinessException>(() => service.Create(new AssetCreateVm
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

        [TestMethod]
        public void AssignmentService_RequiresTargetUserOrDepartment()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 10, assetTag: "AST-010", status: AssetStatus.InStore));

            var service = new AssignmentService(unitOfWork, new NoOpAuditWriter());
            Assert.ThrowsException<BusinessException>(() => service.Assign(new AssetAssignmentVm
            {
                AssetId = 10,
                AssignmentType = "Permanent",
                AssignedDate = DateTime.UtcNow
            }));
        }

        [TestMethod]
        public void AssignmentService_CustodyEventIncludesPreviousCustodian()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 11, assetTag: "AST-011", status: AssetStatus.Assigned, custodianId: "user-old", departmentId: 2));

            var service = new AssignmentService(unitOfWork, new NoOpAuditWriter());
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

        [TestMethod]
        public void TransferService_RejectsTransferWhenAssetIsNotAssigned()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 20, assetTag: "AST-020", status: AssetStatus.InStore));

            var service = new TransferService(unitOfWork, new NoOpAuditWriter());
            Assert.ThrowsException<BusinessException>(() => service.Transfer(new AssetTransferVm
            {
                AssetId = 20,
                ToUserId = "user-2",
                ToDepartmentId = 2
            }));
        }

        [TestMethod]
        public void TransferService_RejectsMismatchedFromUser()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 21, assetTag: "AST-021", status: AssetStatus.Assigned, custodianId: "user-a"));

            var service = new TransferService(unitOfWork, new NoOpAuditWriter());
            Assert.ThrowsException<BusinessException>(() => service.Transfer(new AssetTransferVm
            {
                AssetId = 21,
                FromUserId = "user-wrong",
                ToUserId = "user-b"
            }));
        }

        [TestMethod]
        public void ReturnService_RejectsReturnWhenAssetIsNotAssigned()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 30, assetTag: "AST-030", status: AssetStatus.InStore));

            var service = new ReturnService(unitOfWork, new NoOpAuditWriter());
            Assert.ThrowsException<BusinessException>(() => service.ReturnAsset(new AssetReturnVm
            {
                AssetId = 30,
                ReturnedById = "user-1",
                ReceivedById = "receiver-1",
                ReturnDate = DateTime.UtcNow
            }));
        }

        [TestMethod]
        public void ReturnService_UsesCurrentCustodianWhenReturnedByNotProvided()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 31, assetTag: "AST-031", status: AssetStatus.Assigned, custodianId: "custodian-1"));

            var service = new ReturnService(unitOfWork, new NoOpAuditWriter());
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

        [TestMethod]
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
            Assert.ThrowsException<BusinessException>(() => service.Create(new AssetMaintenanceVm
            {
                AssetId = 40,
                MaintenanceType = MaintenanceType.Corrective.ToString(),
                ReportedIssue = "Another issue"
            }));
        }

        [TestMethod]
        public void IncidentService_LostIncidentMarksAssetAsLost()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 50, assetTag: "AST-050", status: AssetStatus.Assigned, custodianId: "user-1"));

            var service = new IncidentService(unitOfWork);
            service.Create(new AssetIncidentVm
            {
                AssetId = 50,
                IncidentType = IncidentType.Lost.ToString(),
                IncidentDate = DateTime.UtcNow.AddHours(-1),
                Description = "Lost while in transit"
            });

            var asset = unitOfWork.Repository<Asset>().GetById(50);
            Assert.AreEqual(AssetStatus.Lost, asset.CurrentStatus);
        }

        [TestMethod]
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
            Assert.ThrowsException<BusinessException>(() => service.Create(new InsuranceClaimVm
            {
                AssetId = 60,
                IncidentId = 1000,
                ClaimDate = DateTime.UtcNow.AddDays(-1),
                ClaimType = "Damage",
                Insurer = "Global Insurance"
            }));
        }

        [TestMethod]
        public void DepreciationService_DoesNotPostDuplicateForSamePeriod()
        {
            var now = DateTime.UtcNow;
            var periodStart = new DateTime(now.Year, now.Month, 1);
            var periodEnd = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));

            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Asset
            {
                Id = 70,
                AssetTag = "AST-070",
                AssetName = "Printer",
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                DepartmentId = 1,
                Currency = "USD",
                AcquisitionCost = 1000,
                CurrentBookValue = 800,
                SalvageValue = 100,
                AccumulatedDepreciation = 200,
                CurrentStatus = AssetStatus.Assigned,
                PurchaseDate = now.AddYears(-1),
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = now.AddYears(-1),
                UsefulLifeMonths = 36,
                IsActive = true,
                CreatedAt = now
            });
            unitOfWork.Seed(new DepreciationRecord
            {
                AssetId = 70,
                PeriodStartDate = periodStart,
                PeriodEndDate = periodEnd,
                Method = DepreciationMethod.StraightLine,
                OpeningBookValue = 820,
                DepreciationAmount = 20,
                ClosingBookValue = 800,
                AccumulatedDepreciation = 200,
                IsPosted = true,
                PostedAt = now,
                CreatedAt = now
            });

            var service = new DepreciationService(unitOfWork);
            service.RunMonthlyDepreciation();

            var asset = unitOfWork.Repository<Asset>().GetById(70);
            Assert.AreEqual(800m, asset.CurrentBookValue);
            Assert.AreEqual(1, unitOfWork.Repository<DepreciationRecord>().Find(x => x.AssetId == 70).Count());
        }

        [TestMethod]
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

            var service = new NotificationService(unitOfWork);
            service.GenerateSystemNotifications();

            var notifications = unitOfWork.Repository<Notification>().GetAll().ToList();
            Assert.AreEqual(1, notifications.Count(x => x.Type == NotificationType.WarrantyExpiry && x.LinkUrl == "/Assets/Details/80"));
        }

        [TestMethod]
        public void AssetService_RequestDisposalSetsAssetAwaitingApproval()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 90, assetTag: "AST-090", status: AssetStatus.InStore));

            var service = new AssetService(unitOfWork, new NoOpAuditWriter());
            service.RequestDisposal(new AssetDisposalRequestVm
            {
                AssetId = 90,
                DisposalReason = "Device end of life",
                DisposalMethod = DisposalMethod.Retire,
                Notes = "Annual refresh cycle"
            }, "manager-1");

            var asset = unitOfWork.Repository<Asset>().GetById(90);
            var request = unitOfWork.Repository<DisposalRecord>().Find(x => x.AssetId == 90).Single();
            Assert.AreEqual(AssetStatus.AwaitingApproval, asset.CurrentStatus);
            Assert.AreEqual(ApprovalStatus.Pending, request.ApprovalStatus);
            Assert.AreEqual(DisposalMethod.Retire, request.DisposalMethod);
        }

        [TestMethod]
        public void AssetService_RequestDisposalRejectsAssignedAsset()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(BuildAsset(id: 91, assetTag: "AST-091", status: AssetStatus.Assigned, custodianId: "user-1"));

            var service = new AssetService(unitOfWork, new NoOpAuditWriter());
            Assert.ThrowsException<BusinessException>(() => service.RequestDisposal(new AssetDisposalRequestVm
            {
                AssetId = 91,
                DisposalReason = "Damaged beyond repair",
                DisposalMethod = DisposalMethod.WriteOff
            }, "manager-1"));
        }

        [TestMethod]
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
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            var service = new AssetService(unitOfWork, new NoOpAuditWriter());
            service.ApproveDisposal(new AssetDisposalApprovalVm
            {
                AssetId = 92,
                DisposalAmount = 25m,
                Notes = "Approved by committee"
            }, "approver-1");

            var asset = unitOfWork.Repository<Asset>().GetById(92);
            var disposal = unitOfWork.Repository<DisposalRecord>().Find(x => x.AssetId == 92).Single();
            var eventItem = unitOfWork.Repository<AssetCustodyEvent>().Find(x => x.AssetId == 92).Single();
            Assert.AreEqual(AssetStatus.Disposed, asset.CurrentStatus);
            Assert.IsNull(asset.CurrentCustodianId);
            Assert.AreEqual(ApprovalStatus.Approved, disposal.ApprovalStatus);
            Assert.AreEqual("approver-1", disposal.DisposalApprovedById);
            Assert.AreEqual(CustodyActionType.Disposed, eventItem.ActionType);
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
                CreatedAt = DateTime.UtcNow
            };
        }
    }

    internal class NoOpAuditWriter : IAuditWriter
    {
        public void Write(string action, string entityType, string entityId, string oldValues, string newValues)
        {
        }
    }

    internal class FakeUnitOfWork : IUnitOfWork
    {
        private readonly Dictionary<Type, object> _repositories = new Dictionary<Type, object>();

        public IRepository<T> Repository<T>() where T : class
        {
            var type = typeof(T);
            if (!_repositories.ContainsKey(type))
            {
                _repositories[type] = new FakeRepository<T>();
            }

            return (IRepository<T>)_repositories[type];
        }

        public void Seed<T>(T entity) where T : class
        {
            Repository<T>().Add(entity);
        }

        public int SaveChanges()
        {
            return 1;
        }

        public void Dispose()
        {
        }
    }

    internal class FakeRepository<T> : IRepository<T> where T : class
    {
        private readonly List<T> _items = new List<T>();

        public IEnumerable<T> GetAll()
        {
            return _items.ToList();
        }

        public IEnumerable<T> Find(Expression<Func<T, bool>> predicate)
        {
            return _items.AsQueryable().Where(predicate).ToList();
        }

        public T GetById(object id)
        {
            var prop = typeof(T).GetProperty("Id");
            return _items.FirstOrDefault(x => prop != null && Equals(prop.GetValue(x), id));
        }

        public void Add(T entity)
        {
            if (typeof(T).GetProperty("Id") != null)
            {
                var idProp = typeof(T).GetProperty("Id");
                if (idProp.PropertyType == typeof(int) && (int)idProp.GetValue(entity) == 0)
                {
                    var nextId = _items.Count == 0 ? 1 : _items.Max(x => (int)idProp.GetValue(x)) + 1;
                    idProp.SetValue(entity, nextId);
                }
            }

            _items.Add(entity);
        }

        public void Update(T entity)
        {
            var prop = typeof(T).GetProperty("Id");
            if (prop == null)
            {
                return;
            }

            var idValue = prop.GetValue(entity);
            var existing = _items.FirstOrDefault(x => Equals(prop.GetValue(x), idValue));
            if (existing != null)
            {
                _items.Remove(existing);
            }

            _items.Add(entity);
        }

        public void Remove(T entity)
        {
            _items.Remove(entity);
        }
    }
}
