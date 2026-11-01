using System;
using System.IO;
using System.Text.Json;

namespace AuctionPurchase.Infrastructure
{
    public class AppSettings
    {
        // ── Session / UI ──────────────────────────────────────────────
        public DateTime? SessionStartDate { get; set; }
        public DateTime? SessionEndDate { get; set; }
        public string ActiveSessionId { get; set; } = string.Empty;
        public List<AuctionSession> AuctionSessions { get; set; } = new();
        public string DateFormat { get; set; } = "dd/MM/yyyy";
        public bool IsDarkMode { get; set; } = true;

        // ── Locations ───────────────────────────────────────────────
        public string DatabaseLocation { get; set; } = string.Empty;
        public string BackupLocation { get; set; } = string.Empty;
        public string ExportLocation { get; set; } = string.Empty;

        // ── Company profile ───────────────────────────────────────────
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyAddress { get; set; } = string.Empty;
        public string CompanyPhone { get; set; } = string.Empty;
        public string CompanyEmail { get; set; } = string.Empty;
        public string CompanyWebsite { get; set; } = string.Empty;
        /// <summary>Base64-encoded PNG or JPEG image string for the company logo.</summary>
        public string LogoBase64 { get; set; } = string.Empty;
        /// <summary>Default text added to report headers (may contain template variables).</summary>
        public string DefaultHeaderText { get; set; } = string.Empty;
        /// <summary>Default text added to report footers (may contain template variables).</summary>
        public string DefaultFooterText { get; set; } = string.Empty;

        // ── Signatories ───────────────────────────────────────────────
        public string PreparedBy { get; set; } = string.Empty;
        public string CheckedBy { get; set; } = string.Empty;
        public string ApprovedBy { get; set; } = string.Empty;
    }

    public static class SettingsManager
    {
        private static bool IsPortable => File.Exists(Path.Combine(AppContext.BaseDirectory, "Data", "settings.json"));
        private static string DevelopmentDataRoot
        {
            get
            {
                var workspace = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Auction Purchase");
                var local = Path.Combine(workspace, ".local");
                return File.Exists(Path.Combine(local, "Data", "settings.json")) ? local : workspace;
            }
        }

        public static string DefaultDatabaseLocation => IsPortable
            ? Path.Combine(AppContext.BaseDirectory, "Data") : Path.Combine(DevelopmentDataRoot, "Data");

        public static string DefaultBackupLocation => IsPortable
            ? Path.Combine(AppContext.BaseDirectory, "Backups") : Path.Combine(DevelopmentDataRoot, "Backups");

        public static string DefaultExportLocation => IsPortable
            ? Path.Combine(AppContext.BaseDirectory, "Exports") : Path.Combine(DevelopmentDataRoot, "Exports");

        public static string DefaultDatabasePath => Path.Combine(DefaultDatabaseLocation, "Auction.db");

        private static string SettingsFilePath
        {
            get
            {
                var localDataSettings = Path.Combine(AppContext.BaseDirectory, "Data", "settings.json");
                if (File.Exists(localDataSettings)) return localDataSettings;
                var localSettings = Path.Combine(AppContext.BaseDirectory, "settings.json");
                if (File.Exists(localSettings)) return localSettings;
                return Path.Combine(DefaultDatabaseLocation, "settings.json");
            }
        }

        private static AppSettings _current = new AppSettings();
        private static bool _loaded = false;

        public static AppSettings Current
        {
            get
            {
                EnsureLoaded();
                return _current;
            }
        }

        /// <summary>Full path to the settings file (used by BackupService).</summary>
        public static string SettingsPath => SettingsFilePath;

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            if (!File.Exists(SettingsFilePath))
            {
                Save();
                return;
            }
            try
            {
                var json = File.ReadAllText(SettingsFilePath);
                _current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
                var json = JsonSerializer.Serialize(_current, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch { }
        }

        /// <summary>Reload settings from disk (used after backup restore).</summary>
        public static void Reload()
        {
            _loaded = false;
            EnsureLoaded();
        }
    }

    public class AuctionSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public DateTime DateStart { get; set; }
        public DateTime DateFinish { get; set; }
    }
}
