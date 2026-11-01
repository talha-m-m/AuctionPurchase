using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AuctionPurchase.Core.Models;
using AuctionPurchase.Core.Reports;
using AuctionPurchase.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace AuctionPurchase.UI.Pages
{
    public partial class SalesListPage : Page
    {
        private readonly IServiceProvider _services;
        private ReportEntry? _lotBeingEdited;
        private int _currentPage = 1;
        private int _pageSize = 50;
        private int _totalPages = 1;
        private List<Garden> _gardens = new();
        private List<Garden> _accounts = new();
        private List<Company> _companies = new();
        private bool _updatingAccount;
        private int _accountSearchVersion;

        public SalesListPage(IServiceProvider services)
        {
            _services = services;
            InitializeComponent();
            CmbGarden.SelectionChanged += (_, _) => RefreshAccounts();
            CmbAccount.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                new TextChangedEventHandler(Account_TextChanged));
            CmbAccount.PreviewKeyDown += Account_KeyDown;
            CmbAccount.DropDownClosed += (_, _) =>
            {
                if (_updatingAccount) return;
                _updatingAccount = true;
                try
                {
                    _accountSearchVersion++;
                    var selected = CmbAccount.SelectedItem;
                    var text = CmbAccount.Text;
                    if (CmbAccount.ItemsSource == null) return;
                    var view = (System.Windows.Data.ListCollectionView)System.Windows.Data.CollectionViewSource.GetDefaultView(CmbAccount.ItemsSource);
                    view.Filter = null;
                    view.CustomSort = null;
                    CmbAccount.SelectedItem = selected;
                    CmbAccount.Text = text;
                }
                finally { _updatingAccount = false; }
            };
            Loaded += async (_, _) =>
            {
                await LoadMasterDataAsync();
                await LoadLotsAsync();
            };
        }

        private async Task LoadMasterDataAsync()
        {
            var brandRepo = _services.GetRequiredService<BrandRepository>();
            var brokerRepo = _services.GetRequiredService<BrokerRepository>();
            var gardenRepo = _services.GetRequiredService<GardenRepository>();
            var gradeRepo = _services.GetRequiredService<GradeRepository>();
            var shedRepo = _services.GetRequiredService<ShedRepository>();
            var blendRepo = _services.GetRequiredService<BlendRepository>();

            // Include inactive masters so existing historic lots remain editable.
            CmbBrand.ItemsSource = await brandRepo.GetAllAsync();
            CmbBroker.ItemsSource = await brokerRepo.GetAllAsync();
            _gardens = (await gardenRepo.GetAllAsync()).ToList();
            _companies = await _services.GetRequiredService<CompanyRepository>().GetAllAsync();
            CmbGarden.ItemsSource = _gardens;
            CmbGrade.ItemsSource = await gradeRepo.GetAllAsync();
            CmbShed.ItemsSource = await shedRepo.GetAllAsync();
            CmbBlend.ItemsSource = await blendRepo.GetAllAsync();
        }

        private async Task LoadLotsAsync()
        {
            try
            {
                var reportRepo = _services.GetRequiredService<ReportRepository>();
                var searchTerm = TxtSearch.Text.Trim();
                var (items, totalCount) = await reportRepo.GetReportEntriesPagedAsync(new ReportFilter
                {
                    SaleNo = string.IsNullOrEmpty(searchTerm) ? null : searchTerm,
                    FromDate = DtpFrom.SelectedDate,
                    ToDate = DtpTo.SelectedDate
                }, _currentPage, _pageSize);
                
                GridLots.ItemsSource = items;
                _totalPages = (int)Math.Ceiling(totalCount / (double)_pageSize);
                if (_totalPages == 0) _totalPages = 1;
                TxtPageInfo.Text = $"Page {_currentPage} of {_totalPages}";
                BtnPrevPage.IsEnabled = _currentPage > 1;
                BtnNextPage.IsEnabled = _currentPage < _totalPages;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading auction lots: {ex.Message}", "Error");
            }
        }

        private async void TxtSearch_KeyUp(object sender, System.Windows.Input.KeyEventArgs e) { _currentPage = 1; await LoadLotsAsync(); }
        private async void FilterChanged(object sender, SelectionChangedEventArgs e) { _currentPage = 1; await LoadLotsAsync(); }

        private async void BtnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1) { _currentPage--; await LoadLotsAsync(); }
        }

        private async void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages) { _currentPage++; await LoadLotsAsync(); }
        }

        private void BtnEditLot_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: ReportEntry lot }) return;

            _lotBeingEdited = lot;
            TxtEditSaleNo.Text = lot.SaleNo;
            DtpEditSaleDate.SelectedDate = lot.SaleDate;
            CmbBrand.SelectedItem = CmbBrand.Items.OfType<Brand>().FirstOrDefault(x => x.BrandId == lot.BrandId);
            CmbBroker.SelectedItem = CmbBroker.Items.OfType<Broker>().FirstOrDefault(x => x.BrokerId == lot.BrokerId);
            TxtLotNo.Text = lot.LotNo;
            CmbGarden.SelectedItem = CmbGarden.Items.OfType<Garden>().FirstOrDefault(x => x.GardenId == lot.GardenId);
            RefreshAccounts(lot.AccountGardenId ?? lot.GardenId);
            TxtInvoiceNo.Text = lot.InvoiceNo;
            CmbGrade.SelectedItem = CmbGrade.Items.OfType<Grade>().FirstOrDefault(x => x.GradeId == lot.GradeId);
            TxtBags.Text = lot.NumberOfBags.ToString();
            TxtKgs.Text = lot.TotalKgs.ToString("0.##");
            TxtPrice.Text = lot.PricePerKg.ToString("0.##");
            TxtValue.Text = lot.Value.ToString("N2");
            CmbShed.SelectedItem = CmbShed.Items.OfType<Shed>().FirstOrDefault(x => x.ShedId == lot.ShedId);
            CmbBlend.SelectedItem = CmbBlend.Items.OfType<Blend>().FirstOrDefault(x => x.BlendId == lot.BlendId);
            EditOverlay.Visibility = Visibility.Visible;
        }

        private void NumericFieldChanged(object sender, TextChangedEventArgs e)
        {
            TxtValue.Text = decimal.TryParse(TxtKgs.Text, out var kgs) && decimal.TryParse(TxtPrice.Text, out var price)
                ? (kgs * price).ToString("N2")
                : "0.00";
        }

        private async void BtnSaveLot_Click(object sender, RoutedEventArgs e)
        {
            if (_lotBeingEdited is null) return;
            if (string.IsNullOrWhiteSpace(TxtEditSaleNo.Text) || DtpEditSaleDate.SelectedDate is null ||
                CmbBrand.SelectedItem is not Brand brand || CmbBroker.SelectedItem is not Broker broker ||
                string.IsNullOrWhiteSpace(TxtLotNo.Text) ||
                !int.TryParse(TxtBags.Text, out var bags) || bags <= 0 ||
                !decimal.TryParse(TxtKgs.Text, out var kgs) || kgs <= 0 ||
                !decimal.TryParse(TxtPrice.Text, out var price) || price <= 0)
            {
                MessageBox.Show("Complete all required fields (Sale No, Date, Brand, Broker, Lot No) with valid Bags, KGs, and Price values.", "Validation");
                return;
            }

            try
            {
                BtnSaveLot.IsEnabled = false;
                var garden = CmbGarden.SelectedItem as Garden;
                var grade = CmbGrade.SelectedItem as Grade;
                var account = CmbAccount.SelectedItem as Garden ?? _accounts.FirstOrDefault(x =>
                    x.Name.Equals(CmbAccount.Text.Trim(), StringComparison.OrdinalIgnoreCase));
                if (garden != null && account == null)
                {
                    MessageBox.Show("Select a valid Account from this company's gardens.", "Validation");
                    return;
                }
                var entry = new AuctionEntry
                {
                    EntryId = _lotBeingEdited.EntryId, SaleId = _lotBeingEdited.SaleId,
                    BrandId = brand.BrandId, BrokerId = broker.BrokerId, LotNo = TxtLotNo.Text.Trim(),
                    GardenId = garden?.GardenId, InvoiceNo = TxtInvoiceNo.Text.Trim(), GradeId = grade?.GradeId,
                    AccountGardenId = account?.GardenId,
                    NumberOfBags = bags, TotalKgs = kgs, PricePerKg = price,
                    ShedId = (CmbShed.SelectedItem as Shed)?.ShedId,
                    BlendId = (CmbBlend.SelectedItem as Blend)?.BlendId
                };

                var saleRepo = _services.GetRequiredService<SaleRepository>();
                await saleRepo.UpdateLotAsync(entry, TxtEditSaleNo.Text, DtpEditSaleDate.SelectedDate.Value);
                CloseEditPopup();
                await LoadLotsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving auction lot: {ex.Message}", "Error");
            }
            finally
            {
                BtnSaveLot.IsEnabled = true;
            }
        }

        private async void BtnDeleteLot_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: ReportEntry lot }) return;
            if (MessageBox.Show($"Delete lot '{lot.LotNo}' from Sale '{lot.SaleNo}'? This cannot be undone.",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                var saleRepo = _services.GetRequiredService<SaleRepository>();
                await saleRepo.DeleteLotAsync(lot.EntryId);
                await LoadLotsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting auction lot: {ex.Message}", "Error");
            }
        }

        private void BtnCloseEditPopup_Click(object sender, RoutedEventArgs e) => CloseEditPopup();

        private void RefreshAccounts(int? historicalId = null)
        {
            var garden = CmbGarden.SelectedItem as Garden;
            var company = _companies.FirstOrDefault(x => x.IsActive && x.GardenIds.Contains(garden?.GardenId ?? 0));
            _accounts = _gardens.Where(x => garden != null && (x.GardenId == garden.GardenId ||
                company?.GardenIds.Contains(x.GardenId) == true || x.GardenId == historicalId)).OrderBy(x => x.Name).ToList();
            _updatingAccount = true;
            try
            {
                _accountSearchVersion++;
                CmbAccount.IsDropDownOpen = false;
                CmbAccount.ItemsSource = _accounts;
                CmbAccount.IsEnabled = garden != null;
                CmbAccount.SelectedItem = _accounts.FirstOrDefault(x => x.GardenId == (historicalId ?? garden?.GardenId));
                CmbAccount.Text = (CmbAccount.SelectedItem as Garden)?.Name ?? "";
            }
            finally { _updatingAccount = false; }
        }

        private void Account_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_updatingAccount || !CmbAccount.IsKeyboardFocusWithin ||
                CmbAccount.Template.FindName("PART_EditableTextBox", CmbAccount) is not TextBox editor) return;
            string query = editor.Text;
            if (CmbAccount.SelectedItem is Garden selected && selected.Name == query) return;
            int version = ++_accountSearchVersion;
            CmbAccount.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                if (_updatingAccount || version != _accountSearchVersion || !editor.IsKeyboardFocusWithin || editor.Text != query) return;
                int start = editor.SelectionStart;
                int length = editor.SelectionLength;
                _updatingAccount = true;
                try
                {
                    CmbAccount.SelectedItem = null;
                    var view = (System.Windows.Data.ListCollectionView)System.Windows.Data.CollectionViewSource.GetDefaultView(CmbAccount.ItemsSource);
                    view.Filter = item => ((Garden)item).Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase);
                    view.CustomSort = Comparer<object>.Create((left, right) =>
                    {
                        var a = ((Garden)left).Name; var b = ((Garden)right).Name;
                        int rank = (a.StartsWith(query.Trim(), StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                            .CompareTo(b.StartsWith(query.Trim(), StringComparison.OrdinalIgnoreCase) ? 0 : 1);
                        return rank != 0 ? rank : StringComparer.OrdinalIgnoreCase.Compare(a, b);
                    });
                    CmbAccount.Text = query;
                    CmbAccount.IsDropDownOpen = true;
                    editor.Select(Math.Min(start, query.Length), Math.Min(length, query.Length - Math.Min(start, query.Length)));
                }
                finally { _updatingAccount = false; }
            }));
        }

        private void Account_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                _accountSearchVersion++;
                CmbAccount.IsDropDownOpen = false;
                e.Handled = true;
                return;
            }
            if (e.Key != System.Windows.Input.Key.Enter && e.Key != System.Windows.Input.Key.Tab) return;
            string query = (CmbAccount.Template.FindName("PART_EditableTextBox", CmbAccount) as TextBox)?.Text ?? CmbAccount.Text;
            var matches = _accounts.Where(x => x.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Name.StartsWith(query.Trim(), StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var choice = matches.FirstOrDefault(x => x.Name.Equals(query.Trim(), StringComparison.OrdinalIgnoreCase)) ??
                (CmbAccount.SelectedItem is Garden current && matches.Contains(current) ? current : null) ??
                (!string.IsNullOrWhiteSpace(query) && matches.Count > 0 && (e.Key == System.Windows.Input.Key.Enter || matches.Count == 1) ? matches[0] : null);
            _accountSearchVersion++;
            _updatingAccount = true;
            try
            {
                var view = (System.Windows.Data.ListCollectionView)System.Windows.Data.CollectionViewSource.GetDefaultView(CmbAccount.ItemsSource);
                view.Filter = null;
                view.CustomSort = null;
                CmbAccount.SelectedItem = choice;
                CmbAccount.Text = choice?.Name ?? query;
                CmbAccount.IsDropDownOpen = false;
            }
            finally { _updatingAccount = false; }
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                CmbAccount.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
                e.Handled = true;
            }
        }

        private void CloseEditPopup()
        {
            EditOverlay.Visibility = Visibility.Collapsed;
            _lotBeingEdited = null;
        }
    }
}
