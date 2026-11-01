using System;

namespace AuctionPurchase.Core.Models
{
    public class Broker
    {
        public int BrokerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? Address { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
    }
}
