using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.DTOs;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// rel1009i: quantity rules for requisition -> purchase order -> receipt. Ordering is capped by the requisition
    /// (summed over every PO raised from it) and receiving by the PO line (summed over every receipt).
    /// </summary>
    public static class PurchaseOrderQuantityRules
    {
        public const string StatusOpen = "Open";
        public const string StatusPartiallyReceived = "Partially Received";
        public const string StatusReceived = "Received";

        public const string FulfilmentNotOrdered = "Not ordered";
        public const string FulfilmentPartiallyOrdered = "Partially ordered";
        public const string FulfilmentFullyOrdered = "Fully ordered";
        public const string FulfilmentPartiallyReceived = "Partially received";
        public const string FulfilmentReceived = "Received";

        public static int Outstanding(int total, int used)
        {
            return Math.Max(0, total - used);
        }

        /// <summary>PO status from its ordered and received quantities.</summary>
        public static string ReceiptStatus(int ordered, int received)
        {
            if (ordered <= 0 || received <= 0)
            {
                return StatusOpen;
            }

            return received >= ordered ? StatusReceived : StatusPartiallyReceived;
        }

        /// <summary>Requisition fulfilment (derived; the requisition approval status itself has no ordered/received states).</summary>
        public static string FulfilmentStatus(int requisitionQuantity, int ordered, int received)
        {
            if (requisitionQuantity > 0 && received >= requisitionQuantity)
            {
                return FulfilmentReceived;
            }

            if (received > 0)
            {
                return FulfilmentPartiallyReceived;
            }

            if (requisitionQuantity > 0 && ordered >= requisitionQuantity)
            {
                return FulfilmentFullyOrdered;
            }

            return ordered > 0 ? FulfilmentPartiallyOrdered : FulfilmentNotOrdered;
        }

        public static void EnsureCanOrder(string lineLabel, int requested, int requisitionQuantity, int alreadyOrdered)
        {
            if (requested < 0)
            {
                throw new BusinessException("Quantity cannot be negative (" + lineLabel + ").");
            }

            var outstanding = Outstanding(requisitionQuantity, alreadyOrdered);
            if (requested > outstanding)
            {
                throw new BusinessException(
                    "Over-ordering is not allowed for " + lineLabel + ": requisition quantity " + requisitionQuantity
                    + ", already on purchase orders " + alreadyOrdered + ", so at most " + outstanding + " can be ordered (requested " + requested + ").");
            }
        }

        public static void EnsureCanReceive(string lineLabel, int requested, int ordered, int alreadyReceived)
        {
            if (requested < 0)
            {
                throw new BusinessException("Quantity received cannot be negative (" + lineLabel + ").");
            }

            var outstanding = Outstanding(ordered, alreadyReceived);
            if (requested > outstanding)
            {
                throw new BusinessException(
                    "Over-receipt is not allowed for " + lineLabel + ": ordered " + ordered + ", already received " + alreadyReceived
                    + ", so at most " + outstanding + " can be received (entered " + requested + ").");
            }
        }

        /// <summary>Bulk paste: one value per line (commas, semicolons and tabs also separate). Trimmed, blanks dropped.</summary>
        public static IList<string> ParseList(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            return text.Split(new[] { '\r', '\n', ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();
        }

        /// <summary>True when the exception is SQL "Invalid object name" (a migration-098 table is missing).</summary>
        public static bool IsMissingTable(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current.GetType().Name == "SqlException"
                    && current.Message != null
                    && current.Message.IndexOf("Invalid object name", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
