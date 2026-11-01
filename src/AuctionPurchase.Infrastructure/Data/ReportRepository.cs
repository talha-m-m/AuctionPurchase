using Dapper;
using AuctionPurchase.Core.Reports;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuctionPurchase.Infrastructure.Data
{
    public class ReportRepository
    {
        private readonly IDatabaseFactory _dbFactory;

        public ReportRepository(IDatabaseFactory dbFactory)
        {
            _dbFactory = dbFactory;
        }

        private string BuildEntrySql(string extraWhere = "")
        {
            return $@"
                SELECT 
                    ae.EntryId,
                    s.SaleId,
                    ae.BrandId,
                    ae.BrokerId,
                    ae.GardenId,
                    COALESCE(ae.AccountGardenId, ae.GardenId) as AccountGardenId,
                    ae.GradeId,
                    ae.ShedId,
                    ae.BlendId,
                    br.Name as BrokerName,
                    bn.Name as BrandName,
                    s.SaleNo,
                    s.SaleDate,
                    ae.LotNo,
                    g.Name as GardenName,
                    COALESCE(ag.Name, g.Name) as AccountName,
                    ae.InvoiceNo,
                    gr.Name as GradeName,
                    -- Older imported workbooks can contain numeric cells stored
                    -- as text. Normalize them in SQLite before Dapper maps them
                    -- to numeric report fields, so one bad legacy cell cannot
                    -- prevent the Dashboard or a sale's View window from loading.
                    CAST(ae.NumberOfBags AS INTEGER) as NumberOfBags,
                    CAST(ae.TotalKgs AS REAL) as TotalKgs,
                    CAST(ae.PricePerKg AS REAL) as PricePerKg,
                    CAST(ae.Value AS REAL) as Value,
                    sh.Name as ShedName,
                    bl.Name as BlendName
                FROM AuctionEntries ae
                JOIN Sales s ON ae.SaleId = s.SaleId
                LEFT JOIN Brands bn ON ae.BrandId = bn.BrandId
                LEFT JOIN Brokers br ON ae.BrokerId = br.BrokerId
                LEFT JOIN Gardens g ON ae.GardenId = g.GardenId
                LEFT JOIN Gardens ag ON ae.AccountGardenId = ag.GardenId
                LEFT JOIN Grades gr ON ae.GradeId = gr.GradeId
                LEFT JOIN Sheds sh ON ae.ShedId = sh.ShedId
                LEFT JOIN Blends bl ON ae.BlendId = bl.BlendId
                WHERE 1=1 {extraWhere}
                ORDER BY s.SaleDate, s.SaleNo, bn.Name, br.Name, ae.LotNo";
        }

        public async Task<IEnumerable<ReportEntry>> GetReportEntriesAsync(ReportFilter filter)
        {
            using var connection = _dbFactory.CreateConnection();
            var extraWhere = "";
            var parameters = new DynamicParameters();

            if (!string.IsNullOrEmpty(filter.SaleNo))
            {
                extraWhere += " AND s.SaleNo LIKE @SaleNo";
                parameters.Add("SaleNo", $"%{filter.SaleNo}%");
            }
            if (filter.SaleId.HasValue)
            {
                extraWhere += " AND s.SaleId = @SaleId";
                parameters.Add("SaleId", filter.SaleId.Value);
            }
            if (filter.FromDate.HasValue)
            {
                extraWhere += " AND s.SaleDate >= @FromDate";
                parameters.Add("FromDate", filter.FromDate.Value.Date);
            }
            if (filter.ToDate.HasValue)
            {
                extraWhere += " AND s.SaleDate <= @ToDate";
                parameters.Add("ToDate", filter.ToDate.Value.Date.AddDays(1).AddSeconds(-1));
            }
            if (filter.BrandId.HasValue)
            {
                extraWhere += " AND ae.BrandId = @BrandId";
                parameters.Add("BrandId", filter.BrandId.Value);
            }
            if (filter.BrokerId.HasValue)
            {
                extraWhere += " AND ae.BrokerId = @BrokerId";
                parameters.Add("BrokerId", filter.BrokerId.Value);
            }
            if (filter.GardenId.HasValue)
            {
                extraWhere += " AND ae.GardenId = @GardenId";
                parameters.Add("GardenId", filter.GardenId.Value);
            }

            return await connection.QueryAsync<ReportEntry>(BuildEntrySql(extraWhere), parameters);
        }

        public async Task<(IEnumerable<ReportEntry> Items, int TotalCount)> GetReportEntriesPagedAsync(ReportFilter filter, int pageNumber, int pageSize)
        {
            using var connection = _dbFactory.CreateConnection();
            var extraWhere = "";
            var parameters = new DynamicParameters();

            if (!string.IsNullOrEmpty(filter.SaleNo))
            {
                extraWhere += " AND s.SaleNo LIKE @SaleNo";
                parameters.Add("SaleNo", $"%{filter.SaleNo}%");
            }
            if (filter.SaleId.HasValue)
            {
                extraWhere += " AND s.SaleId = @SaleId";
                parameters.Add("SaleId", filter.SaleId.Value);
            }
            if (filter.FromDate.HasValue)
            {
                extraWhere += " AND s.SaleDate >= @FromDate";
                parameters.Add("FromDate", filter.FromDate.Value.Date);
            }
            if (filter.ToDate.HasValue)
            {
                extraWhere += " AND s.SaleDate <= @ToDate";
                parameters.Add("ToDate", filter.ToDate.Value.Date.AddDays(1).AddSeconds(-1));
            }
            if (filter.BrandId.HasValue)
            {
                extraWhere += " AND ae.BrandId = @BrandId";
                parameters.Add("BrandId", filter.BrandId.Value);
            }
            if (filter.BrokerId.HasValue)
            {
                extraWhere += " AND ae.BrokerId = @BrokerId";
                parameters.Add("BrokerId", filter.BrokerId.Value);
            }
            if (filter.GardenId.HasValue)
            {
                extraWhere += " AND ae.GardenId = @GardenId";
                parameters.Add("GardenId", filter.GardenId.Value);
            }

            var countSql = $"SELECT COUNT(*) FROM AuctionEntries ae JOIN Sales s ON ae.SaleId = s.SaleId WHERE 1=1 {extraWhere}";
            var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);

            var sql = BuildEntrySql(extraWhere) + " LIMIT @Limit OFFSET @Offset";
            parameters.Add("Limit", pageSize);
            parameters.Add("Offset", (pageNumber - 1) * pageSize);

            var items = await connection.QueryAsync<ReportEntry>(sql, parameters);
            return (items, totalCount);
        }

        public async Task<List<BrandReportGroup>> GetBrandWiseReportAsync(ReportFilter filter)
        {
            var entries = await GetReportEntriesAsync(filter);
            return entries
                .GroupBy(e => e.BrandName)
                .Select(g => new BrandReportGroup
                {
                    BrandName = g.Key,
                    Entries = g.ToList()
                })
                .OrderBy(g => g.BrandName)
                .ToList();
        }

        public async Task<List<BrokerReportGroup>> GetBrokerWiseReportAsync(ReportFilter filter)
        {
            var entries = await GetReportEntriesAsync(filter);
            return entries
                .GroupBy(e => e.BrokerName)
                .Select(g => new BrokerReportGroup
                {
                    BrokerName = g.Key,
                    Entries = g.ToList()
                })
                .OrderBy(g => g.BrokerName)
                .ToList();
        }

        public async Task<(int TotalSales, int TotalEntries, decimal TotalKgs, decimal TotalValue)> GetDashboardMetricsAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            using var connection = _dbFactory.CreateConnection();
            var metrics = await connection.QuerySingleAsync<DashboardMetrics>(
                @"SELECT 
                    (SELECT COUNT(*) FROM Sales WHERE (@FromDate IS NULL OR SaleDate >= @FromDate) AND (@ToDate IS NULL OR SaleDate <= @ToDate)) AS TotalSales,
                    (SELECT COUNT(*) FROM AuctionEntries ae JOIN Sales s ON ae.SaleId = s.SaleId WHERE (@FromDate IS NULL OR s.SaleDate >= @FromDate) AND (@ToDate IS NULL OR s.SaleDate <= @ToDate)) AS TotalEntries,
                    CAST(COALESCE((SELECT SUM(ae.TotalKgs) FROM AuctionEntries ae JOIN Sales s ON ae.SaleId = s.SaleId WHERE (@FromDate IS NULL OR s.SaleDate >= @FromDate) AND (@ToDate IS NULL OR s.SaleDate <= @ToDate)), 0) AS REAL) AS TotalKgs,
                    CAST(COALESCE((SELECT SUM(ae.Value) FROM AuctionEntries ae JOIN Sales s ON ae.SaleId = s.SaleId WHERE (@FromDate IS NULL OR s.SaleDate >= @FromDate) AND (@ToDate IS NULL OR s.SaleDate <= @ToDate)), 0) AS REAL) AS TotalValue",
                new { FromDate = fromDate?.Date, ToDate = toDate?.Date.AddDays(1).AddSeconds(-1) });
            return (metrics.TotalSales, metrics.TotalEntries, metrics.TotalKgs, metrics.TotalValue);
        }

        private sealed class DashboardMetrics
        {
            public int TotalSales { get; set; }
            public int TotalEntries { get; set; }
            public decimal TotalKgs { get; set; }
            public decimal TotalValue { get; set; }
        }
    }
}
