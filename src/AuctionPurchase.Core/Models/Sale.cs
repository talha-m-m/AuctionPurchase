using System;
using System.Collections.Generic;

namespace AuctionPurchase.Core.Models
{
    public class Sale
    {
        public int SaleId { get; set; }
        public string SaleNo { get; set; } = string.Empty;
        public DateTime SaleDate { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Populated when a sale is shown in the sales list.  A sale remains one
        // list item regardless of how many auction lots it contains.
        public int EntryCount { get; set; }

        public List<AuctionEntry> Entries { get; set; } = new();
    }
}
