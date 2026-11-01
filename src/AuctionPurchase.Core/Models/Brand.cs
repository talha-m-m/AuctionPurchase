using System;

namespace AuctionPurchase.Core.Models
{
    public class Brand
    {
        public int BrandId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
    }
}
