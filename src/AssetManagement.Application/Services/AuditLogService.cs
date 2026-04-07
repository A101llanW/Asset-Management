using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly IUnitOfWork _unitOfWork;

        public AuditLogService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IEnumerable<AuditLogVm> GetLogs(AuditLogFilterVm filter)
        {
            var query = _unitOfWork.Repository<AuditLog>().GetAll();
            if (filter != null)
            {
                if (!string.IsNullOrWhiteSpace(filter.EntityType))
                {
                    query = query.Where(x => x.EntityType == filter.EntityType);
                }

                if (!string.IsNullOrWhiteSpace(filter.Action))
                {
                    query = query.Where(x => x.Action == filter.Action);
                }

                if (filter.FromDate.HasValue)
                {
                    query = query.Where(x => x.Timestamp >= filter.FromDate.Value);
                }

                if (filter.ToDate.HasValue)
                {
                    query = query.Where(x => x.Timestamp <= filter.ToDate.Value);
                }
            }

            return query.OrderByDescending(x => x.Timestamp)
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
}
