using System;
using System.Collections.Generic;
using AuctionPurchase.Core.Reports;

namespace AuctionPurchase.UI.ViewModels
{
    public class ExecutiveKpiData
    {
        public int TotalSales { get; set; }
        public int TotalLots { get; set; }
        public decimal TotalWeightKg { get; set; }
        public decimal TotalValueBdt { get; set; }
        public decimal TotalValueInr { get => TotalValueBdt; set => TotalValueBdt = value; }
        public double GrowthPercent { get; set; }
        public bool IsPositiveGrowth { get; set; } = true;
        public string GrowthBadgeText { get; set; } = "+0.0%";
    }

    public class BrokerVolumeItem
    {
        public string BrokerName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public decimal TotalKgs { get; set; }
        public decimal TotalValueBdt { get; set; }
        public decimal TotalValueInr { get => TotalValueBdt; set => TotalValueBdt = value; }
        public int LotCount { get; set; }
        public decimal AvgPricePerKg => TotalKgs > 0 ? TotalValueBdt / TotalKgs : 0;
        public double Percentage { get; set; }
        public double BarWidthPercent { get; set; }
        public string ColorHex { get; set; } = "#4361EE";
        public string DisplayText => $"{BrokerName} — {TotalKgs:N0} Kg ({Percentage:F1}%)";
    }

    public class BrandVolumeItem
    {
        public string BrandName { get; set; } = string.Empty;
        public decimal TotalKgs { get; set; }
        public decimal TotalValueBdt { get; set; }
        public decimal TotalValueInr { get => TotalValueBdt; set => TotalValueBdt = value; }
        public int LotCount { get; set; }
        public decimal AvgPricePerKg => TotalKgs > 0 ? TotalValueBdt / TotalKgs : 0;
        public double Percentage { get; set; }
        public string ColorHex { get; set; } = "#4361EE";
        public double StartAngle { get; set; }
        public double SweepAngle { get; set; }
    }

    public class GardenMapPin
    {
        public int GardenId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public string RegionGroup { get; set; } = string.Empty;
        public string? MapLink { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double CanvasX { get; set; }
        public double CanvasY { get; set; }
        public decimal TotalKgs { get; set; }
        public decimal TotalValueBdt { get; set; }
        public decimal TotalValueInr { get => TotalValueBdt; set => TotalValueBdt = value; }
        public int LotsCount { get; set; }
        public bool HasPurchases => LotsCount > 0;
        public double PinSize { get; set; } = 14;
        public string PinColor { get; set; } = "#4361EE";
        public string StatusText => HasPurchases ? $"{LotsCount} lots ({TotalKgs:N0} Kg)" : "Catalog Garden";
    }

    public class RecentSaleRowItem
    {
        public int SaleId { get; set; }
        public string SaleNo { get; set; } = string.Empty;
        public DateTime SaleDate { get; set; }
        public int TotalLots { get; set; }
        public int TotalBags { get; set; }
        public decimal TotalKgs { get; set; }
        public decimal TotalValueBdt { get; set; }
        public decimal TotalValueInr { get => TotalValueBdt; set => TotalValueBdt = value; }
        public decimal AvgPricePerKg => TotalKgs > 0 ? TotalValueBdt / TotalKgs : 0;
        public List<ReportEntry> Entries { get; set; } = new();

        public List<BrokerVolumeItem> BrokerSummary { get; set; } = new();
        public List<BrandVolumeItem> BrandSummary { get; set; } = new();
    }
}
