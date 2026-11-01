using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace AuctionPurchase.Infrastructure.Data
{
    public class DatabaseConfig
    {
        public string DatabasePath { get; }
        public string ConnectionString { get; }

        public DatabaseConfig()
        {
            var dbLoc = SettingsManager.Current.DatabaseLocation;
            // If configured dbLoc doesn't exist, check local ./Data next to application
            if (string.IsNullOrWhiteSpace(dbLoc) || !Directory.Exists(dbLoc) || !File.Exists(Path.Combine(dbLoc, "Auction.db")))
            {
                var localData = Path.Combine(AppContext.BaseDirectory, "Data");
                if (Directory.Exists(localData) && File.Exists(Path.Combine(localData, "Auction.db")))
                {
                    dbLoc = localData;
                }
                else if (string.IsNullOrWhiteSpace(dbLoc) || !Directory.Exists(dbLoc))
                {
                    dbLoc = SettingsManager.DefaultDatabaseLocation;
                }
            }
            
            if (!Directory.Exists(dbLoc))
            {
                Directory.CreateDirectory(dbLoc);
            }

            DatabasePath = Path.Combine(dbLoc, "Auction.db");
            ConnectionString = $"Data Source={DatabasePath};Foreign Keys=True;";
        }
    }

    public interface IDatabaseFactory
    {
        SqliteConnection CreateConnection();
    }

    public class DatabaseFactory : IDatabaseFactory
    {
        private readonly DatabaseConfig _config;

        public DatabaseFactory(DatabaseConfig config)
        {
            _config = config;
        }

        public SqliteConnection CreateConnection()
        {
            return new SqliteConnection(_config.ConnectionString);
        }
    }
}
