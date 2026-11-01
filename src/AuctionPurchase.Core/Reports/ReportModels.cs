using System;
using System.Collections.Generic;

namespace AuctionPurchase.Core.Reports
{
    public class ReportEntry
    {
        public int EntryId { get; set; }
        public int SaleId { get; set; }
        public int? BrandId { get; set; }
        public int? BrokerId { get; set; }
        public int? GardenId { get; set; }
        public int? AccountGardenId { get; set; }
        public int? GradeId { get; set; }
        public int? ShedId { get; set; }
        public int? BlendId { get; set; }
        public string BrokerName { get; set; } = string.Empty;
        public string BrandName { get; set; } = string.Empty;
        public string SaleNo { get; set; } = string.Empty;
        public DateTime SaleDate { get; set; }
        // Lot numbers are identifiers, not quantities. They can contain letters,
        // leading zeros, or separators, so keep the value exactly as entered.
        public string LotNo { get; set; } = string.Empty;
        public string GardenName { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string GardenDisplay => AccountGardenId.HasValue && AccountGardenId != GardenId && !string.IsNullOrWhiteSpace(AccountName)
            ? $"{GardenName} acc {AccountName}" : GardenName;
        public string InvoiceNo { get; set; } = string.Empty;
        public string GradeName { get; set; } = string.Empty;
        public int NumberOfBags { get; set; }
        public decimal TotalKgs { get; set; }
        public decimal PricePerKg { get; set; }
        public decimal Value { get; set; }
        public string? ShedName { get; set; }
        public string? BlendName { get; set; }
    }

    public class BrandReportGroup
    {
        public string BrandName { get; set; } = string.Empty;
        public List<ReportEntry> Entries { get; set; } = new();
        public decimal TotalKgs => Entries.Sum(e => e.TotalKgs);
        public decimal TotalValue => Entries.Sum(e => e.Value);
        public decimal AvgPricePerKg => TotalKgs > 0 ? TotalValue / TotalKgs : 0;
        
        private decimal Sum(Func<ReportEntry, decimal> selector) => 
            Entries.Count > 0 ? Entries.ConvertAll(e => selector(e)).Aggregate((a, b) => a + b) : 0;
    }

    public class BrokerReportGroup
    {
        public string BrokerName { get; set; } = string.Empty;
        public List<ReportEntry> Entries { get; set; } = new();
        public decimal TotalKgs => Entries.Sum(e => e.TotalKgs);
        public decimal TotalValue => Entries.Sum(e => e.Value);
        public decimal AvgPricePerKg => TotalKgs > 0 ? TotalValue / TotalKgs : 0;
    }

    public class ReportFilter
    {
        public string? SaleNo { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? BrandId { get; set; }
        public int? BrokerId { get; set; }
        public int? GardenId { get; set; }
        public int? SaleId { get; set; }
    }

    public class SaleSummaryReport
    {
        public string SaleNo { get; set; } = string.Empty;
        public DateTime SaleDate { get; set; }
        public int TotalEntries { get; set; }
        public int TotalBags { get; set; }
        public decimal TotalKgs { get; set; }
        public decimal TotalValue { get; set; }
        public decimal AvgPricePerKg => TotalKgs > 0 ? TotalValue / TotalKgs : 0;
        public List<ReportEntry> Entries { get; set; } = new();
    }
}
