using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuctionPurchase.Core.Interfaces
{
    public interface IMasterDataRepository<T> : IRepository<T> where T : class
    {
        Task<IEnumerable<T>> GetActiveAsync();
    }
}
