using Dapper;
using AuctionPurchase.Core.Models;
using System.Threading.Tasks;

namespace AuctionPurchase.Infrastructure.Data
{
    public class BrokerRepository : MasterDataRepository<Broker>
    {
        protected override string TableName => "Brokers";
        protected override string PrimaryKeyColumn => "BrokerId";

        public BrokerRepository(IDatabaseFactory dbFactory) : base(dbFactory) { }

        public override async Task<int> AddAsync(Broker entity)
        {
            using var connection = _dbFactory.CreateConnection();
            return await connection.QuerySingleAsync<int>(
                @"INSERT INTO Brokers (Name, FullName, Address, IsActive, SortOrder) 
                  VALUES (@Name, @FullName, @Address, @IsActive, @SortOrder)
                  RETURNING BrokerId;", entity);
        }

        public override async Task<bool> UpdateAsync(Broker entity)
        {
            using var connection = _dbFactory.CreateConnection();
            var result = await connection.ExecuteAsync(
                @"UPDATE Brokers SET 
                    Name = @Name, 
                    FullName = @FullName,
                    Address = @Address,
                    IsActive = @IsActive, 
                    SortOrder = @SortOrder 
                  WHERE BrokerId = @BrokerId", entity);
            return result > 0;
        }
    }
}
