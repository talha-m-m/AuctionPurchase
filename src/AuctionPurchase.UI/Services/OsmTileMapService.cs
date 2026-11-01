using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace AuctionPurchase.UI.Services
{
    /// <summary>
    /// Fetches and caches OpenStreetMap tile images for the interactive world map.
    /// Converts between geographic lat/lon and tile/world-pixel coordinate systems.
    /// </summary>
    public static class OsmTileMapService
    {
        public const int TileSize = 256;
        public const int MinZoom = 4;
        public const int MaxZoom = 16;

        private static readonly HttpClient _http;
        private static readonly ConcurrentDictionary<string, BitmapImage?> _cache = new();
        private static readonly SemaphoreSlim _semaphore = new(6, 6);

        static OsmTileMapService()
        {
            _http = new HttpClient();
            _http.DefaultRequestHeaders.Add("User-Agent",
                "AuctionPurchase-TeaDashboard/1.0 (WPF; contact=internal)");
            _http.Timeout = TimeSpan.FromSeconds(10);
        }

        // ─── Coordinate Math ──────────────────────────────────────────────────

        /// <summary>
        /// Convert geographic lat/lon → world-pixel at the given zoom level.
        /// World pixel (0,0) = top-left of the full world tile grid.
        /// </summary>
        public static (double X, double Y) LatLonToWorldPixel(double lat, double lon, int zoom)
        {
            double n = Math.Pow(2, zoom) * TileSize;
            double x = (lon + 180.0) / 360.0 * n;

            double latRad = lat * Math.PI / 180.0;
            double sinLat = Math.Sin(latRad);
            sinLat = Math.Clamp(sinLat, -0.9999, 0.9999);
            double y = (0.5 - Math.Log((1 + sinLat) / (1 - sinLat)) / (4 * Math.PI)) * n;

            return (x, y);
        }

        /// <summary>
        /// Convert world-pixel → geographic lat/lon (inverse of above).
        /// </summary>
        public static (double Lat, double Lon) WorldPixelToLatLon(double wx, double wy, int zoom)
        {
            double n = Math.Pow(2, zoom) * TileSize;
            double lon = wx / n * 360.0 - 180.0;
            double latRad = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * wy / n)));
            double lat = latRad * 180.0 / Math.PI;
            return (lat, lon);
        }

        public static int ClampZoom(int z) => Math.Clamp(z, MinZoom, MaxZoom);

        // ─── Tile Fetching ────────────────────────────────────────────────────

        /// <summary>
        /// Download (or return cached) tile image. Returns null on failure.
        /// </summary>
        public static async Task<BitmapImage?> GetTileAsync(int z, int x, int y)
        {
            int maxTile = (int)Math.Pow(2, z);
            x = ((x % maxTile) + maxTile) % maxTile; // wrap X
            if (y < 0 || y >= maxTile) return null;

            string key = $"{z}/{x}/{y}";
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_cache.TryGetValue(key, out var cached2))
                    return cached2;

                string[] subs = { "a", "b", "c" };
                string sub = subs[(x + y) % 3];
                string url = $"https://{sub}.tile.openstreetmap.org/{z}/{x}/{y}.png";

                byte[] bytes = await _http.GetByteArrayAsync(url).ConfigureAwait(false);

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = new MemoryStream(bytes);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze(); // Cross-thread safe

                _cache[key] = bmp;
                return bmp;
            }
            catch
            {
                _cache.TryAdd(key, null); // Avoid retry storms
                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public static void ClearCache() => _cache.Clear();
    }
}
