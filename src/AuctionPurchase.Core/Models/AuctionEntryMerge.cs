using System;
using System.Collections.Generic;
using System.Linq;

namespace AuctionPurchase.Core.Models
{
    public static class AuctionEntryMerge
    {
        private static bool Same(string? left, string? right) =>
            string.Equals(left?.Trim() ?? "", right?.Trim() ?? "", StringComparison.OrdinalIgnoreCase);

        // Lot numbers are scoped to a broker. Only infer a missing key when the
        // remaining identifiers select exactly one row.
        public static AuctionEntry? FindMatch(IEnumerable<AuctionEntry> entries, AuctionEntry incoming)
        {
            var all = entries.ToList();
            // Incomplete rows may have no lot or invoice yet. An identical
            // repeat still must not create a second row.
            var identical = all.FirstOrDefault(x => Snapshot(x).SequenceEqual(Snapshot(incoming)));
            if (identical != null) return identical;

            var candidates = all.Where(existing =>
                (!existing.BrokerId.HasValue || !incoming.BrokerId.HasValue || existing.BrokerId == incoming.BrokerId) &&
                ((!string.IsNullOrWhiteSpace(existing.LotNo) && !string.IsNullOrWhiteSpace(incoming.LotNo))
                    ? Same(existing.LotNo, incoming.LotNo)
                    : !string.IsNullOrWhiteSpace(existing.InvoiceNo) &&
                      !string.IsNullOrWhiteSpace(incoming.InvoiceNo) && Same(existing.InvoiceNo, incoming.InvoiceNo) &&
                      existing.GardenId == incoming.GardenId)).ToList();

            if (candidates.Count > 1 && !string.IsNullOrWhiteSpace(incoming.InvoiceNo))
                candidates = candidates.Where(x => Same(x.InvoiceNo, incoming.InvoiceNo)).ToList();

            return candidates.Count == 1 ? candidates[0] : null;
        }

        public static bool FillMissing(AuctionEntry existing, AuctionEntry incoming)
        {
            var before = Snapshot(existing);
            existing.BrandId ??= incoming.BrandId;
            existing.BrokerId ??= incoming.BrokerId;
            existing.GardenId ??= incoming.GardenId;
            existing.AccountGardenId ??= incoming.AccountGardenId;
            existing.GradeId ??= incoming.GradeId;
            existing.ShedId ??= incoming.ShedId;
            existing.BlendId ??= incoming.BlendId;
            if (string.IsNullOrWhiteSpace(existing.LotNo)) existing.LotNo = incoming.LotNo;
            if (string.IsNullOrWhiteSpace(existing.InvoiceNo)) existing.InvoiceNo = incoming.InvoiceNo;
            if (existing.NumberOfBags == 0) existing.NumberOfBags = incoming.NumberOfBags;
            if (existing.TotalKgs == 0) existing.TotalKgs = incoming.TotalKgs;
            if (existing.PricePerKg == 0) existing.PricePerKg = incoming.PricePerKg;
            existing.Value = existing.TotalKgs * existing.PricePerKg;
            return !before.SequenceEqual(Snapshot(existing));
        }

        private static object?[] Snapshot(AuctionEntry entry) => new object?[]
        {
            entry.BrandId, entry.BrokerId, entry.LotNo, entry.GardenId, entry.InvoiceNo,
            entry.GradeId, entry.NumberOfBags, entry.TotalKgs, entry.PricePerKg,
            entry.Value, entry.ShedId, entry.BlendId, entry.AccountGardenId
        };
    }
}
