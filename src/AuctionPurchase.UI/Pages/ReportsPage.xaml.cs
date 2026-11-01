using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AuctionPurchase.Core.Reports;
using AuctionPurchase.Infrastructure;
using AuctionPurchase.Infrastructure.Data;
using AuctionPurchase.Infrastructure.Export;
using Microsoft.Extensions.DependencyInjection;

namespace AuctionPurchase.UI.Pages
{
    public partial class ReportsPage : Page
    {
        private readonly IServiceProvider _services;
        private List<ReportEntry> _currentEntries = new();
        private bool _loaded = false;

        public ReportsPage(IServiceProvider services)
        {
            _services = services;
            InitializeComponent();
            _loaded = true;
            Loaded += async (_, _) =>
            {
                var s = SettingsManager.Current;
                CmbSessionFilter.ItemsSource = s.AuctionSessions;
                if (s.AuctionSessions.Any())
                {
                    var active = s.AuctionSessions.FirstOrDefault(x => x.Id == s.ActiveSessionId);
                    CmbSessionFilter.SelectedItem = active ?? s.AuctionSessions.First();
                }
                
                try { await LoadTemplatesAsync(); }
                catch (Exception ex) { MessageBox.Show($"Failed to load report templates: {ex.Message}", "Error"); }
            };
        }

        private string ReportType => CmbReportType?.SelectedIndex switch 
        { 
            1 => "BrandWise", 
            2 => "BrokerWise", 
            3 => "BrokerWiseSummary",
            4 => "BrandWiseSummary",
            _ => "AllEntries" 
        };

        private string ReportTitle => CmbReportType?.SelectedIndex switch 
        { 
            1 => "Brand Wise Purchase Report", 
            2 => "Broker Wise Purchase Report", 
            3 => "Broker Wise Summary Report",
            4 => "Brand Wise Summary Report",
            _ => "Tea Purchase Report" 
        };

        private ReportFilter BuildFilter() 
        {
            var session = CmbSessionFilter.SelectedItem as AuctionSession;
            return new ReportFilter
            {
                FromDate = session?.DateStart,
                ToDate = session?.DateFinish,
                SaleNo = string.IsNullOrWhiteSpace(TxtSaleNo?.Text) ? null : TxtSaleNo.Text.Trim()
            };
        }

        private async Task LoadTemplatesAsync()
        {
            if (!_loaded || CmbTemplate == null) return;
            var repo = _services.GetRequiredService<ReportTemplateRepository>();
            CmbTemplate.ItemsSource = await repo.GetByTypeAsync(ReportType);
            CmbTemplate.DisplayMemberPath = "TemplateName";
            CmbTemplate.SelectedValuePath = "TemplateId";
            if (CmbTemplate.Items.Count > 0) CmbTemplate.SelectedIndex = 0;
        }

        private async void ReportTypeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loaded) return;
            ConfigureGridColumns();
            try { await LoadTemplatesAsync(); }
            catch (Exception ex) { MessageBox.Show($"Failed to load templates: {ex.Message}", "Error"); }
        }

        private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BtnGenerate.IsEnabled = false;
                var reportRepo = _services.GetRequiredService<ReportRepository>();
                var rawEntries = (await reportRepo.GetReportEntriesAsync(BuildFilter())).ToList();

                if (ReportType == "BrokerWiseSummary")
                {
                    _currentEntries = rawEntries
                        .GroupBy(x => string.IsNullOrWhiteSpace(x.BrokerName) ? "(Unassigned)" : x.BrokerName.Trim())
                        .OrderBy(g => g.Key)
                        .Select((g, idx) =>
                        {
                            var lotNos = string.Join(", ", g.Select(x => x.LotNo?.Trim()).Where(s => !string.IsNullOrEmpty(s)).Distinct());
                            var invoiceNos = string.Join(", ", g.Select(x => x.InvoiceNo?.Trim()).Where(s => !string.IsNullOrEmpty(s)).Distinct());
                            var bags = g.Sum(x => x.NumberOfBags);
                            var kgs = g.Sum(x => x.TotalKgs);
                            var val = g.Sum(x => x.Value);
                            var avgPrice = kgs > 0 ? val / kgs : 0;
                            var saleNo = string.Join(", ", g.Select(x => x.SaleNo?.Trim()).Where(s => !string.IsNullOrEmpty(s)).Distinct());
                            var saleDate = g.FirstOrDefault(x => x.SaleDate != default)?.SaleDate ?? default;

                            return new ReportEntry
                            {
                                EntryId = idx + 1,
                                BrokerName = g.Key,
                                LotNo = lotNos,
                                InvoiceNo = invoiceNos,
                                NumberOfBags = bags,
                                TotalKgs = kgs,
                                PricePerKg = avgPrice,
                                Value = val,
                                SaleNo = saleNo,
                                SaleDate = saleDate
                            };
                        }).ToList();
                }
                else if (ReportType == "BrandWiseSummary")
                {
                    _currentEntries = rawEntries
                        .GroupBy(x => string.IsNullOrWhiteSpace(x.BrandName) ? "(Unbranded)" : x.BrandName.Trim())
                        .OrderBy(g => g.Key)
                        .Select((g, idx) =>
                        {
                            var brokers = string.Join(", ", g.Select(x => x.BrokerName?.Trim()).Where(s => !string.IsNullOrEmpty(s)).Distinct());
                            var bags = g.Sum(x => x.NumberOfBags);
                            var kgs = g.Sum(x => x.TotalKgs);
                            var val = g.Sum(x => x.Value);
                            var avgPrice = kgs > 0 ? val / kgs : 0;
                            var saleNo = string.Join(", ", g.Select(x => x.SaleNo?.Trim()).Where(s => !string.IsNullOrEmpty(s)).Distinct());
                            var saleDate = g.FirstOrDefault(x => x.SaleDate != default)?.SaleDate ?? default;

                            return new ReportEntry
                            {
                                EntryId = idx + 1,
                                BrandName = g.Key,
                                BrokerName = brokers,
                                NumberOfBags = bags,
                                TotalKgs = kgs,
                                PricePerKg = avgPrice,
                                Value = val,
                                SaleNo = saleNo,
                                SaleDate = saleDate
                            };
                        }).ToList();
                }
                else
                {
                    _currentEntries = rawEntries;
                }

                ConfigureGridColumns();
                GridReport.ItemsSource = _currentEntries;
                UpdateSummaryCards(_currentEntries);
                var enabled = _currentEntries.Count > 0 && CmbTemplate.SelectedItem is ReportTemplate;
                BtnExportPdf.IsEnabled = enabled; BtnExportXlsx.IsEnabled = enabled; BtnExportDocx.IsEnabled = enabled;
            }
            catch (Exception ex) { MessageBox.Show($"Error generating report: {ex.Message}", "Error"); }
            finally { BtnGenerate.IsEnabled = true; }
        }

        private void ConfigureGridColumns()
        {
            if (GridReport == null) return;
            GridReport.Columns.Clear();
            if (ReportType == "BrokerWiseSummary")
            {
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Brokers Name", Binding = new System.Windows.Data.Binding("BrokerName"), Width = 140 });
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Lot No", Binding = new System.Windows.Data.Binding("LotNo"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Invoice No", Binding = new System.Windows.Data.Binding("InvoiceNo"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Total Bags", Binding = new System.Windows.Data.Binding("NumberOfBags") { StringFormat = "N0" }, Width = 90 });
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Total KGS", Binding = new System.Windows.Data.Binding("TotalKgs") { StringFormat = "N2" }, Width = 100 });
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Avg Price/KG", Binding = new System.Windows.Data.Binding("PricePerKg") { StringFormat = "N2" }, Width = 100 });
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Total Value", Binding = new System.Windows.Data.Binding("Value") { StringFormat = "N2" }, Width = 120 });
            }
            else if (ReportType == "BrandWiseSummary")
            {
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Brand", Binding = new System.Windows.Data.Binding("BrandName"), Width = 140 });
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Brokers", Binding = new System.Windows.Data.Binding("BrokerName"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Total Bags", Binding = new System.Windows.Data.Binding("NumberOfBags") { StringFormat = "N0" }, Width = 90 });
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Total KGS", Binding = new System.Windows.Data.Binding("TotalKgs") { StringFormat = "N2" }, Width = 100 });
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Avg Price/KG", Binding = new System.Windows.Data.Binding("PricePerKg") { StringFormat = "N2" }, Width = 100 });
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Total Value", Binding = new System.Windows.Data.Binding("Value") { StringFormat = "N2" }, Width = 120 });
            }
            else
            {
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Broker", Binding = new System.Windows.Data.Binding("BrokerName"), Width = DataGridLength.Auto });
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Brand", Binding = new System.Windows.Data.Binding("BrandName"), Width = DataGridLength.Auto });
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Sale No", Binding = new System.Windows.Data.Binding("SaleNo"), Width = DataGridLength.Auto });
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Date", Binding = AuctionPurchase.UI.Services.UiPreferences.DateBinding("SaleDate"), Width = DataGridLength.Auto });
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Lot No", Binding = new System.Windows.Data.Binding("LotNo"), Width = DataGridLength.Auto });
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Garden", Binding = new System.Windows.Data.Binding("GardenDisplay"), Width = DataGridLength.Auto, MinWidth = 120 });
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Invoice", Binding = new System.Windows.Data.Binding("InvoiceNo"), Width = DataGridLength.Auto });
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Grade", Binding = new System.Windows.Data.Binding("GradeName"), Width = DataGridLength.Auto });
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Bags", Binding = new System.Windows.Data.Binding("NumberOfBags"), Width = DataGridLength.Auto });
                GridReport.Columns.Add(new DataGridTextColumn { Header = "KGs", Binding = new System.Windows.Data.Binding("TotalKgs") { StringFormat = "N2" }, Width = DataGridLength.Auto });
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Price/KG", Binding = new System.Windows.Data.Binding("PricePerKg") { StringFormat = "N2" }, Width = DataGridLength.Auto });
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Value", Binding = new System.Windows.Data.Binding("Value") { StringFormat = "N2" }, Width = DataGridLength.Auto });
                GridReport.Columns.Add(new DataGridTextColumn { Header = "Blend", Binding = new System.Windows.Data.Binding("BlendName"), Width = DataGridLength.Auto });
            }
        }

        private void UpdateSummaryCards(List<ReportEntry>? entries)
        {
            if (entries == null || entries.Count == 0)
            {
                TxtSummaryEntries.Text = "0";
                TxtSummaryBags.Text = "0";
                TxtSummaryKgs.Text = "0.00";
                TxtSummaryAvgPrice.Text = "৳0.00";
                TxtSummaryValue.Text = "৳0.00";
                return;
            }

            var totalBags = entries.Sum(e => e.NumberOfBags);
            var totalKgs = entries.Sum(e => e.TotalKgs);
            var totalValue = entries.Sum(e => e.Value);
            var avgPrice = totalKgs > 0 ? totalValue / totalKgs : 0;

            TxtSummaryEntries.Text = entries.Count.ToString("N0");
            TxtSummaryBags.Text = totalBags.ToString("N0");
            TxtSummaryKgs.Text = totalKgs.ToString("N2");
            TxtSummaryAvgPrice.Text = "৳" + avgPrice.ToString("N2");
            TxtSummaryValue.Text = "৳" + totalValue.ToString("N2");
        }

        private (ReportTemplateLayout Layout, ReportRenderContext Context) GetRenderInfo()
        {
            if (CmbTemplate.SelectedItem is not ReportTemplate template) throw new InvalidOperationException("Choose a report template first.");
            var templateRepo = _services.GetRequiredService<ReportTemplateRepository>();
            var settings = SettingsManager.Current;
            var session = CmbSessionFilter.SelectedItem as AuctionSession;

            DateTime? saleDate = _currentEntries.Any(e => e.SaleDate != default)
                ? _currentEntries.First(e => e.SaleDate != default).SaleDate
                : null;
            var saleNo = !string.IsNullOrWhiteSpace(TxtSaleNo?.Text)
                ? TxtSaleNo.Text.Trim()
                : _currentEntries.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.SaleNo))?.SaleNo ?? "";

            var layout = templateRepo.GetLayout(template);
            foreach (var column in layout.Columns.Where(c => c.Field == "SaleDate"))
            {
                column.DateFormat = AuctionPurchase.UI.Services.UiPreferences.Current.DateFormat;
                column.Format = AuctionPurchase.UI.Services.UiPreferences.Current.DateFormat;
            }
            return (layout, new ReportRenderContext
            {
                ReportTitle = ReportTitle,
                DateFormat = AuctionPurchase.UI.Services.UiPreferences.Current.DateFormat,
                ReportType = ReportType,
                SaleNo = saleNo,
                SaleDate = saleDate,
                FromDate = session?.DateStart,
                ToDate = session?.DateFinish,
                CompanyName = settings.CompanyName,
                CompanyAddress = settings.CompanyAddress,
                CompanyPhone = settings.CompanyPhone,
                CompanyEmail = settings.CompanyEmail,
                CompanyWebsite = settings.CompanyWebsite,
                CompanyLogoBase64 = settings.LogoBase64,
                PreparedBy = settings.PreparedBy,
                CheckedBy = settings.CheckedBy,
                ApprovedBy = settings.ApprovedBy
            });
        }

        private string GetDefaultFileName(string extension)
        {
            var title = (CmbTemplate?.SelectedItem as ReportTemplate)?.TemplateName ?? ReportTitle;
            if (string.IsNullOrWhiteSpace(title)) title = "Report";

            var timestamp = DateTime.Now.ToString("dd-MM-yyyy-HH-mm-ss");
            var fileName = $"{title} {timestamp}.{extension}";

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '-');
            }
            return fileName;
        }

        private async void BtnExportPdf_Click(object sender, RoutedEventArgs e) => await ExportAsync("PDF Files|*.pdf", GetDefaultFileName("pdf"), path =>
        {
            var info = GetRenderInfo(); return Task.Run(() => _services.GetRequiredService<PdfExporter>().ExportTemplate(info.Layout, info.Context, _currentEntries));
        });
        private async void BtnExportXlsx_Click(object sender, RoutedEventArgs e) => await ExportAsync("Excel Files|*.xlsx", GetDefaultFileName("xlsx"), path =>
        {
            var info = GetRenderInfo(); return Task.Run(() => _services.GetRequiredService<ExcelExporter>().ExportTemplate(info.Layout, info.Context, _currentEntries));
        });
        private async void BtnExportDocx_Click(object sender, RoutedEventArgs e) => await ExportAsync("Word Files|*.docx", GetDefaultFileName("docx"), path =>
        {
            var info = GetRenderInfo(); return Task.Run(() => _services.GetRequiredService<WordExporter>().ExportTemplate(info.Layout, info.Context, _currentEntries));
        });

        private async Task ExportAsync(string filter, string defaultName, Func<string, Task<byte[]>> create)
        {
            var exportDir = SettingsManager.Current.ExportLocation;
            if (string.IsNullOrWhiteSpace(exportDir))
            {
                exportDir = SettingsManager.DefaultExportLocation;
            }
            if (!Directory.Exists(exportDir)) Directory.CreateDirectory(exportDir);

            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = filter, FileName = defaultName, InitialDirectory = exportDir };
            if (dialog.ShowDialog() != true) return;
            try { var bytes = await create(dialog.FileName); await File.WriteAllBytesAsync(dialog.FileName, bytes); OpenFile(dialog.FileName); }
            catch (Exception ex) { MessageBox.Show($"Report export failed: {ex.Message}", "Error"); }
        }

        private void BtnDesignTemplates_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mainWindow)
                mainWindow.NavigateTo(new ReportTemplatesPage(_services));
        }
        private static void OpenFile(string path) { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); } catch { } }
    }
}
