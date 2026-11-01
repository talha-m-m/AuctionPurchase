using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuctionPurchase.Core.Models;
using AuctionPurchase.Infrastructure.Data;
using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace AuctionPurchase.UI.Pages
{
    // A display-friendly entry DTO for the grid
    public class EntryDisplayItem : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

        private int _tempId;
        public int TempId { get => _tempId; set { _tempId = value; OnPropertyChanged(); } }

        private string _brandName = "";
        public string BrandName
        {
            get => _brandName;
            set
            {
                _brandName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BrandDisplay));
                OnPropertyChanged(nameof(IsBrandUnmatched));
                OnPropertyChanged(nameof(HasEmptyField));
            }
        }

        private string _brokerName = "";
        public string BrokerName
        {
            get => _brokerName;
            set
            {
                _brokerName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BrokerDisplay));
                OnPropertyChanged(nameof(IsBrokerUnmatched));
                OnPropertyChanged(nameof(HasEmptyField));
            }
        }

        private string _lotNo = "";
        public string LotNo
        {
            get => _lotNo;
            set
            {
                _lotNo = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LotNoDisplay));
                OnPropertyChanged(nameof(IsLotNoUnmatched));
                OnPropertyChanged(nameof(HasEmptyField));
            }
        }

        private string _gardenName = "";
        public string GardenName
        {
            get => _gardenName;
            set
            {
                _gardenName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GardenDisplay));
                OnPropertyChanged(nameof(IsGardenUnmatched));
                OnPropertyChanged(nameof(HasEmptyField));
            }
        }

        private string _invoiceNo = "";
        private int? _accountGardenId;
        public int? AccountGardenId { get => _accountGardenId; set { _accountGardenId = value; OnPropertyChanged(); } }
        private string _accountName = "";
        public string AccountName { get => _accountName; set { _accountName = value; OnPropertyChanged(); } }
        public string InvoiceNo { get => _invoiceNo; set { _invoiceNo = value; OnPropertyChanged(); } }

        private string _gradeName = "";
        public string GradeName
        {
            get => _gradeName;
            set
            {
                _gradeName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GradeDisplay));
                OnPropertyChanged(nameof(IsGradeUnmatched));
                OnPropertyChanged(nameof(HasEmptyField));
            }
        }

        private int _numberOfBags;
        public int NumberOfBags
        {
            get => _numberOfBags;
            set
            {
                _numberOfBags = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BagsDisplay));
                OnPropertyChanged(nameof(IsBagsUnmatched));
                OnPropertyChanged(nameof(HasEmptyField));
            }
        }

        private decimal _totalKgs;
        public decimal TotalKgs
        {
            get => _totalKgs;
            set
            {
                _totalKgs = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(KgsDisplay));
                OnPropertyChanged(nameof(IsKgsUnmatched));
                OnPropertyChanged(nameof(HasEmptyField));
            }
        }

        private decimal _pricePerKg;
        public decimal PricePerKg
        {
            get => _pricePerKg;
            set
            {
                _pricePerKg = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PriceDisplay));
                OnPropertyChanged(nameof(IsPriceUnmatched));
                OnPropertyChanged(nameof(HasEmptyField));
            }
        }

        private decimal _value;
        public decimal Value { get => _value; set { _value = value; OnPropertyChanged(); } }

        private string? _shedName;
        public string? ShedName
        {
            get => _shedName;
            set
            {
                _shedName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShedDisplay));
                OnPropertyChanged(nameof(IsShedUnmatched));
                OnPropertyChanged(nameof(HasEmptyField));
            }
        }

        private string? _blendName;
        public string? BlendName
        {
            get => _blendName;
            set
            {
                _blendName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BlendDisplay));
                OnPropertyChanged(nameof(IsBlendUnmatched));
                OnPropertyChanged(nameof(HasEmptyField));
            }
        }

        // Keep refs for saving
        private int? _brandId;
        public int? BrandId
        {
            get => _brandId;
            set
            {
                _brandId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BrandDisplay));
                OnPropertyChanged(nameof(IsBrandUnmatched));
                OnPropertyChanged(nameof(HasEmptyField));
            }
        }

        private int? _brokerId;
        public int? BrokerId
        {
            get => _brokerId;
            set
            {
                _brokerId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BrokerDisplay));
                OnPropertyChanged(nameof(IsBrokerUnmatched));
                OnPropertyChanged(nameof(HasEmptyField));
            }
        }

        private int? _gardenId;
        public int? GardenId
        {
            get => _gardenId;
            set
            {
                _gardenId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GardenDisplay));
                OnPropertyChanged(nameof(IsGardenUnmatched));
                OnPropertyChanged(nameof(HasEmptyField));
            }
        }

        private int? _gradeId;
        public int? GradeId
        {
            get => _gradeId;
            set
            {
                _gradeId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GradeDisplay));
                OnPropertyChanged(nameof(IsGradeUnmatched));
                OnPropertyChanged(nameof(HasEmptyField));
            }
        }

        private int? _shedId;
        public int? ShedId
        {
            get => _shedId;
            set
            {
                _shedId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShedDisplay));
                OnPropertyChanged(nameof(IsShedUnmatched));
                OnPropertyChanged(nameof(HasEmptyField));
            }
        }

        private int? _blendId;
        public int? BlendId
        {
            get => _blendId;
            set
            {
                _blendId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BlendDisplay));
                OnPropertyChanged(nameof(IsBlendUnmatched));
                OnPropertyChanged(nameof(HasEmptyField));
            }
        }

        // Match status properties for individual cells and form controls
        public bool IsBrandUnmatched => string.IsNullOrWhiteSpace(BrandName) || BrandId is null;
        public bool IsBrokerUnmatched => string.IsNullOrWhiteSpace(BrokerName) || BrokerId is null;
        public bool IsLotNoUnmatched => string.IsNullOrWhiteSpace(LotNo);
        public bool IsGardenUnmatched => !string.IsNullOrWhiteSpace(GardenName) && GardenId is null;
        public bool IsGradeUnmatched => string.IsNullOrWhiteSpace(GradeName) || GradeId is null;
        public bool IsBagsUnmatched => NumberOfBags <= 0;
        public bool IsKgsUnmatched => TotalKgs <= 0;
        public bool IsPriceUnmatched => PricePerKg <= 0;
        public bool IsShedUnmatched => !string.IsNullOrWhiteSpace(ShedName) && ShedId is null;
        public bool IsBlendUnmatched => !string.IsNullOrWhiteSpace(BlendName) && BlendId is null;

        // Display properties for grid
        public string BrandDisplay => string.IsNullOrWhiteSpace(BrandName) ? "[Missing]" : BrandName;
        public string BrokerDisplay => string.IsNullOrWhiteSpace(BrokerName) ? "[Missing]" : BrokerName;
        public string LotNoDisplay => string.IsNullOrWhiteSpace(LotNo) ? "[Missing]" : LotNo;
        public string GardenDisplay => GardenName ?? "";
        public string GradeDisplay => string.IsNullOrWhiteSpace(GradeName) ? "[Missing]" : GradeName;
        public string BagsDisplay => NumberOfBags <= 0 ? "[Missing]" : NumberOfBags.ToString();
        public string KgsDisplay => TotalKgs <= 0 ? "[Missing]" : TotalKgs.ToString("N2");
        public string PriceDisplay => PricePerKg <= 0 ? "[Missing]" : PricePerKg.ToString("N2");
        public string ShedDisplay => ShedName ?? "";
        public string BlendDisplay => BlendName ?? "";

        // Row highlighting trigger
        public bool HasEmptyField =>
            IsBrandUnmatched ||
            IsBrokerUnmatched ||
            IsLotNoUnmatched ||
            IsGardenUnmatched ||
            IsGradeUnmatched ||
            IsBagsUnmatched ||
            IsKgsUnmatched ||
            IsPriceUnmatched ||
            IsShedUnmatched ||
            IsBlendUnmatched;
    }

    public partial class NewSalePage : Page
    {
        private readonly IServiceProvider _services;
        private readonly ObservableCollection<EntryDisplayItem> _entries = new();
        private readonly Sale? _saleBeingEdited;
        private EntryDisplayItem? _entryBeingEdited;
        private int _nextTempId = 1;

        // Full master-data lists (used for filtering)
        private List<Brand>  _allBrands  = new();
        private List<Broker> _allBrokers = new();
        private List<Garden> _allGardens = new();
        private List<Garden> _accountGardens = new();
        private List<Company> _companies = new();
        private int? _accountOptionsGardenId;
        private List<Grade>  _allGrades  = new();
        private List<Shed>   _allSheds   = new();
        private List<Blend>  _allBlends  = new();

        // Track when we are programmatically updating the ComboBox text so we don't re-filter
        private bool _suppressFilter = false;
        private bool _masterDataLoaded;
        private readonly Dictionary<ComboBox, int> _searchVersions = new();

        public NewSalePage(IServiceProvider services, Sale? saleBeingEdited = null)
        {
            _services = services;
            _saleBeingEdited = saleBeingEdited;
            InitializeComponent();
            GridEntries.ItemsSource = _entries;
            DtpSaleDate.SelectedDate = DateTime.Today;
            Loaded += async (_, _) =>
            {
                if (_masterDataLoaded) return;
                _masterDataLoaded = true;
                await LoadMasterDataAsync();
                LoadSaleForEditing();
                // Wire live-search text-changed and drop-down closed after controls are ready
                CmbBrand.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                    new TextChangedEventHandler(CmbBrand_TextChanged));
                CmbBroker.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                    new TextChangedEventHandler(CmbBroker_TextChanged));
                CmbGarden.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                    new TextChangedEventHandler(CmbGarden_TextChanged));
                CmbAccount.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                    new TextChangedEventHandler(CmbAccount_TextChanged));
                CmbGrade.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                    new TextChangedEventHandler(CmbGrade_TextChanged));
                CmbShed.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                    new TextChangedEventHandler(CmbShed_TextChanged));
                CmbBlend.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                    new TextChangedEventHandler(CmbBlend_TextChanged));

                CmbBrand.DropDownClosed  += (_, _) => ComboBox_DropDownClosed(CmbBrand, _allBrands);
                CmbBroker.DropDownClosed += (_, _) => ComboBox_DropDownClosed(CmbBroker, _allBrokers);
                CmbGarden.DropDownClosed += (_, _) => ComboBox_DropDownClosed(CmbGarden, _allGardens);
                CmbAccount.DropDownClosed += (_, _) => ComboBox_DropDownClosed(CmbAccount, _accountGardens);
                CmbGarden.LostKeyboardFocus += (_, _) => UpdateAccountOptions();
                CmbGrade.DropDownClosed  += (_, _) => ComboBox_DropDownClosed(CmbGrade, _allGrades);
                CmbShed.DropDownClosed   += (_, _) => ComboBox_DropDownClosed(CmbShed, _allSheds);
                CmbBlend.DropDownClosed  += (_, _) => ComboBox_DropDownClosed(CmbBlend, _allBlends);
                foreach (var combo in new[] { CmbBrand, CmbBroker, CmbGarden, CmbAccount, CmbGrade, CmbShed, CmbBlend })
                    combo.PreviewKeyDown += SearchField_PreviewKeyDown;
            };
        }

        private async Task LoadMasterDataAsync()
        {
            try
            {
                var brandRepo  = _services.GetRequiredService<BrandRepository>();
                var brokerRepo = _services.GetRequiredService<BrokerRepository>();
                var gardenRepo = _services.GetRequiredService<GardenRepository>();
                var gradeRepo  = _services.GetRequiredService<GradeRepository>();
                var shedRepo   = _services.GetRequiredService<ShedRepository>();
                var blendRepo  = _services.GetRequiredService<BlendRepository>();

                _allBrands  = ((_saleBeingEdited is null) ? await brandRepo.GetActiveAsync()  : await brandRepo.GetAllAsync()).ToList();
                _allBrokers = ((_saleBeingEdited is null) ? await brokerRepo.GetActiveAsync() : await brokerRepo.GetAllAsync()).ToList();
                _allGardens = ((_saleBeingEdited is null) ? await gardenRepo.GetActiveAsync() : await gardenRepo.GetAllAsync()).ToList();
                _companies = await _services.GetRequiredService<CompanyRepository>().GetAllAsync();
                _allGrades  = ((_saleBeingEdited is null) ? await gradeRepo.GetActiveAsync()  : await gradeRepo.GetAllAsync()).ToList();
                _allSheds   = ((_saleBeingEdited is null) ? await shedRepo.GetActiveAsync()   : await shedRepo.GetAllAsync()).ToList();
                _allBlends  = ((_saleBeingEdited is null) ? await blendRepo.GetActiveAsync()  : await blendRepo.GetAllAsync()).ToList();

                CmbBrand.ItemsSource  = _allBrands;
                CmbBroker.ItemsSource = _allBrokers;
                CmbGarden.ItemsSource = _allGardens;
                CmbGrade.ItemsSource  = _allGrades;
                CmbShed.ItemsSource   = _allSheds;
                CmbBlend.ItemsSource  = _allBlends;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading master data: {ex.Message}", "Error");
            }
        }

        // ── Shared ComboBox filtering ──────────────────────────────────────────
        private void FilterComboBox<T>(ComboBox cmb, List<T> allItems, Func<T, string> nameSelector)
        {
            if (_suppressFilter) return;
            if (!cmb.IsKeyboardFocusWithin) return;
            if (cmb.Template.FindName("PART_EditableTextBox", cmb) is not TextBox editor) return;
            var text = editor.Text;
            if (cmb.SelectedItem is T selected && nameSelector(selected).Equals(text, StringComparison.OrdinalIgnoreCase))
                return;
            int version = _searchVersions.GetValueOrDefault(cmb) + 1;
            _searchVersions[cmb] = version;
            // Run after the ComboBox has processed the editor change. Refreshing
            // its view inside TextChanged otherwise fights WPF's text/selection sync.
            cmb.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                if (_searchVersions.GetValueOrDefault(cmb) != version || !editor.IsKeyboardFocusWithin || editor.Text != text)
                    return;
                int start = editor.SelectionStart;
                int length = editor.SelectionLength;
                _suppressFilter = true;
                try
                {
                    cmb.SelectedItem = null;
                    var terms = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    var view = System.Windows.Data.CollectionViewSource.GetDefaultView(cmb.ItemsSource);
                    view.Filter = item => item is T value && terms.All(term =>
                        nameSelector(value).Contains(term, StringComparison.OrdinalIgnoreCase));
                    if ((cmb == CmbGarden || cmb == CmbAccount) && view is System.Windows.Data.ListCollectionView gardenView)
                        gardenView.CustomSort = string.IsNullOrWhiteSpace(text) ? null
                            : Comparer<object>.Create((left, right) => CompareGardenMatches(left, right, text));
                    cmb.Text = text;
                    editor.Text = text;
                    cmb.IsDropDownOpen = true;
                    editor.Select(Math.Min(start, text.Length), Math.Min(length, text.Length - Math.Min(start, text.Length)));
                }
                finally { _suppressFilter = false; }
            }));
        }

        private void ComboBox_DropDownClosed<T>(ComboBox cmb, List<T> allItems)
        {
            if (_suppressFilter) return;
            _suppressFilter = true;
            try
            {
                _searchVersions[cmb] = _searchVersions.GetValueOrDefault(cmb) + 1;
                var sel = cmb.SelectedItem;
                var text = cmb.Text;
                var view = System.Windows.Data.CollectionViewSource.GetDefaultView(cmb.ItemsSource);
                view.Filter = null;
                if (view is System.Windows.Data.ListCollectionView listView) listView.CustomSort = null;
                cmb.SelectedItem = sel;
                cmb.Text = text;
            }
            finally
            {
                _suppressFilter = false;
            }
        }

        private void SearchField_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not ComboBox combo) return;
            if (e.Key == Key.Escape)
            {
                _searchVersions[combo] = _searchVersions.GetValueOrDefault(combo) + 1;
                combo.IsDropDownOpen = false;
                e.Handled = true;
            }
            else if (e.Key == Key.Enter || e.Key == Key.Tab)
            {
                // Enter accepts the first suggestion; Tab only accepts an exact
                // name or a single result, so tabbing never chooses an arbitrary lot detail.
                var query = (combo.Template.FindName("PART_EditableTextBox", combo) as TextBox)?.Text ?? combo.Text;
                var terms = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                string Name(object item) => item.GetType().GetProperty("Name")?.GetValue(item)?.ToString() ?? "";
                // Read the source as well as the query: a fast Enter can arrive
                // before the deferred view refresh from the last typed character.
                var matches = System.Windows.Data.CollectionViewSource.GetDefaultView(combo.ItemsSource)
                    .SourceCollection.Cast<object>().Where(item => terms.All(term =>
                        Name(item).Contains(term, StringComparison.OrdinalIgnoreCase))).ToList();
                if ((combo == CmbGarden || combo == CmbAccount) && terms.Length > 0)
                    matches.Sort((left, right) => CompareGardenMatches(left, right, query));
                var exact = matches.FirstOrDefault(item => string.Equals(Name(item), query.Trim(), StringComparison.OrdinalIgnoreCase));
                var choice = exact ?? (matches.Contains(combo.SelectedItem) ? combo.SelectedItem : null) ??
                    (terms.Length > 0 && matches.Count > 0 && (e.Key == Key.Enter || matches.Count == 1)
                        ? matches[0] : null);
                _suppressFilter = true;
                try
                {
                    var view = System.Windows.Data.CollectionViewSource.GetDefaultView(combo.ItemsSource);
                    view.Filter = null;
                    if (view is System.Windows.Data.ListCollectionView listView) listView.CustomSort = null;
                    combo.SelectedItem = choice;
                    combo.Text = choice != null ? Name(choice) : query;
                }
                finally { _suppressFilter = false; }
                if (combo == CmbGarden) UpdateAccountOptions();
                _searchVersions[combo] = _searchVersions.GetValueOrDefault(combo) + 1;
                combo.IsDropDownOpen = false;
                if (e.Key == Key.Enter)
                {
                    combo.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    e.Handled = true;
                }
            }
        }

        private void ResetSearchViews()
        {
            foreach (var combo in new[] { CmbBrand, CmbBroker, CmbGarden, CmbAccount, CmbGrade, CmbShed, CmbBlend })
            {
                _searchVersions[combo] = _searchVersions.GetValueOrDefault(combo) + 1;
                if (combo.ItemsSource != null)
                {
                    var view = System.Windows.Data.CollectionViewSource.GetDefaultView(combo.ItemsSource);
                    view.Filter = null;
                    if (view is System.Windows.Data.ListCollectionView listView) listView.CustomSort = null;
                }
                combo.IsDropDownOpen = false;
            }
        }

        private static int CompareGardenMatches(object left, object right, string query)
        {
            var leftName = ((Garden)left).Name.TrimStart();
            var rightName = ((Garden)right).Name.TrimStart();
            var prefix = query.Trim();
            int leftRank = leftName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            int rightRank = rightName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            int rank = leftRank.CompareTo(rightRank);
            return rank != 0 ? rank : StringComparer.OrdinalIgnoreCase.Compare(leftName, rightName);
        }

        private void CmbBrand_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterComboBox(CmbBrand, _allBrands, x => x.Name);
            if (_entryBeingEdited != null) UpdateFormHighlights();
        }
        private void CmbBroker_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterComboBox(CmbBroker, _allBrokers, x => x.Name);
            if (_entryBeingEdited != null) UpdateFormHighlights();
        }
        private void CmbGarden_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterComboBox(CmbGarden, _allGardens, x => x.Name);
            if (_entryBeingEdited != null) UpdateFormHighlights();
        }
        private void CmbAccount_TextChanged(object sender, TextChangedEventArgs e) =>
            FilterComboBox(CmbAccount, _accountGardens, x => x.Name);

        private Garden? SelectedGarden() => CmbGarden.SelectedItem as Garden ??
            _allGardens.FirstOrDefault(x => x.Name.Equals(CmbGarden.Text.Trim(), StringComparison.OrdinalIgnoreCase));

        private List<Garden> EligibleAccounts(Garden garden)
        {
            var company = _companies.FirstOrDefault(x => x.IsActive && x.GardenIds.Contains(garden.GardenId));
            return _allGardens.Where(x => x.GardenId == garden.GardenId || company?.GardenIds.Contains(x.GardenId) == true)
                .OrderBy(x => x.Name).ToList();
        }

        private void UpdateAccountOptions() => SetAccountOptions(SelectedGarden());

        private void SetAccountOptions(Garden? garden, int? existingAccountId = null)
        {
            if (_accountOptionsGardenId == garden?.GardenId && existingAccountId == null) return;
            bool wasSuppressed = _suppressFilter;
            _suppressFilter = true;
            try
            {
                _searchVersions[CmbAccount] = _searchVersions.GetValueOrDefault(CmbAccount) + 1;
                _accountOptionsGardenId = garden?.GardenId;
                _accountGardens = garden == null ? new() : EligibleAccounts(garden);
                // Keep historical accounts editable even if company ownership has changed.
                var historical = _allGardens.FirstOrDefault(x => x.GardenId == existingAccountId);
                if (historical != null && !_accountGardens.Any(x => x.GardenId == historical.GardenId))
                    _accountGardens.Add(historical);
                CmbAccount.IsDropDownOpen = false;
                CmbAccount.ItemsSource = _accountGardens;
                CmbAccount.IsEnabled = garden != null;
                CmbAccount.SelectedItem = _accountGardens.FirstOrDefault(x => x.GardenId == (existingAccountId ?? garden?.GardenId));
                CmbAccount.Text = (CmbAccount.SelectedItem as Garden)?.Name ?? "";
                CmbAccount.ToolTip = garden == null ? "Select a garden first." : "Choose a selling account from this company's gardens.";
            }
            finally { _suppressFilter = wasSuppressed; }
        }
        private void CmbGrade_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterComboBox(CmbGrade, _allGrades, x => x.Name);
            if (_entryBeingEdited != null) UpdateFormHighlights();
        }
        private void CmbShed_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterComboBox(CmbShed, _allSheds, x => x.Name);
            if (_entryBeingEdited != null) UpdateFormHighlights();
        }
        private void CmbBlend_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterComboBox(CmbBlend, _allBlends, x => x.Name);
            if (_entryBeingEdited != null) UpdateFormHighlights();
        }

        private void CmbField_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender == CmbGarden && !_suppressFilter) UpdateAccountOptions();
            if (_entryBeingEdited != null)
                UpdateFormHighlights();
        }

        private void FormField_Changed(object sender, TextChangedEventArgs e)
        {
            if (_entryBeingEdited != null)
                UpdateFormHighlights();
        }

        private void ValueField_Changed(object sender, TextChangedEventArgs e)
        {
            if (decimal.TryParse(TxtKgs.Text, out var kgs) && decimal.TryParse(TxtPrice.Text, out var price))
                TxtValue.Text = (kgs * price).ToString("N2");
            else
                TxtValue.Text = "0.00";

            if (_entryBeingEdited != null)
                UpdateFormHighlights();
        }

        private void EntryField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && _entryBeingEdited != null)
            {
                CancelEdit();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter || e.Key == Key.Tab)
            {
                // Move to next field
                var element = sender as UIElement;
                element?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                e.Handled = true;
            }
        }

        private void BtnAddEntry_Click(object sender, RoutedEventArgs e)
        {
            var brand = CmbBrand.SelectedItem as Brand
                ?? _allBrands.FirstOrDefault(x => x.Name.Equals(CmbBrand.Text.Trim(), StringComparison.OrdinalIgnoreCase));
            if (brand == null)
            {
                UpdateFormHighlights();
                MessageBox.Show("Please select or enter a valid Brand from the list.", "Validation");
                CmbBrand.Focus();
                return;
            }

            var broker = CmbBroker.SelectedItem as Broker
                ?? _allBrokers.FirstOrDefault(x => x.Name.Equals(CmbBroker.Text.Trim(), StringComparison.OrdinalIgnoreCase));
            if (broker == null)
            {
                UpdateFormHighlights();
                MessageBox.Show("Please select or enter a valid Broker from the list.", "Validation");
                CmbBroker.Focus();
                return;
            }

            var grade = CmbGrade.SelectedItem as Grade
                ?? _allGrades.FirstOrDefault(x => x.Name.Equals(CmbGrade.Text.Trim(), StringComparison.OrdinalIgnoreCase));
            if (grade == null)
            {
                UpdateFormHighlights();
                MessageBox.Show("Please select or enter a valid Grade from the list.", "Validation");
                CmbGrade.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtLotNo.Text))
            {
                UpdateFormHighlights();
                MessageBox.Show("Enter Lot No.", "Validation");
                TxtLotNo.Focus();
                return;
            }
            if (!int.TryParse(TxtBags.Text, out var bags) || bags <= 0)
            {
                UpdateFormHighlights();
                MessageBox.Show("Enter a valid number of bags (> 0).", "Validation");
                TxtBags.Focus();
                return;
            }
            if (!decimal.TryParse(TxtKgs.Text, out var kgs) || kgs <= 0)
            {
                UpdateFormHighlights();
                MessageBox.Show("Enter valid KGs (> 0).", "Validation");
                TxtKgs.Focus();
                return;
            }
            if (!decimal.TryParse(TxtPrice.Text, out var price) || price <= 0)
            {
                UpdateFormHighlights();
                MessageBox.Show("Enter valid Price/KG (> 0).", "Validation");
                TxtPrice.Focus();
                return;
            }

            var garden = CmbGarden.SelectedItem as Garden
                ?? _allGardens.FirstOrDefault(x => x.Name.Equals(CmbGarden.Text.Trim(), StringComparison.OrdinalIgnoreCase));
            var gardenName = garden?.Name ?? (string.IsNullOrWhiteSpace(CmbGarden.Text) ? string.Empty : CmbGarden.Text.Trim());
            UpdateAccountOptions();
            var account = CmbAccount.SelectedItem as Garden ?? _accountGardens.FirstOrDefault(x =>
                x.Name.Equals(CmbAccount.Text.Trim(), StringComparison.OrdinalIgnoreCase));
            if (garden != null && account == null)
            {
                MessageBox.Show("Select a valid Account from this company's gardens.", "Validation");
                CmbAccount.Focus();
                return;
            }

            var shed = CmbShed.SelectedItem as Shed
                ?? _allSheds.FirstOrDefault(x => x.Name.Equals(CmbShed.Text.Trim(), StringComparison.OrdinalIgnoreCase));
            var shedName = shed?.Name ?? (string.IsNullOrWhiteSpace(CmbShed.Text) ? null : CmbShed.Text.Trim());

            var blend = CmbBlend.SelectedItem as Blend
                ?? _allBlends.FirstOrDefault(x => x.Name.Equals(CmbBlend.Text.Trim(), StringComparison.OrdinalIgnoreCase));
            var blendName = blend?.Name ?? (string.IsNullOrWhiteSpace(CmbBlend.Text) ? null : CmbBlend.Text.Trim());

            var value  = kgs * price;

            var entry = new EntryDisplayItem
            {
                TempId = _entryBeingEdited?.TempId ?? _nextTempId++,
                BrandId = brand.BrandId,   BrandName = brand.Name,
                BrokerId = broker.BrokerId, BrokerName = broker.Name,
                LotNo = TxtLotNo.Text.Trim(),
                GardenId = garden?.GardenId, GardenName = gardenName,
                AccountGardenId = account?.GardenId, AccountName = account?.Name ?? "",
                InvoiceNo = TxtInvoiceNo.Text.Trim(),
                GradeId = grade.GradeId, GradeName = grade.Name,
                NumberOfBags = bags,
                TotalKgs = kgs,
                PricePerKg = price,
                Value = value,
                ShedId = shed?.ShedId,   ShedName = shedName,
                BlendId = blend?.BlendId, BlendName = blendName
            };

            if (_entryBeingEdited is null)
            {
                _entries.Add(entry);
            }
            else
            {
                var index = _entries.IndexOf(_entryBeingEdited);
                if (index >= 0)
                    _entries[index] = entry;

                _entryBeingEdited = null;
                BtnAddEntry.Content = "+ Add";
                BtnCancelEdit.Visibility = Visibility.Collapsed;
                PnlEditModeNotice.Visibility = Visibility.Collapsed;
                ClearAllFormHighlights();
            }

            UpdateFooterTotals();
            ClearEntryFields();
        }

        private void GridEntriesRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && row.Item is EntryDisplayItem entry)
            {
                // Don't trigger if clicked directly on an action button (Edit or Delete)
                if (e.OriginalSource is DependencyObject dep && FindVisualParent<Button>(dep) != null)
                    return;

                StartEditingEntry(entry);
            }
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent) return parent;
                child = System.Windows.Media.VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private void BtnEditEntry_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: EntryDisplayItem entry }) return;
            StartEditingEntry(entry);
        }

        private void StartEditingEntry(EntryDisplayItem entry)
        {
            _entryBeingEdited = entry;
            GridEntries.SelectedItem = entry;
            GridEntries.ScrollIntoView(entry);

            _suppressFilter = true;
            try
            {
                // Restore all items sources to full lists
                ResetSearchViews();
                CmbBrand.ItemsSource  = _allBrands;
                CmbBroker.ItemsSource = _allBrokers;
                CmbGarden.ItemsSource = _allGardens;
                CmbGrade.ItemsSource  = _allGrades;
                CmbShed.ItemsSource   = _allSheds;
                CmbBlend.ItemsSource  = _allBlends;

                // Close any open dropdowns
                CmbBrand.IsDropDownOpen  = false;
                CmbBroker.IsDropDownOpen = false;
                CmbGarden.IsDropDownOpen = false;
                CmbGrade.IsDropDownOpen  = false;
                CmbShed.IsDropDownOpen   = false;
                CmbBlend.IsDropDownOpen  = false;

                // Set selections & texts
                var brand = _allBrands.FirstOrDefault(x => x.BrandId == entry.BrandId);
                if (brand != null) { CmbBrand.SelectedItem = brand; CmbBrand.Text = brand.Name; }
                else { CmbBrand.SelectedItem = null; CmbBrand.Text = entry.BrandName ?? string.Empty; }

                var broker = _allBrokers.FirstOrDefault(x => x.BrokerId == entry.BrokerId);
                if (broker != null) { CmbBroker.SelectedItem = broker; CmbBroker.Text = broker.Name; }
                else { CmbBroker.SelectedItem = null; CmbBroker.Text = entry.BrokerName ?? string.Empty; }

                var garden = _allGardens.FirstOrDefault(x => x.GardenId == entry.GardenId);
                if (garden != null) { CmbGarden.SelectedItem = garden; CmbGarden.Text = garden.Name; }
                else { CmbGarden.SelectedItem = null; CmbGarden.Text = entry.GardenName ?? string.Empty; }
                SetAccountOptions(garden, entry.AccountGardenId ?? entry.GardenId);

                var grade = _allGrades.FirstOrDefault(x => x.GradeId == entry.GradeId);
                if (grade != null) { CmbGrade.SelectedItem = grade; CmbGrade.Text = grade.Name; }
                else { CmbGrade.SelectedItem = null; CmbGrade.Text = entry.GradeName ?? string.Empty; }

                var shed = _allSheds.FirstOrDefault(x => x.ShedId == entry.ShedId);
                if (shed != null) { CmbShed.SelectedItem = shed; CmbShed.Text = shed.Name; }
                else { CmbShed.SelectedItem = null; CmbShed.Text = entry.ShedName ?? string.Empty; }

                var blend = _allBlends.FirstOrDefault(x => x.BlendId == entry.BlendId);
                if (blend != null) { CmbBlend.SelectedItem = blend; CmbBlend.Text = blend.Name; }
                else { CmbBlend.SelectedItem = null; CmbBlend.Text = entry.BlendName ?? string.Empty; }

                TxtLotNo.Text = entry.LotNo;
                TxtInvoiceNo.Text = entry.InvoiceNo;
                TxtBags.Text = entry.NumberOfBags > 0 ? entry.NumberOfBags.ToString() : "";
                TxtKgs.Text = entry.TotalKgs > 0 ? entry.TotalKgs.ToString() : "";
                TxtPrice.Text = entry.PricePerKg > 0 ? entry.PricePerKg.ToString() : "";
                TxtValue.Text = entry.Value.ToString("N2");

                BtnAddEntry.Content = "Update Lot";
                BtnCancelEdit.Visibility = Visibility.Visible;
                PnlEditModeNotice.Visibility = Visibility.Visible;
            }
            finally
            {
                _suppressFilter = false;
            }

            UpdateFormHighlights();
            FocusFirstUnmatchedField();
        }

        private void BtnCancelEdit_Click(object sender, RoutedEventArgs e)
        {
            CancelEdit();
        }

        private void CancelEdit()
        {
            _entryBeingEdited = null;
            BtnAddEntry.Content = "+ Add";
            BtnCancelEdit.Visibility = Visibility.Collapsed;
            PnlEditModeNotice.Visibility = Visibility.Collapsed;
            ClearAllFormHighlights();
            ClearEntryFields();
        }

        private void ApplyHighlight(Control control, bool isUnmatched, string? tooltipMessage)
        {
            if (isUnmatched)
            {
                control.SetResourceReference(Control.BorderBrushProperty, "ValidationBorder");
                control.BorderThickness = new Thickness(1);
                control.SetResourceReference(Control.BackgroundProperty, "ValidationBackground");
                control.ToolTip = tooltipMessage;
            }
            else
            {
                control.ClearValue(Control.BorderBrushProperty);
                control.ClearValue(Control.BorderThicknessProperty);
                control.ClearValue(Control.BackgroundProperty);
                control.ToolTip = null;
            }
        }

        private void UpdateFormHighlights()
        {
            if (_entryBeingEdited == null)
            {
                ClearAllFormHighlights();
                return;
            }

            var brandText = CmbBrand.Text?.Trim() ?? "";
            var brandMatched = (CmbBrand.SelectedItem is Brand) ||
                (!string.IsNullOrWhiteSpace(brandText) && _allBrands.Any(x => x.Name.Equals(brandText, StringComparison.OrdinalIgnoreCase)));
            ApplyHighlight(CmbBrand, !brandMatched, "Brand is required and must match master data");

            var brokerText = CmbBroker.Text?.Trim() ?? "";
            var brokerMatched = (CmbBroker.SelectedItem is Broker) ||
                (!string.IsNullOrWhiteSpace(brokerText) && _allBrokers.Any(x => x.Name.Equals(brokerText, StringComparison.OrdinalIgnoreCase)));
            ApplyHighlight(CmbBroker, !brokerMatched, "Broker is required and must match master data");

            var lotMatched = !string.IsNullOrWhiteSpace(TxtLotNo.Text);
            ApplyHighlight(TxtLotNo, !lotMatched, "Lot No is required");

            var gardenText = CmbGarden.Text?.Trim() ?? "";
            var gardenUnmatched = !string.IsNullOrWhiteSpace(gardenText) &&
                (CmbGarden.SelectedItem is not Garden && !_allGardens.Any(x => x.Name.Equals(gardenText, StringComparison.OrdinalIgnoreCase)));
            ApplyHighlight(CmbGarden, gardenUnmatched, "Garden does not match any existing Garden in master data");

            var gradeText = CmbGrade.Text?.Trim() ?? "";
            var gradeMatched = (CmbGrade.SelectedItem is Grade) ||
                (!string.IsNullOrWhiteSpace(gradeText) && _allGrades.Any(x => x.Name.Equals(gradeText, StringComparison.OrdinalIgnoreCase)));
            ApplyHighlight(CmbGrade, !gradeMatched, "Grade is required and must match master data");

            var bagsValid = int.TryParse(TxtBags.Text, out var bags) && bags > 0;
            ApplyHighlight(TxtBags, !bagsValid, "Valid number of bags (> 0) is required");

            var kgsValid = decimal.TryParse(TxtKgs.Text, out var kgs) && kgs > 0;
            ApplyHighlight(TxtKgs, !kgsValid, "Valid total KGs (> 0) is required");

            var priceValid = decimal.TryParse(TxtPrice.Text, out var price) && price > 0;
            ApplyHighlight(TxtPrice, !priceValid, "Valid price per KG (> 0) is required");

            var shedText = CmbShed.Text?.Trim() ?? "";
            var shedUnmatched = !string.IsNullOrWhiteSpace(shedText) &&
                (CmbShed.SelectedItem is not Shed && !_allSheds.Any(x => x.Name.Equals(shedText, StringComparison.OrdinalIgnoreCase)));
            ApplyHighlight(CmbShed, shedUnmatched, "Shed does not match master data");

            var blendText = CmbBlend.Text?.Trim() ?? "";
            var blendUnmatched = !string.IsNullOrWhiteSpace(blendText) &&
                (CmbBlend.SelectedItem is not Blend && !_allBlends.Any(x => x.Name.Equals(blendText, StringComparison.OrdinalIgnoreCase)));
            ApplyHighlight(CmbBlend, blendUnmatched, "Blend does not match master data");
        }

        private void ClearAllFormHighlights()
        {
            ApplyHighlight(CmbBrand, false, null);
            ApplyHighlight(CmbBroker, false, null);
            ApplyHighlight(TxtLotNo, false, null);
            ApplyHighlight(CmbGarden, false, null);
            ApplyHighlight(CmbGrade, false, null);
            ApplyHighlight(TxtBags, false, null);
            ApplyHighlight(TxtKgs, false, null);
            ApplyHighlight(TxtPrice, false, null);
            ApplyHighlight(CmbShed, false, null);
            ApplyHighlight(CmbBlend, false, null);
        }

        private void FocusFirstUnmatchedField()
        {
            if (_entryBeingEdited == null) return;

            if (_entryBeingEdited.IsBrandUnmatched)
                CmbBrand.Focus();
            else if (_entryBeingEdited.IsBrokerUnmatched)
                CmbBroker.Focus();
            else if (_entryBeingEdited.IsLotNoUnmatched)
                TxtLotNo.Focus();
            else if (_entryBeingEdited.IsGardenUnmatched)
                CmbGarden.Focus();
            else if (_entryBeingEdited.IsGradeUnmatched)
                CmbGrade.Focus();
            else if (_entryBeingEdited.IsBagsUnmatched)
                TxtBags.Focus();
            else if (_entryBeingEdited.IsKgsUnmatched)
                TxtKgs.Focus();
            else if (_entryBeingEdited.IsPriceUnmatched)
                TxtPrice.Focus();
            else if (_entryBeingEdited.IsShedUnmatched)
                CmbShed.Focus();
            else if (_entryBeingEdited.IsBlendUnmatched)
                CmbBlend.Focus();
            else
                TxtLotNo.Focus();
        }

        private void BtnRemoveEntry_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is EntryDisplayItem item)
            {
                if (ReferenceEquals(item, _entryBeingEdited))
                {
                    CancelEdit();
                }
                _entries.Remove(item);
                UpdateFooterTotals();
            }
        }

        private void UpdateFooterTotals()
        {
            TxtEntryCount.Text = $"{_entries.Count} entr{(_entries.Count == 1 ? "y" : "ies")}";
            TxtTotalBagsFooter.Text = _entries.Sum(x => x.NumberOfBags).ToString("N0");
            TxtTotalKgsFooter.Text = _entries.Sum(x => x.TotalKgs).ToString("N2");
            TxtTotalValueFooter.Text = "৳" + _entries.Sum(x => x.Value).ToString("N2");
        }

        private async void BtnSaveSale_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtSaleNo.Text)) { MessageBox.Show("Enter a Sale No.", "Validation"); return; }
            if (DtpSaleDate.SelectedDate == null) { MessageBox.Show("Select a Sale Date.", "Validation"); return; }
            if (!_entries.Any()) { MessageBox.Show("Add at least one auction entry.", "Validation"); return; }

            try
            {
                BtnSaveSale.IsEnabled = false;
                var saleRepo = _services.GetRequiredService<SaleRepository>();
                var sale = new Sale
                {
                    SaleId = _saleBeingEdited?.SaleId ?? 0,
                    SaleNo = TxtSaleNo.Text.Trim(),
                    SaleDate = DtpSaleDate.SelectedDate!.Value,
                    Notes = TxtNotes.Text.Trim(),
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    Entries = _entries.Select(ei => new AuctionEntry
                    {
                        BrandId = ei.BrandId,
                        BrokerId = ei.BrokerId,
                        LotNo = ei.LotNo,
                        GardenId = ei.GardenId,
                        AccountGardenId = ei.AccountGardenId ?? ei.GardenId,
                        InvoiceNo = ei.InvoiceNo,
                        GradeId = ei.GradeId,
                        NumberOfBags = ei.NumberOfBags,
                        TotalKgs = ei.TotalKgs,
                        PricePerKg = ei.PricePerKg,
                        Value = ei.Value,
                        ShedId = ei.ShedId,
                        BlendId = ei.BlendId,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    }).ToList()
                };

                if (_saleBeingEdited is null)
                    await saleRepo.AddAsync(sale);
                else
                    await saleRepo.UpdateAsync(sale);

                var action = _saleBeingEdited is null ? "saved" : "updated";
                var details = _saleBeingEdited is null
                    ? "Existing lots were kept, missing fields filled, and new lots added."
                    : $"{_entries.Count} entries saved.";
                MessageBox.Show($"Sale '{sale.SaleNo}' {action} successfully!\n{details}", "Success",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                ClearAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving sale: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnSaveSale.IsEnabled = true;
            }
        }

        private void BtnClearSale_Click(object sender, RoutedEventArgs e)
        {
            if (_entries.Any() &&
                MessageBox.Show("Clear all entries?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                return;
            ClearAll();
        }

        private void BtnDownloadTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                FileName = "Sale_Upload_Template.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using var wb = new XLWorkbook();
                    var ws = wb.Worksheets.Add("Auction Entries");
                    
                    var headers = new[] { "Brand", "Broker", "Lot No", "Garden", "Invoice", "Grade", "Bags", "KGs", "Price", "Shed", "Blend", "Account" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        ws.Cell(1, i + 1).Value = headers[i];
                        ws.Cell(1, i + 1).Style.Font.Bold = true;
                        ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                    }

                    // Sample data
                    ws.Cell(2, 1).Value = "Sample Brand";
                    ws.Cell(2, 2).Value = "Sample Broker";
                    ws.Cell(2, 3).Value = "LOT-001";
                    ws.Cell(2, 4).Value = "Sample Garden";
                    ws.Cell(2, 5).Value = "INV-001";
                    ws.Cell(2, 6).Value = "BOP";
                    ws.Cell(2, 7).Value = 20;
                    ws.Cell(2, 8).Value = 1100.50;
                    ws.Cell(2, 9).Value = 250.00;
                    ws.Cell(2, 10).Value = "Shed-1";
                    ws.Cell(2, 11).Value = "";

                    ws.Columns().AdjustToContents();
                    wb.SaveAs(dialog.FileName);
                    MessageBox.Show("Template downloaded successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error creating template: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnUploadExcel_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                Title = "Select Sale Upload File"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                BtnUploadExcel.IsEnabled = false;

                var brandRepo = _services.GetRequiredService<BrandRepository>();
                var brokerRepo = _services.GetRequiredService<BrokerRepository>();
                var gardenRepo = _services.GetRequiredService<GardenRepository>();
                var gradeRepo = _services.GetRequiredService<GradeRepository>();
                var shedRepo = _services.GetRequiredService<ShedRepository>();
                var blendRepo = _services.GetRequiredService<BlendRepository>();

                var brands = await brandRepo.GetActiveAsync();
                var brokers = await brokerRepo.GetActiveAsync();
                var gardens = await gardenRepo.GetActiveAsync();
                var grades = await gradeRepo.GetActiveAsync();
                var sheds = await shedRepo.GetActiveAsync();
                var blends = await blendRepo.GetActiveAsync();

                using var wb = new XLWorkbook(dialog.FileName);
                var ws = wb.Worksheets.First();
                var rows = ws.RowsUsed().Skip(1); // Skip header

                int added = 0;
                int updated = 0;
                int skipped = 0;
                int highlighted = 0;
                var rowErrors = new List<string>();

                foreach (var row in rows)
                {
                    try
                    {
                        var brandName = row.Cell(1).GetString().Trim();
                        var brokerName = row.Cell(2).GetString().Trim();
                        var lotNo = row.Cell(3).GetString().Trim();
                        var gardenName = row.Cell(4).GetString().Trim();
                        var invoice = row.Cell(5).GetString().Trim();
                        var gradeName = row.Cell(6).GetString().Trim();
                        var bagsStr = row.Cell(7).GetString();
                        var kgsStr = row.Cell(8).GetString();
                        var priceStr = row.Cell(9).GetString();
                        var shedName = row.Cell(10).GetString().Trim();
                        var blendName = row.Cell(11).GetString().Trim();
                        var accountName = row.Cell(12).GetString().Trim();

                        // Skip completely empty rows
                        if (string.IsNullOrEmpty(brandName) && string.IsNullOrEmpty(brokerName) &&
                            string.IsNullOrEmpty(lotNo) && string.IsNullOrEmpty(gardenName) &&
                            string.IsNullOrEmpty(gradeName) && string.IsNullOrEmpty(bagsStr) &&
                            string.IsNullOrEmpty(kgsStr) && string.IsNullOrEmpty(priceStr))
                            continue;

                        // Try to match master data — leave null if no match (row will be highlighted)
                        var brand = string.IsNullOrEmpty(brandName) ? null
                            : brands.FirstOrDefault(b => b.Name.Equals(brandName, StringComparison.OrdinalIgnoreCase));
                        var broker = string.IsNullOrEmpty(brokerName) ? null
                            : brokers.FirstOrDefault(b => b.Name.Equals(brokerName, StringComparison.OrdinalIgnoreCase));
                        var garden = string.IsNullOrEmpty(gardenName) ? null
                            : gardens.FirstOrDefault(g => g.Name.Equals(gardenName, StringComparison.OrdinalIgnoreCase));
                        var account = string.IsNullOrWhiteSpace(accountName) ? garden
                            : gardens.FirstOrDefault(g => g.Name.Equals(accountName, StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrWhiteSpace(accountName) && (garden == null || account == null ||
                            !EligibleAccounts(garden).Any(x => x.GardenId == account.GardenId)))
                            throw new InvalidOperationException($"Account '{accountName}' must be a garden in the selected garden's company.");
                        var grade = string.IsNullOrEmpty(gradeName) ? null
                            : grades.FirstOrDefault(g => g.Name.Equals(gradeName, StringComparison.OrdinalIgnoreCase));
                        var shed = string.IsNullOrEmpty(shedName) ? null
                            : sheds.FirstOrDefault(s => s.Name.Equals(shedName, StringComparison.OrdinalIgnoreCase));
                        var blend = string.IsNullOrEmpty(blendName) ? null
                            : blends.FirstOrDefault(b => b.Name.Equals(blendName, StringComparison.OrdinalIgnoreCase));

                        int.TryParse(bagsStr, out int bags);
                        decimal.TryParse(kgsStr, out decimal kgs);
                        decimal.TryParse(priceStr, out decimal price);

                        var entry = new EntryDisplayItem
                        {
                            TempId = _nextTempId++,
                            BrandId = brand?.BrandId,
                            BrandName = brand != null ? brand.Name : brandName,
                            BrokerId = broker?.BrokerId,
                            BrokerName = broker != null ? broker.Name : brokerName,
                            LotNo = lotNo,
                            GardenId = garden?.GardenId,
                            GardenName = garden != null ? garden.Name : gardenName,
                            AccountGardenId = account?.GardenId,
                            AccountName = account?.Name ?? "",
                            InvoiceNo = invoice,
                            GradeId = grade?.GradeId,
                            GradeName = grade != null ? grade.Name : gradeName,
                            NumberOfBags = bags,
                            TotalKgs = kgs,
                            PricePerKg = price,
                            Value = kgs * price,
                            ShedId = shed?.ShedId,
                            ShedName = shed?.Name ?? (string.IsNullOrEmpty(shedName) ? null : shedName),
                            BlendId = blend?.BlendId,
                            BlendName = blend?.Name ?? (string.IsNullOrEmpty(blendName) ? null : blendName)
                        };

                        var candidates = _entries.Select(ToAuctionEntry).ToList();
                        var match = AuctionEntryMerge.FindMatch(candidates, ToAuctionEntry(entry));
                        if (match != null)
                        {
                            var existing = _entries[candidates.IndexOf(match)];
                            if (FillMissingUploadFields(existing, entry)) updated++;
                            else skipped++;
                        }
                        else
                        {
                            _entries.Add(entry);
                            added++;
                        }
                    }
                    catch (Exception ex)
                    {
                        rowErrors.Add($"Row {row.RowNumber()}: {ex.Message}");
                    }
                }

                UpdateFooterTotals();

                highlighted = _entries.Count(entry => entry.HasEmptyField);
                var msg = $"Added {added} new entries.\nUpdated {updated} entries with missing fields.\nSkipped {skipped} existing entries.";
                if (rowErrors.Count > 0)
                    msg += $"\n\n{rowErrors.Count} row(s) could not be imported:\n" + string.Join("\n", rowErrors.Take(5));
                if (highlighted > 0)
                    msg += $"\n\n⚠️ {highlighted} row(s) have empty or unmatched fields.\nUnmatched items in each row are highlighted in red.\n\nDouble-click any row to edit and fill them in.";

                MessageBox.Show(msg, "Upload Complete", MessageBoxButton.OK,
                    highlighted > 0 || rowErrors.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading Excel file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnUploadExcel.IsEnabled = true;
            }
        }

        private static AuctionEntry ToAuctionEntry(EntryDisplayItem item) => new AuctionEntry
        {
            BrandId = item.BrandId, BrokerId = item.BrokerId, LotNo = item.LotNo,
            GardenId = item.GardenId, InvoiceNo = item.InvoiceNo, GradeId = item.GradeId,
            AccountGardenId = item.AccountGardenId,
            NumberOfBags = item.NumberOfBags, TotalKgs = item.TotalKgs,
            PricePerKg = item.PricePerKg, Value = item.Value,
            ShedId = item.ShedId, BlendId = item.BlendId
        };

        private static bool FillMissingUploadFields(EntryDisplayItem existing, EntryDisplayItem incoming)
        {
            var merged = ToAuctionEntry(existing);
            bool changed = AuctionEntryMerge.FillMissing(merged, ToAuctionEntry(incoming));
            if (!existing.AccountGardenId.HasValue && merged.AccountGardenId.HasValue) existing.AccountName = incoming.AccountName;
            existing.AccountGardenId = merged.AccountGardenId;
            if (!existing.BrandId.HasValue && merged.BrandId.HasValue) existing.BrandName = incoming.BrandName;
            if (!existing.BrokerId.HasValue && merged.BrokerId.HasValue) existing.BrokerName = incoming.BrokerName;
            if (!existing.GardenId.HasValue && merged.GardenId.HasValue) existing.GardenName = incoming.GardenName;
            if (!existing.GradeId.HasValue && merged.GradeId.HasValue) existing.GradeName = incoming.GradeName;
            if (!existing.ShedId.HasValue && merged.ShedId.HasValue) existing.ShedName = incoming.ShedName;
            if (!existing.BlendId.HasValue && merged.BlendId.HasValue) existing.BlendName = incoming.BlendName;
            if (string.IsNullOrWhiteSpace(existing.BrandName) && !string.IsNullOrWhiteSpace(incoming.BrandName)) { existing.BrandName = incoming.BrandName; changed = true; }
            if (string.IsNullOrWhiteSpace(existing.BrokerName) && !string.IsNullOrWhiteSpace(incoming.BrokerName)) { existing.BrokerName = incoming.BrokerName; changed = true; }
            if (string.IsNullOrWhiteSpace(existing.GardenName) && !string.IsNullOrWhiteSpace(incoming.GardenName)) { existing.GardenName = incoming.GardenName; changed = true; }
            if (string.IsNullOrWhiteSpace(existing.GradeName) && !string.IsNullOrWhiteSpace(incoming.GradeName)) { existing.GradeName = incoming.GradeName; changed = true; }
            if (string.IsNullOrWhiteSpace(existing.ShedName) && !string.IsNullOrWhiteSpace(incoming.ShedName)) { existing.ShedName = incoming.ShedName; changed = true; }
            if (string.IsNullOrWhiteSpace(existing.BlendName) && !string.IsNullOrWhiteSpace(incoming.BlendName)) { existing.BlendName = incoming.BlendName; changed = true; }
            existing.BrandId = merged.BrandId;
            existing.BrokerId = merged.BrokerId;
            existing.GardenId = merged.GardenId;
            existing.GradeId = merged.GradeId;
            existing.ShedId = merged.ShedId;
            existing.BlendId = merged.BlendId;
            existing.LotNo = merged.LotNo;
            existing.InvoiceNo = merged.InvoiceNo ?? "";
            existing.NumberOfBags = merged.NumberOfBags;
            existing.TotalKgs = merged.TotalKgs;
            existing.PricePerKg = merged.PricePerKg;
            existing.Value = merged.Value;
            return changed;
        }

        private void ClearAll()
        {
            TxtSaleNo.Text = "";
            DtpSaleDate.SelectedDate = DateTime.Today;
            TxtNotes.Text = "";
            _entries.Clear();
            UpdateFooterTotals();
            _nextTempId = 1;
            _entryBeingEdited = null;
            BtnAddEntry.Content = "+ Add";
            BtnCancelEdit.Visibility = Visibility.Collapsed;
            PnlEditModeNotice.Visibility = Visibility.Collapsed;
            ClearAllFormHighlights();
            ClearEntryFields();
        }

        private void LoadSaleForEditing()
        {
            if (_saleBeingEdited is null) return;

            TxtPageTitle.Text = $"Edit Sale: {_saleBeingEdited.SaleNo}";
            TxtPageSubtitle.Text = "Edit the sale header or any auction lot, then save all changes.";
            BtnSaveSale.Content = "Save Changes";
            TxtSaleNo.Text = _saleBeingEdited.SaleNo;
            DtpSaleDate.SelectedDate = _saleBeingEdited.SaleDate;
            TxtNotes.Text = _saleBeingEdited.Notes ?? string.Empty;

            foreach (var entry in _saleBeingEdited.Entries)
            {
                var brand = CmbBrand.Items.OfType<Brand>().FirstOrDefault(x => x.BrandId == entry.BrandId);
                var broker = CmbBroker.Items.OfType<Broker>().FirstOrDefault(x => x.BrokerId == entry.BrokerId);
                var garden = CmbGarden.Items.OfType<Garden>().FirstOrDefault(x => x.GardenId == entry.GardenId);
                var grade = CmbGrade.Items.OfType<Grade>().FirstOrDefault(x => x.GradeId == entry.GradeId);
                var shed = CmbShed.Items.OfType<Shed>().FirstOrDefault(x => x.ShedId == entry.ShedId);
                var blend = CmbBlend.Items.OfType<Blend>().FirstOrDefault(x => x.BlendId == entry.BlendId);
                _entries.Add(new EntryDisplayItem
                {
                    TempId = _nextTempId++,
                    BrandId = entry.BrandId, BrandName = brand?.Name ?? $"Brand #{entry.BrandId}",
                    BrokerId = entry.BrokerId, BrokerName = broker?.Name ?? $"Broker #{entry.BrokerId}",
                    LotNo = entry.LotNo,
                    GardenId = entry.GardenId, GardenName = garden?.Name ?? $"Garden #{entry.GardenId}",
                    AccountGardenId = entry.AccountGardenId ?? entry.GardenId,
                    AccountName = _allGardens.FirstOrDefault(x => x.GardenId == (entry.AccountGardenId ?? entry.GardenId))?.Name ?? "",
                    InvoiceNo = entry.InvoiceNo ?? string.Empty,
                    GradeId = entry.GradeId, GradeName = grade?.Name ?? $"Grade #{entry.GradeId}",
                    NumberOfBags = entry.NumberOfBags,
                    TotalKgs = entry.TotalKgs,
                    PricePerKg = entry.PricePerKg,
                    Value = entry.TotalKgs * entry.PricePerKg,
                    ShedId = entry.ShedId, ShedName = shed?.Name,
                    BlendId = entry.BlendId, BlendName = blend?.Name
                });
            }
            UpdateFooterTotals();
        }

        private void ClearEntryFields()
        {
            _suppressFilter = true;
            try
            {
                ResetSearchViews();
                TxtLotNo.Text    = "";
                TxtInvoiceNo.Text = "";
                TxtBags.Text     = "";
                TxtKgs.Text      = "";
                TxtPrice.Text    = "";
                TxtValue.Text    = "0.00";

                // Clear selections, text values, and restore full lists
                CmbBrand.ItemsSource  = _allBrands;  CmbBrand.SelectedItem  = null; CmbBrand.Text  = "";
                CmbBroker.ItemsSource = _allBrokers; CmbBroker.SelectedItem = null; CmbBroker.Text = "";
                CmbGarden.ItemsSource = _allGardens; CmbGarden.SelectedItem = null; CmbGarden.Text = "";
                _accountOptionsGardenId = null;
                _accountGardens = new();
                CmbAccount.ItemsSource = _accountGardens; CmbAccount.SelectedItem = null; CmbAccount.Text = ""; CmbAccount.IsEnabled = false;
                CmbGrade.ItemsSource  = _allGrades;  CmbGrade.SelectedItem  = null; CmbGrade.Text  = "";
                CmbShed.ItemsSource   = _allSheds;   CmbShed.SelectedItem   = null; CmbShed.Text   = "";
                CmbBlend.ItemsSource  = _allBlends;  CmbBlend.SelectedItem  = null; CmbBlend.Text  = "";

                CmbBrand.IsDropDownOpen  = false;
                CmbBroker.IsDropDownOpen = false;
                CmbGarden.IsDropDownOpen = false;
                CmbGrade.IsDropDownOpen  = false;
                CmbShed.IsDropDownOpen   = false;
                CmbBlend.IsDropDownOpen  = false;
            }
            finally
            {
                _suppressFilter = false;
            }

            if (_entryBeingEdited == null)
            {
                ClearAllFormHighlights();
            }

            TxtLotNo.Focus();
        }
    }
}
