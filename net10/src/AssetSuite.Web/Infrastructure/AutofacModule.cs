using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Organizations;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Queries;
using AssetManagement.Application.Services.Organizations;
using AssetManagement.Infrastructure.Repositories;
using AssetManagement.Infrastructure.Security;
using AssetManagement.Infrastructure.Services;
using Autofac;
using Microsoft.AspNetCore.Http;

namespace AssetSuite.Web.Infrastructure;

public static class AutofacModule
{
    public static void Register(ContainerBuilder builder)
    {
        builder.RegisterBuildCallback(container =>
        {
            var accessor = container.Resolve<IHttpContextAccessor>();
            AssetManagement.Infrastructure.Compat.AspNetCoreHttpBridge.Configure(accessor);
        });

        builder.RegisterType<SqlConnectionFactory>().As<ISqlConnectionFactory>().InstancePerLifetimeScope();
        builder.RegisterType<UnitOfWork>().As<IUnitOfWork>().As<IUnitOfWorkEnlistment>().InstancePerLifetimeScope();
        builder.RegisterType<HttpCurrentUserContext>().As<ICurrentUserContext>().InstancePerLifetimeScope();
        builder.RegisterType<OrganizationScopeService>().As<IOrganizationScopeService>().InstancePerLifetimeScope();
        builder.RegisterType<MembershipService>().As<IUserAccountService>().InstancePerLifetimeScope();
        builder.RegisterType<AccountSecurityService>().As<IAccountSecurityService>().InstancePerLifetimeScope();
        builder.RegisterType<AuthFlowRateLimiterService>().As<IAuthFlowRateLimiter>().InstancePerLifetimeScope();
        builder.RegisterType<SqlDistributedRateLimiter>().As<IDistributedRateLimiter>().InstancePerLifetimeScope();
        builder.RegisterType<OrganizationLicenseService>().As<IOrganizationLicenseService>().InstancePerLifetimeScope();
        builder.RegisterType<OrganizationLicenseQueryRepository>().As<IOrganizationLicenseQueryRepository>().InstancePerLifetimeScope();
        builder.RegisterType<OutboxWriter>().As<IOutboxWriter>().InstancePerLifetimeScope();
        builder.RegisterType<OutboxDispatcher>().As<IOutboxDispatcher>().InstancePerLifetimeScope();
        builder.RegisterType<AuditWriter>().As<IAuditWriter>().InstancePerLifetimeScope();
        builder.RegisterType<EmailService>().As<IEmailService>().InstancePerLifetimeScope();
        builder.RegisterType<PlatformSettingsService>().As<IPlatformSettingsService>().InstancePerLifetimeScope();
        builder.RegisterType<ReferenceDataCache>().As<IReferenceDataCache>().InstancePerLifetimeScope();
        builder.RegisterType<AssetSuiteAuthService>().AsSelf().InstancePerLifetimeScope();
    }
}
