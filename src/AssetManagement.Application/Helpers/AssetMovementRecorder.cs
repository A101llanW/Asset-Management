using System;
using AssetManagement.Application.Contracts;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Writes AssetMovement rows. Every path that changes an asset's department or custodian calls this in the
    /// same unit of work as the change. OccurredAtUtc is always DateTime.UtcNow (never a user-entered date).
    /// </summary>
    public static class AssetMovementRecorder
    {
        public const int NotesMaxLength = 500;
        public const int SourceRefMaxLength = 100;

        public static string NewBatchId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static AssetMovement Record(
            IUnitOfWork unitOfWork,
            Asset asset,
            AssetMovementType movementType,
            int? fromDepartmentId,
            int? toDepartmentId,
            string fromCustodianUserId,
            string toCustodianUserId,
            string performedByUserId,
            string batchId = null,
            string notes = null,
            string sourceRef = null)
        {
            if (unitOfWork == null || asset == null || asset.Id <= 0)
            {
                return null;
            }

            // Nothing moved (e.g. a maintenance completion that kept the same department and custodian).
            if (movementType != AssetMovementType.Import
                && NormalizeId(fromDepartmentId) == NormalizeId(toDepartmentId)
                && string.Equals(NormalizeText(fromCustodianUserId, 128), NormalizeText(toCustodianUserId, 128), StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var movement = new AssetMovement
            {
                OrganizationId = asset.OrganizationId,
                AssetId = asset.Id,
                FromDepartmentId = NormalizeId(fromDepartmentId),
                ToDepartmentId = NormalizeId(toDepartmentId),
                FromCustodianUserId = NormalizeText(fromCustodianUserId, 128),
                ToCustodianUserId = NormalizeText(toCustodianUserId, 128),
                MovementType = movementType,
                BatchId = NormalizeText(batchId, 64),
                PerformedByUserId = NormalizeText(performedByUserId, 128),
                OccurredAtUtc = DateTime.UtcNow,
                Notes = NormalizeText(notes, NotesMaxLength),
                SourceRef = NormalizeText(sourceRef, SourceRefMaxLength)
            };
            unitOfWork.Repository<AssetMovement>().Add(movement);
            return movement;
        }

        /// <summary>Records the movement described by a custody event that is being written for the same change.</summary>
        public static AssetMovement FromCustodyEvent(
            IUnitOfWork unitOfWork,
            Asset asset,
            AssetCustodyEvent custodyEvent,
            AssetMovementType movementType,
            string performedByUserId,
            string batchId = null,
            string sourceRef = null)
        {
            if (custodyEvent == null)
            {
                return null;
            }

            var notes = custodyEvent.Reason;
            if (!string.IsNullOrWhiteSpace(custodyEvent.Notes))
            {
                notes = string.IsNullOrWhiteSpace(notes) ? custodyEvent.Notes : notes + " - " + custodyEvent.Notes;
            }

            return Record(
                unitOfWork,
                asset,
                movementType,
                custodyEvent.FromDepartmentId,
                custodyEvent.ToDepartmentId,
                custodyEvent.FromUserId,
                custodyEvent.ToUserId,
                performedByUserId,
                batchId,
                notes,
                sourceRef);
        }

        public static string Label(AssetMovementType type)
        {
            switch (type)
            {
                case AssetMovementType.Edit: return "Edit (department change)";
                case AssetMovementType.BulkDepartment: return "Bulk move to department";
                case AssetMovementType.RelocateClass: return "Move to class";
                case AssetMovementType.RelocateRoom: return "Move to room";
                case AssetMovementType.Transfer: return "Transfer";
                case AssetMovementType.Assign: return "Assign";
                case AssetMovementType.Return: return "Return";
                case AssetMovementType.Import: return "Import";
                default: return "Other";
            }
        }

        private static int? NormalizeId(int? id)
        {
            return id.HasValue && id.Value > 0 ? id : null;
        }

        private static string NormalizeText(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length > maxLength ? trimmed.Substring(0, maxLength) : trimmed;
        }
    }
}
