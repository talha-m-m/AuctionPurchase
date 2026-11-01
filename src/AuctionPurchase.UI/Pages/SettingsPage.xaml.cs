using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using AuctionPurchase.Infrastructure;
using AuctionPurchase.Infrastructure.Backup;
using AuctionPurchase.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace AuctionPurchase.UI.Pages
{
    public partial class SettingsPage : Page
    {
        private readonly IServiceProvider _services;

        public SettingsPage(IServiceProvider services)
        {
            _services = services;
            InitializeComponent();
            LoadSettings();
        }

        // ─────────────────────────────────────────────────────────────
        // Load
        // ─────────────────────────────────────────────────────────────

        private void LoadSettings()
        {
            var s = SettingsManager.Current;

            // System tab
            LoadSessionsGrid();
            if (s.AuctionSessions.Any())
            {
                var active = s.AuctionSessions.FirstOrDefault(x => x.Id == s.ActiveSessionId);
                if (active != null) CmbActiveSession.SelectedItem = active;
            }
            CmbTheme.SelectedValue = s.IsDarkMode ? "Dark" : "Light";
            CmbDateFormat.SelectedItem = CmbDateFormat.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(
                    item.Content?.ToString(), s.DateFormat, StringComparison.Ordinal));
            if (CmbDateFormat.SelectedIndex < 0) CmbDateFormat.SelectedIndex = 0;

            var defaultDb = SettingsManager.DefaultDatabaseLocation;
            var defaultExport = SettingsManager.DefaultExportLocation;

            TxtDbLocation.Text = string.IsNullOrWhiteSpace(s.DatabaseLocation) ? defaultDb : s.DatabaseLocation;
            TxtExportLocation.Text = string.IsNullOrWhiteSpace(s.ExportLocation) ? defaultExport : s.ExportLocation;

            TxtDefaultDbHint.Text = $"Default: {defaultDb} (Auction.db)";
            TxtDefaultExportHint.Text = $"Default: {defaultExport}";

            // Company Profile tab
            TxtCompanyName.Text    = s.CompanyName;
            TxtCompanyPhone.Text   = s.CompanyPhone;
            TxtCompanyEmail.Text   = s.CompanyEmail;
            TxtCompanyAddress.Text = s.CompanyAddress;
            TxtCompanyWebsite.Text = s.CompanyWebsite;
            TxtPreparedBy.Text     = s.PreparedBy;
            TxtCheckedBy.Text      = s.CheckedBy;
            TxtApprovedBy.Text     = s.ApprovedBy;
            TxtDefaultHeader.Text  = s.DefaultHeaderText;
            TxtDefaultFooter.Text  = s.DefaultFooterText;

            // Logo preview
            LoadLogoPreview(s.LogoBase64);
        }

        private void LoadLogoPreview(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
            {
                LogoPreview.Source = null;
                return;
            }
            try
            {
                var bytes = Convert.FromBase64String(base64);
                var bmp   = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = new MemoryStream(bytes);
                bmp.CacheOption  = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                LogoPreview.Source = bmp;
            }
            catch { LogoPreview.Source = null; }
        }

        // ─────────────────────────────────────────────────────────────
        // Tab switching
        // ─────────────────────────────────────────────────────────────

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (PanelSystem is null || PanelBackup is null || PanelCompany is null) return;

            PanelSystem.Visibility  = Visibility.Collapsed;
            PanelCompany.Visibility = Visibility.Collapsed;
            PanelBackup.Visibility  = Visibility.Collapsed;

            if (TabSystem.IsChecked  == true) PanelSystem.Visibility  = Visibility.Visible;
            else if (TabCompany?.IsChecked == true) PanelCompany.Visibility = Visibility.Visible;
            else PanelBackup.Visibility = Visibility.Visible;
        }

        // ─────────────────────────────────────────────────────────────
        // System Settings save
        // ─────────────────────────────────────────────────────────────

        private async void BtnSaveSystem_Click(object sender, RoutedEventArgs e)
        {
            var s = SettingsManager.Current;
            if (CmbActiveSession.SelectedItem is AuctionSession active)
            {
                s.ActiveSessionId = active.Id;
                s.SessionStartDate = active.DateStart;
                s.SessionEndDate = active.DateFinish;
            }
            s.IsDarkMode       = string.Equals(CmbTheme.SelectedValue?.ToString(), "Dark",
                                     StringComparison.Ordinal);
            s.DateFormat       = (CmbDateFormat.SelectedItem as ComboBoxItem)?.Content?.ToString()
                                 ?? "dd/MM/yyyy";
            
            s.DatabaseLocation = TxtDbLocation.Text.Trim();
            s.ExportLocation   = TxtExportLocation.Text.Trim();

            SettingsManager.Save();
            TxtSystemSaveStatus.Text = "System settings saved.";
            AuctionPurchase.UI.Services.UiPreferences.Apply(s);
            await Task.Delay(3000);
            TxtSystemSaveStatus.Text = string.Empty;
        }

        private void LoadSessionsGrid()
        {
            var s = SettingsManager.Current;
            GridSessions.ItemsSource = null;
            GridSessions.ItemsSource = s.AuctionSessions;
            CmbActiveSession.ItemsSource = null;
            CmbActiveSession.ItemsSource = s.AuctionSessions;
            if (s.AuctionSessions.Any())
            {
                var active = s.AuctionSessions.FirstOrDefault(x => x.Id == s.ActiveSessionId);
                if (active != null) CmbActiveSession.SelectedItem = active;
            }
        }

        private void BtnAddSession_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtNewSessionTitle.Text) || !DtpNewSessionStart.SelectedDate.HasValue || !DtpNewSessionFinish.SelectedDate.HasValue)
            {
                MessageBox.Show("Please enter Title, Start Date, and Finish Date.");
                return;
            }

            SettingsManager.Current.AuctionSessions.Add(new AuctionSession
            {
                Title = TxtNewSessionTitle.Text.Trim(),
                DateStart = DtpNewSessionStart.SelectedDate.Value,
                DateFinish = DtpNewSessionFinish.SelectedDate.Value
            });
            SettingsManager.Save();
            LoadSessionsGrid();
            
            TxtNewSessionTitle.Text = "";
            DtpNewSessionStart.SelectedDate = null;
            DtpNewSessionFinish.SelectedDate = null;
        }

        private void BtnDeleteSession_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: AuctionSession session }) return;
            if (MessageBox.Show($"Delete session '{session.Title}'?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                SettingsManager.Current.AuctionSessions.Remove(session);
                SettingsManager.Save();
                LoadSessionsGrid();
            }
        }

        private void BtnBrowseDb_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select Database Folder" };
            if (dialog.ShowDialog() == true) TxtDbLocation.Text = dialog.FolderName;
        }


        private void BtnBrowseExport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select Export Folder" };
            if (dialog.ShowDialog() == true) TxtExportLocation.Text = dialog.FolderName;
        }

        private void BtnDefaultDb_Click(object sender, RoutedEventArgs e)
        {
            TxtDbLocation.Text = SettingsManager.DefaultDatabaseLocation;
        }


        private void BtnDefaultExport_Click(object sender, RoutedEventArgs e)
        {
            TxtExportLocation.Text = SettingsManager.DefaultExportLocation;
        }

        // ─────────────────────────────────────────────────────────────
        // Company Profile save
        // ─────────────────────────────────────────────────────────────

        private async void BtnSaveCompany_Click(object sender, RoutedEventArgs e)
        {
            var s = SettingsManager.Current;
            s.CompanyName       = TxtCompanyName.Text.Trim();
            s.CompanyPhone      = TxtCompanyPhone.Text.Trim();
            s.CompanyEmail      = TxtCompanyEmail.Text.Trim();
            s.CompanyAddress    = TxtCompanyAddress.Text.Trim();
            s.CompanyWebsite    = TxtCompanyWebsite.Text.Trim();
            s.PreparedBy        = TxtPreparedBy.Text.Trim();
            s.CheckedBy         = TxtCheckedBy.Text.Trim();
            s.ApprovedBy        = TxtApprovedBy.Text.Trim();
            s.DefaultHeaderText = TxtDefaultHeader.Text.Trim();
            s.DefaultFooterText = TxtDefaultFooter.Text.Trim();
            SettingsManager.Save();
            TxtCompanySaveStatus.Text = "Company profile saved successfully.";
            await Task.Delay(3000);
            TxtCompanySaveStatus.Text = string.Empty;
        }

        // ─────────────────────────────────────────────────────────────
        // Logo picker
        // ─────────────────────────────────────────────────────────────

        private void BtnPickLogo_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title  = "Select Company Logo",
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All files (*.*)|*.*"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                var bytes = File.ReadAllBytes(dialog.FileName);
                if (bytes.Length > 2 * 1024 * 1024)
                {
                    MessageBox.Show("The selected image exceeds 2 MB. Please choose a smaller image.",
                        "Image Too Large", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var base64 = Convert.ToBase64String(bytes);
                SettingsManager.Current.LogoBase64 = base64;
                LoadLogoPreview(base64);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load image: {ex.Message}", "Error");
            }
        }

        private void BtnClearLogo_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.LogoBase64 = string.Empty;
            LogoPreview.Source = null;
        }

        // ─────────────────────────────────────────────────────────────
        // Backup & Restore
        // ─────────────────────────────────────────────────────────────

        private async void BtnCreateBackup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BtnCreateBackup.IsEnabled = false;
                TxtBackupStatus.Text = "Creating backup…";
                var backupService = _services.GetRequiredService<BackupService>();
                var path = await backupService.CreateBackupAsync(schemaVersion: 1);
                TxtBackupStatus.Text = $"Backup created: {path}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Backup failed: {ex.Message}", "Backup Failed");
                TxtBackupStatus.Text = "Backup failed.";
            }
            finally
            {
                BtnCreateBackup.IsEnabled = true;
            }
        }

        private async void BtnRestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Auction Backup files (*.tkbackup)|*.tkbackup|All files (*.*)|*.*",
                Title  = "Select Backup File"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                BtnRestoreBackup.IsEnabled = false;
                TxtRestoreStatus.Text = "Restoring backup…";

                var backupService = _services.GetRequiredService<BackupService>();
                await backupService.RestoreBackupAsync(dialog.FileName);

                var migrationRunner = _services.GetRequiredService<MigrationRunner>();
                await migrationRunner.RunMigrationsAsync();

                // Reload settings (BackupService already called Reload, but be safe)
                LoadSettings();

                MessageBox.Show(
                    "Restore completed successfully.\nThe application will now close. Please restart it.",
                    "Restore Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Restore failed: {ex.Message}", "Restore Failed");
                TxtRestoreStatus.Text = "Restore failed.";
                BtnRestoreBackup.IsEnabled = true;
            }
        }
    }
}
