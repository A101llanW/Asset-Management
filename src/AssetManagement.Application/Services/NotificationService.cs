using System;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _unitOfWork;

        public NotificationService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public void GenerateSystemNotifications()
        {
            var now = DateTime.UtcNow;
            var warningThreshold = now.AddDays(30);
            var dueSoonThreshold = now.AddDays(7);

            var assets = _unitOfWork.Repository<Asset>().GetAll().ToList();
            var policies = _unitOfWork.Repository<InsurancePolicy>().GetAll().ToList();
            var assignments = _unitOfWork.Repository<AssetAssignment>().GetAll().ToList();
            var existingNotifications = _unitOfWork.Repository<Notification>().GetAll().ToList();

            var expiringWarranties = assets
                .Where(x => x.WarrantyEndDate.HasValue && x.WarrantyEndDate.Value.Date >= now.Date && x.WarrantyEndDate.Value <= warningThreshold)
                .ToList();
            foreach (var asset in expiringWarranties)
            {
                AddNotificationIfMissing(
                    existingNotifications,
                    NotificationType.WarrantyExpiry,
                    "Warranty expiring",
                    "Warranty nearing expiry for asset " + asset.AssetTag + " on " + asset.WarrantyEndDate.Value.ToString("yyyy-MM-dd") + ".",
                    "/Assets/Details/" + asset.Id,
                    now);
            }

            var expiringInsurance = policies
                .Where(x => x.PolicyEndDate.Date >= now.Date && x.PolicyEndDate <= warningThreshold)
                .ToList();
            foreach (var policy in expiringInsurance)
            {
                var asset = assets.FirstOrDefault(x => x.Id == policy.AssetId);
                var assetLabel = asset?.AssetTag ?? ("Asset #" + policy.AssetId);
                AddNotificationIfMissing(
                    existingNotifications,
                    NotificationType.InsuranceExpiry,
                    "Insurance policy expiring",
                    "Insurance policy " + policy.PolicyNumber + " for " + assetLabel + " expires on " + policy.PolicyEndDate.ToString("yyyy-MM-dd") + ".",
                    "/Assets/Details/" + policy.AssetId,
                    now);
            }

            var dueSoonAssignments = assignments
                .Where(x => x.ExpectedReturnDate.HasValue
                            && x.ExpectedReturnDate.Value.Date >= now.Date
                            && x.ExpectedReturnDate.Value <= dueSoonThreshold)
                .ToList();
            foreach (var assignment in dueSoonAssignments)
            {
                var asset = assets.FirstOrDefault(x => x.Id == assignment.AssetId);
                var assetLabel = asset?.AssetTag ?? ("Asset #" + assignment.AssetId);
                AddNotificationIfMissing(
                    existingNotifications,
                    NotificationType.TemporaryAssignmentDue,
                    "Temporary assignment due soon",
                    assetLabel + " is due for return on " + assignment.ExpectedReturnDate.Value.ToString("yyyy-MM-dd") + ".",
                    "/Assets/Details/" + assignment.AssetId,
                    now);
            }

            var overdueAssignments = assignments
                .Where(x => x.ExpectedReturnDate.HasValue && x.ExpectedReturnDate.Value.Date < now.Date)
                .ToList();
            foreach (var assignment in overdueAssignments)
            {
                var asset = assets.FirstOrDefault(x => x.Id == assignment.AssetId);
                if (asset == null || asset.CurrentStatus != AssetStatus.Assigned)
                {
                    continue;
                }

                AddNotificationIfMissing(
                    existingNotifications,
                    NotificationType.OverdueReturn,
                    "Asset return overdue",
                    asset.AssetTag + " was due on " + assignment.ExpectedReturnDate.Value.ToString("yyyy-MM-dd") + " and is still assigned.",
                    "/Assets/Details/" + assignment.AssetId,
                    now);
            }

            _unitOfWork.SaveChanges();
        }

        private void AddNotificationIfMissing(
            System.Collections.Generic.ICollection<Notification> existingNotifications,
            NotificationType type,
            string subject,
            string message,
            string linkUrl,
            DateTime createdAt)
        {
            var alreadyExists = existingNotifications.Any(x =>
                x.Type == type
                && x.Subject == subject
                && x.LinkUrl == linkUrl
                && x.Status == NotificationStatus.Unread);
            if (alreadyExists)
            {
                return;
            }

            var notification = new Notification
            {
                UserId = null,
                Type = type,
                Subject = subject,
                Message = message,
                Status = NotificationStatus.Unread,
                LinkUrl = linkUrl,
                CreatedAt = createdAt
            };

            _unitOfWork.Repository<Notification>().Add(notification);
            existingNotifications.Add(notification);
        }
    }
}
