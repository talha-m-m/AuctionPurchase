using System;

namespace AuctionPurchase.Core.Models
{
    public class Grade
    {
        public int GradeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
    }
}
