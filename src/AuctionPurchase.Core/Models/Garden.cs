using System;

namespace AuctionPurchase.Core.Models
{
    public class Garden
    {
        public int GardenId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? Address { get; set; }
        public string? District { get; set; }
        public string? Division { get; set; }
        public double? Acre { get; set; }
        public string? GardenType { get; set; }
        public string? MapLink { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
    }
}
