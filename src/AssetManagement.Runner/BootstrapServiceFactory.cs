using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Organizations;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Services;
using AssetManagement.Application.Services.Organizations;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Queries;
using AssetManagement.Infrastructure.Repositories;
using AssetManagement.Infrastructure.Services;

namespace AssetManagement.Runner
{
    internal static class BootstrapServiceFactory
    {
        public static ISchoolOrganizationBootstrapService CreateSchoolBootstrap(string connectionStringName = "AssetManagementConnection")
        {
            var organizationScope = CreateOrganizationScope(connectionStringName);
            return new SchoolOrganizationBootstrapService(
                CreateOrganizationService(connectionStringName, organizationScope),
                CreateAssetImportService(connectionStringName, organizationScope),
                CreateUnitOfWork(connectionStringName, organizationScope),
                organizationScope,
                CreateReferenceDataCache(connectionStringName));
        }

        public static IAssetImportService CreateAssetImportService(string connectionStringName = "AssetManagementConnection")
        {
            return CreateAssetImportService(connectionStringName, CreateOrganizationScope(connectionStringName));
        }

        private static IAssetImportService CreateAssetImportService(string connectionStringName, OrganizationScopeService organizationScope)
        {
            var unitOfWork = CreateUnitOfWork(connectionStringName, organizationScope);
            var currentUser = new BootstrapCurrentUserContext();
            var connectionFactory = new SqlConnectionFactory(connectionStringName);
            var outboxWriter = new OutboxWriter(unitOfWork, organizationScope);
            var auditWriter = new AuditWriter(outboxWriter, unitOfWork, currentUser, organizationScope);
            var referenceDataCache = new ReferenceDataCache(connectionFactory);
            var authorizationService = new AuthorizationService(unitOfWork, organizationScope, connectionFactory);
            var roleService = new RoleService(
                unitOfWork,
                auditWriter,
                authorizationService,
                organizationScope,
                currentUser);
            var userAccountQueryRepository = new UserAccountQueryRepository(connectionFactory);
            var userService = new UserService(
                unitOfWork,
                authorizationService,
                organizationScope,
                currentUser,
                auditWriter,
                userAccountQueryRepository,
                referenceDataCache);
            var departmentScope = new DepartmentScopeService(
                unitOfWork,
                currentUser,
                userService,
                organizationScope,
                authorizationService);
            var operationsQueryRepository = new OperationsQueryRepository(connectionFactory);
            var workflowGuard = new AssetWorkflowGuard(unitOfWork);
            var assignmentService = new AssignmentService(
                unitOfWork,
                auditWriter,
                userService,
                departmentScope,
                workflowGuard,
                operationsQueryRepository,
                organizationScope);
            var assetStockService = new AssetStockService(unitOfWork);
            var assetSubTypeService = new AssetSubTypeService(unitOfWork, assetStockService);
            return new AssetImportService(
                unitOfWork,
                assignmentService,
                departmentScope,
                roleService,
                auditWriter,
                operationsQueryRepository,
                organizationScope,
                referenceDataCache,
                assetSubTypeService);
        }

        private static OrganizationService CreateOrganizationService(string connectionStringName)
        {
            return CreateOrganizationService(connectionStringName, CreateOrganizationScope(connectionStringName));
        }

        private static OrganizationService CreateOrganizationService(string connectionStringName, OrganizationScopeService organizationScope)
        {
            var unitOfWork = CreateUnitOfWork(connectionStringName, organizationScope);
            var connectionFactory = new SqlConnectionFactory(connectionStringName);
            var currentUser = new BootstrapCurrentUserContext();
            var outboxWriter = new OutboxWriter(unitOfWork, organizationScope);
            var auditWriter = new AuditWriter(outboxWriter, unitOfWork, currentUser, organizationScope);
            var platformSettings = new PlatformSettingsService(connectionFactory);
            var emailService = new EmailService(platformSettings);
            var licenseQueryRepository = new OrganizationLicenseQueryRepository(connectionFactory);
            var licenseService = new OrganizationLicenseService(
                unitOfWork,
                licenseQueryRepository,
                auditWriter,
                organizationScope);
            var userAccountService = new MembershipService(
                connectionFactory,
                organizationScope,
                licenseService,
                emailService,
                platformSettings,
                unitOfWork);
            return new OrganizationService(
                unitOfWork,
                organizationScope,
                userAccountService,
                auditWriter);
        }

        private static UnitOfWork CreateUnitOfWork(string connectionStringName)
        {
            return CreateUnitOfWork(connectionStringName, CreateOrganizationScope(connectionStringName));
        }

        private static UnitOfWork CreateUnitOfWork(string connectionStringName, OrganizationScopeService organizationScope)
        {
            return new UnitOfWork(new SqlConnectionFactory(connectionStringName), organizationScope);
        }

        private static OrganizationScopeService CreateOrganizationScope(string connectionStringName)
        {
            return new OrganizationScopeService(new BootstrapCurrentUserContext(), new SqlConnectionFactory(connectionStringName));
        }

        private static ReferenceDataCache CreateReferenceDataCache(string connectionStringName)
        {
            return new ReferenceDataCache(new SqlConnectionFactory(connectionStringName));
        }
    }
}
