using System.Threading.Tasks;
using System.Windows;
using AuctionPurchase.Infrastructure.Backup;
using AuctionPurchase.Infrastructure.Data;
using AuctionPurchase.Infrastructure.Export;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AuctionPurchase.UI;

public partial class App : Application
{
    private IHost _host;

    public App()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            var msg = args.Exception.InnerException != null
                ? args.Exception.Message + "\n\nDetail: " + args.Exception.InnerException.Message
                : args.Exception.Message;
            MessageBox.Show(msg, "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            var ex = args.Exception.InnerException ?? args.Exception;
            Dispatcher.InvokeAsync(() =>
                MessageBox.Show("Background error:\n\n" + ex.Message, "Background Error",
                    MessageBoxButton.OK, MessageBoxImage.Error));
            args.SetObserved();
        };

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<DatabaseConfig>();
                services.AddSingleton<IDatabaseFactory, DatabaseFactory>();
                services.AddTransient<MigrationRunner>();
                services.AddTransient<DatabaseSeeder>();
                services.AddTransient<BrandRepository>();
                services.AddTransient<BrokerRepository>();
                services.AddTransient<GardenRepository>();
                services.AddTransient<CompanyRepository>();
                services.AddTransient<GradeRepository>();
                services.AddTransient<ShedRepository>();
                services.AddTransient<BlendRepository>();
                services.AddTransient<SaleRepository>();
                services.AddTransient<ReportRepository>();
                services.AddTransient<ReportTemplateRepository>();
                services.AddTransient<BackupService>();
                services.AddTransient<PdfExporter>();
                services.AddTransient<ExcelExporter>();
                services.AddTransient<WordExporter>();
                services.AddSingleton<MainWindow>(sp => new MainWindow(sp));
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        Services.UiPreferences.Apply(AuctionPurchase.Infrastructure.SettingsManager.Current);
        await _host.StartAsync();
        var migrationRunner = _host.Services.GetRequiredService<MigrationRunner>();
        await migrationRunner.RunMigrationsAsync();
        var seeder = _host.Services.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
