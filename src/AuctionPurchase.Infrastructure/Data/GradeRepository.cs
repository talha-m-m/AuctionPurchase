using Dapper;
using AuctionPurchase.Core.Models;
using System.Threading.Tasks;

namespace AuctionPurchase.Infrastructure.Data
{
    public class GradeRepository : MasterDataRepository<Grade>
    {
        protected override string TableName => "Grades";
        protected override string PrimaryKeyColumn => "GradeId";

        public GradeRepository(IDatabaseFactory dbFactory) : base(dbFactory) { }

        public override async Task<int> AddAsync(Grade entity)
        {
            using var connection = _dbFactory.CreateConnection();
            return await connection.QuerySingleAsync<int>(
                @"INSERT INTO Grades (Name, IsActive, SortOrder) 
                  VALUES (@Name, @IsActive, @SortOrder)
                  RETURNING GradeId;", entity);
        }

        public override async Task<bool> UpdateAsync(Grade entity)
        {
            using var connection = _dbFactory.CreateConnection();
            var result = await connection.ExecuteAsync(
                @"UPDATE Grades SET 
                    Name = @Name, 
                    IsActive = @IsActive, 
                    SortOrder = @SortOrder 
                  WHERE GradeId = @GradeId", entity);
            return result > 0;
        }
    }
}
