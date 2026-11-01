using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace AuctionPurchase.Infrastructure.Backup
{
    public class BackupManifest
    {
        public string AppName { get; set; } = "AuctionPurchase";
        /// <summary>
        /// "1.0" = database only.
        /// "1.1" = database + settings.json.
        /// </summary>
        public string Version { get; set; } = "1.1";
        public int SchemaVersion { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string MachineName { get; set; } = Environment.MachineName;
        public string DatabaseFileName { get; set; } = "Auction.db";
        public string SettingsFileName { get; set; } = "settings.json";
    }

    public class BackupService
    {
        private readonly Data.DatabaseConfig _config;

        public BackupService(Data.DatabaseConfig config)
        {
            _config = config;
        }

        public string GetBackupFolder()
        {
            var backupFolder = SettingsManager.Current.BackupLocation;
            if (string.IsNullOrWhiteSpace(backupFolder))
            {
                backupFolder = SettingsManager.DefaultBackupLocation;
            }
            Directory.CreateDirectory(backupFolder);
            return backupFolder;
        }

        public async Task<string> CreateBackupAsync(int schemaVersion)
        {
            var backupFolder   = GetBackupFolder();
            var timestamp      = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            var backupFileName = $"AuctionBackup_{timestamp}.tkbackup";
            var backupPath     = Path.Combine(backupFolder, backupFileName);

            var manifest = new BackupManifest
            {
                Version       = "1.1",
                SchemaVersion = schemaVersion,
                CreatedAt     = DateTime.Now
            };

            var manifestJson = JsonSerializer.Serialize(
                manifest, new JsonSerializerOptions { WriteIndented = true });

            using (var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
            {
                // ── manifest.json ─────────────────────────────────────
                var manifestEntry = archive.CreateEntry("manifest.json");
                using (var writer = new StreamWriter(manifestEntry.Open()))
                    await writer.WriteAsync(manifestJson);

                // ── Auction.db ────────────────────────────────────────
                if (File.Exists(_config.DatabasePath))
                {
                    SqliteConnection.ClearAllPools();
                    var tempDbPath = Path.GetTempFileName();
                    try
                    {
                        File.Copy(_config.DatabasePath, tempDbPath, overwrite: true);
                        archive.CreateEntryFromFile(tempDbPath, manifest.DatabaseFileName);
                    }
                    finally
                    {
                        try { File.Delete(tempDbPath); } catch { }
                    }
                }

                // ── settings.json ─────────────────────────────────────
                var settingsPath = SettingsManager.SettingsPath;
                if (File.Exists(settingsPath))
                    archive.CreateEntryFromFile(settingsPath, manifest.SettingsFileName);
            }

            return backupPath;
        }

        public async Task RestoreBackupAsync(string backupPath)
        {
            SqliteConnection.ClearAllPools();

            var dbPath = _config.DatabasePath;

            // Safety copy of existing DB
            if (File.Exists(dbPath))
            {
                var safetyPath = dbPath + $".safety_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
                File.Copy(dbPath, safetyPath, overwrite: true);
            }

            try
            {
                using var archive = ZipFile.OpenRead(backupPath);

                // ── Validate manifest ─────────────────────────────────
                var manifestEntry = archive.GetEntry("manifest.json")
                    ?? throw new InvalidDataException("Invalid backup file: missing manifest.json");

                string manifestJson;
                using (var reader = new StreamReader(manifestEntry.Open()))
                    manifestJson = await reader.ReadToEndAsync();

                var manifest = JsonSerializer.Deserialize<BackupManifest>(manifestJson);
                if (manifest == null || manifest.AppName != "AuctionPurchase")
                    throw new InvalidDataException("Invalid backup file: wrong application manifest.");

                // ── Restore Auction.db ────────────────────────────────
                var dbEntry = archive.GetEntry(manifest.DatabaseFileName)
                    ?? throw new InvalidDataException(
                        $"Invalid backup file: database '{manifest.DatabaseFileName}' not found.");

                Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
                using (var dbStream   = dbEntry.Open())
                using (var fileStream = File.Create(dbPath))
                    await dbStream.CopyToAsync(fileStream);

                // ── Restore settings.json (if present — backward-compat) ──
                var settingsEntry = archive.GetEntry(manifest.SettingsFileName ?? "settings.json");
                if (settingsEntry != null)
                {
                    var settingsPath = SettingsManager.SettingsPath;
                    Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
                    using var sIn  = settingsEntry.Open();
                    using var sOut = File.Create(settingsPath);
                    await sIn.CopyToAsync(sOut);

                    // Reload in-memory settings
                    SettingsManager.Reload();
                }
            }
            catch
            {
                // Roll back to safety copy
                var safetyFiles = Directory.GetFiles(
                    Path.GetDirectoryName(dbPath)!, "*.safety_*.bak");
                if (safetyFiles.Length > 0)
                {
                    Array.Sort(safetyFiles);
                    File.Copy(safetyFiles[^1], dbPath, overwrite: true);
                }
                throw;
            }
        }
    }
}
