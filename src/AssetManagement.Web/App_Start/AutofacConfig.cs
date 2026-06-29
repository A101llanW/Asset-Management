using System.Reflection;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Contracts.Organizations;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Services;
using AssetManagement.Application.Services.Organizations;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Queries;
using AssetManagement.Infrastructure.Repositories;
using AssetManagement.Infrastructure.Services;
using Autofac;
using Autofac.Integration.Mvc;

namespace AssetManagement.Web.App_Start
{
    public static class AutofacConfig
    {
        public static void Register()
        {
            var builder = new ContainerBuilder();
            builder.RegisterControllers(Assembly.GetExecutingAssembly());

            builder.RegisterType<SqlConnectionFactory>().As<ISqlConnectionFactory>().InstancePerHttpRequest();
            builder.RegisterType<UnitOfWork>().As<IUnitOfWork>().InstancePerHttpRequest();
            builder.RegisterType<AssetScanLookupRepository>().As<IAssetScanLookupRepository>().InstancePerHttpRequest();
            builder.RegisterType<HttpCurrentUserContext>().As<ICurrentUserContext>().InstancePerHttpRequest();
            builder.RegisterType<AuditWriter>().As<IAuditWriter>().InstancePerHttpRequest();
            builder.RegisterType<OutboxWriter>().As<IOutboxWriter>().InstancePerHttpRequest();
            builder.RegisterType<OutboxDispatcher>().As<IOutboxDispatcher>().InstancePerHttpRequest();
            builder.RegisterType<AuthorizationService>().As<IAuthorizationService>().InstancePerHttpRequest();
            builder.RegisterType<DepartmentScopeService>().As<IDepartmentScopeService>().InstancePerHttpRequest();
            builder.RegisterType<OrganizationScopeService>().As<IOrganizationScopeService>().InstancePerHttpRequest();
            builder.RegisterType<OrganizationService>().As<IOrganizationService>().InstancePerHttpRequest();
            builder.RegisterType<OrganizationLicenseService>().As<IOrganizationLicenseService>().InstancePerHttpRequest();
            builder.RegisterType<OrganizationLicenseQueryRepository>().As<IOrganizationLicenseQueryRepository>().InstancePerHttpRequest();
            builder.RegisterType<AssetQueryService>().As<IAssetQueryService>().InstancePerHttpRequest();
            builder.RegisterType<NotificationQueryService>().As<INotificationQueryService>().InstancePerHttpRequest();
            builder.RegisterType<NotificationScheduleQueryService>().As<INotificationScheduleQueryService>().InstancePerHttpRequest();
            builder.RegisterType<DashboardQueryService>().As<IDashboardQueryService>().InstancePerHttpRequest();
            builder.RegisterType<SearchQueryService>().As<ISearchQueryService>().InstancePerHttpRequest();
            builder.RegisterType<PendingApprovalQueryRepository>().As<IPendingApprovalQueryRepository>().InstancePerHttpRequest();
            builder.RegisterType<ReferenceDataCache>().As<IReferenceDataCache>().InstancePerHttpRequest();
            builder.RegisterType<UserAccountQueryRepository>().As<IUserAccountQueryRepository>().InstancePerHttpRequest();
            builder.RegisterType<OperationsQueryRepository>().As<IOperationsQueryRepository>().InstancePerHttpRequest();
            builder.RegisterType<AuditLogQueryRepository>().As<IAuditLogQueryRepository>().InstancePerHttpRequest();
            builder.RegisterType<MembershipService>().As<IUserAccountService>().InstancePerHttpRequest();
            builder.RegisterType<AccountSecurityService>().As<IAccountSecurityService>().InstancePerHttpRequest();
            builder.RegisterType<EmailService>().As<IEmailService>().InstancePerHttpRequest();
            builder.RegisterType<PlatformSettingsService>().As<IPlatformSettingsService>().InstancePerHttpRequest();
            builder.RegisterType<SecurityLogQueryRepository>().As<ISecurityLogQueryRepository>().InstancePerHttpRequest();
            builder.RegisterType<SecurityLogService>().As<ISecurityLogService>().InstancePerHttpRequest();

            builder.RegisterType<AssetService>().As<IAssetService>().InstancePerHttpRequest();
            builder.RegisterType<SearchService>().As<ISearchService>().InstancePerHttpRequest();
            builder.RegisterType<AssetBulkService>().As<IAssetBulkService>().InstancePerHttpRequest();
            builder.RegisterType<AssetImportService>().As<IAssetImportService>().InstancePerHttpRequest();
            builder.RegisterType<CustodianService>().As<ICustodianService>().InstancePerHttpRequest();
            builder.RegisterType<AssetRequestService>().As<IAssetRequestService>().InstancePerHttpRequest();
            builder.RegisterType<RoleService>().As<IRoleService>().InstancePerHttpRequest();
            builder.RegisterType<RoleTemplateService>().As<IRoleTemplateService>().InstancePerHttpRequest();
            builder.RegisterType<PermissionService>().As<IPermissionService>().InstancePerHttpRequest();
            builder.RegisterType<DepartmentService>().As<IDepartmentService>().InstancePerHttpRequest();
            builder.RegisterType<SupplierService>().As<ISupplierService>().InstancePerHttpRequest();
            builder.RegisterType<SupplierCatalogService>().As<ISupplierCatalogService>().InstancePerHttpRequest();
            builder.RegisterType<AssignmentService>().As<IAssignmentService>().InstancePerHttpRequest();
            builder.RegisterType<TransferService>().As<ITransferService>().InstancePerHttpRequest();
            builder.RegisterType<ReturnService>().As<IReturnService>().InstancePerHttpRequest();
            builder.RegisterType<MaintenanceService>().As<IMaintenanceService>().InstancePerHttpRequest();
            builder.RegisterType<IncidentService>().As<IIncidentService>().InstancePerHttpRequest();
            builder.RegisterType<ClaimService>().As<IClaimService>().InstancePerHttpRequest();
            builder.RegisterType<PurchaseService>().As<IPurchaseService>().InstancePerHttpRequest();
            builder.RegisterType<ReceivingService>().As<IReceivingService>().InstancePerHttpRequest();
            builder.RegisterType<PurchaseRequestService>().As<IPurchaseRequestService>().InstancePerHttpRequest();
            builder.RegisterType<AuditLogService>().As<IAuditLogService>().InstancePerHttpRequest();
            builder.RegisterType<ReportService>().As<IReportService>().InstancePerHttpRequest();
            builder.RegisterType<MetricsService>().As<IMetricsService>().InstancePerHttpRequest();
            builder.RegisterType<PendingApprovalQueryService>().As<IPendingApprovalQueryService>().InstancePerHttpRequest();
            builder.RegisterType<NotificationService>().As<INotificationService>().InstancePerHttpRequest();
            builder.RegisterType<UserService>().As<IUserService>().InstancePerHttpRequest();
            builder.RegisterType<ApprovalWorkflowService>().As<IApprovalWorkflowService>().InstancePerHttpRequest();
            builder.RegisterType<ApprovalWorkflowEngine>().As<IApprovalWorkflowEngine>().InstancePerHttpRequest();
            builder.RegisterType<AssetWorkflowGuard>().As<IAssetWorkflowGuard>().InstancePerHttpRequest();
            builder.RegisterType<SegregationOfDutiesService>().As<ISegregationOfDutiesService>().InstancePerHttpRequest();
            builder.RegisterType<InsurancePolicyService>().As<IInsurancePolicyService>().InstancePerHttpRequest();
            builder.RegisterType<WebhookService>().As<IWebhookService>().InstancePerHttpRequest();
            builder.RegisterType<FormsSsoAuthenticationProvider>().As<ISsoAuthenticationProvider>().SingleInstance();

            AttachmentStorageRegistration.Register(builder);

            var container = builder.Build();
            DependencyResolver.SetResolver(new AutofacDependencyResolver(container));
        }
    }
}
