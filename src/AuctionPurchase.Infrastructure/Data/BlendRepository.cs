using Dapper;
using AuctionPurchase.Core.Models;
using System.Threading.Tasks;

namespace AuctionPurchase.Infrastructure.Data
{
    public class BlendRepository : MasterDataRepository<Blend>
    {
        protected override string TableName => "Blends";
        protected override string PrimaryKeyColumn => "BlendId";

        public BlendRepository(IDatabaseFactory dbFactory) : base(dbFactory) { }

        public override async Task<int> AddAsync(Blend entity)
        {
            using var connection = _dbFactory.CreateConnection();
            return await connection.QuerySingleAsync<int>(
                @"INSERT INTO Blends (Name, IsActive, SortOrder) 
                  VALUES (@Name, @IsActive, @SortOrder)
                  RETURNING BlendId;", entity);
        }

        public override async Task<bool> UpdateAsync(Blend entity)
        {
            using var connection = _dbFactory.CreateConnection();
            var result = await connection.ExecuteAsync(
                @"UPDATE Blends SET 
                    Name = @Name, 
                    IsActive = @IsActive, 
                    SortOrder = @SortOrder 
                  WHERE BlendId = @BlendId", entity);
            return result > 0;
        }
    }
}
