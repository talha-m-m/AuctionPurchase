using System;

namespace AuctionPurchase.Core.Models
{
    public class AuctionEntry
    {
        public int EntryId { get; set; }
        public int SaleId { get; set; }
        public int? BrandId { get; set; }
        public int? BrokerId { get; set; }
        public string LotNo { get; set; } = string.Empty;
        public int? GardenId { get; set; }
        public int? AccountGardenId { get; set; }
        public string? InvoiceNo { get; set; } = string.Empty;
        public int? GradeId { get; set; }
        public int NumberOfBags { get; set; }
        public decimal TotalKgs { get; set; }
        public decimal PricePerKg { get; set; }
        public decimal Value { get; set; } // Auto-calculated TotalKgs * PricePerKg
        public int? ShedId { get; set; }
        public int? BlendId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
