using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Common;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Tests.Helpers
{
    internal class FakeUserService : IUserService
    {
        private readonly Dictionary<string, UserVm> _users = new Dictionary<string, UserVm>(StringComparer.OrdinalIgnoreCase);

        public void Seed(UserVm user)
        {
            if (user != null && !string.IsNullOrWhiteSpace(user.Id))
            {
                _users[user.Id] = user;
            }
        }

        public IEnumerable<UserVm> GetAll()
        {
            return _users.Values.ToList();
        }

        public UserVm GetById(string id)
        {
            UserVm user;
            return string.IsNullOrWhiteSpace(id) || !_users.TryGetValue(id, out user) ? null : user;
        }

        public void AssignRole(string userId, int roleId)
        {
        }

        public void AssignDepartment(string userId, int? departmentId)
        {
        }
    }

    internal class PermissiveMetricsService : IMetricsService
    {
        public int CountDepartments(bool activeOnly = true) => 1;

        public int CountAssets(AssetFilterVm filter) => 1;

        public int CountPendingApprovals(PendingApprovalCountMode mode) => 0;

        public int CountExpiringWarranties(int days) => 0;

        public int CountExpiringInsurance(int days) => 0;

        public int CountCustodyMovements(DateTime from, DateTime to) => 0;

        public int GetWarrantyThresholdDays() => 30;

        public int GetInsuranceThresholdDays() => 30;

        public int GetMaintenanceThresholdDays() => 14;
    }

    internal class PermissiveDepartmentScopeService : IDepartmentScopeService
    {
        public bool BypassesDepartmentScope => true;

        public int? ScopedDepartmentId => null;

        public IQueryable<Asset> ApplyAssetScope(IQueryable<Asset> query) => query;

        public void EnsureCanAccessAsset(Asset asset)
        {
        }

        public IQueryable<Department> ApplyDepartmentScope(IQueryable<Department> query) => query;

        public void EnsureCanAccessDepartment(Department department)
        {
        }

        public void EnsureCanAccessDepartmentId(int departmentId)
        {
        }

        public int CountVisibleDepartments(bool activeOnly = true) => 0;
    }

    internal class NoOpDepartmentScopeService : IDepartmentScopeService
    {
        public bool BypassesDepartmentScope => true;

        public int? ScopedDepartmentId => null;

        public IQueryable<Asset> ApplyAssetScope(IQueryable<Asset> query) => query;

        public void EnsureCanAccessAsset(Asset asset)
        {
        }

        public IQueryable<Department> ApplyDepartmentScope(IQueryable<Department> query) => query;

        public void EnsureCanAccessDepartment(Department department)
        {
        }

        public void EnsureCanAccessDepartmentId(int departmentId)
        {
        }

        public int CountVisibleDepartments(bool activeOnly = true) => 0;
    }

    internal class StrictDepartmentScopeService : IDepartmentScopeService
    {
        private readonly int? _departmentId;

        public StrictDepartmentScopeService(int? departmentId)
        {
            _departmentId = departmentId;
        }

        public bool BypassesDepartmentScope => false;

        public int? ScopedDepartmentId => _departmentId;

        public IQueryable<Asset> ApplyAssetScope(IQueryable<Asset> query)
        {
            if (query == null)
            {
                return query;
            }

            if (!_departmentId.HasValue)
            {
                return query.Where(x => false);
            }

            return query.Where(x => x.DepartmentId == _departmentId.Value);
        }

        public void EnsureCanAccessAsset(Asset asset)
        {
            if (asset == null)
            {
                return;
            }

            if (!_departmentId.HasValue)
            {
                throw new BusinessException("Your account is not assigned to a department. Contact an administrator.");
            }

            if (asset.DepartmentId != _departmentId.Value)
            {
                throw new BusinessException("This asset belongs to another department. Only administrators can access it.");
            }
        }

        public IQueryable<Department> ApplyDepartmentScope(IQueryable<Department> query)
        {
            if (query == null)
            {
                return query;
            }

            if (!_departmentId.HasValue)
            {
                return query.Where(x => false);
            }

            return query.Where(x => x.Id == _departmentId.Value);
        }

        public void EnsureCanAccessDepartment(Department department)
        {
            if (department != null && _departmentId.HasValue && department.Id != _departmentId.Value)
            {
                throw new BusinessException("This department is outside your scope. Only administrators can access it.");
            }
        }

        public void EnsureCanAccessDepartmentId(int departmentId)
        {
            if (_departmentId.HasValue && departmentId != _departmentId.Value)
            {
                throw new BusinessException("This department is outside your scope. Only administrators can access it.");
            }
        }

        public int CountVisibleDepartments(bool activeOnly = true) => _departmentId.HasValue ? 1 : 0;
    }

    internal class NoOpAuditWriter : IAuditWriter
    {
        public void Write(string action, string entityType, string entityId, string oldValues, string newValues)
        {
        }
    }

    internal class FakeOrganizationScopeService : IOrganizationScopeService
    {
        private readonly int? _organizationId;
        private readonly bool _companyAdmin;
        private readonly bool _impersonating;
        private readonly bool _platformAdmin;

        public FakeOrganizationScopeService(
            int organizationId = 1,
            bool companyAdmin = true,
            bool impersonating = false,
            bool platformAdmin = false)
        {
            _organizationId = organizationId;
            _companyAdmin = companyAdmin;
            _impersonating = impersonating;
            _platformAdmin = platformAdmin;
        }

        public int? GetCurrentOrganizationId() => _organizationId;

        public int? GetTenantFilterOrganizationId(Type entityType) => _organizationId;

        public void SetOrganizationFilterOverride(int? organizationId)
        {
        }

        public bool IsImpersonating() => _impersonating;

        public bool IsPlatformAdmin() => _platformAdmin;

        public bool IsActualPlatformAdmin() => _platformAdmin;

        public bool IsCompanyAdmin() => _companyAdmin;

        public string GetImpersonationReason() => null;

        public IQueryable<T> ApplyOrganizationFilter<T>(IQueryable<T> query) where T : class
        {
            if (query == null || !_organizationId.HasValue)
            {
                return query;
            }

            var tenantEntityType = typeof(ITenantEntity);
            if (!tenantEntityType.IsAssignableFrom(typeof(T)))
            {
                return query;
            }

            return query.Cast<ITenantEntity>()
                .Where(x => x.OrganizationId.HasValue && x.OrganizationId.Value == _organizationId.Value)
                .Cast<T>();
        }
    }

    internal class NoOpWorkflowGuard : IAssetWorkflowGuard
    {
        public void EnsureNoBlockingWorkflow(int assetId)
        {
        }
    }

    internal class RealWorkflowGuard : IAssetWorkflowGuard
    {
        private readonly AssetManagement.Application.Services.AssetWorkflowGuard _guard;

        public RealWorkflowGuard(IUnitOfWork unitOfWork)
        {
            _guard = new AssetManagement.Application.Services.AssetWorkflowGuard(unitOfWork);
        }

        public void EnsureNoBlockingWorkflow(int assetId)
        {
            _guard.EnsureNoBlockingWorkflow(assetId);
        }
    }

    internal class NoOpOutboxWriter : IOutboxWriter
    {
        public void Enqueue(string messageType, string payloadJson)
        {
        }
    }

    internal class NoOpWebhookService : IWebhookService
    {
        public IEnumerable<WebhookSubscriptionVm> GetSubscriptions() => new List<WebhookSubscriptionVm>();

        public int Register(WebhookSubscriptionEditVm model, string createdByUserId) => 0;

        public void QueueDelivery(string eventType, string payloadJson)
        {
        }

        public void Deactivate(int id)
        {
        }
    }

    internal class NoOpAuthorizationService : IAuthorizationService
    {
        public bool HasPermission(string userId, string permissionCode) => true;
    }

    internal class DenyAllAuthorizationService : IAuthorizationService
    {
        public bool HasPermission(string userId, string permissionCode) => false;
    }

    internal class FakeCurrentUserContext : ICurrentUserContext
    {
        public FakeCurrentUserContext(string userId)
        {
            UserId = userId;
            UserName = userId;
        }

        public string UserId { get; private set; }

        public string UserName { get; private set; }

        public string IPAddress => "127.0.0.1";
    }

    internal class FakeAssetScanLookupRepository : IAssetScanLookupRepository
    {
        private readonly IUnitOfWork _unitOfWork;

        public FakeAssetScanLookupRepository(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public bool ExistsByScanCode(string code, int organizationId)
        {
            return FindByScanCode(code, organizationId, null) != null;
        }

        public AssetScanLookupResult FindByScanCode(string code, int organizationId, int? departmentId)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            var lookupKey = code.Trim();
            var match = _unitOfWork.Repository<Asset>().GetAll()
                .Where(x => x.IsActive)
                .Where(x => x.OrganizationId == organizationId)
                .Where(x => !departmentId.HasValue || x.DepartmentId == departmentId.Value)
                .Where(x => ScanCodeHelper.FieldMatchesLookupKey(x.AssetTag, lookupKey)
                    || ScanCodeHelper.FieldMatchesLookupKey(x.BarcodeOrQRCode, lookupKey)
                    || ScanCodeHelper.FieldMatchesLookupKey(x.SerialNumber, lookupKey))
                .OrderBy(x => ScanCodeHelper.FieldMatchesLookupKey(x.AssetTag, lookupKey) ? 0
                    : ScanCodeHelper.FieldMatchesLookupKey(x.BarcodeOrQRCode, lookupKey) ? 1 : 2)
                .FirstOrDefault();

            if (match == null)
            {
                return null;
            }

            return new AssetScanLookupResult
            {
                Id = match.Id,
                AssetTag = match.AssetTag,
                AssetName = match.AssetName,
                CurrentStatus = match.CurrentStatus,
                BarcodeOrQRCode = match.BarcodeOrQRCode,
                SerialNumber = match.SerialNumber,
                Brand = match.Brand,
                Model = match.Model
            };
        }
    }

    internal class FakeOperationsQueryRepository : IOperationsQueryRepository
    {
        private readonly FakeUnitOfWork _unitOfWork;

        public FakeOperationsQueryRepository()
        {
        }

        public FakeOperationsQueryRepository(FakeUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IList<PurchaseRequestListItemVm> GetPurchaseRequestList(
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope)
        {
            return new List<PurchaseRequestListItemVm>();
        }

        public AssetRequestListPageVm GetAssetRequestListPage(
            AssetRequestFilterVm filter,
            string sort,
            string direction,
            int page,
            int pageSize,
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            bool restrictToOwnDepartment)
        {
            if (_unitOfWork == null || denyDepartmentScope)
            {
                return new AssetRequestListPageVm { Items = new List<AssetRequestListVm>(), TotalCount = 0, Page = page, PageSize = pageSize };
            }

            var query = _unitOfWork.Repository<AssetRequest>().Query().Where(x => x.IsActive);
            if (!bypassDepartmentScope && departmentId.HasValue)
            {
                query = query.Where(x => x.DepartmentId == departmentId.Value);
            }

            var items = query
                .Select(x => new AssetRequestListVm { Id = x.Id })
                .ToList();

            return new AssetRequestListPageVm
            {
                Items = items,
                TotalCount = items.Count,
                Page = page,
                PageSize = pageSize
            };
        }

        public AssignmentListPageVm GetAssignmentListPage(
            AssignmentFilterVm filter,
            string sort,
            string direction,
            int page,
            int pageSize,
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope)
        {
            return new AssignmentListPageVm { Items = new List<AssignmentListVm>(), TotalCount = 0, Page = page, PageSize = pageSize };
        }

        public IList<PurchaseRecordVm> GetPurchaseRecordList(int organizationId)
        {
            return new List<PurchaseRecordVm>();
        }

        public bool ExistsActiveAssetTag(int organizationId, string assetTag) => false;

        public bool ExistsActiveSerialNumber(int organizationId, string serialNumber) => false;
    }

    internal class FakeAssetQueryService : IAssetQueryService
    {
        public AssetListPageVm GetListPage(AssetFilterVm filter, string sort, string direction, int page, int pageSize)
        {
            return new AssetListPageVm { Items = new List<AssetListVm>(), TotalCount = 0, Page = page, PageSize = pageSize };
        }

        public int Count(AssetFilterVm filter) => 0;

        public AssetExportResultVm StreamExport(AssetFilterVm filter, string sort, string direction, Action<AssetExportRowVm> writeRow, int? maxRows = null)
        {
            return new AssetExportResultVm { RowCount = 0, Truncated = false };
        }
    }

    internal class FakeDashboardQueryService : IDashboardQueryService
    {
        public DashboardKpisDto GetKpis(int organizationId, int? departmentId, bool bypassDepartmentScope, bool denyDepartmentScope)
        {
            return new DashboardKpisDto();
        }
    }

    internal class FakeNotificationQueryService : INotificationQueryService
    {
        private readonly FakeUnitOfWork _unitOfWork;

        public FakeNotificationQueryService(FakeUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IList<NotificationInboxVm> GetInbox(string userId, bool unreadOnly, int take)
        {
            return new List<NotificationInboxVm>();
        }

        public bool ExistsByIdempotencyKey(string userId, string idempotencyKey)
        {
            return _unitOfWork.Repository<Notification>().Find(x =>
                x.IdempotencyKey == idempotencyKey
                && (x.UserId == userId || string.IsNullOrWhiteSpace(x.UserId))).Any();
        }
    }

    internal class FakeNotificationScheduleQueryService : INotificationScheduleQueryService
    {
        public IList<int> GetActiveOrganizationIds() => new List<int>();

        public IList<ScheduledAssetRow> GetExpiringWarranties(int organizationId, DateTime nowUtc, int thresholdDays) => new List<ScheduledAssetRow>();

        public IList<ScheduledInsuranceRow> GetExpiringInsurance(int organizationId, DateTime nowUtc, int thresholdDays) => new List<ScheduledInsuranceRow>();

        public IList<ScheduledAssignmentRow> GetDueSoonAssignments(int organizationId, DateTime nowUtc, int thresholdDays) => new List<ScheduledAssignmentRow>();

        public IList<ScheduledAssignmentRow> GetOverdueAssignments(int organizationId, DateTime nowUtc) => new List<ScheduledAssignmentRow>();

        public IList<ScheduledApprovalRow> GetPendingTransferApprovals(int organizationId) => new List<ScheduledApprovalRow>();

        public IList<ScheduledApprovalRow> GetPendingDisposalApprovals(int organizationId) => new List<ScheduledApprovalRow>();
    }

    internal class FakeAuditLogQueryRepository : IAuditLogQueryRepository
    {
        private readonly FakeUnitOfWork _unitOfWork;

        public FakeAuditLogQueryRepository(FakeUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IList<AuditLogVm> GetLogs(
            AuditLogFilterVm filter,
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            string actorUserId)
        {
            return _unitOfWork.Repository<AuditLog>().GetAll()
                .Select(x => new AuditLogVm
                {
                    Id = x.Id,
                    ActorUserId = x.ActorUserId,
                    Action = x.Action,
                    EntityType = x.EntityType,
                    EntityId = x.EntityId,
                    Timestamp = x.Timestamp,
                    IPAddress = x.IPAddress
                })
                .ToList();
        }
    }

    internal class FakeReferenceDataCache : IReferenceDataCache
    {
        public IList<DepartmentVm> GetDepartments(int organizationId, bool activeOnly = true) => new List<DepartmentVm>();

        public IList<RoleVm> GetRoles(int organizationId) => new List<RoleVm>();

        public IDictionary<string, string> GetSettings(int organizationId) => new Dictionary<string, string>();

        public IList<UserVm> GetUsersForDropdown(int organizationId, int? departmentId = null) => new List<UserVm>();

        public IList<UserVm> GetUsersByIds(int organizationId, IEnumerable<string> userIds) => new List<UserVm>();

        public IList<CategoryLookupVm> GetCategories(int organizationId, bool activeOnly = true) => new List<CategoryLookupVm>();

        public IList<AssetTypeLookupVm> GetAssetTypes(int organizationId, bool activeOnly = true) => new List<AssetTypeLookupVm>();

        public IList<SupplierVm> GetSuppliers(int organizationId, bool activeOnly = true) => new List<SupplierVm>();

        public void InvalidateDepartments(int organizationId)
        {
        }

        public void InvalidateRoles(int organizationId)
        {
        }

        public void InvalidateSettings(int organizationId)
        {
        }
    }

    internal class FakeApprovalWorkflowEngine : IApprovalWorkflowEngine
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditWriter _auditWriter;
        private readonly bool _simulateDuplicateStage;

        public FakeApprovalWorkflowEngine(IUnitOfWork unitOfWork, IAuditWriter auditWriter, bool simulateDuplicateStage = false)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
            _simulateDuplicateStage = simulateDuplicateStage;
        }

        public void ExecuteStageDecision(ApprovalStageDecisionRequest request)
        {
            if (_simulateDuplicateStage)
            {
                throw new BusinessException("This approval stage has already been recorded by another approver.");
            }

            new AssetManagement.Application.Services.ApprovalWorkflowEngine(_unitOfWork, _auditWriter)
                .ExecuteStageDecision(request);
        }
    }

    internal class FakeUnitOfWork : IUnitOfWork
    {
        private readonly Dictionary<Type, object> _repositories = new Dictionary<Type, object>();
        private readonly HashSet<string> _conditionalKeys = new HashSet<string>(StringComparer.Ordinal);
        public bool FailConditionalOnDuplicate { get; set; }

        public IRepository<T> Repository<T>() where T : class, new()
        {
            var type = typeof(T);
            if (!_repositories.ContainsKey(type))
            {
                _repositories[type] = new FakeRepository<T>();
            }

            return (IRepository<T>)_repositories[type];
        }

        public void Seed<T>(T entity) where T : class, new()
        {
            Repository<T>().Add(entity);
        }

        public int SaveChanges() => 1;

        public IEntityWriter<T> Writer<T>() where T : class, new()
        {
            return new FakeEntityWriter<T>(Repository<T>());
        }

        public void BeginTransaction()
        {
        }

        public void Commit()
        {
        }

        public void Rollback()
        {
        }

        public void ExecuteInTransaction(Action action)
        {
            action();
        }

        public void TrackAdd(object entity)
        {
            if (entity == null)
            {
                return;
            }

            var addMethod = typeof(FakeUnitOfWork).GetMethod("TrackAddTyped", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var generic = addMethod.MakeGenericMethod(entity.GetType());
            generic.Invoke(this, new[] { entity });
        }

        private void TrackAddTyped<T>(T entity) where T : class, new()
        {
            Repository<T>().Add(entity);
        }

        public void PersistConditionalApprovalUpdate(object entity, int expectedStage)
        {
            if (entity == null)
            {
                return;
            }

            var key = entity.GetType().FullName + ":" + expectedStage;
            if (FailConditionalOnDuplicate && !_conditionalKeys.Add(key))
            {
                throw new BusinessException("This approval stage has already been recorded by another approver.");
            }

            var updateMethod = typeof(FakeUnitOfWork).GetMethod("PersistConditionalApprovalUpdateTyped", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var generic = updateMethod.MakeGenericMethod(entity.GetType());
            generic.Invoke(this, new[] { entity, expectedStage });
        }

        private void PersistConditionalApprovalUpdateTyped<T>(T entity, int expectedStage) where T : class, new()
        {
            Repository<T>().Update(entity);
        }

        public int GetRemainingPurchaseQuantity(int purchaseRecordId) => 0;

        public void Dispose()
        {
        }
    }

    internal class FakeEntityWriter<T> : IEntityWriter<T> where T : class, new()
    {
        private readonly IRepository<T> _repository;

        public FakeEntityWriter(IRepository<T> repository)
        {
            _repository = repository;
        }

        public T GetById(object id) => _repository.GetById(id);

        public void Add(T entity) => _repository.Add(entity);

        public void Update(T entity) => _repository.Update(entity);

        public void Remove(T entity) => _repository.Remove(entity);
    }

    internal class FakeRepository<T> : IRepository<T> where T : class, new()
    {
        private readonly List<T> _items = new List<T>();

        public IQueryable<T> Query() => _items.AsQueryable();

        public IEnumerable<T> GetAll() => _items.ToList();

        public IEnumerable<T> Find(Expression<Func<T, bool>> predicate) => _items.AsQueryable().Where(predicate).ToList();

        public T GetById(object id)
        {
            var prop = typeof(T).GetProperty("Id");
            return _items.FirstOrDefault(x => prop != null && Equals(prop.GetValue(x, null), id));
        }

        public void Add(T entity)
        {
            if (typeof(T).GetProperty("Id") != null)
            {
                var idProp = typeof(T).GetProperty("Id");
                if (idProp.PropertyType == typeof(int) && (int)idProp.GetValue(entity, null) == 0)
                {
                    var nextId = _items.Count == 0 ? 1 : _items.Max(x => (int)idProp.GetValue(x, null)) + 1;
                    idProp.SetValue(entity, nextId, null);
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

            var idValue = prop.GetValue(entity, null);
            var existing = _items.FirstOrDefault(x => Equals(prop.GetValue(x, null), idValue));
            if (existing != null)
            {
                _items.Remove(existing);
            }

            _items.Add(entity);
        }

        public void Remove(T entity) => _items.Remove(entity);
    }

    internal class NoOpFileStorageProvider : IFileStorageProvider
    {
        public string Save(Stream stream, string fileName, string contentType, string folder)
        {
            if (stream != null)
            {
                stream.Dispose();
            }

            return folder + "/" + fileName;
        }

        public void Delete(string relativePath)
        {
        }

        public string GetFullPath(string relativePath) => relativePath;
    }
}

