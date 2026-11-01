using Dapper;
using AuctionPurchase.Core.Models;
using System.Threading.Tasks;

namespace AuctionPurchase.Infrastructure.Data
{
    public class ShedRepository : MasterDataRepository<Shed>
    {
        protected override string TableName => "Sheds";
        protected override string PrimaryKeyColumn => "ShedId";

        public ShedRepository(IDatabaseFactory dbFactory) : base(dbFactory) { }

        public override async Task<int> AddAsync(Shed entity)
        {
            using var connection = _dbFactory.CreateConnection();
            return await connection.QuerySingleAsync<int>(
                @"INSERT INTO Sheds (Name, Location, Address, IsActive, SortOrder) 
                  VALUES (@Name, @Location, @Address, @IsActive, @SortOrder)
                  RETURNING ShedId;", entity);
        }

        public override async Task<bool> UpdateAsync(Shed entity)
        {
            using var connection = _dbFactory.CreateConnection();
            var result = await connection.ExecuteAsync(
                @"UPDATE Sheds SET 
                    Name = @Name, 
                    Location = @Location,
                    Address = @Address,
                    IsActive = @IsActive, 
                    SortOrder = @SortOrder 
                  WHERE ShedId = @ShedId", entity);
            return result > 0;
        }
    }
}
