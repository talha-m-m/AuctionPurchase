using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.Windows.Navigation;
using AuctionPurchase.UI.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace AuctionPurchase.UI;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _serviceProvider;
    private Button? _activeNavButton;

    public MainWindow(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _activeNavButton = BtnDashboard;
        NavigateTo(new DashboardPage(_serviceProvider));
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;

        // Update nav button styles
        if (_activeNavButton != null)
            _activeNavButton.Style = (Style)Resources["NavButtonStyle"];

        btn.Style = (Style)Resources["NavButtonActiveStyle"];
        _activeNavButton = btn;

        var tag = btn.Tag?.ToString() ?? "";
        Page page = tag switch
        {
            "Dashboard"  => new DashboardPage(_serviceProvider),
            "NewSale"    => new NewSalePage(_serviceProvider),
            "SalesList"  => new SalesListPage(_serviceProvider),
            "Reports"    => new ReportsPage(_serviceProvider),
            "Brands"     => new MasterDataPage(_serviceProvider, "Brands"),
            "Brokers"    => new MasterDataPage(_serviceProvider, "Brokers"),
            "Gardens"    => new MasterDataPage(_serviceProvider, "Gardens"),
            "Companies"  => new CompaniesPage(_serviceProvider),
            "Grades"     => new MasterDataPage(_serviceProvider, "Grades"),
            "Sheds"      => new MasterDataPage(_serviceProvider, "Sheds"),
            "Blends"     => new MasterDataPage(_serviceProvider, "Blends"),
            "Settings"   => new SettingsPage(_serviceProvider),
            _            => new DashboardPage(_serviceProvider)
        };

        NavigateTo(page);
    }

    public void NavigateTo(Page page)
    {
        MainFrame.Navigate(page);
    }

    private void AuthorLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        if (e.Uri.AbsoluteUri is not ("https://www.linkedin.com/in/talhamm/" or "https://github.com/talha-m-m")) return;
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show($"Could not open the profile link: {ex.Message}", "Open Profile", MessageBoxButton.OK, MessageBoxImage.Information); }
    }
}