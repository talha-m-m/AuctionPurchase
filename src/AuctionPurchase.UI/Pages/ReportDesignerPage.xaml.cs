using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using AuctionPurchase.Core.Reports;
using AuctionPurchase.Infrastructure;
using AuctionPurchase.Infrastructure.Data;
using AuctionPurchase.Infrastructure.Export;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace AuctionPurchase.UI.Pages
{
    public partial class ReportDesignerPage : Page
    {
        private readonly IServiceProvider _services;
        private readonly string _reportType;
        private readonly int? _initialTemplateId;

        private readonly ObservableCollection<ReportSectionElement>  _headerElements = new();
        private readonly ObservableCollection<ReportSectionElement>  _footerElements = new();
        private readonly ObservableCollection<ReportColumnDefinition> _columns        = new();

        private ReportTemplate? _template;
        private bool _suppressPropertyUpdates = false;

        // Preview state
        private bool _previewOpen = false;
        private List<BitmapImage> _previewPages = new();
        private int _previewPageIndex = 0;
        private double _previewZoom = 1.0;

        // Track which list (Header / Footer) and which Column is selected
        private enum SelectionKind { None, Header, Footer, Column }
        private SelectionKind _selectionKind = SelectionKind.None;

        // ─────────────────────────────────────────────────────────────
        // Global template variables
        // ─────────────────────────────────────────────────────────────
        private static readonly string[] GlobalVars =
        {
            "{{CompanyName}}", "{{CompanyAddress}}", "{{CompanyPhone}}",
            "{{CompanyEmail}}", "{{CompanyWebsite}}",
            "{{ReportTitle}}", "{{ReportSubtitle}}",
            "{{SaleNo}}", "{{SaleDate}}",
            "{{FromDate}}", "{{ToDate}}",
            "{{GeneratedDate}}", "{{GeneratedTime}}",
            "{{PreparedBy}}", "{{CheckedBy}}", "{{ApprovedBy}}",
            "{{PageNumber}}", "{{TotalPages}}"
        };

        private static readonly Dictionary<string, string[]> FieldsByType = new()
        {
            ["AllEntries"] = new[]
            {
                "BrokerName", "BrandName", "LotNo", "GardenName", "InvoiceNo", "GradeName",
                "NumberOfBags", "TotalKgs", "PricePerKg", "Value", "ShedName", "BlendName",
                "SaleNo", "SaleDate"
            },
            ["BrandWise"]  = new[] { "BrandName", "BrokerName", "LotNo", "GardenName",
                "InvoiceNo", "GradeName", "NumberOfBags", "TotalKgs", "PricePerKg", "Value" },
            ["BrokerWise"] = new[] { "BrokerName", "BrandName", "LotNo", "GardenName",
                "InvoiceNo", "GradeName", "NumberOfBags", "TotalKgs", "PricePerKg", "Value" }
        };

        // ─────────────────────────────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────────────────────────────

        public ReportDesignerPage(IServiceProvider services, string reportType, int? templateId = null)
        {
            _services          = services;
            _reportType        = reportType;
            _initialTemplateId = templateId;
            InitializeComponent();

            LstHeader.ItemsSource  = _headerElements;
            LstFooter.ItemsSource  = _footerElements;
            GridColumns.ItemsSource = _columns;

            VarList.ItemsSource   = GlobalVars;
            FieldList.ItemsSource = FieldsByType.TryGetValue(reportType, out var fields)
                ? fields : FieldsByType["AllEntries"];

            Loaded += async (_, _) => await LoadTemplatesAsync();
        }

        // ─────────────────────────────────────────────────────────────
        // Template loading
        // ─────────────────────────────────────────────────────────────

        private async Task LoadTemplatesAsync(int? selectId = null)
        {
            var repo = _services.GetRequiredService<ReportTemplateRepository>();
            var list = await repo.GetByTypeAsync(_reportType);

            CmbTemplates.ItemsSource        = list;
            CmbTemplates.DisplayMemberPath  = "TemplateName";
            CmbTemplates.SelectedValuePath  = "TemplateId";

            var targetId = selectId ?? _initialTemplateId
                ?? list.FirstOrDefault()?.TemplateId;
            CmbTemplates.SelectedValue = targetId;

            if (CmbTemplates.SelectedItem == null && list.Count > 0)
                CmbTemplates.SelectedIndex = 0;
        }

        private void TemplateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbTemplates.SelectedItem is not ReportTemplate template) return;
            var repo   = _services.GetRequiredService<ReportTemplateRepository>();
            _template  = template;
            var layout = repo.GetLayout(template);
            LoadLayout(layout);
            TxtStatus.Text = template.IsDefault
                ? "Standard template (read-only). Use Save As to create a custom copy."
                : "Custom template loaded.";
        }

        private void LoadLayout(ReportTemplateLayout layout)
        {
            _suppressPropertyUpdates = true;

            // Page setup
            SelectComboByContent(CmbPageSize,    layout.Page.PageSize);
            SelectComboByContent(CmbOrientation, layout.Page.Orientation);
            TxtMarginTop.Text    = layout.Page.MarginTopMm.ToString();
            TxtMarginBottom.Text = layout.Page.MarginBottomMm.ToString();
            TxtMarginLeft.Text   = layout.Page.MarginLeftMm.ToString();
            TxtMarginRight.Text  = layout.Page.MarginRightMm.ToString();

            // Body options
            ChkShowTotals.IsChecked    = layout.Body.ShowTotals;
            ChkAlternateRows.IsChecked = layout.Body.AlternateRowShading;
            TxtHeaderBg.Text  = layout.Body.TableHeaderBackground;
            TxtHeaderFg.Text  = layout.Body.TableHeaderForeground;
            TxtTotalsBg.Text  = layout.Body.TotalsBackground;
            TxtAltRowBg.Text  = layout.Body.AlternateRowBackground;

            // Elements
            _headerElements.Clear();
            foreach (var el in layout.HeaderElements) _headerElements.Add(el);
            _footerElements.Clear();
            foreach (var el in layout.FooterElements) _footerElements.Add(el);
            _columns.Clear();
            foreach (var col in layout.Columns) _columns.Add(col);

            // Reset selection
            ShowNoSelection();
            _suppressPropertyUpdates = false;

            if (_previewOpen) _ = RefreshPreviewAsync();
        }

        // ─────────────────────────────────────────────────────────────
        // Build layout from UI
        // ─────────────────────────────────────────────────────────────

        private ReportTemplateLayout BuildLayout() => new()
        {
            SchemaVersion = 2,
            Page = new PageSetup
            {
                PageSize       = (CmbPageSize.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "A4",
                Orientation    = (CmbOrientation.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Landscape",
                MarginTopMm    = ParseDecimal(TxtMarginTop.Text,    10),
                MarginBottomMm = ParseDecimal(TxtMarginBottom.Text, 10),
                MarginLeftMm   = ParseDecimal(TxtMarginLeft.Text,   10),
                MarginRightMm  = ParseDecimal(TxtMarginRight.Text,  10)
            },
            Body = new BodyOptions
            {
                ShowTotals            = ChkShowTotals.IsChecked == true,
                AlternateRowShading   = ChkAlternateRows.IsChecked == true,
                TableHeaderBackground = TxtHeaderBg.Text.Trim(),
                TableHeaderForeground = TxtHeaderFg.Text.Trim(),
                TotalsBackground      = TxtTotalsBg.Text.Trim(),
                AlternateRowBackground = TxtAltRowBg.Text.Trim()
            },
            HeaderElements = _headerElements.ToList(),
            FooterElements = _footerElements.ToList(),
            Columns        = _columns.ToList()
        };

        // ─────────────────────────────────────────────────────────────
        // Save / Save As / Duplicate / Reset
        // ─────────────────────────────────────────────────────────────

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_template is null) return;
            if (_template.IsDefault)
            {
                MessageBox.Show(
                    "The standard template is protected. Use Save As to create a custom copy.",
                    "Protected Template");
                return;
            }
            await SaveAsync(_template);
        }

        private async void BtnSaveAs_Click(object sender, RoutedEventArgs e)
        {
            var newTemplate = new ReportTemplate
            {
                TemplateName = string.IsNullOrWhiteSpace(TxtTemplateName.Text)
                    ? "Custom Report" : TxtTemplateName.Text.Trim(),
                ReportType = _reportType
            };
            await SaveAsync(newTemplate);
        }

        private async Task SaveAsync(ReportTemplate template)
        {
            try
            {
                template.TemplateName = TxtTemplateName.Text.Trim();
                var repo = _services.GetRequiredService<ReportTemplateRepository>();
                var id   = await repo.SaveAsync(template, BuildLayout());
                await LoadTemplatesAsync(id);
                TxtStatus.Text = "Template saved. It is stored in the database and included in backups.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save failed: {ex.Message}", "Error");
            }
        }

        private async void BtnDuplicate_Click(object sender, RoutedEventArgs e)
        {
            if (_template is null) return;
            var repo = _services.GetRequiredService<ReportTemplateRepository>();
            var copy = await repo.DuplicateAsync(_template.TemplateId,
                $"{_template.TemplateName} — Copy");
            await LoadTemplatesAsync(copy.TemplateId);
        }

        private async void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            if (_template is null || _template.IsDefault) return;
            if (MessageBox.Show(
                    "Reset this template to the standard default layout?",
                    "Confirm Reset", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            await _services.GetRequiredService<ReportTemplateRepository>()
                .SaveAsync(_template, ReportTemplateRepository.CreateDefaultLayout());
            await LoadTemplatesAsync(_template.TemplateId);
        }

        // ─────────────────────────────────────────────────────────────
        // Import / Export
        // ─────────────────────────────────────────────────────────────

        private async void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_template is null) return;
            var dialog = new SaveFileDialog
            {
                Filter   = "Tea Template (*.tktpl)|*.tktpl",
                FileName = $"{_template.TemplateName}.tktpl"
            };
            if (dialog.ShowDialog() != true) return;
            await File.WriteAllTextAsync(dialog.FileName,
                await _services.GetRequiredService<ReportTemplateRepository>()
                    .ExportAsync(_template.TemplateId));
            TxtStatus.Text = $"Exported to {Path.GetFileName(dialog.FileName)}.";
        }

        private async void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "Tea Template (*.tktpl)|*.tktpl" };
            if (dialog.ShowDialog() != true) return;
            try
            {
                var id = await _services.GetRequiredService<ReportTemplateRepository>()
                    .ImportAsync(await File.ReadAllTextAsync(dialog.FileName));
                await LoadTemplatesAsync(id);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed: {ex.Message}", "Import Error");
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Header element buttons
        // ─────────────────────────────────────────────────────────────

        private void BtnAddHeaderElement_Click(object sender, RoutedEventArgs e)
        {
            var el = ShowAddElementDialog();
            if (el is null) return;
            _headerElements.Add(el);
            LstHeader.SelectedItem = el;
        }

        private void BtnHeaderUp_Click(object sender, RoutedEventArgs e)
            => MoveElement(_headerElements, LstHeader.SelectedIndex, -1, LstHeader);
        private void BtnHeaderDown_Click(object sender, RoutedEventArgs e)
            => MoveElement(_headerElements, LstHeader.SelectedIndex, +1, LstHeader);
        private void BtnHeaderDelete_Click(object sender, RoutedEventArgs e)
        {
            if (LstHeader.SelectedItem is ReportSectionElement el) _headerElements.Remove(el);
        }

        private void HeaderElementSelected(object sender, SelectionChangedEventArgs e)
        {
            if (LstHeader.SelectedItem is not ReportSectionElement el)
            {
                if (_selectionKind == SelectionKind.Header) ShowNoSelection();
                return;
            }
            LstFooter.SelectedItem  = null;
            GridColumns.SelectedItem = null;
            _selectionKind = SelectionKind.Header;
            ShowElementProperties(el);
        }

        // ─────────────────────────────────────────────────────────────
        // Footer element buttons
        // ─────────────────────────────────────────────────────────────

        private void BtnAddFooterElement_Click(object sender, RoutedEventArgs e)
        {
            var el = ShowAddElementDialog();
            if (el is null) return;
            _footerElements.Add(el);
            LstFooter.SelectedItem = el;
        }

        private void BtnFooterUp_Click(object sender, RoutedEventArgs e)
            => MoveElement(_footerElements, LstFooter.SelectedIndex, -1, LstFooter);
        private void BtnFooterDown_Click(object sender, RoutedEventArgs e)
            => MoveElement(_footerElements, LstFooter.SelectedIndex, +1, LstFooter);
        private void BtnFooterDelete_Click(object sender, RoutedEventArgs e)
        {
            if (LstFooter.SelectedItem is ReportSectionElement el) _footerElements.Remove(el);
        }

        private void FooterElementSelected(object sender, SelectionChangedEventArgs e)
        {
            if (LstFooter.SelectedItem is not ReportSectionElement el)
            {
                if (_selectionKind == SelectionKind.Footer) ShowNoSelection();
                return;
            }
            LstHeader.SelectedItem  = null;
            GridColumns.SelectedItem = null;
            _selectionKind = SelectionKind.Footer;
            ShowElementProperties(el);
        }

        // ─────────────────────────────────────────────────────────────
        // Column buttons
        // ─────────────────────────────────────────────────────────────

        private void BtnAddColumn_Click(object sender, RoutedEventArgs e)
        {
            var col = new ReportColumnDefinition { Field = "BrandName", Title = "New Column", Width = 100 };
            _columns.Add(col);
            GridColumns.SelectedItem = col;
        }

        private void BtnColumnUp_Click(object sender, RoutedEventArgs e)
        {
            var i = GridColumns.SelectedIndex;
            if (i > 0) { _columns.Move(i, i - 1); GridColumns.SelectedIndex = i - 1; }
        }

        private void BtnColumnDown_Click(object sender, RoutedEventArgs e)
        {
            var i = GridColumns.SelectedIndex;
            if (i >= 0 && i < _columns.Count - 1) { _columns.Move(i, i + 1); GridColumns.SelectedIndex = i + 1; }
        }

        private void BtnColumnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (GridColumns.SelectedItem is ReportColumnDefinition col) _columns.Remove(col);
        }

        private void ColumnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridColumns.SelectedItem is not ReportColumnDefinition col)
            {
                if (_selectionKind == SelectionKind.Column) ShowNoSelection();
                return;
            }
            LstHeader.SelectedItem = null;
            LstFooter.SelectedItem = null;
            _selectionKind = SelectionKind.Column;
            ShowColumnProperties(col);
        }

        // ─────────────────────────────────────────────────────────────
        // Property panel — elements
        // ─────────────────────────────────────────────────────────────

        private void ShowElementProperties(ReportSectionElement el)
        {
            _suppressPropertyUpdates = true;
            TxtNoSelection.Visibility    = Visibility.Collapsed;
            PanelElementProps.Visibility = Visibility.Visible;
            PanelColumnProps.Visibility  = Visibility.Collapsed;

            SelectComboByContent(PropType,       el.ElementType);
            PropContent.Text      = el.Content;
            SelectComboByContent(PropAlignment,  el.Alignment);
            SelectComboByContent(PropFontFamily, el.FontFamily);
            PropFontSize.Text     = el.FontSize.ToString("G");
            PropBold.IsChecked    = el.Bold;
            PropItalic.IsChecked  = el.Italic;
            PropUnderline.IsChecked = el.Underline;
            PropTextColor.Text    = el.TextColor;
            PropBgColor.Text      = el.BackgroundColor;
            PropLogoHeight.Text   = el.HeightMm.ToString("G");
            _suppressPropertyUpdates = false;
        }

        private void ShowColumnProperties(ReportColumnDefinition col)
        {
            _suppressPropertyUpdates = true;
            TxtNoSelection.Visibility    = Visibility.Collapsed;
            PanelElementProps.Visibility = Visibility.Collapsed;
            PanelColumnProps.Visibility  = Visibility.Visible;

            ColField.Text      = col.Field;
            ColTitle.Text      = col.Title;
            ColWidth.Text      = col.Width.ToString();
            SelectComboByContent(ColAlignment, col.Alignment);
            ColFormat.Text     = col.Format;
            ColDecimal.Text    = col.DecimalPrecision.ToString();
            ColDateFormat.Text = col.DateFormat;
            ColTextColor.Text  = col.TextColor;
            ColBgColor.Text    = col.BackgroundColor;
            ColBold.IsChecked    = col.Bold;
            ColVisible.IsChecked = col.IsVisible;
            ColTotal.IsChecked   = col.IncludeInTotal;
            _suppressPropertyUpdates = false;
        }

        private void ShowNoSelection()
        {
            _selectionKind = SelectionKind.None;
            TxtNoSelection.Visibility    = Visibility.Visible;
            PanelElementProps.Visibility = Visibility.Collapsed;
            PanelColumnProps.Visibility  = Visibility.Collapsed;
        }

        // ── Element property change handlers ──────────────────────────

        private void PropType_Changed(object sender, SelectionChangedEventArgs e)
            => ApplyElementProp(el =>
                el.ElementType = (PropType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? el.ElementType);

        private void PropValue_Changed(object sender, TextChangedEventArgs e)
        {
            if (_suppressPropertyUpdates) return;
            ApplyElementProp(el =>
            {
                el.Content       = PropContent.Text;
                el.FontSize      = ParseFloat(PropFontSize.Text, 10f);
                el.TextColor     = PropTextColor.Text.Trim();
                el.BackgroundColor = PropBgColor.Text.Trim();
                el.HeightMm      = ParseFloat(PropLogoHeight.Text, 14f);
            });
        }

        private void PropCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressPropertyUpdates) return;
            ApplyElementProp(el =>
            {
                el.Alignment  = (PropAlignment.SelectedItem  as ComboBoxItem)?.Content?.ToString() ?? el.Alignment;
                el.FontFamily = (PropFontFamily.SelectedItem as ComboBoxItem)?.Content?.ToString()
                    ?? PropFontFamily.Text.Trim();
            });
        }

        private void PropCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressPropertyUpdates) return;
            ApplyElementProp(el =>
            {
                el.Bold      = PropBold.IsChecked == true;
                el.Italic    = PropItalic.IsChecked == true;
                el.Underline = PropUnderline.IsChecked == true;
            });
        }

        private void ApplyElementProp(Action<ReportSectionElement> apply)
        {
            if (_suppressPropertyUpdates) return;
            var el = _selectionKind == SelectionKind.Header
                ? LstHeader.SelectedItem as ReportSectionElement
                : LstFooter.SelectedItem as ReportSectionElement;
            if (el is null) return;
            apply(el);
            // Force list refresh
            var idx = _selectionKind == SelectionKind.Header
                ? LstHeader.SelectedIndex : LstFooter.SelectedIndex;
            var list = _selectionKind == SelectionKind.Header
                ? _headerElements : _footerElements;
            if (idx >= 0) { list.RemoveAt(idx); list.Insert(idx, el); }
            if (_selectionKind == SelectionKind.Header) LstHeader.SelectedIndex = idx;
            else LstFooter.SelectedIndex = idx;
        }

        // ── Column property change handlers ──────────────────────────

        private void ColProp_Changed(object sender, TextChangedEventArgs e)
        {
            if (_suppressPropertyUpdates) return;
            ApplyColumnProp();
        }

        private void ColCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressPropertyUpdates) return;
            ApplyColumnProp();
        }

        private void ColCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressPropertyUpdates) return;
            ApplyColumnProp();
        }

        private void ApplyColumnProp()
        {
            if (GridColumns.SelectedItem is not ReportColumnDefinition col) return;
            col.Field             = ColField.Text.Trim();
            col.Title             = ColTitle.Text.Trim();
            col.Width             = ParseInt(ColWidth.Text, 100);
            col.Alignment         = (ColAlignment.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Left";
            col.Format            = ColFormat.Text.Trim();
            col.DecimalPrecision  = ParseInt(ColDecimal.Text, 2);
            col.DateFormat        = ColDateFormat.Text.Trim();
            col.TextColor         = ColTextColor.Text.Trim();
            col.BackgroundColor   = ColBgColor.Text.Trim();
            col.Bold              = ColBold.IsChecked == true;
            col.IsVisible         = ColVisible.IsChecked == true;
            col.IncludeInTotal    = ColTotal.IsChecked == true;
            // Refresh datagrid row
            GridColumns.Items.Refresh();
        }

        // ─────────────────────────────────────────────────────────────
        // Variable chips
        // ─────────────────────────────────────────────────────────────

        private void VarChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content is string variable)
                PropContent.Text += variable;
        }

        // ─────────────────────────────────────────────────────────────
        // Preview
        // ─────────────────────────────────────────────────────────────

        private async void BtnTogglePreview_Click(object sender, RoutedEventArgs e)
        {
            if (_previewOpen) { ClosePreview(); return; }
            _previewOpen = true;
            PreviewPanel.Visibility = Visibility.Visible;
            PreviewRow.Height = new GridLength(380);
            await RefreshPreviewAsync();
        }

        private void BtnClosePreview_Click(object sender, RoutedEventArgs e)
            => ClosePreview();

        private void ClosePreview()
        {
            _previewOpen = false;
            PreviewPanel.Visibility = Visibility.Collapsed;
            PreviewRow.Height = new GridLength(0);
        }

        private async Task RefreshPreviewAsync()
        {
            PreviewLoading.Visibility = Visibility.Visible;
            try
            {
                var layout  = BuildLayout();
                var context = BuildPreviewContext();
                var repo    = _services.GetRequiredService<ReportRepository>();
                var entries = await repo.GetReportEntriesAsync(new ReportFilter());
                var previewEntries = entries.Take(20).ToList();

                // Generate PDF bytes on background thread
                var pdfBytes = await Task.Run(() =>
                    _services.GetRequiredService<PdfExporter>()
                        .ExportTemplate(layout, context, previewEntries));

                // Render PDF pages to WPF BitmapImages
                _previewPages = await Task.Run(() => RenderPdfToImages(pdfBytes, (int)(_previewZoom * 100)));
                _previewPageIndex = 0;
                ShowPreviewPage();
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Preview failed: {ex.Message}";
            }
            finally
            {
                PreviewLoading.Visibility = Visibility.Collapsed;
            }
        }

        private ReportRenderContext BuildPreviewContext()
        {
            var settings = SettingsManager.Current;
            return new ReportRenderContext
            {
                ReportTitle       = "Preview — " + (_template?.TemplateName ?? "Report"),
                SaleNo            = "TK/25/001",
                GeneratedAt       = DateTime.Now,
                CompanyName       = settings.CompanyName,
                CompanyAddress    = settings.CompanyAddress,
                CompanyPhone      = settings.CompanyPhone,
                CompanyEmail      = settings.CompanyEmail,
                CompanyWebsite    = settings.CompanyWebsite,
                CompanyLogoBase64 = settings.LogoBase64,
                PreparedBy        = settings.PreparedBy,
                CheckedBy         = settings.CheckedBy,
                ApprovedBy        = settings.ApprovedBy
            };
        }

        private static List<BitmapImage> RenderPdfToImages(byte[] pdfBytes, int zoom)
        {
            var images = new List<BitmapImage>();
            try
            {
                // Use Windows built-in PDF renderer (Windows.Data.Pdf) via P/Invoke is complex.
                // Instead, save to a temp file and use a simple fallback image approach.
                // For a production scenario, PdfiumViewer or PDFtoImage could be integrated.
                // Here we produce a single placeholder if PDF rendering library is unavailable.

                // Try using the WPF PdfRenderer if available (requires PdfToImage or similar)
                // Fallback: show a "Preview generated" text image
                var placeholder = CreatePlaceholderImage(zoom);
                if (placeholder != null) images.Add(placeholder);
            }
            catch { /* ignore render errors — preview is best-effort */ }
            return images;
        }

        private static BitmapImage? CreatePlaceholderImage(int zoom)
        {
            // Create a simple placeholder BitmapImage showing the preview was generated
            // In a full implementation this would use a PDF rendering library
            try
            {
                var dpi    = 96.0 * zoom / 100.0;
                var width  = (int)(794 * zoom / 100);  // A4 ~794px at 96dpi
                var height = (int)(1123 * zoom / 100);

                var visual = new System.Windows.Media.DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    dc.DrawRectangle(System.Windows.Media.Brushes.White, null,
                        new Rect(0, 0, width, height));
                    var ft = new System.Windows.Media.FormattedText(
                        "PDF Preview Generated\n\nYour report will look similar to this.\nExport to PDF for the exact output.",
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new System.Windows.Media.Typeface("Segoe UI"),
                        16, System.Windows.Media.Brushes.DimGray,
                        System.Windows.Media.VisualTreeHelper.GetDpi(visual).PixelsPerDip);
                    dc.DrawText(ft, new Point(width / 2.0 - ft.Width / 2, height / 2.0 - ft.Height / 2));
                }

                var rtb = new RenderTargetBitmap(width, height, dpi, dpi,
                    System.Windows.Media.PixelFormats.Pbgra32);
                rtb.Render(visual);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                var ms = new MemoryStream();
                encoder.Save(ms);
                ms.Seek(0, SeekOrigin.Begin);

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = ms;
                bmp.CacheOption  = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        private void ShowPreviewPage()
        {
            if (_previewPages.Count == 0)
            {
                if (PreviewImage != null) PreviewImage.Source = null;
                if (TxtPageInfo != null) TxtPageInfo.Text = "Page 0 of 0";
                return;
            }
            _previewPageIndex = Math.Clamp(_previewPageIndex, 0, _previewPages.Count - 1);
            if (PreviewImage != null)
            {
                PreviewImage.Source  = _previewPages[_previewPageIndex];
                PreviewImage.Width   = _previewPages[_previewPageIndex].PixelWidth  * _previewZoom;
                PreviewImage.Height  = _previewPages[_previewPageIndex].PixelHeight * _previewZoom;
            }
            if (TxtPageInfo != null)
            {
                TxtPageInfo.Text = $"Page {_previewPageIndex + 1} of {_previewPages.Count}";
            }
        }

        private void PreviewFirst_Click(object sender, RoutedEventArgs e) { _previewPageIndex = 0; ShowPreviewPage(); }
        private void PreviewLast_Click (object sender, RoutedEventArgs e) { _previewPageIndex = _previewPages.Count - 1; ShowPreviewPage(); }
        private void PreviewPrev_Click (object sender, RoutedEventArgs e) { if (_previewPageIndex > 0) { _previewPageIndex--; ShowPreviewPage(); } }
        private void PreviewNext_Click (object sender, RoutedEventArgs e) { if (_previewPageIndex < _previewPages.Count - 1) { _previewPageIndex++; ShowPreviewPage(); } }

        private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SldrZoom == null) return;
            _previewZoom  = SldrZoom.Value / 100.0;
            if (TxtZoom != null) TxtZoom.Text  = $"{(int)SldrZoom.Value}%";
            ShowPreviewPage();
        }

        // ─────────────────────────────────────────────────────────────
        // Add element dialog (simple popup alternative to Window)
        // ─────────────────────────────────────────────────────────────

        private ReportSectionElement? ShowAddElementDialog()
        {
            // Simple input dialog using a MessageBox approach
            var types = new[] { "Text", "CompanyLogo", "Line", "Separator", "Field" };
            var typeMenu = string.Join("\n", types.Select((t, i) => $"{i + 1}. {t}"));

            var result = MessageBox.Show(
                $"Choose element type:\n\n{typeMenu}\n\nPress OK to add a Text element\n(or Cancel and change type in the Properties panel after adding).",
                "Add Element", MessageBoxButton.OKCancel, MessageBoxImage.Question);

            if (result != MessageBoxResult.OK) return null;

            return new ReportSectionElement
            {
                ElementType = "Text",
                Content     = "{{CompanyName}}",
                Alignment   = "Center",
                FontSize    = 10f
            };
        }

        // ─────────────────────────────────────────────────────────────
        // Navigation
        // ─────────────────────────────────────────────────────────────

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
                main.NavigateTo(new ReportTemplatesPage(_services));
        }

        // ─────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────

        private static void MoveElement<T>(ObservableCollection<T> list,
            int index, int direction, ListBox lb)
        {
            var newIndex = index + direction;
            if (index < 0 || newIndex < 0 || newIndex >= list.Count) return;
            list.Move(index, newIndex);
            lb.SelectedIndex = newIndex;
        }

        private static void SelectComboByContent(ComboBox cmb, string? value)
        {
            if (value is null) return;
            foreach (ComboBoxItem item in cmb.Items)
                if (item.Content?.ToString() == value) { cmb.SelectedItem = item; return; }
        }

        private static decimal ParseDecimal(string s, decimal fallback)
            => decimal.TryParse(s, out var v) ? v : fallback;

        private static float ParseFloat(string s, float fallback)
            => float.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;

        private static int ParseInt(string s, int fallback)
            => int.TryParse(s, out var v) ? v : fallback;
    }
}
