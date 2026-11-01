using System.Collections.Generic;
using System.Threading.Tasks;
using AuctionPurchase.Core.Models;

namespace AuctionPurchase.Core.Interfaces
{
    public interface ISaleRepository : IRepository<Sale>
    {
        Task<Sale?> GetWithEntriesAsync(int id);
        Task<IEnumerable<Sale>> SearchAsync(string? searchTerm, System.DateTime? fromDate, System.DateTime? toDate);
    }
}
