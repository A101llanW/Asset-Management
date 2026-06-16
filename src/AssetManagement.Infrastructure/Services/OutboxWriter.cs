using System;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Outbox;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Infrastructure.Services
{
    public class OutboxWriter : IOutboxWriter
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrganizationScopeService _organizationScope;

        public OutboxWriter(IUnitOfWork unitOfWork, IOrganizationScopeService organizationScope)
        {
            _unitOfWork = unitOfWork;
            _organizationScope = organizationScope;
        }

        public void Enqueue(string messageType, string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(messageType))
            {
                throw new ArgumentException("Message type is required.", "messageType");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                throw new ArgumentException("Payload is required.", "payloadJson");
            }

            _unitOfWork.Repository<OutboxMessage>().Add(new OutboxMessage
            {
                OrganizationId = _organizationScope.GetCurrentOrganizationId(),
                MessageType = messageType.Trim(),
                Payload = payloadJson,
                Status = OutboxMessageStatus.Pending,
                Attempts = 0,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
        }
    }
}
