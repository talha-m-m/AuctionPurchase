using Dapper;
using AuctionPurchase.Core.Models;
using System.Threading.Tasks;

namespace AuctionPurchase.Infrastructure.Data
{
    public class GardenRepository : MasterDataRepository<Garden>
    {
        protected override string TableName => "Gardens";
        protected override string PrimaryKeyColumn => "GardenId";

        public GardenRepository(IDatabaseFactory dbFactory) : base(dbFactory) { }

        public override async Task<int> AddAsync(Garden entity)
        {
            using var connection = _dbFactory.CreateConnection();
            return await connection.QuerySingleAsync<int>(
                @"INSERT INTO Gardens (Name, FullName, Address, District, Division, Acre, GardenType, MapLink, IsActive, SortOrder) 
                  VALUES (@Name, @FullName, @Address, @District, @Division, @Acre, @GardenType, @MapLink, @IsActive, @SortOrder)
                  RETURNING GardenId;", entity);
        }

        public override async Task<bool> UpdateAsync(Garden entity)
        {
            using var connection = _dbFactory.CreateConnection();
            var result = await connection.ExecuteAsync(
                @"UPDATE Gardens SET 
                    Name = @Name, 
                    FullName = @FullName,
                    Address = @Address,
                    District = @District,
                    Division = @Division,
                    Acre = @Acre,
                    GardenType = @GardenType,
                    MapLink = @MapLink,
                    IsActive = @IsActive, 
                    SortOrder = @SortOrder 
                  WHERE GardenId = @GardenId", entity);
            return result > 0;
        }
    }
}
