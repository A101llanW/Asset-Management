using System;
using AssetManagement.Application.Contracts;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Infrastructure.Services
{
    public class AuditWriter : IAuditWriter
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserContext _currentUser;

        public AuditWriter(IUnitOfWork unitOfWork, ICurrentUserContext currentUser)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        public void Write(string action, string entityType, string entityId, string oldValues, string newValues)
        {
            _unitOfWork.Repository<AuditLog>().Add(new AuditLog
            {
                ActorUserId = _currentUser?.UserId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                OldValues = oldValues,
                NewValues = newValues,
                Timestamp = DateTime.UtcNow,
                IPAddress = _currentUser?.IPAddress,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            _unitOfWork.SaveChanges();
        }
    }
}
