using Dapper;
using AuctionPurchase.Core.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuctionPurchase.Infrastructure.Data
{
    public abstract class MasterDataRepository<T> : IMasterDataRepository<T> where T : class
    {
        protected readonly IDatabaseFactory _dbFactory;
        protected abstract string TableName { get; }
        protected abstract string PrimaryKeyColumn { get; }

        public MasterDataRepository(IDatabaseFactory dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task<T?> GetByIdAsync(int id)
        {
            using var connection = _dbFactory.CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<T>(
                $"SELECT * FROM {TableName} WHERE {PrimaryKeyColumn} = @Id", new { Id = id });
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            using var connection = _dbFactory.CreateConnection();
            return await connection.QueryAsync<T>($"SELECT * FROM {TableName} ORDER BY SortOrder");
        }

        public async Task<IEnumerable<T>> GetActiveAsync()
        {
            using var connection = _dbFactory.CreateConnection();
            return await connection.QueryAsync<T>($"SELECT * FROM {TableName} WHERE IsActive = 1 ORDER BY SortOrder");
        }
        
        public abstract Task<int> AddAsync(T entity);
        public abstract Task<bool> UpdateAsync(T entity);
        
        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = _dbFactory.CreateConnection();
            // Soft delete
            var result = await connection.ExecuteAsync(
                $"UPDATE {TableName} SET IsActive = 0 WHERE {PrimaryKeyColumn} = @Id", new { Id = id });
            return result > 0;
        }

        public async Task<bool> DeletePermanentlyAsync(int id)
        {
            using var connection = _dbFactory.CreateConnection();
            var result = await connection.ExecuteAsync(
                $"DELETE FROM {TableName} WHERE {PrimaryKeyColumn} = @Id", new { Id = id });
            return result > 0;
        }
    }
}
