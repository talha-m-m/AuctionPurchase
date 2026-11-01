using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Markup;
using System.Xml.Linq;
using AuctionPurchase.Core.Models;
using AuctionPurchase.Infrastructure.Data;
using AuctionPurchase.UI.Pages;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using AuctionPurchase.Infrastructure;
using AuctionPurchase.UI.Services;
using System.Windows.Media;
using System.Windows.Data;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"AuctionSearchTests-{Guid.NewGuid():N}.db");
        Window? window = null;
        try
        {
            var factory = new TestDatabaseFactory(databasePath);
            new MigrationRunner(factory).RunMigrationsAsync().GetAwaiter().GetResult();
            using (var connection = factory.CreateConnection())
                connection.Execute(@"INSERT INTO Brands (Name) VALUES ('Alpha Tea'), ('Beta Tea'), ('Alpha Gold');
                    INSERT INTO Brokers (Name) VALUES ('Test Broker');
                    INSERT INTO Grades (Name) VALUES ('Test Grade');
                    INSERT INTO Gardens (GardenId, Name) VALUES (10000, 'Garden X'), (10001, 'Garden Y'), (10002, 'Garden Z'), (10003, 'Unlinked Garden');");
            new CompanyRepository(factory).SaveAsync(new Company
                { Name = "Company A", GardenIds = new() { 10000, 10001, 10002 } }).GetAwaiter().GetResult();
            using var services = new ServiceCollection()
                .AddSingleton<IDatabaseFactory>(factory)
                .AddTransient<BrandRepository>().AddTransient<BrokerRepository>()
                .AddTransient<GardenRepository>().AddTransient<CompanyRepository>().AddTransient<GradeRepository>()
                .AddTransient<SaleRepository>().AddTransient<ReportRepository>().AddTransient<ReportTemplateRepository>()
                .AddTransient<ShedRepository>().AddTransient<BlendRepository>()
                .BuildServiceProvider();
            // Load the actual application resources without starting its production host.
            var app = new Application();
            var resources = XDocument.Load(Path.Combine(Environment.CurrentDirectory, "src/AuctionPurchase.UI/App.xaml"));
            XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            var dictionary = new XElement(presentation + "ResourceDictionary",
                new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"),
                new XAttribute(XNamespace.Xmlns + "ui", "clr-namespace:AuctionPurchase.UI.Services;assembly=" + typeof(NewSalePage).Assembly.GetName().Name),
                resources.Root!.Element(presentation + "Application.Resources")!.Elements());
            app.Resources = (ResourceDictionary)XamlReader.Parse(dictionary.ToString());
            UiPreferences.Apply(new AppSettings { IsDarkMode = true, DateFormat = "dd/MM/yyyy" });
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var page = new NewSalePage(services);
            window = new Window { Title = "Auction search verification", Width = 1450, Height = 850, Content = new Frame { Content = page } };
            window.Show();
            Pump();
            var combo = (ComboBox)page.FindName("CmbBrand");
            combo.ApplyTemplate();
            var editor = (TextBox)combo.Template.FindName("PART_EditableTextBox", combo);
            editor.Focus();
            Pump();

            editor.Text = "tea";
            editor.Select(1, 0);
            Pump();
            Check(combo.Items.Count == 2 && editor.Text == "tea", "Substring filtering keeps the typed query");
            Check(editor.CaretIndex == 1, $"Filtering preserves cursor position inside the query (actual {editor.CaretIndex})");
            Check(combo.SelectedItem == null, "Search text does not silently select a result");
            editor.Text = " ALPHA   tea ";
            Pump();
            Check(combo.Items.Count == 1, "Search ignores case and extra spaces across multiple words");
            editor.Text = "zz-no-result";
            Pump();
            Check(combo.Items.Count == 0 && combo.IsDropDownOpen && editor.Text == "zz-no-result",
                "No results keeps the query and opens the empty state");
            editor.Text = "";
            Pump();
            Check(combo.Items.Count == 3, "Clearing the query restores all options");
            editor.Text = "tea";
            Pump();
            combo.SelectedItem = combo.Items[0];
            combo.IsDropDownOpen = false;
            Pump();
            Check(combo.SelectedItem is Brand && combo.Text == ((Brand)combo.SelectedItem).Name && !combo.IsDropDownOpen,
                "Selecting a suggestion keeps its name and closes the menu");
            editor.Focus();
            editor.Text = "gold";
            Pump();
            Check(combo.SelectedItem == null && combo.Items.Count == 1, "Editing a selection clears the stale selected value");
            Press(combo, Key.Enter);
            Pump();
            Check(combo.SelectedItem is Brand chosen && chosen.Name == "Alpha Gold" && !combo.IsDropDownOpen,
                "Enter accepts the visible suggestion");
            editor.Focus();
            editor.Text = "beta";
            Press(combo, Key.Escape);
            Pump();
            Check(!combo.IsDropDownOpen && editor.Text == "beta", "Escape cancels queued searches without erasing the query");
            editor.Text = "tea";
            Pump();
            Press(combo, Key.Tab);
            Pump();
            Check(combo.SelectedItem == null, "Tab leaves an ambiguous search unselected");

            editor.Focus();
            editor.Text = "no-such-brand";
            Pump();
            typeof(NewSalePage).GetMethod("ClearEntryFields", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(page, null);
            Pump();
            Check(combo.Items.Count == 3 && combo.Text == "" && !combo.IsDropDownOpen,
                "Resetting the entry form removes old filters and pending queries");

            editor.Focus();
            editor.Text = "beta";
            Press(combo, Key.Enter);
            Pump();
            Check(combo.SelectedItem is Brand fastChoice && fastChoice.Name == "Beta Tea",
                "Fast Enter chooses from the current query before the pending filter runs");

            var gardenCombo = (ComboBox)page.FindName("CmbGarden");
            gardenCombo.ItemsSource = new List<Garden>
            {
                new() { Name = "Tea ba" }, new() { Name = "Amberba Estate" },
                new() { Name = "Baraoora" }, new() { Name = "Balisera" }, new() { Name = "Other Garden" }
            };
            gardenCombo.ApplyTemplate();
            var gardenEditor = (TextBox)gardenCombo.Template.FindName("PART_EditableTextBox", gardenCombo);
            gardenEditor.Focus();
            gardenEditor.Text = "BA";
            Pump();
            Check(gardenCombo.Items.Cast<Garden>().Select(x => x.Name).SequenceEqual(
                new[] { "Balisera", "Baraoora", "Amberba Estate", "Tea ba" }),
                "Garden names starting with the query appear before middle and end matches");
            Press(gardenCombo, Key.Enter);
            Pump();
            Check(gardenCombo.SelectedItem is Garden rankedChoice && rankedChoice.Name == "Balisera",
                "Garden Enter accepts the first ranked suggestion");
            gardenEditor.Focus();
            gardenEditor.Text = "ba";
            Press(gardenCombo, Key.Enter);
            Pump();
            Check(gardenCombo.SelectedItem is Garden fastGarden && fastGarden.Name == "Balisera",
                "Fast Enter uses the same prefix-first garden order");

            var actualGardens = new GardenRepository(factory).GetActiveAsync().GetAwaiter().GetResult().ToList();
            gardenCombo.ItemsSource = actualGardens;
            gardenCombo.SelectedItem = actualGardens.Single(x => x.GardenId == 10000);
            Pump();
            var accountCombo = (ComboBox)page.FindName("CmbAccount");
            Check(accountCombo.SelectedItem is Garden defaultAccount && defaultAccount.GardenId == 10000,
                "Selecting a garden defaults Account to the same garden");
            Check(accountCombo.Items.Cast<Garden>().Select(x => x.GardenId).Order().SequenceEqual(new[] { 10000, 10001, 10002 }),
                "Account offers exactly the gardens linked to the selected garden's company");
            accountCombo.ApplyTemplate();
            var accountEditor = (TextBox)accountCombo.Template.FindName("PART_EditableTextBox", accountCombo);
            accountEditor.Focus();
            accountEditor.Text = "Y";
            Pump();
            Check(accountCombo.Items.Count == 1 && accountCombo.Items[0] is Garden foundAccount && foundAccount.GardenId == 10001,
                "Account supports typed search across the company gardens");
            Press(accountCombo, Key.Enter);
            Pump();
            Check(accountCombo.SelectedItem is Garden selectedAccount && selectedAccount.GardenId == 10001,
                "Account search accepts a different garden as the selling account");
            gardenCombo.SelectedItem = actualGardens.Single(x => x.GardenId == 10002);
            Pump();
            Check(accountCombo.SelectedItem is Garden changedDefault && changedDefault.GardenId == 10002 && accountCombo.Items.Count == 3,
                "Changing garden resets Account to the new garden");
            gardenCombo.SelectedItem = actualGardens.Single(x => x.GardenId == 10003);
            Pump();
            Check(accountCombo.Items.Count == 1 && accountCombo.SelectedItem is Garden unlinked && unlinked.GardenId == 10003,
                "A garden without a company offers its own account");
            typeof(NewSalePage).GetMethod("StartEditingEntry", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(page, new object[] { new EntryDisplayItem
                {
                    TempId = 1, GardenId = 10000, GardenName = "Garden X", AccountGardenId = 10001, AccountName = "Garden Y"
                } });
            Pump();
            Check(accountCombo.SelectedItem is Garden editedAccount && editedAccount.GardenId == 10001,
                "Editing an entry restores its explicit selling account");

            typeof(NewSalePage).GetMethod("CancelEdit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(page, null);
            foreach (string field in new[] { "CmbBrand", "CmbBroker", "CmbGrade" })
                ((ComboBox)page.FindName(field)).SelectedIndex = 0;
            gardenCombo.SelectedItem = gardenCombo.Items.Cast<Garden>().Single(x => x.GardenId == 10000);
            accountCombo.SelectedItem = accountCombo.Items.Cast<Garden>().Single(x => x.GardenId == 10001);
            ((TextBox)page.FindName("TxtLotNo")).Text = "UI-ACCOUNT-1";
            ((TextBox)page.FindName("TxtBags")).Text = "10";
            ((TextBox)page.FindName("TxtKgs")).Text = "500";
            ((TextBox)page.FindName("TxtPrice")).Text = "250";
            ((Button)page.FindName("BtnAddEntry")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
            var addedEntry = ((DataGrid)page.FindName("GridEntries")).Items.Cast<EntryDisplayItem>().Single();
            Check(addedEntry.GardenId == 10000 && addedEntry.AccountGardenId == 10001 && addedEntry.AccountName == "Garden Y",
                "Adding a lot stores its chosen selling account in the entry list");
            typeof(NewSalePage).GetMethod("StartEditingEntry", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(page, new object[] { addedEntry });
            accountCombo.SelectedItem = accountCombo.Items.Cast<Garden>().Single(x => x.GardenId == 10002);
            ((Button)page.FindName("BtnAddEntry")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
            Check(((DataGrid)page.FindName("GridEntries")).Items.Cast<EntryDisplayItem>().Single().AccountGardenId == 10002,
                "Editing a lot can change its account to another company garden");

            var companiesPage = new CompaniesPage(services);
            ((Frame)window.Content).Content = companiesPage;
            Pump();
            var companyList = (ListBox)companiesPage.FindName("CompanyList");
            Check(companyList.Items.Count == 1, "Company tab loads saved companies");
            companyList.SelectedIndex = 0;
            Pump();
            var companyGardens = (ListBox)companiesPage.FindName("GardenList");
            Check(companyGardens.Items.Cast<CompaniesPage.GardenChoice>().Count(x => x.IsSelected) == 3,
                "Company tab restores all selected garden memberships");
            ((TextBox)companiesPage.FindName("TxtName")).Text = "Company A Renamed";
            ((Button)companiesPage.FindName("BtnSave")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
            Check(new CompanyRepository(factory).GetAllAsync().GetAwaiter().GetResult().Single().Name == "Company A Renamed",
                "Company tab saves edits and linked garden selections");

            var salesPage = new SalesListPage(services);
            ((Frame)window.Content).Content = salesPage;
            Pump();
            var editGarden = (ComboBox)salesPage.FindName("CmbGarden");
            editGarden.SelectedItem = editGarden.Items.Cast<Garden>().Single(x => x.GardenId == 10000);
            Pump();
            var editAccount = (ComboBox)salesPage.FindName("CmbAccount");
            Check(editAccount.Items.Count == 3 && editAccount.SelectedItem is Garden editingDefault && editingDefault.GardenId == 10000,
                "Existing-lot editor also defaults and limits accounts by company");
            ((Grid)salesPage.FindName("EditOverlay")).Visibility = Visibility.Visible;
            Pump();
            editAccount.ApplyTemplate();
            var editAccountEditor = (TextBox)editAccount.Template.FindName("PART_EditableTextBox", editAccount);
            editAccountEditor.Focus();
            editAccountEditor.Text = "Y";
            Pump();
            Check(editAccount.Items.Count == 1, "Existing-lot Account supports typed filtering");
            Press(editAccount, Key.Enter);
            Pump();
            Check(editAccount.SelectedItem is Garden selectedEditAccount && selectedEditAccount.GardenId == 10001,
                "Existing-lot Account accepts searched options with Enter");
            editAccountEditor.Focus();
            editAccountEditor.Text = "Z";
            Press(editAccount, Key.Escape);
            Pump();
            Check(!editAccount.IsDropDownOpen && editAccountEditor.Text == "Z",
                "Existing-lot Account cancels pending search with Escape");

            ((Frame)window.Content).Content = page;
            Pump();
            var datePicker = (DatePicker)page.FindName("DtpSaleDate");
            datePicker.SelectedDate = new DateTime(2026, 11, 1);
            var dateProbe = new TextBlock { DataContext = new AuctionPurchase.Core.Reports.ReportEntry { SaleDate = datePicker.SelectedDate.Value } };
            BindingOperations.SetBinding(dateProbe, TextBlock.TextProperty, UiPreferences.DateBinding("SaleDate"));
            UiPreferences.Apply(new AppSettings { IsDarkMode = false, DateFormat = "yyyy-MM-dd" });
            Pump();
            ((Frame)window.Content).Content = salesPage;
            Pump();
            Check(((SolidColorBrush)page.Background).Color.R > 200 && ((SolidColorBrush)((DataGrid)salesPage.FindName("GridLots")).RowBackground).Color.R > 200,
                $"Light theme changes page and table backgrounds immediately (page {page.Background}, rows {((DataGrid)salesPage.FindName("GridLots")).RowBackground})");
            ((Frame)window.Content).Content = page;
            Pump();
            var dateEditor = (TextBox)datePicker.Template.FindName("PART_TextBox", datePicker);
            Check(dateProbe.Text == "2026-11-01" && dateEditor.Text == "2026-11-01",
                "ISO date preference updates date bindings and date inputs");
            UiPreferences.Apply(new AppSettings { IsDarkMode = true, DateFormat = "MM/dd/yyyy" });
            Pump();
            Check(((SolidColorBrush)page.Background).Color.R < 100 && dateProbe.Text == "11/01/2026" && dateEditor.Text == "11/01/2026",
                "Theme switches back to dark and US date format updates live");
            UiPreferences.Apply(new AppSettings { IsDarkMode = true, DateFormat = "dd/MM/yyyy" });
            Pump();
            Check(dateProbe.Text == "01/11/2026" && dateEditor.Text == "01/11/2026", "Day-first date format updates live");

            var entryGrid = (DataGrid)page.FindName("GridEntries");
            Check(entryGrid.Columns.Single(x => x.Header.ToString() == "Garden").MinWidth >= 150 &&
                entryGrid.Columns.Single(x => x.Header.ToString() == "Account").Width.IsStar,
                "Entry table gives garden and account readable flexible widths");
            var bagsColumn = (DataGridTextColumn)entryGrid.Columns.Single(x => x.Header.ToString() == "Bags");
            Check(bagsColumn.ElementStyle.Setters.OfType<Setter>().Any(x => x.Property == TextBlock.TextAlignmentProperty && Equals(x.Value, TextAlignment.Right)),
                "Numeric table cells align right");
            var dashboard = new DashboardPage(services);
            var recentGrid = (DataGrid)dashboard.FindName("GridRecentSales");
            TableLayout.Apply(recentGrid);
            Check(recentGrid.Columns.Single(x => x.Header.ToString() == "Actions").Width.IsAbsolute && recentGrid.ColumnHeaderHeight == 44,
                "Dashboard table uses compact action columns and consistent headers");
            var reportsPage = new ReportsPage(services);
            typeof(ReportsPage).GetMethod("ConfigureGridColumns", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(reportsPage, null);
            var reportGrid = (DataGrid)reportsPage.FindName("GridReport");
            TableLayout.Apply(reportGrid);
            Check(reportGrid.Columns.Single(x => x.Header.ToString() == "Garden").Width.IsStar &&
                reportGrid.Columns.Single(x => x.Header.ToString() == "Date") is DataGridTextColumn { Binding: MultiBinding },
                "Generated report tables use flexible garden widths and preferred date bindings");
            var outline = BangladeshMapOverlay.Geometry(23.7, 90.35, 6, 750, 380);
            Check(outline.FillContains(new Point(375, 190)) && !outline.FillContains(new Point(15, 15)) && outline.Bounds.Height < 380,
                "Bangladesh outline surrounds the centered country and fits the closer map view");

            if (args.Contains("--light")) UiPreferences.Apply(new AppSettings { IsDarkMode = false, DateFormat = "yyyy-MM-dd" });
            ((Frame)window.Content).Content = args.Contains("--dashboard") ? dashboard : args.Contains("--reports") ? reportsPage : args.Contains("--sales") ? salesPage : args.Contains("--companies") ? companiesPage : page;
            Pump();

            if (args.Contains("--snapshots"))
            {
                UiPreferences.Apply(new AppSettings { IsDarkMode = false, DateFormat = "dd/MM/yyyy" });
                ((Frame)window.Content).Content = page;
                var complete = new EntryDisplayItem
                {
                    TempId = 1, BrandId = 1, BrandName = "Alpha Tea", BrokerId = 1, BrokerName = "Test Broker",
                    LotNo = "BA-001", GardenId = 10000, GardenName = "Garden X", AccountGardenId = 10001, AccountName = "Garden Y",
                    InvoiceNo = "INV-001", GradeId = 1, GradeName = "BOP", NumberOfBags = 20, TotalKgs = 1000, PricePerKg = 250, Value = 250000
                };
                var incomplete = new EntryDisplayItem
                {
                    TempId = 2, BrandName = "Unknown brand", BrokerId = 1, BrokerName = "Test Broker",
                    LotNo = "BA-002", GardenId = 10000, GardenName = "Garden X", AccountGardenId = 10000, AccountName = "Garden X",
                    InvoiceNo = "INV-002", GradeName = "Unmatched grade", TotalKgs = 500, PricePerKg = 220, Value = 110000
                };
                entryGrid.ItemsSource = new[] { complete, incomplete };
                typeof(NewSalePage).GetMethod("StartEditingEntry", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(page, new object[] { incomplete });
                Pump();
                var highlighted = (ComboBox)page.FindName("CmbBrand");
                Check(((SolidColorBrush)highlighted.Background).Color.R > 230 && highlighted.BorderThickness.Left == 1,
                    "Unmatched Excel fields use a soft light-theme fill and a thin border");
                UiPreferences.Apply(new AppSettings { IsDarkMode = true });
                Pump();
                Check(((SolidColorBrush)highlighted.Background).Color.R < 100,
                    "Unmatched field styling follows theme changes without stale light colors");
                UiPreferences.Apply(new AppSettings { IsDarkMode = false });
                Pump();
                Check(((SolidColorBrush)datePicker.BorderBrush).Color == Color.FromRgb(0xCB, 0xD5, 0xE1) &&
                      ((SolidColorBrush)datePicker.Background).Color == Colors.White,
                    "Date inputs use a white surface with the light-theme neutral border");
                Check(Math.Abs(((TextBox)page.FindName("TxtNotes")).ActualHeight - datePicker.ActualHeight) < 1,
                    "Text and date inputs align at the same height");
                Snapshot((FrameworkElement)window.Content, "new-sale-light");
                var masterPage = new MasterDataPage(services, "Gardens");
                ((Frame)window.Content).Content = masterPage;
                Pump();
                var masterGrid = (DataGrid)masterPage.FindName("GridData");
                Check(masterGrid.RowHeight == 46 && ((DataGridColumn)masterPage.FindName("ColActions")).Width.Value >= 260,
                    "Master Data action buttons have consistent row spacing and sufficient width");
                Snapshot((FrameworkElement)window.Content, "master-data-light");
                var settingsPage = new SettingsPage(services);
                ((Frame)window.Content).Content = settingsPage;
                Pump();
                Check(settingsPage.FindName("TxtBackupLocation") == null && settingsPage.FindName("TabBackup") != null,
                    "Backup storage field is removed while Backup and Restore remains available");
                Snapshot((FrameworkElement)window.Content, "settings-light");
            }

            if (args.Contains("--preview"))
            {
                window.Closed += (_, _) => Dispatcher.ExitAllFrames();
                Dispatcher.Run();
            }
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            window?.Close();
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    private static void Press(ComboBox combo, Key key) => combo.RaiseEvent(new KeyEventArgs(
        Keyboard.PrimaryDevice, PresentationSource.FromVisual(combo), Environment.TickCount, key)
        { RoutedEvent = Keyboard.PreviewKeyDownEvent });

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine($"PASS: {message}");
    }

    private static void Snapshot(FrameworkElement element, string name)
    {
        element.UpdateLayout();
        var image = new System.Windows.Media.Imaging.RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight,
            96, 96, PixelFormats.Pbgra32);
        image.Render(element);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
        var directory = Path.Combine(Environment.CurrentDirectory, "tmp", "ui-review");
        Directory.CreateDirectory(directory);
        using var stream = File.Create(Path.Combine(directory, name + ".png"));
        encoder.Save(stream);
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private sealed class TestDatabaseFactory(string path) : IDatabaseFactory
    {
        public SqliteConnection CreateConnection() => new($"Data Source={path};Foreign Keys=True");
    }
}
