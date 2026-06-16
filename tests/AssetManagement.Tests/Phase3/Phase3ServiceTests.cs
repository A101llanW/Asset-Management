using System;
using System.Linq;
using System.Text;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Phase3
{
    [TestFixture]
    public class Phase3ServiceTests
    {
        [Test]
        public void LookupByScanCode_FindsAsset_ByBarcode()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Asset
            {
                Id = 7,
                AssetTag = "AST-007",
                AssetName = "Scanner Test",
                BarcodeOrQRCode = "QR-007",
                OrganizationId = 1,
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
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

            var service = TestServiceFactory.CreateAssetService(unitOfWork);
            var result = service.LookupByScanCode("QR-007");

            Assert.IsTrue(result.Found);
            Assert.AreEqual(7, result.AssetId);
            Assert.AreEqual("AST-007", result.AssetTag);
        }

        [Test]
        public void LookupByScanCode_FindsAsset_WithFlexibleAssetTagInput()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Asset
            {
                Id = 8,
                AssetTag = "AST-2026-007",
                AssetName = "Flexible Tag Test",
                OrganizationId = 1,
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
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

            var service = TestServiceFactory.CreateAssetService(unitOfWork);
            var result = service.LookupByScanCode("ast 2026 007");

            Assert.IsTrue(result.Found);
            Assert.AreEqual(8, result.AssetId);
            Assert.AreEqual("AST-2026-007", result.AssetTag);
        }

        [Test]
        public void WebhookService_QueueDelivery_PersistsDeliveryForActiveSubscribers()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new WebhookSubscription
            {
                Id = 1,
                EventType = "asset.status_changed",
                TargetUrl = "https://example.test/hook",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            unitOfWork.Seed(new WebhookSubscription
            {
                Id = 2,
                EventType = "asset.status_changed",
                TargetUrl = "https://example.test/inactive",
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            });

            var service = TestServiceFactory.CreateWebhookService(unitOfWork);
            service.QueueDelivery("asset.status_changed", "{\"assetId\":1}");

            var deliveries = unitOfWork.Repository<WebhookDelivery>().Find(x => x.EventType == "asset.status_changed").ToList();
            Assert.AreEqual(1, deliveries.Count);
            Assert.AreEqual(1, deliveries[0].WebhookSubscriptionId);
        }

        [Test]
        public void AuditLogService_ExportCsv_IncludesHeaderRow()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new AuditLog
            {
                Id = 1,
                ActorUserId = "user-1",
                Action = "Assets.UpdateStatus",
                EntityType = nameof(Asset),
                EntityId = "10",
                Timestamp = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc),
                IPAddress = "127.0.0.1"
            });

            var service = TestServiceFactory.CreateAuditLogService(unitOfWork, "user-1");
            var csv = Encoding.UTF8.GetString(service.ExportCsv(new AuditLogFilterVm()));
            StringAssert.Contains("Timestamp", csv);
            StringAssert.Contains("Assets.UpdateStatus", csv);
        }

        [Test]
        public void AssetRequestService_GetRequests_FiltersByScopedDepartment()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new AssetRequest
            {
                Id = 1,
                RequestedById = "user-a",
                DepartmentId = 10,
                Justification = "Dept 10",
                Status = AssetRequestStatus.Pending,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            unitOfWork.Seed(new AssetRequest
            {
                Id = 2,
                RequestedById = "user-b",
                DepartmentId = 20,
                Justification = "Dept 20",
                Status = AssetRequestStatus.Pending,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            var scope = new ScopedDepartmentScopeService(10);
            var service = TestServiceFactory.CreateAssetRequestService(unitOfWork, scope);
            var page = service.GetRequests(null, "created", "desc", 1, 20);

            Assert.AreEqual(1, page.TotalCount);
            Assert.AreEqual(1, page.Items.Count());
            Assert.AreEqual(1, page.Items.First().Id);
        }
    }

    internal class RecordingAuditWriter : IAuditWriter
    {
        public System.Collections.Generic.List<AuditEntry> Entries { get; } =
            new System.Collections.Generic.List<AuditEntry>();

        public void Write(string action, string entityType, string entityId, string oldValues, string newValues)
        {
            Entries.Add(new AuditEntry
            {
                Action = action,
                EntityType = entityType,
                EntityId = entityId
            });
        }
    }

    internal struct AuditEntry
    {
        public string Action { get; set; }

        public string EntityType { get; set; }

        public string EntityId { get; set; }
    }

    internal class ScopedDepartmentScopeService : IDepartmentScopeService
    {
        private readonly int _departmentId;

        public ScopedDepartmentScopeService(int departmentId)
        {
            _departmentId = departmentId;
        }

        public bool BypassesDepartmentScope => false;

        public int? ScopedDepartmentId => _departmentId;

        public IQueryable<Asset> ApplyAssetScope(IQueryable<Asset> query)
        {
            return query == null ? query : query.Where(x => x.DepartmentId == _departmentId);
        }

        public void EnsureCanAccessAsset(Asset asset)
        {
            if (asset != null && asset.DepartmentId != _departmentId)
            {
                throw new BusinessException("Department scope violation.");
            }
        }

        public IQueryable<Department> ApplyDepartmentScope(IQueryable<Department> query)
        {
            return query == null ? query : query.Where(x => x.Id == _departmentId);
        }

        public void EnsureCanAccessDepartment(Department department)
        {
            if (department != null && department.Id != _departmentId)
            {
                throw new BusinessException("Department scope violation.");
            }
        }

        public void EnsureCanAccessDepartmentId(int departmentId)
        {
            if (departmentId != _departmentId)
            {
                throw new BusinessException("Department scope violation.");
            }
        }

        public int CountVisibleDepartments(bool activeOnly = true) => 1;
    }
}
