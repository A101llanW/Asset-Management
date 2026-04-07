using System.Web.Mvc;
using AssetManagement.Domain.Entities;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Services;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Repositories;
using AssetManagement.Infrastructure.Services;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Controllers
{
    [Authorize]
    public abstract class BaseController : Controller
    {
        protected readonly AssetManagementDbContext DbContext;
        protected readonly IUnitOfWork UnitOfWork;
        protected readonly ICurrentUserContext CurrentUserContext;
        protected readonly IAuditWriter AuditWriter;

        protected BaseController()
        {
            DbContext = new AssetManagementDbContext();
            UnitOfWork = new UnitOfWork(DbContext);
            CurrentUserContext = new HttpCurrentUserContext();
            AuditWriter = new AuditWriter(UnitOfWork, CurrentUserContext);
        }

        protected IAssetService BuildAssetService() => new AssetService(UnitOfWork, AuditWriter);

        protected IRoleService BuildRoleService() => new RoleService(UnitOfWork, AuditWriter);

        protected IPermissionService BuildPermissionService() => new PermissionService(UnitOfWork);

        protected IDepartmentService BuildDepartmentService() => new DepartmentService(UnitOfWork);

        protected ISupplierService BuildSupplierService() => new SupplierService(UnitOfWork);

        protected IAssignmentService BuildAssignmentService() => new AssignmentService(UnitOfWork, AuditWriter);

        protected ITransferService BuildTransferService() => new TransferService(UnitOfWork, AuditWriter);

        protected IReturnService BuildReturnService() => new ReturnService(UnitOfWork, AuditWriter);

        protected IMaintenanceService BuildMaintenanceService() => new MaintenanceService(UnitOfWork, AuditWriter);

        protected IIncidentService BuildIncidentService() => new IncidentService(UnitOfWork, AuditWriter);

        protected IClaimService BuildClaimService() => new ClaimService(UnitOfWork, AuditWriter);

        protected IPurchaseService BuildPurchaseService() => new PurchaseService(UnitOfWork);

        protected IAuditLogService BuildAuditLogService() => new AuditLogService(UnitOfWork);

        protected IReportService BuildReportService() => new ReportService(UnitOfWork);

        protected IDepreciationService BuildDepreciationService() => new DepreciationService(UnitOfWork);

        protected INotificationService BuildNotificationService() => new NotificationService(UnitOfWork);

        protected IUserService BuildUserService() => new UserService(UnitOfWork);

        protected IAuthorizationService BuildAuthorizationService() => new AuthorizationService(UnitOfWork);

        protected AssetWorkflowContextViewModel BuildAssetWorkflowContext(int assetId)
        {
            var asset = UnitOfWork.Repository<Asset>().GetById(assetId);
            if (asset == null)
            {
                return null;
            }

            var departmentName = asset.DepartmentId > 0
                ? UnitOfWork.Repository<Department>().GetById(asset.DepartmentId)?.Name
                : null;
            var custodian = !string.IsNullOrWhiteSpace(asset.CurrentCustodianId)
                ? UnitOfWork.Repository<AssetManagement.Infrastructure.Identity.ApplicationUser>().GetById(asset.CurrentCustodianId)
                : null;
            var custodianName = custodian == null
                ? null
                : ((custodian.FirstName + " " + custodian.LastName).Trim());

            return new AssetWorkflowContextViewModel
            {
                AssetId = asset.Id,
                AssetName = asset.AssetName,
                AssetTag = asset.AssetTag,
                Status = asset.CurrentStatus.ToString(),
                DepartmentName = departmentName,
                CustodianName = string.IsNullOrWhiteSpace(custodianName) ? custodian?.Email : custodianName
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnitOfWork.Dispose();
                DbContext.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
