using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AuctionPurchase.UI.Services;

public static class BangladeshMapOverlay
{
    // Natural Earth 1:50m Admin 0 countries, public domain.
    // https://github.com/nvkelso/natural-earth-vector/blob/master/geojson/ne_50m_admin_0_countries.geojson
    private static readonly List<List<(double Lon, double Lat)>> Rings = LoadRings();

    private static List<List<(double Lon, double Lat)>> LoadRings()
    {
        using var stream = typeof(BangladeshMapOverlay).Assembly.GetManifestResourceStream("AuctionPurchase.UI.Assets.bangladesh-boundary.json")!;
        using var json = JsonDocument.Parse(stream);
        var root = json.RootElement;
        var polygons = root.GetProperty("type").GetString() == "MultiPolygon"
            ? root.GetProperty("coordinates").EnumerateArray().ToList()
            : new List<JsonElement> { root.GetProperty("coordinates") };
        return polygons.SelectMany(p => p.EnumerateArray()).Select(r => r.EnumerateArray()
            .Select(p => (p[0].GetDouble(), p[1].GetDouble())).ToList()).ToList();
    }

    public static Geometry Geometry(double centerLat, double centerLon, int zoom, double width, double height)
    {
        var (cx, cy) = OsmTileMapService.LatLonToWorldPixel(centerLat, centerLon, zoom);
        var geometry = new StreamGeometry { FillRule = FillRule.EvenOdd };
        using (var context = geometry.Open())
            foreach (var ring in Rings)
            {
                var points = ring.Select(p =>
                {
                    var (x, y) = OsmTileMapService.LatLonToWorldPixel(p.Lat, p.Lon, zoom);
                    return new Point(x - cx + width / 2, y - cy + height / 2);
                }).ToList();
                if (points.Count < 3) continue;
                context.BeginFigure(points[0], true, true);
                context.PolyLineTo(points.Skip(1).ToList(), true, false);
            }
        geometry.Freeze();
        return geometry;
    }

    public static void Draw(Canvas canvas, double lat, double lon, int zoom, double width, double height)
    {
        var country = Geometry(lat, lon, zoom, width, height);
        canvas.Children.Add(new System.Windows.Shapes.Path
        {
            Data = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(0, 0, width, height)), country),
            Fill = new SolidColorBrush(Color.FromArgb(90, 12, 23, 40)), IsHitTestVisible = false
        });
        canvas.Children.Add(new System.Windows.Shapes.Path
        {
            Data = country, Fill = new SolidColorBrush(Color.FromArgb(22, 16, 185, 129)),
            Stroke = new SolidColorBrush(Color.FromRgb(13, 148, 136)), StrokeThickness = 2.2, IsHitTestVisible = false,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.White, BlurRadius = 5, ShadowDepth = 0, Opacity = 0.7 }
        });
    }
}
