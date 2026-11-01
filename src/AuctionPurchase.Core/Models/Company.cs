namespace AuctionPurchase.Core.Models;

public class Company
{
    public int CompanyId { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<int> GardenIds { get; set; } = new();
    public string GardenNames { get; set; } = "";
    public int GardenCount => GardenIds.Count;
}
