using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AuctionPurchase.Core.Reports;
using AuctionPurchase.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace AuctionPurchase.UI.Pages
{
    public partial class ReportTemplatesPage : Page
    {
        private readonly IServiceProvider _services;
        private List<ReportTemplate> _allTemplates = new();

        public ReportTemplatesPage(IServiceProvider services)
        {
            _services = services;
            InitializeComponent();
            Loaded += async (_, _) => await RefreshAsync();
        }

        // ─────────────────────────────────────────────────────────────
        // Data
        // ─────────────────────────────────────────────────────────────

        private async Task RefreshAsync()
        {
            try
            {
                var repo = _services.GetRequiredService<ReportTemplateRepository>();
                _allTemplates = await repo.GetAllAsync();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load templates: {ex.Message}", "Error");
            }
        }

        private void ApplyFilter()
        {
            if (TemplateList == null || TxtEmpty == null) return;
            var filter = TxtSearch?.Text?.Trim() ?? string.Empty;
            var type   = (CmbFilterType?.SelectedItem as ComboBoxItem)?.Content?.ToString();

            var filtered = _allTemplates.Where(t =>
                (string.IsNullOrEmpty(type) || type == "All Types" || t.ReportType == type) &&
                (string.IsNullOrEmpty(filter) ||
                 t.TemplateName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                 t.ReportType.Contains(filter,   StringComparison.OrdinalIgnoreCase))
            ).ToList();

            TemplateList.ItemsSource = filtered;
            TxtEmpty.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ─────────────────────────────────────────────────────────────
        // Toolbar buttons
        // ─────────────────────────────────────────────────────────────

        private void BtnNew_Click(object sender, RoutedEventArgs e)
        {
            // Open designer with no pre-selected template (creates new)
            NavigateToDesigner(templateId: null, reportType: "AllEntries");
        }

        private async void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Tea Template (*.tktpl)|*.tktpl|All files (*.*)|*.*",
                Title  = "Import Report Template"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                var repo = _services.GetRequiredService<ReportTemplateRepository>();
                var json = await File.ReadAllTextAsync(dialog.FileName);
                await repo.ImportAsync(json);
                await RefreshAsync();
                TxtStatus.Text = "Template imported successfully.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed: {ex.Message}", "Import Error");
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
            => _ = RefreshAsync();

        private void FilterChanged(object sender, object e)
            => ApplyFilter();

        // ─────────────────────────────────────────────────────────────
        // Row action buttons
        // ─────────────────────────────────────────────────────────────

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (GetTemplateFromTag(sender) is not ReportTemplate t) return;
            NavigateToDesigner(templateId: t.TemplateId, reportType: t.ReportType);
        }

        private async void BtnDuplicate_Click(object sender, RoutedEventArgs e)
        {
            if (GetTemplateFromTag(sender) is not ReportTemplate t) return;
            try
            {
                var repo = _services.GetRequiredService<ReportTemplateRepository>();
                await repo.DuplicateAsync(t.TemplateId, $"{t.TemplateName} — Copy");
                await RefreshAsync();
                TxtStatus.Text = $"Duplicated '{t.TemplateName}'.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Duplicate failed: {ex.Message}", "Error");
            }
        }

        private async void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (GetTemplateFromTag(sender) is not ReportTemplate t) return;
            var dialog = new SaveFileDialog
            {
                Filter   = "Tea Template (*.tktpl)|*.tktpl",
                FileName = $"{t.TemplateName}.tktpl"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                var repo = _services.GetRequiredService<ReportTemplateRepository>();
                var json = await repo.ExportAsync(t.TemplateId);
                await File.WriteAllTextAsync(dialog.FileName, json);
                TxtStatus.Text = $"Exported to {Path.GetFileName(dialog.FileName)}.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error");
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (GetTemplateFromTag(sender) is not ReportTemplate t) return;
            if (MessageBox.Show(
                    $"Delete template '{t.TemplateName}'?\nThis cannot be undone.",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning)
                != MessageBoxResult.Yes) return;

            try
            {
                var repo = _services.GetRequiredService<ReportTemplateRepository>();
                await repo.DeleteAsync(t.TemplateId);
                await RefreshAsync();
                TxtStatus.Text = $"Deleted '{t.TemplateName}'.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Delete failed: {ex.Message}", "Error");
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Navigation
        // ─────────────────────────────────────────────────────────────

        private void NavigateToDesigner(int? templateId, string reportType)
        {
            try
            {
                if (NavigationService != null)
                {
                    NavigationService.Navigate(new ReportDesignerPage(_services, reportType, templateId));
                }
                else if (Window.GetWindow(this) is MainWindow main)
                {
                    main.NavigateTo(new ReportDesignerPage(_services, reportType, templateId));
                }
                else
                {
                    MessageBox.Show("Navigation failed: Cannot find NavigationService or MainWindow.", "Error");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening designer: {ex.Message}\n\n{ex.StackTrace}", "Error");
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
                main.NavigateTo(new ReportsPage(_services));
        }

        // ─────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────

        private ReportTemplate? GetTemplateFromTag(object sender)
        {
            if (sender is not Button btn || btn.Tag is not int id) return null;
            return _allTemplates.FirstOrDefault(t => t.TemplateId == id);
        }
    }
}
