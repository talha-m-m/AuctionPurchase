using System;

namespace AuctionPurchase.Core.Models
{
    public class Shed
    {
        public int ShedId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Location { get; set; }
        public string? Address { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
    }
}
