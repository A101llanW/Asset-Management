using System.Linq;
using System.Web;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Services;
using Autofac;
using Autofac.Integration.Mvc;

namespace AssetManagement.Web.App_Start
{
    public static class AttachmentStorageRegistration
    {
        private const string AttachmentRootPathKey = "Attachment.RootPath";

        public static void Register(ContainerBuilder builder)
        {
            builder.Register(ctx => new FileSystemStorageProvider(ResolveAttachmentRoot(ctx)))
                .As<IFileStorageProvider>()
                .InstancePerHttpRequest();

            builder.RegisterType<AssetDocumentService>()
                .As<IAssetDocumentService>()
                .InstancePerHttpRequest();
        }

        private static string ResolveAttachmentRoot(IComponentContext context)
        {
            var unitOfWork = context.Resolve<IUnitOfWork>();
            var configuredPath = ApprovalWorkflowSettingsHelper.GetString(
                ApprovalWorkflowSettingsHelper.ToDictionary(unitOfWork.Repository<SystemSetting>().GetAll()),
                AttachmentRootPathKey,
                "~/App_Data/Attachments");

            if (HttpContext.Current != null)
            {
                return HttpContext.Current.Server.MapPath(configuredPath);
            }

            return configuredPath;
        }
    }
}
