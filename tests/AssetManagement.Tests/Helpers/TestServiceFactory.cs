using AssetManagement.Application.Contracts;
using AssetManagement.Application.Services;
using AssetManagement.Application.Services.Organizations;
using AssetManagement.Application.ViewModels.Organizations;

namespace AssetManagement.Tests.Helpers
{
    internal static class TestServiceFactory
    {
        public static AssignmentService CreateAssignmentService(
            FakeUnitOfWork unitOfWork,
            FakeUserService users = null,
            IDepartmentScopeService departmentScope = null,
            IAssetWorkflowGuard workflowGuard = null)
        {
            return new AssignmentService(
                unitOfWork,
                new NoOpAuditWriter(),
                users ?? new FakeUserService(),
                departmentScope ?? new NoOpDepartmentScopeService(),
                workflowGuard ?? new NoOpWorkflowGuard(),
                new FakeOperationsQueryRepository(),
                new FakeOrganizationScopeService());
        }

        public static AssetService CreateAssetService(
            FakeUnitOfWork unitOfWork,
            IDepartmentScopeService departmentScope = null,
            FakeUserService users = null,
            IApprovalWorkflowEngine approvalEngine = null)
        {
            return new AssetService(
                unitOfWork,
                new NoOpAuditWriter(),
                departmentScope ?? new NoOpDepartmentScopeService(),
                new FakeAssetScanLookupRepository(unitOfWork),
                users ?? new FakeUserService(),
                new FakeCurrentUserContext("test-user"),
                new FakeOrganizationScopeService(),
                new FakeAssetQueryService(),
                new NoOpWorkflowGuard(),
                approvalEngine ?? new FakeApprovalWorkflowEngine(unitOfWork, new NoOpAuditWriter()),
                new NoOpOutboxWriter(),
                new NoOpWebhookService(),
                new FakeReferenceDataCache());
        }

        public static TransferService CreateTransferService(
            FakeUnitOfWork unitOfWork,
            FakeUserService users = null,
            IDepartmentScopeService departmentScope = null,
            IApprovalWorkflowEngine approvalEngine = null)
        {
            return new TransferService(
                unitOfWork,
                new NoOpAuditWriter(),
                users ?? new FakeUserService(),
                departmentScope ?? new NoOpDepartmentScopeService(),
                new FakeOrganizationScopeService(),
                new NoOpOutboxWriter(),
                new NoOpWebhookService(),
                new NoOpWorkflowGuard(),
                approvalEngine ?? new FakeApprovalWorkflowEngine(unitOfWork, new NoOpAuditWriter()));
        }

        public static ReturnService CreateReturnService(FakeUnitOfWork unitOfWork, FakeUserService users = null)
        {
            return new ReturnService(
                unitOfWork,
                new NoOpAuditWriter(),
                users ?? new FakeUserService(),
                new NoOpWorkflowGuard());
        }

        public static MaintenanceService CreateMaintenanceService(
            FakeUnitOfWork unitOfWork,
            FakeUserService users = null,
            IDepartmentScopeService departmentScope = null,
            IAssignmentService assignmentService = null)
        {
            var userService = users ?? new FakeUserService();
            return new MaintenanceService(
                unitOfWork,
                new NoOpAuditWriter(),
                departmentScope ?? new NoOpDepartmentScopeService(),
                assignmentService ?? CreateAssignmentService(unitOfWork, userService, workflowGuard: new AssetWorkflowGuard(unitOfWork)),
                userService);
        }

        public static NotificationService CreateNotificationService(FakeUnitOfWork unitOfWork)
        {
            return new NotificationService(
                unitOfWork,
                new PermissiveDepartmentScopeService(),
                new FakeNotificationQueryService(unitOfWork),
                new FakeNotificationScheduleQueryService(),
                new FakeOrganizationScopeService(),
                new NoOpOutboxWriter());
        }

        public static ReportService CreateReportService(FakeUnitOfWork unitOfWork)
        {
            return new ReportService(
                unitOfWork,
                new PermissiveDepartmentScopeService(),
                new PermissiveMetricsService(),
                new FakeDashboardQueryService(),
                new FakeOrganizationScopeService(),
                new FakeAssetQueryService());
        }

        public static PurchaseService CreatePurchaseService(FakeUnitOfWork unitOfWork)
        {
            return new PurchaseService(
                unitOfWork,
                new FakeOperationsQueryRepository(),
                new FakeOrganizationScopeService());
        }

        public static WebhookService CreateWebhookService(FakeUnitOfWork unitOfWork, IAuditWriter auditWriter = null)
        {
            return new WebhookService(
                unitOfWork,
                auditWriter ?? new NoOpAuditWriter(),
                new NoOpOutboxWriter(),
                new FakeOrganizationScopeService());
        }

        public static AuditLogService CreateAuditLogService(FakeUnitOfWork unitOfWork, string userId = "user-1")
        {
            return new AuditLogService(
                new FakeAuditLogQueryRepository(unitOfWork),
                new NoOpDepartmentScopeService(),
                new FakeCurrentUserContext(userId),
                new FakeOrganizationScopeService());
        }

        public static AssetRequestService CreateAssetRequestService(
            FakeUnitOfWork unitOfWork,
            IDepartmentScopeService departmentScope)
        {
            return new AssetRequestService(
                unitOfWork,
                new NoOpAuditWriter(),
                CreateAssignmentService(unitOfWork),
                new SegregationOfDutiesService(),
                departmentScope,
                new FakeUserService(),
                new NoOpAuthorizationService(),
                new FakeCurrentUserContext("user-a"),
                new FakeOrganizationScopeService(),
                new NoOpOutboxWriter(),
                new FakeOperationsQueryRepository(unitOfWork));
        }

        public static AssetBulkService CreateAssetBulkService(
            IUnitOfWork unitOfWork,
            IDepartmentScopeService departmentScope = null,
            IAuthorizationService authorization = null)
        {
            return new AssetBulkService(
                unitOfWork,
                new NoOpAuditWriter(),
                departmentScope ?? new NoOpDepartmentScopeService(),
                new NoOpWorkflowGuard(),
                authorization ?? new NoOpAuthorizationService());
        }

        public static AssetDocumentService CreateAssetDocumentService(
            FakeUnitOfWork unitOfWork,
            IDepartmentScopeService departmentScope,
            IAuthorizationService authorization = null,
            ICurrentUserContext currentUser = null)
        {
            return new AssetDocumentService(
                unitOfWork,
                new NoOpFileStorageProvider(),
                new FakeUserService(),
                new NoOpAuditWriter(),
                departmentScope,
                authorization ?? new NoOpAuthorizationService(),
                currentUser ?? new FakeCurrentUserContext("user-1"));
        }

        public static OrganizationLicenseService CreateOrganizationLicenseService(FakeUnitOfWork unitOfWork)
        {
            return new OrganizationLicenseService(
                unitOfWork,
                new FakeOrganizationLicenseQueryRepository(),
                new NoOpAuditWriter(),
                new FakeOrganizationScopeService(platformAdmin: true));
        }
    }

    internal class FakeOrganizationLicenseQueryRepository : Application.Contracts.Organizations.IOrganizationLicenseQueryRepository
    {
        public int CountLicenses(LicenseListFilterVm filter) => 0;

        public System.Collections.Generic.IList<LicenseListItemVm> GetLicensePage(
            LicenseListFilterVm filter,
            string sort,
            string direction,
            int skip,
            int take)
        {
            return new System.Collections.Generic.List<LicenseListItemVm>();
        }

        public System.Collections.Generic.IList<LicenseHistoryItemVm> GetHistoryForOrganization(int organizationId)
        {
            return new System.Collections.Generic.List<LicenseHistoryItemVm>();
        }

        public System.Collections.Generic.IList<Application.Contracts.Organizations.LicenseExpiryCandidateVm> GetLicensesDueForExpiry()
        {
            return new System.Collections.Generic.List<Application.Contracts.Organizations.LicenseExpiryCandidateVm>();
        }
    }
}
