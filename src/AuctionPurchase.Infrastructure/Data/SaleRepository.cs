using Dapper;
using AuctionPurchase.Core.Models;
using AuctionPurchase.Core.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace AuctionPurchase.Infrastructure.Data
{
    public class SaleRepository : ISaleRepository
    {
        private readonly IDatabaseFactory _dbFactory;

        public SaleRepository(IDatabaseFactory dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task<Sale?> GetByIdAsync(int id)
        {
            using var connection = _dbFactory.CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<Sale>(
                "SELECT * FROM Sales WHERE SaleId = @Id", new { Id = id });
        }

        public async Task<Sale?> GetWithEntriesAsync(int id)
        {
            using var connection = _dbFactory.CreateConnection();
            var sale = await connection.QuerySingleOrDefaultAsync<Sale>(
                "SELECT * FROM Sales WHERE SaleId = @Id", new { Id = id });
            if (sale != null)
            {
                var entries = await connection.QueryAsync<AuctionEntry>(
                    @"SELECT
                        EntryId, SaleId, BrandId, BrokerId, LotNo, GardenId, AccountGardenId, InvoiceNo, GradeId,
                        CAST(NumberOfBags AS INTEGER) AS NumberOfBags,
                        CAST(TotalKgs AS REAL) AS TotalKgs,
                        CAST(PricePerKg AS REAL) AS PricePerKg,
                        CAST(Value AS REAL) AS Value,
                        ShedId, BlendId, CreatedAt, UpdatedAt
                      FROM AuctionEntries
                      WHERE SaleId = @SaleId
                      ORDER BY EntryId", 
                    new { SaleId = id });
                sale.Entries = entries.ToList();
            }
            return sale;
        }

        public async Task<IEnumerable<Sale>> GetAllAsync()
        {
            using var connection = _dbFactory.CreateConnection();
            return await connection.QueryAsync<Sale>(
                @"SELECT s.*, COUNT(ae.EntryId) AS EntryCount
                  FROM Sales s
                  LEFT JOIN AuctionEntries ae ON ae.SaleId = s.SaleId
                  GROUP BY s.SaleId
                  ORDER BY s.SaleDate DESC, s.SaleId DESC");
        }

        public async Task<IEnumerable<Sale>> SearchAsync(string? searchTerm, System.DateTime? fromDate, System.DateTime? toDate)
        {
            using var connection = _dbFactory.CreateConnection();
            var sql = @"SELECT s.*, COUNT(ae.EntryId) AS EntryCount
                        FROM Sales s
                        LEFT JOIN AuctionEntries ae ON ae.SaleId = s.SaleId
                        WHERE 1=1";
            var parameters = new DynamicParameters();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                sql += " AND s.SaleNo LIKE @SearchTerm";
                parameters.Add("SearchTerm", $"%{searchTerm}%");
            }
            if (fromDate.HasValue)
            {
                sql += " AND s.SaleDate >= @FromDate";
                parameters.Add("FromDate", fromDate.Value.Date);
            }
            if (toDate.HasValue)
            {
                sql += " AND s.SaleDate <= @ToDate";
                parameters.Add("ToDate", toDate.Value.Date.AddDays(1).AddSeconds(-1));
            }
            sql += " GROUP BY s.SaleId ORDER BY s.SaleDate DESC, s.SaleId DESC";
            return await connection.QueryAsync<Sale>(sql, parameters);
        }

        public async Task<int> AddAsync(Sale entity)
        {
            using var connection = _dbFactory.CreateConnection();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                entity.SaleNo = entity.SaleNo.Trim();
                var saleId = await connection.QuerySingleOrDefaultAsync<int>(
                    "SELECT SaleId FROM Sales WHERE lower(trim(SaleNo)) = lower(trim(@SaleNo))",
                    new { entity.SaleNo }, transaction);

                if (saleId == 0)
                {
                    saleId = await connection.QuerySingleAsync<int>(
                        @"INSERT INTO Sales (SaleNo, SaleDate, Notes, CreatedAt, UpdatedAt) 
                          VALUES (@SaleNo, @SaleDate, @Notes, @CreatedAt, @UpdatedAt) 
                          RETURNING SaleId;", entity, transaction);
                }
                else
                {
                    // Merge repeat uploads into the existing sale rather than appending every lot.
                    await connection.ExecuteAsync(
                        @"UPDATE Sales
                          SET SaleDate = @SaleDate,
                              Notes = CASE WHEN trim(@Notes) <> '' THEN @Notes ELSE Notes END,
                              UpdatedAt = @UpdatedAt
                          WHERE SaleId = @SaleId",
                        new { entity.SaleDate, entity.Notes, entity.UpdatedAt, SaleId = saleId }, transaction);
                }

                var existingEntries = (await connection.QueryAsync<AuctionEntry>(
                    "SELECT * FROM AuctionEntries WHERE SaleId = @SaleId",
                    new { SaleId = saleId }, transaction)).ToList();

                foreach (var entry in entity.Entries)
                {
                    entry.SaleId = saleId;
                    entry.AccountGardenId ??= entry.GardenId;
                    entry.Value = entry.TotalKgs * entry.PricePerKg;
                    var existing = AuctionEntryMerge.FindMatch(existingEntries, entry);
                    if (existing != null)
                    {
                        if (AuctionEntryMerge.FillMissing(existing, entry))
                        {
                            existing.UpdatedAt = entity.UpdatedAt;
                            await connection.ExecuteAsync(
                                @"UPDATE AuctionEntries SET
                                    BrandId=@BrandId, BrokerId=@BrokerId, LotNo=@LotNo, GardenId=@GardenId, AccountGardenId=@AccountGardenId,
                                    InvoiceNo=@InvoiceNo, GradeId=@GradeId, NumberOfBags=@NumberOfBags,
                                    TotalKgs=@TotalKgs, PricePerKg=@PricePerKg, Value=@Value,
                                    ShedId=@ShedId, BlendId=@BlendId, UpdatedAt=@UpdatedAt
                                  WHERE EntryId=@EntryId", existing, transaction);
                        }
                        continue;
                    }
                    entry.EntryId = await connection.QuerySingleAsync<int>(
                        @"INSERT INTO AuctionEntries 
                            (SaleId, BrandId, BrokerId, LotNo, GardenId, AccountGardenId, InvoiceNo, GradeId, 
                             NumberOfBags, TotalKgs, PricePerKg, Value, ShedId, BlendId, CreatedAt, UpdatedAt) 
                          VALUES 
                            (@SaleId, @BrandId, @BrokerId, @LotNo, @GardenId, @AccountGardenId, @InvoiceNo, @GradeId, 
                             @NumberOfBags, @TotalKgs, @PricePerKg, @Value, @ShedId, @BlendId, @CreatedAt, @UpdatedAt)
                          RETURNING EntryId;",
                        entry, transaction);
                    existingEntries.Add(entry);
                }

                transaction.Commit();
                return saleId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<bool> UpdateAsync(Sale entity)
        {
            using var connection = _dbFactory.CreateConnection();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                entity.SaleNo = entity.SaleNo.Trim();
                var matchingSaleId = await connection.QuerySingleOrDefaultAsync<int>(
                    @"SELECT SaleId FROM Sales
                      WHERE lower(trim(SaleNo)) = lower(trim(@SaleNo)) AND SaleId <> @SaleId",
                    new { entity.SaleNo, entity.SaleId }, transaction);
                if (matchingSaleId != 0)
                    throw new System.InvalidOperationException("Another sale already uses this Sale No. Edit that sale instead.");

                await connection.ExecuteAsync(
                    @"UPDATE Sales SET SaleNo=@SaleNo, SaleDate=@SaleDate, Notes=@Notes, UpdatedAt=@UpdatedAt 
                      WHERE SaleId=@SaleId", entity, transaction);

                // Delete and re-insert entries
                await connection.ExecuteAsync(
                    "DELETE FROM AuctionEntries WHERE SaleId=@SaleId", 
                    new { entity.SaleId }, transaction);

                foreach (var entry in entity.Entries)
                {
                    entry.SaleId = entity.SaleId;
                    entry.AccountGardenId ??= entry.GardenId;
                    entry.Value = entry.TotalKgs * entry.PricePerKg;
                    await connection.ExecuteAsync(
                        @"INSERT INTO AuctionEntries 
                            (SaleId, BrandId, BrokerId, LotNo, GardenId, AccountGardenId, InvoiceNo, GradeId, 
                             NumberOfBags, TotalKgs, PricePerKg, Value, ShedId, BlendId, CreatedAt, UpdatedAt) 
                          VALUES 
                            (@SaleId, @BrandId, @BrokerId, @LotNo, @GardenId, @AccountGardenId, @InvoiceNo, @GradeId, 
                             @NumberOfBags, @TotalKgs, @PricePerKg, @Value, @ShedId, @BlendId, @CreatedAt, @UpdatedAt);",
                        entry, transaction);
                }

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = _dbFactory.CreateConnection();
            var result = await connection.ExecuteAsync(
                "DELETE FROM Sales WHERE SaleId = @Id", new { Id = id });
            return result > 0;
        }

        public async Task UpdateLotAsync(AuctionEntry entry, string saleNo, System.DateTime saleDate)
        {
            using var connection = _dbFactory.CreateConnection();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                saleNo = saleNo.Trim();
                var conflictingSaleId = await connection.QuerySingleOrDefaultAsync<int>(
                    @"SELECT SaleId FROM Sales
                      WHERE lower(trim(SaleNo)) = lower(trim(@SaleNo)) AND SaleId <> @SaleId",
                    new { SaleNo = saleNo, entry.SaleId }, transaction);
                if (conflictingSaleId != 0)
                    throw new System.InvalidOperationException("Another sale already uses this Sale No.");

                await connection.ExecuteAsync(
                    @"UPDATE Sales
                      SET SaleNo = @SaleNo, SaleDate = @SaleDate, UpdatedAt = @UpdatedAt
                      WHERE SaleId = @SaleId",
                    new { SaleNo = saleNo, SaleDate = saleDate, UpdatedAt = System.DateTime.Now, entry.SaleId }, transaction);

                entry.AccountGardenId ??= entry.GardenId;
                entry.Value = entry.TotalKgs * entry.PricePerKg;
                var updated = await connection.ExecuteAsync(
                    @"UPDATE AuctionEntries SET
                        BrandId=@BrandId, BrokerId=@BrokerId, LotNo=@LotNo, GardenId=@GardenId, AccountGardenId=@AccountGardenId,
                        InvoiceNo=@InvoiceNo, GradeId=@GradeId, NumberOfBags=@NumberOfBags,
                        TotalKgs=@TotalKgs, PricePerKg=@PricePerKg, Value=@Value,
                        ShedId=@ShedId, BlendId=@BlendId, UpdatedAt=@UpdatedAt
                      WHERE EntryId=@EntryId AND SaleId=@SaleId",
                    new
                    {
                        entry.BrandId, entry.BrokerId, entry.LotNo, entry.GardenId, entry.AccountGardenId, entry.InvoiceNo,
                        entry.GradeId, entry.NumberOfBags, entry.TotalKgs, entry.PricePerKg, entry.Value,
                        entry.ShedId, entry.BlendId, UpdatedAt = System.DateTime.Now, entry.EntryId, entry.SaleId
                    }, transaction);
                if (updated == 0)
                    throw new System.InvalidOperationException("This auction lot no longer exists.");

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<bool> DeleteLotAsync(int entryId)
        {
            using var connection = _dbFactory.CreateConnection();
            var result = await connection.ExecuteAsync(
                "DELETE FROM AuctionEntries WHERE EntryId = @EntryId", new { EntryId = entryId });
            return result > 0;
        }
    }
}
