using Dapper;
using AuctionPurchase.Core.Models;
using System.Threading.Tasks;

namespace AuctionPurchase.Infrastructure.Data
{
    public class BrandRepository : MasterDataRepository<Brand>
    {
        protected override string TableName => "Brands";
        protected override string PrimaryKeyColumn => "BrandId";

        public BrandRepository(IDatabaseFactory dbFactory) : base(dbFactory) { }

        public override async Task<int> AddAsync(Brand entity)
        {
            using var connection = _dbFactory.CreateConnection();
            return await connection.QuerySingleAsync<int>(
                @"INSERT INTO Brands (Name, Description, IsActive, SortOrder) 
                  VALUES (@Name, @Description, @IsActive, @SortOrder)
                  RETURNING BrandId;", entity);
        }

        public override async Task<bool> UpdateAsync(Brand entity)
        {
            using var connection = _dbFactory.CreateConnection();
            var result = await connection.ExecuteAsync(
                @"UPDATE Brands SET 
                    Name = @Name, 
                    Description = @Description, 
                    IsActive = @IsActive, 
                    SortOrder = @SortOrder 
                  WHERE BrandId = @BrandId", entity);
            return result > 0;
        }
    }
}
