#pragma warning disable 618 // GetAll() is fine for in-memory fakes
using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Assets
{
    /// <summary>rel1019g: inline asset type + first sub-type creation from the classification picker.</summary>
    [TestFixture]
    public partial class AssetClassificationCreateServiceTests
    {
        private TransactionalUnitOfWork _uow;
        private RecordingAuditWriter _audit;

        [SetUp]
        public void SetUp()
        {
            var inner = new FakeUnitOfWork();
            inner.Seed(new AssetCategory { Id = 1, OrganizationId = 1, Name = "Furniture", IsActive = true, CreatedAt = DateTime.UtcNow });
            inner.Seed(new AssetCategory { Id = 2, OrganizationId = 1, Name = "ICT", IsActive = true, CreatedAt = DateTime.UtcNow });
            inner.Seed(new AssetCategory { Id = 3, OrganizationId = 1, Name = "Old stock", IsActive = false, CreatedAt = DateTime.UtcNow });
            inner.Seed(new AssetCategory { Id = 9, OrganizationId = 2, Name = "Other org", IsActive = true, CreatedAt = DateTime.UtcNow });
            inner.Seed(new AssetType { Id = 10, OrganizationId = 1, AssetCategoryId = 1, Name = "Desks", IsActive = true, CreatedAt = DateTime.UtcNow });
            inner.Seed(new AssetType { Id = 11, OrganizationId = 1, AssetCategoryId = 1, Name = "Lockers", IsActive = false, CreatedAt = DateTime.UtcNow });
            _uow = new TransactionalUnitOfWork(inner);
            _audit = new RecordingAuditWriter();
            _audit.InTransactionProbe = () => _uow.InTransaction;
        }

        private AssetClassificationCreateService Service()
        {
            return new AssetClassificationCreateService(_uow, new FakeOrganizationScopeService(organizationId: 1), _audit);
        }

        private int TypeCount() { return _uow.Repository<AssetType>().GetAll().Count(); }

        private int SubTypeCount() { return _uow.Repository<AssetSubType>().GetAll().Count(); }

        [Test]
        public void CreatesTypeAndFirstSubTypeInOneTransaction()
        {
            var result = Service().CreateTypeWithSubType(new AssetTypeWithSubTypeCreateVm
            {
                CategoryId = 1,
                TypeName = "  Chairs  ",
                SubTypeName = " Student   chair "
            });

            Assert.AreEqual(1, _uow.TransactionsCommitted);
            Assert.AreEqual(0, _uow.TransactionsRolledBack);
            var type = _uow.Repository<AssetType>().GetById(result.AssetTypeId);
            var subType = _uow.Repository<AssetSubType>().GetById(result.AssetSubTypeId);
            Assert.IsNotNull(type);
            Assert.IsNotNull(subType);
            Assert.AreEqual("Chairs", type.Name);
            Assert.AreEqual(1, type.AssetCategoryId);
            Assert.AreEqual(1, type.OrganizationId);
            Assert.IsTrue(type.IsActive);
            Assert.AreEqual("Student chair", subType.Name);
            Assert.AreEqual(type.Id, subType.AssetTypeId);
            Assert.AreEqual(1, subType.OrganizationId);
            Assert.AreEqual(string.Empty, subType.Brand);
            Assert.AreEqual("Student chair", subType.Model);
            Assert.IsTrue(subType.IsActive);
            Assert.AreEqual(1, result.CategoryId);
            Assert.AreEqual("Furniture", result.CategoryName);
            Assert.AreEqual("Chairs", result.AssetTypeName);
            Assert.AreEqual("Student chair", result.AssetSubTypeName);

            // Both inserts happened inside the transaction, and both audit rows were written inside it too.
            Assert.AreEqual(2, _uow.AddsInsideTransaction);
            CollectionAssert.AreEqual(
                new[] { AssetClassificationCreateService.AuditActionCreateType, AssetClassificationCreateService.AuditActionCreateSubType },
                _audit.Rows.Select(r => r.Action).ToArray());
            Assert.AreEqual(type.Id.ToString(), _audit.Rows[0].EntityId);
            Assert.AreEqual(subType.Id.ToString(), _audit.Rows[1].EntityId);
            Assert.IsTrue(_audit.Rows.All(r => r.InsideTransaction));
        }

        [TestCase("Desks")]
        [TestCase("  desks ")]
        [TestCase("DESKS")]
        public void RejectsDuplicateTypeNameInCategory_CaseInsensitiveAndTrimmed(string name)
        {
            var ex = Assert.Throws<BusinessException>(() => Service().CreateTypeWithSubType(new AssetTypeWithSubTypeCreateVm
            {
                CategoryId = 1,
                TypeName = name,
                SubTypeName = "Teacher desk"
            }));
            StringAssert.Contains("already has an asset type", ex.Message);
            Assert.AreEqual(2, TypeCount());
            Assert.AreEqual(0, SubTypeCount());
            Assert.AreEqual(0, _uow.TransactionsCommitted);
            Assert.AreEqual(0, _audit.Rows.Count);
        }

        [Test]
        public void RejectsDuplicateOfInactiveType_WithReactivateHint()
        {
            var ex = Assert.Throws<BusinessException>(() => Service().CreateTypeWithSubType(new AssetTypeWithSubTypeCreateVm
            {
                CategoryId = 1,
                TypeName = "lockers",
                SubTypeName = "Steel locker"
            }));
            StringAssert.Contains("inactive", ex.Message);
            Assert.AreEqual(2, TypeCount());
        }

        [Test]
        public void SameTypeNameInAnotherCategory_IsAllowed()
        {
            var result = Service().CreateTypeWithSubType(new AssetTypeWithSubTypeCreateVm
            {
                CategoryId = 2,
                TypeName = "Desks",
                SubTypeName = "Server desk"
            });
            Assert.AreEqual(2, result.CategoryId);
            Assert.AreEqual(3, TypeCount());
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void RequiresFirstSubType_NoTypeWithoutSubType(string subTypeName)
        {
            var ex = Assert.Throws<BusinessException>(() => Service().CreateTypeWithSubType(new AssetTypeWithSubTypeCreateVm
            {
                CategoryId = 1,
                TypeName = "Chairs",
                SubTypeName = subTypeName
            }));
            StringAssert.Contains("sub-type", ex.Message);
            Assert.AreEqual(2, TypeCount());
            Assert.AreEqual(0, SubTypeCount());
        }

        [Test]
        public void RequiresTypeName()
        {
            Assert.Throws<BusinessException>(() => Service().CreateTypeWithSubType(new AssetTypeWithSubTypeCreateVm
            {
                CategoryId = 1,
                TypeName = " ",
                SubTypeName = "Student chair"
            }));
            Assert.AreEqual(2, TypeCount());
        }

        [TestCase(0)]
        [TestCase(404)]
        public void CategoryMustExist_CategoriesAreNeverCreated(int categoryId)
        {
            var categoriesBefore = _uow.Repository<AssetCategory>().GetAll().Count();
            Assert.Throws<BusinessException>(() => Service().CreateTypeWithSubType(new AssetTypeWithSubTypeCreateVm
            {
                CategoryId = categoryId,
                TypeName = "Chairs",
                SubTypeName = "Student chair"
            }));
            Assert.AreEqual(categoriesBefore, _uow.Repository<AssetCategory>().GetAll().Count());
            Assert.AreEqual(2, TypeCount());
        }

        [Test]
        public void RejectsInactiveCategory()
        {
            var ex = Assert.Throws<BusinessException>(() => Service().CreateTypeWithSubType(new AssetTypeWithSubTypeCreateVm
            {
                CategoryId = 3,
                TypeName = "Chairs",
                SubTypeName = "Student chair"
            }));
            StringAssert.Contains("inactive", ex.Message);
        }

        [Test]
        public void RejectsCategoryOfAnotherOrganisation()
        {
            var ex = Assert.Throws<BusinessException>(() => Service().CreateTypeWithSubType(new AssetTypeWithSubTypeCreateVm
            {
                CategoryId = 9,
                TypeName = "Chairs",
                SubTypeName = "Student chair"
            }));
            StringAssert.Contains("not found", ex.Message);
            Assert.AreEqual(2, TypeCount());
        }

        [Test]
        public void FailureWhileSavingSubType_RollsBackTheType()
        {
            _uow.FailOnSaveNumber = 2; // 1st save inserts the type, 2nd (the sub-type) fails.
            Assert.Throws<InvalidOperationException>(() => Service().CreateTypeWithSubType(new AssetTypeWithSubTypeCreateVm
            {
                CategoryId = 1,
                TypeName = "Chairs",
                SubTypeName = "Student chair"
            }));
            Assert.AreEqual(1, _uow.TransactionsRolledBack);
            Assert.AreEqual(0, _uow.TransactionsCommitted);
            Assert.AreEqual(2, TypeCount(), "the half-created type must be rolled back");
            Assert.AreEqual(0, SubTypeCount());
            Assert.IsFalse(_uow.Repository<AssetType>().GetAll().Any(x => x.Name == "Chairs"));
        }

        [Test]
        public void RejectsOverlongNames()
        {
            Assert.Throws<BusinessException>(() => Service().CreateTypeWithSubType(new AssetTypeWithSubTypeCreateVm
            {
                CategoryId = 1,
                TypeName = new string('a', 201),
                SubTypeName = "Student chair"
            }));
            Assert.Throws<BusinessException>(() => Service().CreateTypeWithSubType(new AssetTypeWithSubTypeCreateVm
            {
                CategoryId = 1,
                TypeName = "Chairs",
                SubTypeName = new string('b', 201)
            }));
            Assert.AreEqual(2, TypeCount());
        }

        [Test]
        public void LongSubTypeName_ModelIsCappedToColumnLength()
        {
            var name = new string('c', 150);
            var result = Service().CreateTypeWithSubType(new AssetTypeWithSubTypeCreateVm
            {
                CategoryId = 1,
                TypeName = "Chairs",
                SubTypeName = name
            });
            var subType = _uow.Repository<AssetSubType>().GetById(result.AssetSubTypeId);
            Assert.AreEqual(name, subType.Name);
            Assert.AreEqual(AssetClassificationCreateService.MaxModelLength, subType.Model.Length);
        }

        [Test]
        public void NamesEqual_IgnoresCaseAndWhitespace()
        {
            Assert.IsTrue(AssetClassificationCreateService.NamesEqual(" Student  Chair ", "student chair"));
            Assert.IsFalse(AssetClassificationCreateService.NamesEqual("Student chair", "Student chairs"));
        }

        /// <summary>Wraps FakeUnitOfWork with commit/rollback semantics so "nothing half-created" can be asserted.</summary>
        private sealed class TransactionalUnitOfWork : IUnitOfWork
        {
            private readonly FakeUnitOfWork _inner;
            private bool _inTransaction;
            private int _saveCount;

            public TransactionalUnitOfWork(FakeUnitOfWork inner)
            {
                _inner = inner;
            }

            public int TransactionsCommitted { get; private set; }
            public int TransactionsRolledBack { get; private set; }
            public int AddsInsideTransaction { get; private set; }
            public int FailOnSaveNumber { get; set; }
            public bool InTransaction { get { return _inTransaction; } }

            public IRepository<T> Repository<T>() where T : class, new()
            {
                return _inner.Repository<T>();
            }

            public IEntityWriter<T> Writer<T>() where T : class, new()
            {
                return _inner.Writer<T>();
            }

            public int SaveChanges()
            {
                _saveCount++;
                if (FailOnSaveNumber > 0 && _saveCount == FailOnSaveNumber)
                {
                    throw new InvalidOperationException("simulated SQL failure");
                }

                return _inner.SaveChanges();
            }

            public void BeginTransaction() { }
            public void Commit() { }
            public void Rollback() { }

            public void ExecuteInTransaction(Action action)
            {
                var typesBefore = Repository<AssetType>().GetAll().ToList();
                var subTypesBefore = Repository<AssetSubType>().GetAll().ToList();
                _inTransaction = true;
                try
                {
                    action();
                    SaveChanges();
                    AddsInsideTransaction += (Repository<AssetType>().GetAll().Count() - typesBefore.Count)
                        + (Repository<AssetSubType>().GetAll().Count() - subTypesBefore.Count);
                    TransactionsCommitted++;
                }
                catch
                {
                    foreach (var added in Repository<AssetType>().GetAll().Where(x => !typesBefore.Contains(x)).ToList())
                    {
                        Repository<AssetType>().Remove(added);
                    }
                    foreach (var added in Repository<AssetSubType>().GetAll().Where(x => !subTypesBefore.Contains(x)).ToList())
                    {
                        Repository<AssetSubType>().Remove(added);
                    }
                    TransactionsRolledBack++;
                    throw;
                }
                finally
                {
                    _inTransaction = false;
                }
            }

            public void TrackAdd(object entity) { _inner.TrackAdd(entity); }
            public void PersistConditionalApprovalUpdate(object entity, int expectedStage) { _inner.PersistConditionalApprovalUpdate(entity, expectedStage); }
            public int GetRemainingPurchaseQuantity(int purchaseRecordId) { return _inner.GetRemainingPurchaseQuantity(purchaseRecordId); }
            public void ClearTracking() { }
            public void Dispose() { }
        }

        private sealed class RecordingAuditWriter : IAuditWriter
        {
            public RecordingAuditWriter() { Rows = new List<AuditRow>(); }
            public List<AuditRow> Rows { get; private set; }
            public Func<bool> InTransactionProbe { get; set; }

            public void Write(string action, string entityType, string entityId, string oldValues, string newValues)
            {
                Rows.Add(new AuditRow
                {
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    NewValues = newValues,
                    InsideTransaction = InTransactionProbe != null && InTransactionProbe()
                });
            }
        }

        private sealed class AuditRow
        {
            public string Action { get; set; }
            public string EntityType { get; set; }
            public string EntityId { get; set; }
            public string NewValues { get; set; }
            public bool InsideTransaction { get; set; }
        }
    }
}