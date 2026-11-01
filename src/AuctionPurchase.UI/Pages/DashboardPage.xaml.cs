using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AuctionPurchase.Core.Models;
using AuctionPurchase.Core.Reports;
using AuctionPurchase.Infrastructure;
using AuctionPurchase.Infrastructure.Data;
using AuctionPurchase.UI.Services;
using AuctionPurchase.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AuctionPurchase.UI.Pages
{
    public partial class DashboardPage : Page
    {
        private readonly IServiceProvider _services;

        // Cached Data
        private List<ReportEntry> _allSeasonEntries = new();
        private List<Garden> _allGardens = new();
        private List<Broker> _allBrokers = new();
        private List<Brand> _allBrands = new();
        private List<GardenMapPin> _gardenPins = new();
        private List<RecentSaleRowItem> _allRecentSales = new();

        // Selected pin/garden state
        private GardenMapPin? _selectedGarden;

        // ─── Map Coordinates & State (Regular Map) ──────────────────────────
        // Default: World map centered directly on Bangladesh
        private const double DefaultCenterLat = 23.7;
        private const double DefaultCenterLon = 90.35;
        private const int DefaultZoom = 6;

        private double _centerLat = DefaultCenterLat;
        private double _centerLon = DefaultCenterLon;
        private int _zoom = DefaultZoom;

        private Point _mapDragStart;
        private bool _isMapDragging = false;
        private double _dragStartLat;
        private double _dragStartLon;

        // ─── Map Coordinates & State (Fullscreen Map) ───────────────────────
        private double _fsCenterLat = DefaultCenterLat;
        private double _fsCenterLon = DefaultCenterLon;
        private int _fsZoom = DefaultZoom;

        private Point _fsMapDragStart;
        private bool _isFsMapDragging = false;
        private double _fsDragStartLat;
        private double _fsDragStartLon;

        // Selected sale for detail drawer
        private RecentSaleRowItem? _inspectedSale;

        // Color palettes
        private static readonly string[] VibrantColors = new[]
        {
            "#3B82F6", "#8B5CF6", "#10B981", "#F59E0B", "#EC4899",
            "#06B6D4", "#EF4444", "#14B8A6", "#6366F1", "#F97316"
        };

        public DashboardPage(IServiceProvider services)
        {
            _services = services;
            InitializeComponent();
            Loaded += async (_, _) => await LoadDataAsync();
        }

        #region Data Loading & Executive KPIs

        private async Task LoadDataAsync()
        {
            try
            {
                var todayBinding = (System.Windows.Data.MultiBinding)UiPreferences.DateBinding(".");
                ((System.Windows.Data.Binding)todayBinding.Bindings[0]).Source = DateTime.Today;
                TxtTodayDate.SetBinding(TextBlock.TextProperty, todayBinding);

                var reportRepo = _services.GetRequiredService<ReportRepository>();
                var gardenRepo = _services.GetRequiredService<GardenRepository>();
                var brokerRepo = _services.GetRequiredService<BrokerRepository>();
                var brandRepo = _services.GetRequiredService<BrandRepository>();

                var sessionStart = SettingsManager.Current.SessionStartDate;
                var sessionEnd = SettingsManager.Current.SessionEndDate;

                // Update Season Badge Label
                if (sessionStart.HasValue && sessionEnd.HasValue)
                {
                    TxtSeasonLabel.Text = $"{sessionStart.Value:yyyy}–{sessionEnd.Value:yyyy}";
                }
                else
                {
                    TxtSeasonLabel.Text = $"{DateTime.Now:yyyy}–{(DateTime.Now.Year + 1)}";
                }

                // 1. Fetch Master Data
                _allGardens = (await gardenRepo.GetAllAsync()).ToList();
                _allBrokers = (await brokerRepo.GetAllAsync()).ToList();
                _allBrands = (await brandRepo.GetAllAsync()).ToList();

                // 2. Fetch Season Entries
                var filter = new ReportFilter
                {
                    FromDate = sessionStart,
                    ToDate = sessionEnd
                };
                _allSeasonEntries = (await reportRepo.GetReportEntriesAsync(filter)).ToList();

                // 3. Compute Executive Metrics
                ComputeExecutiveMetrics(sessionStart, sessionEnd);

                // 4. Render Visualizations
                RenderPurchasesByBroker();
                RenderPurchasesByBrand();
                InitializeGardenMapPins();
                RenderMap(isFullscreen: false);

                // 5. Populate Recent Sales Table & Filters
                InitializeRecentSalesFilters();
                BuildRecentSalesItems();
                ApplyRecentSalesFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading Executive Dashboard: {ex.Message}", "Dashboard Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ComputeExecutiveMetrics(DateTime? sessionStart, DateTime? sessionEnd)
        {
            var distinctSales = _allSeasonEntries
                .GroupBy(e => new { e.SaleNo, e.SaleDate })
                .Count();

            int totalLots = _allSeasonEntries.Count;
            decimal totalWeight = _allSeasonEntries.Sum(e => e.TotalKgs);
            decimal totalValue = _allSeasonEntries.Sum(e => e.Value);
            decimal avgPrice = totalWeight > 0 ? totalValue / totalWeight : 0;

            TxtKpiTotalSales.Text = distinctSales.ToString("N0");
            TxtKpiTotalLots.Text = totalLots.ToString("N0");
            TxtKpiTotalWeight.Text = totalWeight.ToString("N2");
            TxtKpiTotalValue.Text = totalValue.ToString("N2");
            TxtKpiAvgPrice.Text = $"৳{avgPrice:N2}/Kg";

            // Previous season growth calculation
            double growthPercent = 0;
            bool isPositive = true;

            try
            {
                var reportRepo = _services.GetRequiredService<ReportRepository>();
                DateTime? prevStart = sessionStart?.AddYears(-1);
                DateTime? prevEnd = sessionEnd?.AddYears(-1);

                if (prevStart.HasValue && prevEnd.HasValue)
                {
                    var (_, _, prevKgs, _) = await reportRepo.GetDashboardMetricsAsync(prevStart, prevEnd);
                    if (prevKgs > 0)
                    {
                        growthPercent = (double)((totalWeight - prevKgs) / prevKgs * 100);
                        isPositive = growthPercent >= 0;
                    }
                    else if (totalWeight > 0)
                    {
                        growthPercent = 100.0;
                        isPositive = true;
                    }
                }
            }
            catch { }

            if (isPositive)
            {
                TxtGrowthArrow.Text = "▲";
                TxtGrowthArrow.SetResourceReference(TextBlock.ForegroundProperty, "ThemeText_10B981_Text");
                TxtWeightGrowth.Text = $"+{Math.Abs(growthPercent):F1}%";
                TxtWeightGrowth.SetResourceReference(TextBlock.ForegroundProperty, "ThemeText_34D399_Text");
                BadgeWeightGrowth.SetResourceReference(Border.BackgroundProperty, "ThemeSurface_133324_Surface");
            }
            else
            {
                TxtGrowthArrow.Text = "▼";
                TxtGrowthArrow.SetResourceReference(TextBlock.ForegroundProperty, "ThemeText_EF4444_Text");
                TxtWeightGrowth.Text = $"-{Math.Abs(growthPercent):F1}%";
                TxtWeightGrowth.SetResourceReference(TextBlock.ForegroundProperty, "ThemeText_F87171_Text");
                BadgeWeightGrowth.SetResourceReference(Border.BackgroundProperty, "ThemeSurface_38181C_Surface");
            }
        }

        #endregion

        #region Chart: Purchases by Broker

        private void RenderPurchasesByBroker()
        {
            if (_allSeasonEntries == null || _allSeasonEntries.Count == 0)
            {
                ListBrokersChart.ItemsSource = null;
                TxtBrokerTotalVolume.Text = "0 Kg";
                return;
            }

            decimal totalVolume = _allSeasonEntries.Sum(e => e.TotalKgs);
            TxtBrokerTotalVolume.Text = $"{totalVolume:N0} Kg Total";

            var brokerGroups = _allSeasonEntries
                .GroupBy(e => string.IsNullOrWhiteSpace(e.BrokerName) ? "Unknown" : e.BrokerName)
                .Select((g, index) =>
                {
                    decimal kgs = g.Sum(e => e.TotalKgs);
                    decimal val = g.Sum(e => e.Value);
                    double pct = totalVolume > 0 ? (double)(kgs / totalVolume * 100) : 0;
                    var brokerObj = _allBrokers.FirstOrDefault(b => b.Name.Equals(g.Key, StringComparison.OrdinalIgnoreCase));

                    return new BrokerVolumeItem
                    {
                        BrokerName = g.Key,
                        FullName = brokerObj?.FullName ?? g.Key,
                        TotalKgs = kgs,
                        TotalValueBdt = val,
                        LotCount = g.Count(),
                        Percentage = pct,
                        ColorHex = VibrantColors[index % VibrantColors.Length]
                    };
                })
                .OrderByDescending(b => b.TotalKgs)
                .ToList();

            decimal maxKgs = brokerGroups.Count > 0 ? brokerGroups.Max(b => b.TotalKgs) : 1;
            if (maxKgs <= 0) maxKgs = 1;

            foreach (var b in brokerGroups)
            {
                b.BarWidthPercent = Math.Max(12, (double)(b.TotalKgs / maxKgs * 360));
            }

            ListBrokersChart.ItemsSource = brokerGroups;
        }

        #endregion

        #region Chart: Purchases by Brand (Donut Chart)

        private void RenderPurchasesByBrand()
        {
            BrandDonutCanvas.Children.Clear();

            if (_allSeasonEntries == null || _allSeasonEntries.Count == 0)
            {
                ListBrandsChart.ItemsSource = null;
                TxtDonutCenterWeight.Text = "0 Kg";
                TxtDonutCenterBrands.Text = "0 Brands";
                TxtBrandCountSummary.Text = "0 Brands";
                return;
            }

            decimal totalVolume = _allSeasonEntries.Sum(e => e.TotalKgs);
            var brandGroups = _allSeasonEntries
                .GroupBy(e => string.IsNullOrWhiteSpace(e.BrandName) ? "Unassigned" : e.BrandName)
                .Select((g, index) =>
                {
                    decimal kgs = g.Sum(e => e.TotalKgs);
                    decimal val = g.Sum(e => e.Value);
                    double pct = totalVolume > 0 ? (double)(kgs / totalVolume * 100) : 0;

                    return new BrandVolumeItem
                    {
                        BrandName = g.Key,
                        TotalKgs = kgs,
                        TotalValueBdt = val,
                        LotCount = g.Count(),
                        Percentage = pct,
                        ColorHex = VibrantColors[index % VibrantColors.Length]
                    };
                })
                .OrderByDescending(b => b.TotalKgs)
                .ToList();

            TxtDonutCenterWeight.Text = $"{totalVolume:N0} Kg";
            TxtDonutCenterBrands.Text = $"{brandGroups.Count} Active Brands";
            TxtBrandCountSummary.Text = $"{brandGroups.Count} Brands";
            ListBrandsChart.ItemsSource = brandGroups;

            double cx = 90;
            double cy = 90;
            double outerRadius = 82;
            double innerRadius = 52;

            double currentAngle = -90; // Start at 12 o'clock

            foreach (var brand in brandGroups)
            {
                double sweepAngle = (brand.Percentage / 100.0) * 360.0;
                if (sweepAngle < 0.5) sweepAngle = 0.5;

                brand.StartAngle = currentAngle;
                brand.SweepAngle = sweepAngle;

                var slicePath = CreateDonutSlicePath(cx, cy, outerRadius, innerRadius, currentAngle, sweepAngle, brand.ColorHex);
                slicePath.ToolTip = $"{brand.BrandName}\nVolume: {brand.TotalKgs:N0} Kg ({brand.Percentage:F1}%)\nValue: ৳{brand.TotalValueBdt:N2}\nLots: {brand.LotCount}";
                slicePath.Cursor = Cursors.Hand;

                BrandDonutCanvas.Children.Add(slicePath);
                currentAngle += sweepAngle;
            }
        }

        private Path CreateDonutSlicePath(double cx, double cy, double rOuter, double rInner, double startAngleDeg, double sweepAngleDeg, string colorHex)
        {
            double endAngleDeg = startAngleDeg + sweepAngleDeg;
            if (sweepAngleDeg >= 360.0) endAngleDeg = startAngleDeg + 359.99;

            double startRad = startAngleDeg * Math.PI / 180.0;
            double endRad = endAngleDeg * Math.PI / 180.0;

            Point pOuterStart = new Point(cx + rOuter * Math.Cos(startRad), cy + rOuter * Math.Sin(startRad));
            Point pOuterEnd = new Point(cx + rOuter * Math.Cos(endRad), cy + rOuter * Math.Sin(endRad));
            Point pInnerEnd = new Point(cx + rInner * Math.Cos(endRad), cy + rInner * Math.Sin(endRad));
            Point pInnerStart = new Point(cx + rInner * Math.Cos(startRad), cy + rInner * Math.Sin(startRad));

            bool isLargeArc = sweepAngleDeg > 180.0;

            var figure = new PathFigure
            {
                StartPoint = pOuterStart,
                IsClosed = true,
                IsFilled = true
            };

            figure.Segments.Add(new ArcSegment(pOuterEnd, new Size(rOuter, rOuter), 0, isLargeArc, SweepDirection.Clockwise, true));
            figure.Segments.Add(new LineSegment(pInnerEnd, true));
            figure.Segments.Add(new ArcSegment(pInnerStart, new Size(rInner, rInner), 0, isLargeArc, SweepDirection.Counterclockwise, true));

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
            brush.Freeze();

            return new Path
            {
                Data = geometry,
                Fill = brush,
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#161B33")),
                StrokeThickness = 1.5
            };
        }

        #endregion

        #region Interactive World Map Centered on Bangladesh

        private void InitializeGardenMapPins()
        {
            _gardenPins.Clear();

            // Group entries by garden
            var gardenPurchases = _allSeasonEntries
                .Where(e => !string.IsNullOrWhiteSpace(e.GardenName))
                .GroupBy(e => e.GardenName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => new { Lots = g.Count(), Weight = g.Sum(e => e.TotalKgs), Value = g.Sum(e => e.Value) },
                    StringComparer.OrdinalIgnoreCase
                );

            foreach (var garden in _allGardens)
            {
                var loc = TeaGardenGeoService.ResolveLocation(garden);

                int lots = 0;
                decimal kgs = 0;
                decimal val = 0;

                if (gardenPurchases.TryGetValue(garden.Name, out var stats))
                {
                    lots = stats.Lots;
                    kgs = stats.Weight;
                    val = stats.Value;
                }

                var pin = new GardenMapPin
                {
                    GardenId = garden.GardenId,
                    Name = garden.Name,
                    FullName = garden.FullName ?? garden.Name,
                    District = garden.District ?? "",
                    Division = garden.Division ?? "",
                    RegionGroup = loc.RegionGroup,
                    MapLink = garden.MapLink,
                    Latitude = loc.Latitude,
                    Longitude = loc.Longitude,
                    TotalKgs = kgs,
                    LotsCount = lots,
                    TotalValueBdt = val,
                    PinSize = lots > 0 ? Math.Min(22, 12 + Math.Sqrt((double)kgs) / 30) : 10,
                    PinColor = lots > 0 ? "#10B981" : "#4A5568"
                };

                _gardenPins.Add(pin);
            }

            TxtMapPinCount.Text = $"{_gardenPins.Count} Gardens Mapped";
            TxtFsMapStats.Text = $"{_gardenPins.Count} Gardens ({_gardenPins.Count(p => p.HasPurchases)} Active)";
        }

        /// <summary>
        /// Renders map tiles and garden pins based on current center coordinates and zoom level.
        /// </summary>
        private void RenderMap(bool isFullscreen)
        {
            var viewport = isFullscreen ? FsMapViewport : MapViewport;
            var tilesCanvas = isFullscreen ? FsMapTilesCanvas : MapTilesCanvas;
            var pinsCanvas = isFullscreen ? FsMapPinsCanvas : MapPinsCanvas;

            double centerLat = isFullscreen ? _fsCenterLat : _centerLat;
            double centerLon = isFullscreen ? _fsCenterLon : _centerLon;
            int zoom = isFullscreen ? _fsZoom : _zoom;

            double width = viewport.ActualWidth > 50 ? viewport.ActualWidth : (isFullscreen ? 1100 : 750);
            double height = viewport.ActualHeight > 50 ? viewport.ActualHeight : (isFullscreen ? 750 : 380);

            tilesCanvas.Children.Clear();
            pinsCanvas.Children.Clear();

            // 1. Calculate World Pixel Center
            var (centerWx, centerWy) = OsmTileMapService.LatLonToWorldPixel(centerLat, centerLon, zoom);

            // 2. Visible world boundaries
            double minWx = centerWx - (width / 2.0);
            double maxWx = centerWx + (width / 2.0);
            double minWy = centerWy - (height / 2.0);
            double maxWy = centerWy + (height / 2.0);

            int minTx = (int)Math.Floor(minWx / OsmTileMapService.TileSize);
            int maxTx = (int)Math.Floor(maxWx / OsmTileMapService.TileSize);
            int minTy = (int)Math.Floor(minWy / OsmTileMapService.TileSize);
            int maxTy = (int)Math.Floor(maxWy / OsmTileMapService.TileSize);

            int maxTileCoord = (int)Math.Pow(2, zoom);

            // 3. Render Visible OSM Tiles
            for (int ty = minTy; ty <= maxTy; ty++)
            {
                if (ty < 0 || ty >= maxTileCoord) continue;

                for (int tx = minTx; tx <= maxTx; tx++)
                {
                    double vx = (tx * OsmTileMapService.TileSize) - centerWx + (width / 2.0);
                    double vy = (ty * OsmTileMapService.TileSize) - centerWy + (height / 2.0);

                    var img = new Image
                    {
                        Width = OsmTileMapService.TileSize,
                        Height = OsmTileMapService.TileSize,
                        Stretch = Stretch.UniformToFill,
                        Opacity = 0.95
                    };

                    Canvas.SetLeft(img, vx);
                    Canvas.SetTop(img, vy);
                    tilesCanvas.Children.Add(img);

                    int tileX = tx;
                    int tileY = ty;
                    int tileZ = zoom;

                    _ = OsmTileMapService.GetTileAsync(tileZ, tileX, tileY).ContinueWith(task =>
                    {
                        if (task.IsCompletedSuccessfully && task.Result != null)
                        {
                            Dispatcher.InvokeAsync(() =>
                            {
                                img.Source = task.Result;
                            });
                        }
                    });
                }
            }

            // 4. Render Garden Pinpoints (Exact Geographic Placement in Bangladesh)
            BangladeshMapOverlay.Draw(pinsCanvas, centerLat, centerLon, zoom, width, height);
            var orderedPins = _gardenPins.OrderBy(p => p.HasPurchases ? 1 : 0).ToList();

            foreach (var pin in orderedPins)
            {
                var (pWx, pWy) = OsmTileMapService.LatLonToWorldPixel(pin.Latitude, pin.Longitude, zoom);
                double pVx = pWx - centerWx + (width / 2.0);
                double pVy = pWy - centerWy + (height / 2.0);

                // Skip pins far outside visible viewport
                if (pVx < -50 || pVx > width + 50 || pVy < -50 || pVy > height + 50)
                    continue;

                double size = pin.PinSize * (isFullscreen ? 1.25 : 1.0);

                var pinBorder = new Border
                {
                    Width = size,
                    Height = size,
                    CornerRadius = new CornerRadius(size / 2),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(pin.PinColor)),
                    BorderBrush = new SolidColorBrush(pin.HasPurchases ? Colors.White : (Color)ColorConverter.ConvertFromString("#94A3B8")),
                    BorderThickness = new Thickness(pin.HasPurchases ? 1.8 : 1),
                    Cursor = Cursors.Hand,
                    Tag = pin
                };

                if (pin.HasPurchases)
                {
                    pinBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = (Color)ColorConverter.ConvertFromString("#10B981"),
                        BlurRadius = 14,
                        ShadowDepth = 0,
                        Opacity = 0.9
                    };

                    pinBorder.Child = new Ellipse
                    {
                        Width = size * 0.45,
                        Height = size * 0.45,
                        Fill = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                }

                pinBorder.ToolTip = $"{pin.Name} ({pin.FullName})\nRegion: {pin.District} ({pin.RegionGroup})\nProcured: {pin.LotsCount} lots ({pin.TotalKgs:N0} Kg)\nValue: ৳{pin.TotalValueBdt:N2}\nClick to view details & Google Maps";

                pinBorder.MouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    if (s is Border b && b.Tag is GardenMapPin selectedPin)
                    {
                        SelectGardenPin(selectedPin, isFullscreen);
                    }
                };

                Canvas.SetLeft(pinBorder, pVx - (size / 2.0));
                Canvas.SetTop(pinBorder, pVy - (size / 2.0));
                pinsCanvas.Children.Add(pinBorder);
            }
        }

        private void MapViewport_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (IsLoaded && MapViewport.ActualWidth > 50)
            {
                RenderMap(isFullscreen: false);
            }
        }

        private void FsMapViewport_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (MapFullscreenOverlay.Visibility == Visibility.Visible && FsMapViewport.ActualWidth > 50)
            {
                RenderMap(isFullscreen: true);
            }
        }

        private void SelectGardenPin(GardenMapPin pin, bool isFullscreen)
        {
            _selectedGarden = pin;

            if (isFullscreen)
            {
                TxtFsSelectedGardenName.Text = pin.Name;
                TxtFsSelectedGardenFull.Text = string.IsNullOrWhiteSpace(pin.FullName) ? pin.Name : pin.FullName;
                TxtFsSelectedGardenDistrict.Text = $"District: {pin.District} ({pin.RegionGroup})";
                TxtFsSelectedGardenLots.Text = $"{pin.LotsCount} lots";
                TxtFsSelectedGardenKgs.Text = $"{pin.TotalKgs:N2} Kg";
                TxtFsSelectedGardenValue.Text = $"৳{pin.TotalValueBdt:N2}";
                FsSelectedGardenCard.Visibility = Visibility.Visible;
            }
            else
            {
                TxtSelectedGardenName.Text = pin.Name;
                TxtSelectedGardenFull.Text = string.IsNullOrWhiteSpace(pin.FullName) ? pin.Name : pin.FullName;
                TxtSelectedGardenDistrict.Text = $"District: {pin.District} ({pin.RegionGroup})";
                TxtSelectedGardenLots.Text = $"{pin.LotsCount} lots";
                TxtSelectedGardenKgs.Text = $"{pin.TotalKgs:N2} Kg";
                TxtSelectedGardenValue.Text = $"৳{pin.TotalValueBdt:N2}";
                SelectedGardenCard.Visibility = Visibility.Visible;
            }
        }

        private void BtnCloseGardenCard_Click(object sender, RoutedEventArgs e)
        {
            SelectedGardenCard.Visibility = Visibility.Collapsed;
        }

        private void BtnCloseFsGardenCard_Click(object sender, RoutedEventArgs e)
        {
            FsSelectedGardenCard.Visibility = Visibility.Collapsed;
        }

        private void BtnOpenGardenMapLink_Click(object sender, RoutedEventArgs e)
        {
            OpenGardenMapInBrowser(_selectedGarden);
        }

        private void BtnFsOpenGardenMapLink_Click(object sender, RoutedEventArgs e)
        {
            OpenGardenMapInBrowser(_selectedGarden);
        }

        private void OpenGardenMapInBrowser(GardenMapPin? pin)
        {
            if (pin == null) return;

            string targetUrl;
            if (!string.IsNullOrWhiteSpace(pin.MapLink) && (pin.MapLink.StartsWith("http://") || pin.MapLink.StartsWith("https://")))
            {
                targetUrl = pin.MapLink;
            }
            else
            {
                targetUrl = $"https://www.google.com/maps/search/?api=1&query={pin.Latitude.ToString(CultureInfo.InvariantCulture)},{pin.Longitude.ToString(CultureInfo.InvariantCulture)}";
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open browser: {ex.Message}", "Error");
            }
        }

        #region Regular Map Pan & Zoom

        private void MapViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _mapDragStart = e.GetPosition(MapViewport);
            _dragStartLat = _centerLat;
            _dragStartLon = _centerLon;
            _isMapDragging = true;
            MapViewport.CaptureMouse();
        }

        private void MapViewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isMapDragging)
            {
                Point current = e.GetPosition(MapViewport);
                double dx = current.X - _mapDragStart.X;
                double dy = current.Y - _mapDragStart.Y;

                // Live panning feedback
                MapPanTransform.X = dx;
                MapPanTransform.Y = dy;
                MapPinsPanTransform.X = dx;
                MapPinsPanTransform.Y = dy;
            }
        }

        private void MapViewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isMapDragging)
            {
                _isMapDragging = false;
                MapViewport.ReleaseMouseCapture();

                Point current = e.GetPosition(MapViewport);
                double dx = current.X - _mapDragStart.X;
                double dy = current.Y - _mapDragStart.Y;

                // Reset temporary pan transforms
                MapPanTransform.X = 0;
                MapPanTransform.Y = 0;
                MapPinsPanTransform.X = 0;
                MapPinsPanTransform.Y = 0;

                // Convert pixel drag to new center coordinates
                var (startWx, startWy) = OsmTileMapService.LatLonToWorldPixel(_dragStartLat, _dragStartLon, _zoom);
                double newWx = startWx - dx;
                double newWy = startWy - dy;

                var (newLat, newLon) = OsmTileMapService.WorldPixelToLatLon(newWx, newWy, _zoom);
                _centerLat = Math.Clamp(newLat, -80.0, 84.0);
                _centerLon = ((newLon + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;

                RenderMap(isFullscreen: false);
            }
        }

        private void MapViewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            int delta = e.Delta > 0 ? 1 : -1;
            int newZoom = Math.Clamp(_zoom + delta, 3, 14);
            if (newZoom != _zoom)
            {
                _zoom = newZoom;
                RenderMap(isFullscreen: false);
            }
            e.Handled = true;
        }

        private void BtnMapZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (_zoom < 14)
            {
                _zoom++;
                RenderMap(isFullscreen: false);
            }
        }

        private void BtnMapZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (_zoom > 3)
            {
                _zoom--;
                RenderMap(isFullscreen: false);
            }
        }

        private void BtnMapReset_Click(object sender, RoutedEventArgs e)
        {
            _centerLat = DefaultCenterLat;
            _centerLon = DefaultCenterLon;
            _zoom = DefaultZoom;
            RenderMap(isFullscreen: false);
        }

        private void BtnFocusWorld_Click(object sender, RoutedEventArgs e)
        {
            _centerLat = DefaultCenterLat;
            _centerLon = DefaultCenterLon;
            _zoom = 3;
            RenderMap(isFullscreen: false);
        }

        private void BtnFocusAll_Click(object sender, RoutedEventArgs e)
        {
            _centerLat = 23.8;
            _centerLon = 90.3;
            _zoom = 6;
            RenderMap(isFullscreen: false);
        }

        private void BtnFocusSylhet_Click(object sender, RoutedEventArgs e)
        {
            _centerLat = 24.35;
            _centerLon = 91.75;
            _zoom = 9;
            RenderMap(isFullscreen: false);
        }

        private void BtnFocusChittagong_Click(object sender, RoutedEventArgs e)
        {
            _centerLat = 22.45;
            _centerLon = 91.85;
            _zoom = 9;
            RenderMap(isFullscreen: false);
        }

        private void BtnFocusNorth_Click(object sender, RoutedEventArgs e)
        {
            _centerLat = 26.30;
            _centerLon = 88.60;
            _zoom = 9;
            RenderMap(isFullscreen: false);
        }

        #endregion

        #region Fullscreen Map Pan & Zoom

        private void BtnMapFullscreen_Click(object sender, RoutedEventArgs e)
        {
            MapFullscreenOverlay.Visibility = Visibility.Visible;
            _fsCenterLat = _centerLat;
            _fsCenterLon = _centerLon;
            _fsZoom = Math.Min(14, _zoom + 1);
            RenderMap(isFullscreen: true);
        }

        private void BtnCloseFullscreen_Click(object sender, RoutedEventArgs e)
        {
            MapFullscreenOverlay.Visibility = Visibility.Collapsed;
        }

        private void FsMapViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _fsMapDragStart = e.GetPosition(FsMapViewport);
            _fsDragStartLat = _fsCenterLat;
            _fsDragStartLon = _fsCenterLon;
            _isFsMapDragging = true;
            FsMapViewport.CaptureMouse();
        }

        private void FsMapViewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isFsMapDragging)
            {
                Point current = e.GetPosition(FsMapViewport);
                double dx = current.X - _fsMapDragStart.X;
                double dy = current.Y - _fsMapDragStart.Y;

                FsMapPanTransform.X = dx;
                FsMapPanTransform.Y = dy;
                FsMapPinsPanTransform.X = dx;
                FsMapPinsPanTransform.Y = dy;
            }
        }

        private void FsMapViewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isFsMapDragging)
            {
                _isFsMapDragging = false;
                FsMapViewport.ReleaseMouseCapture();

                Point current = e.GetPosition(FsMapViewport);
                double dx = current.X - _fsMapDragStart.X;
                double dy = current.Y - _fsMapDragStart.Y;

                FsMapPanTransform.X = 0;
                FsMapPanTransform.Y = 0;
                FsMapPinsPanTransform.X = 0;
                FsMapPinsPanTransform.Y = 0;

                var (startWx, startWy) = OsmTileMapService.LatLonToWorldPixel(_fsDragStartLat, _fsDragStartLon, _fsZoom);
                double newWx = startWx - dx;
                double newWy = startWy - dy;

                var (newLat, newLon) = OsmTileMapService.WorldPixelToLatLon(newWx, newWy, _fsZoom);
                _fsCenterLat = Math.Clamp(newLat, -80.0, 84.0);
                _fsCenterLon = ((newLon + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;

                RenderMap(isFullscreen: true);
            }
        }

        private void FsMapViewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            int delta = e.Delta > 0 ? 1 : -1;
            int newZoom = Math.Clamp(_fsZoom + delta, 3, 14);
            if (newZoom != _fsZoom)
            {
                _fsZoom = newZoom;
                RenderMap(isFullscreen: true);
            }
            e.Handled = true;
        }

        private void BtnFsZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (_fsZoom < 14)
            {
                _fsZoom++;
                RenderMap(isFullscreen: true);
            }
        }

        private void BtnFsZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (_fsZoom > 3)
            {
                _fsZoom--;
                RenderMap(isFullscreen: true);
            }
        }

        private void BtnFsReset_Click(object sender, RoutedEventArgs e)
        {
            _fsCenterLat = DefaultCenterLat;
            _fsCenterLon = DefaultCenterLon;
            _fsZoom = DefaultZoom;
            RenderMap(isFullscreen: true);
        }

        private void BtnFsFocusWorld_Click(object sender, RoutedEventArgs e)
        {
            _fsCenterLat = DefaultCenterLat;
            _fsCenterLon = DefaultCenterLon;
            _fsZoom = 3;
            RenderMap(isFullscreen: true);
        }

        private void BtnFsFocusAll_Click(object sender, RoutedEventArgs e)
        {
            _fsCenterLat = 23.8;
            _fsCenterLon = 90.3;
            _fsZoom = 7;
            RenderMap(isFullscreen: true);
        }

        private void BtnFsFocusSylhet_Click(object sender, RoutedEventArgs e)
        {
            _fsCenterLat = 24.35;
            _fsCenterLon = 91.75;
            _fsZoom = 10;
            RenderMap(isFullscreen: true);
        }

        private void BtnFsFocusChittagong_Click(object sender, RoutedEventArgs e)
        {
            _fsCenterLat = 22.45;
            _fsCenterLon = 91.85;
            _fsZoom = 10;
            RenderMap(isFullscreen: true);
        }

        private void BtnFsFocusNorth_Click(object sender, RoutedEventArgs e)
        {
            _fsCenterLat = 26.30;
            _fsCenterLon = 88.60;
            _fsZoom = 10;
            RenderMap(isFullscreen: true);
        }

        #endregion

        #endregion

        #region Recent Sales Table & Filtering

        private void InitializeRecentSalesFilters()
        {
            // Brokers dropdown
            var brokerItems = new List<string> { "All Brokers" };
            brokerItems.AddRange(_allBrokers.Select(b => b.Name).Distinct());
            CmbBrokerFilter.ItemsSource = brokerItems;
            CmbBrokerFilter.SelectedIndex = 0;

            // Gardens dropdown
            var gardenItems = new List<string> { "All Gardens" };
            gardenItems.AddRange(_allGardens.Select(g => g.Name).Distinct());
            CmbGardenFilter.ItemsSource = gardenItems;
            CmbGardenFilter.SelectedIndex = 0;

            // Date Range
            DtpFromDate.SelectedDate = SettingsManager.Current.SessionStartDate;
            DtpToDate.SelectedDate = SettingsManager.Current.SessionEndDate;
        }

        private void BuildRecentSalesItems()
        {
            var salesGrouped = _allSeasonEntries
                .GroupBy(e => new { e.SaleNo, e.SaleDate, e.SaleId })
                .Select(g =>
                {
                    var entries = g.ToList();
                    decimal totalKgs = entries.Sum(e => e.TotalKgs);
                    decimal totalVal = entries.Sum(e => e.Value);

                    var brokerSummary = entries
                        .GroupBy(e => e.BrokerName)
                        .Select(bg => new BrokerVolumeItem
                        {
                            BrokerName = bg.Key,
                            TotalKgs = bg.Sum(e => e.TotalKgs),
                            Percentage = totalKgs > 0 ? (double)(bg.Sum(e => e.TotalKgs) / totalKgs * 100) : 0
                        })
                        .OrderByDescending(b => b.TotalKgs)
                        .ToList();

                    var brandSummary = entries
                        .GroupBy(e => e.BrandName)
                        .Select(bg => new BrandVolumeItem
                        {
                            BrandName = bg.Key,
                            TotalKgs = bg.Sum(e => e.TotalKgs),
                            Percentage = totalKgs > 0 ? (double)(bg.Sum(e => e.TotalKgs) / totalKgs * 100) : 0
                        })
                        .OrderByDescending(b => b.TotalKgs)
                        .ToList();

                    return new RecentSaleRowItem
                    {
                        SaleId = g.Key.SaleId,
                        SaleNo = g.Key.SaleNo,
                        SaleDate = g.Key.SaleDate,
                        TotalLots = entries.Count,
                        TotalBags = entries.Sum(e => e.NumberOfBags),
                        TotalKgs = totalKgs,
                        TotalValueBdt = totalVal,
                        Entries = entries,
                        BrokerSummary = brokerSummary,
                        BrandSummary = brandSummary
                    };
                })
                .OrderByDescending(s => int.TryParse(s.SaleNo, out int num) ? num : 0)
                .ThenByDescending(s => s.SaleDate)
                .ToList();

            _allRecentSales = salesGrouped;
        }

        private void ApplyRecentSalesFilter()
        {
            if (_allRecentSales == null) return;

            string search = TxtSearchRecent.Text.Trim();
            string saleNoFilter = TxtSaleNoFilter.Text.Trim();
            string selectedBroker = CmbBrokerFilter.SelectedItem?.ToString() ?? "All Brokers";
            string selectedGarden = CmbGardenFilter.SelectedItem?.ToString() ?? "All Gardens";
            DateTime? fromDate = DtpFromDate.SelectedDate;
            DateTime? toDate = DtpToDate.SelectedDate;

            var filtered = _allRecentSales.Where(s =>
            {
                // Quick Search
                if (!string.IsNullOrEmpty(search))
                {
                    bool match = s.SaleNo.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                 s.Entries.Any(e => e.BrokerName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                                    e.GardenName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                                    e.InvoiceNo.Contains(search, StringComparison.OrdinalIgnoreCase));
                    if (!match) return false;
                }

                // Sale No filter
                if (!string.IsNullOrEmpty(saleNoFilter))
                {
                    if (!s.SaleNo.Contains(saleNoFilter, StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                // Broker filter
                if (selectedBroker != "All Brokers")
                {
                    if (!s.Entries.Any(e => e.BrokerName.Equals(selectedBroker, StringComparison.OrdinalIgnoreCase)))
                        return false;
                }

                // Garden filter
                if (selectedGarden != "All Gardens")
                {
                    if (!s.Entries.Any(e => e.GardenName.Equals(selectedGarden, StringComparison.OrdinalIgnoreCase)))
                        return false;
                }

                // Date filter
                if (fromDate.HasValue && s.SaleDate < fromDate.Value.Date) return false;
                if (toDate.HasValue && s.SaleDate > toDate.Value.Date.AddDays(1).AddSeconds(-1)) return false;

                return true;
            }).ToList();

            GridRecentSales.ItemsSource = filtered;
            TxtRecentSalesCount.Text = $"{filtered.Count} Sales";
        }

        private void FilterChanged(object sender, EventArgs e)
        {
            ApplyRecentSalesFilter();
        }

        private void BtnResetFilters_Click(object sender, RoutedEventArgs e)
        {
            TxtSearchRecent.Text = "";
            TxtSaleNoFilter.Text = "";
            CmbBrokerFilter.SelectedIndex = 0;
            CmbGardenFilter.SelectedIndex = 0;
            DtpFromDate.SelectedDate = SettingsManager.Current.SessionStartDate;
            DtpToDate.SelectedDate = SettingsManager.Current.SessionEndDate;
            ApplyRecentSalesFilter();
        }

        private void GridRecentSales_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridRecentSales.SelectedItem is RecentSaleRowItem sale)
            {
                OpenDetailDrawer(sale);
            }
        }

        private void GridRecentSales_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (GridRecentSales.SelectedItem is RecentSaleRowItem sale)
            {
                OpenDetailDrawer(sale);
            }
        }

        private void BtnInspectSale_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is RecentSaleRowItem sale)
            {
                OpenDetailDrawer(sale);
            }
        }

        #endregion

        #region Detail Drawer: Sale Inspection

        private void OpenDetailDrawer(RecentSaleRowItem sale)
        {
            _inspectedSale = sale;

            TxtDrawerSaleNo.Text = sale.SaleNo;
            TxtDrawerSaleDate.Text = UiPreferences.FormatDate(sale.SaleDate);
            TxtDrawerLots.Text = sale.TotalLots.ToString("N0");
            TxtDrawerBags.Text = sale.TotalBags.ToString("N0");
            TxtDrawerWeight.Text = $"{sale.TotalKgs:N2} Kg";
            TxtDrawerValue.Text = $"৳{sale.TotalValueBdt:N2}";
            TxtDrawerAvgPrice.Text = $"৳{sale.AvgPricePerKg:N2}";

            ListDrawerBrokers.ItemsSource = sale.BrokerSummary;
            ListDrawerBrands.ItemsSource = sale.BrandSummary;
            GridDrawerEntries.ItemsSource = sale.Entries;

            SaleDetailDrawerOverlay.Visibility = Visibility.Visible;
        }

        private void BtnCloseDrawer_Click(object sender, RoutedEventArgs e)
        {
            SaleDetailDrawerOverlay.Visibility = Visibility.Collapsed;
        }

        private void DrawerBackdrop_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == SaleDetailDrawerOverlay)
            {
                SaleDetailDrawerOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnDrawerEditSale_Click(object sender, RoutedEventArgs e)
        {
            if (_inspectedSale == null) return;

            try
            {
                var saleRepo = _services.GetRequiredService<SaleRepository>();
                var saleToEdit = await saleRepo.GetWithEntriesAsync(_inspectedSale.SaleId);

                if (saleToEdit != null)
                {
                    NavigationService?.Navigate(new NewSalePage(_services, saleToEdit));
                }
                else
                {
                    MessageBox.Show("Sale record could not be loaded for editing.", "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening sale for editing: {ex.Message}", "Error");
            }
        }

        #endregion

        #region Header Navigation & Refresh

        private async void BtnRefreshDashboard_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        #endregion
    }
}
