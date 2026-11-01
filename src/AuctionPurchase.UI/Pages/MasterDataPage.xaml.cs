using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AuctionPurchase.Core.Models;
using AuctionPurchase.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace AuctionPurchase.UI.Pages
{
    // Generic display row
    public class MasterDataRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? FullName { get; set; }
        public string? Address { get; set; }
        public string? District { get; set; }
        public double? Acre { get; set; }
        public string? GardenType { get; set; }
        public string? MapLink { get; set; }
        public bool IsActive { get; set; }
        public string IsActiveDisplay => IsActive ? "Active" : "Inactive";
        public int SortOrder { get; set; }
    }

    public partial class MasterDataPage : Page
    {
        private readonly IServiceProvider _services;
        private readonly string _entity;
        private int? _editingId = null;
        private List<MasterDataRow> _allRows = new();

        public MasterDataPage(IServiceProvider services, string entity)
        {
            _services = services;
            _entity = entity;
            InitializeComponent();
            TxtPageTitle.Text = entity;
            Loaded += (_, _) =>
            {
                ConfigureUI();
                _ = LoadDataAsync();
            };
        }

        private void ConfigureUI()
        {
            // Hide all optional columns/panels initially
            ColDescription.Visibility = Visibility.Collapsed;
            ColFullName.Visibility = Visibility.Collapsed;
            ColAddress.Visibility = Visibility.Collapsed;
            ColDistrict.Visibility = Visibility.Collapsed;
            ColAcre.Visibility = Visibility.Collapsed;
            ColGardenType.Visibility = Visibility.Collapsed;
            ColMapLink.Visibility = Visibility.Collapsed;

            PanelDescription.Visibility = Visibility.Collapsed;
            PanelFullName.Visibility = Visibility.Collapsed;
            PanelAddress.Visibility = Visibility.Collapsed;
            PanelDistrict.Visibility = Visibility.Collapsed;
            PanelAcre.Visibility = Visibility.Collapsed;
            PanelGardenType.Visibility = Visibility.Collapsed;
            PanelMapLink.Visibility = Visibility.Collapsed;

            switch (_entity)
            {
                case "Brands":
                    ColDescription.Visibility = Visibility.Visible;
                    PanelDescription.Visibility = Visibility.Visible;
                    break;

                case "Brokers":
                    ColFullName.Visibility = Visibility.Visible;
                    ColAddress.Visibility = Visibility.Visible;
                    PanelFullName.Visibility = Visibility.Visible;
                    LblFullName.Text = "FULL NAME";
                    PanelAddress.Visibility = Visibility.Visible;
                    break;

                case "Gardens":
                    ColFullName.Visibility = Visibility.Visible;
                    ColAddress.Visibility = Visibility.Visible;
                    ColDistrict.Visibility = Visibility.Visible;
                    ColAcre.Visibility = Visibility.Visible;
                    ColGardenType.Visibility = Visibility.Visible;
                    ColMapLink.Visibility = Visibility.Visible;
                    PanelFullName.Visibility = Visibility.Visible;
                    LblFullName.Text = "FULL NAME (Bangla Font)";
                    PanelAddress.Visibility = Visibility.Visible;
                    PanelDistrict.Visibility = Visibility.Visible;
                    PanelAcre.Visibility = Visibility.Visible;
                    PanelGardenType.Visibility = Visibility.Visible;
                    PanelMapLink.Visibility = Visibility.Visible;
                    break;

                case "Sheds":
                    ColAddress.Visibility = Visibility.Visible;
                    PanelAddress.Visibility = Visibility.Visible;
                    break;
            }
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var rows = await GetAllRowsAsync();
                _allRows = rows.OrderBy(r => r.Id).ToList();
                FilterData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading {_entity}: {ex.Message}", "Error");
            }
        }

        private void FilterData()
        {
            var query = TxtSearch.Text?.Trim().ToLower() ?? "";
            if (string.IsNullOrEmpty(query))
            {
                GridData.ItemsSource = _allRows;
            }
            else
            {
                GridData.ItemsSource = _allRows.Where(r =>
                    r.Name.ToLower().Contains(query) ||
                    (r.Description != null && r.Description.ToLower().Contains(query)) ||
                    (r.FullName != null && r.FullName.ToLower().Contains(query)) ||
                    (r.Address != null && r.Address.ToLower().Contains(query)) ||
                    (r.District != null && r.District.ToLower().Contains(query))
                ).ToList();
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterData();
        }

        private async Task<List<MasterDataRow>> GetAllRowsAsync()
        {
            return _entity switch
            {
                "Brands" => (await _services.GetRequiredService<BrandRepository>().GetAllAsync())
                    .Select(x => new MasterDataRow { Id = x.BrandId, Name = x.Name, Description = x.Description, IsActive = x.IsActive, SortOrder = x.SortOrder }).ToList(),
                "Brokers" => (await _services.GetRequiredService<BrokerRepository>().GetAllAsync())
                    .Select(x => new MasterDataRow { Id = x.BrokerId, Name = x.Name, FullName = x.FullName, Address = x.Address, IsActive = x.IsActive, SortOrder = x.SortOrder }).ToList(),
                "Gardens" => (await _services.GetRequiredService<GardenRepository>().GetAllAsync())
                    .Select(x => new MasterDataRow { Id = x.GardenId, Name = x.Name, FullName = x.FullName, Address = x.Address, District = x.District, Acre = x.Acre, GardenType = x.GardenType, MapLink = x.MapLink, IsActive = x.IsActive, SortOrder = x.SortOrder }).ToList(),
                "Grades" => (await _services.GetRequiredService<GradeRepository>().GetAllAsync())
                    .Select(x => new MasterDataRow { Id = x.GradeId, Name = x.Name, IsActive = x.IsActive, SortOrder = x.SortOrder }).ToList(),
                "Sheds" => (await _services.GetRequiredService<ShedRepository>().GetAllAsync())
                    .Select(x => new MasterDataRow { Id = x.ShedId, Name = x.Name, Address = x.Address, IsActive = x.IsActive, SortOrder = x.SortOrder }).ToList(),
                "Blends" => (await _services.GetRequiredService<BlendRepository>().GetAllAsync())
                    .Select(x => new MasterDataRow { Id = x.BlendId, Name = x.Name, IsActive = x.IsActive, SortOrder = x.SortOrder }).ToList(),
                _ => new List<MasterDataRow>()
            };
        }

        private void BtnAddNew_Click(object sender, RoutedEventArgs e)
        {
            _editingId = null;
            TxtEditName.Text = "";
            TxtEditDescription.Text = "";
            TxtEditFullName.Text = "";
            TxtEditAddress.Text = "";
            TxtEditDistrict.Text = "";
            TxtEditAcre.Text = "";
            TxtEditGardenType.Text = "";
            TxtEditMapLink.Text = "";
            PopupTitle.Text = $"Add New {_entity}";
            PopupTransform.X = 0;
            PopupTransform.Y = 0;
            PopupOverlay.Visibility = Visibility.Visible;
            TxtEditName.Focus();
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MasterDataRow row)
            {
                _editingId = row.Id;
                TxtEditName.Text = row.Name;
                TxtEditDescription.Text = row.Description ?? "";
                TxtEditFullName.Text = row.FullName ?? "";
                TxtEditAddress.Text = row.Address ?? "";
                TxtEditDistrict.Text = row.District ?? "";
                TxtEditAcre.Text = row.Acre?.ToString() ?? "";
                TxtEditGardenType.Text = row.GardenType ?? "";
                TxtEditMapLink.Text = row.MapLink ?? "";
                PopupTitle.Text = $"Edit {_entity}";
                PopupTransform.X = 0;
                PopupTransform.Y = 0;
                PopupOverlay.Visibility = Visibility.Visible;
                TxtEditName.Focus();
            }
        }

        private void BtnCancelEdit_Click(object sender, RoutedEventArgs e)
        {
            PopupOverlay.Visibility = Visibility.Collapsed;
            _editingId = null;
        }

        private async void BtnSaveItem_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtEditName.Text)) { MessageBox.Show("Name is required.", "Validation"); return; }

            try
            {
                BtnSaveItem.IsEnabled = false;
                var name = TxtEditName.Text.Trim();
                var description = string.IsNullOrWhiteSpace(TxtEditDescription.Text) ? null : TxtEditDescription.Text.Trim();
                var fullName = string.IsNullOrWhiteSpace(TxtEditFullName.Text) ? null : TxtEditFullName.Text.Trim();
                var address = string.IsNullOrWhiteSpace(TxtEditAddress.Text) ? null : TxtEditAddress.Text.Trim();
                var district = string.IsNullOrWhiteSpace(TxtEditDistrict.Text) ? null : TxtEditDistrict.Text.Trim();
                double? acre = double.TryParse(TxtEditAcre.Text.Trim(), out var parsedAcre) ? parsedAcre : (double?)null;
                var gardenType = string.IsNullOrWhiteSpace(TxtEditGardenType.Text) ? null : TxtEditGardenType.Text.Trim();
                var googleLink = string.IsNullOrWhiteSpace(TxtEditMapLink.Text) ? null : TxtEditMapLink.Text.Trim();

                if (_editingId == null)
                    await AddItemAsync(name, description, fullName, address, district, acre, gardenType, googleLink);
                else
                    await UpdateItemAsync(_editingId.Value, name, description, fullName, address, district, acre, gardenType, googleLink);

                PopupOverlay.Visibility = Visibility.Collapsed;
                _editingId = null;
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving: {ex.Message}", "Error");
            }
            finally { BtnSaveItem.IsEnabled = true; }
        }

        private async Task AddItemAsync(string name, string? description, string? fullName, string? address, string? district, double? acre, string? gardenType, string? mapLink)
        {
            switch (_entity)
            {
                case "Brands":
                    await _services.GetRequiredService<BrandRepository>().AddAsync(new Brand { Name = name, Description = description, IsActive = true });
                    break;
                case "Brokers":
                    await _services.GetRequiredService<BrokerRepository>().AddAsync(new Broker { Name = name, FullName = fullName, Address = address, IsActive = true });
                    break;
                case "Gardens":
                    await _services.GetRequiredService<GardenRepository>().AddAsync(new Garden { Name = name, FullName = fullName, Address = address, District = district, Acre = acre, GardenType = gardenType, MapLink = mapLink, IsActive = true });
                    break;
                case "Grades":
                    await _services.GetRequiredService<GradeRepository>().AddAsync(new Grade { Name = name, IsActive = true });
                    break;
                case "Sheds":
                    await _services.GetRequiredService<ShedRepository>().AddAsync(new Shed { Name = name, Address = address, IsActive = true });
                    break;
                case "Blends":
                    await _services.GetRequiredService<BlendRepository>().AddAsync(new Blend { Name = name, IsActive = true });
                    break;
            }
        }

        private async Task UpdateItemAsync(int id, string name, string? description, string? fullName, string? address, string? district, double? acre, string? gardenType, string? mapLink)
        {
            switch (_entity)
            {
                case "Brands":
                    await _services.GetRequiredService<BrandRepository>().UpdateAsync(new Brand { BrandId = id, Name = name, Description = description, IsActive = true });
                    break;
                case "Brokers":
                    await _services.GetRequiredService<BrokerRepository>().UpdateAsync(new Broker { BrokerId = id, Name = name, FullName = fullName, Address = address, IsActive = true });
                    break;
                case "Gardens":
                    await _services.GetRequiredService<GardenRepository>().UpdateAsync(new Garden { GardenId = id, Name = name, FullName = fullName, Address = address, District = district, Acre = acre, GardenType = gardenType, MapLink = mapLink, IsActive = true });
                    break;
                case "Grades":
                    await _services.GetRequiredService<GradeRepository>().UpdateAsync(new Grade { GradeId = id, Name = name, IsActive = true });
                    break;
                case "Sheds":
                    await _services.GetRequiredService<ShedRepository>().UpdateAsync(new Shed { ShedId = id, Name = name, Address = address, IsActive = true });
                    break;
                case "Blends":
                    await _services.GetRequiredService<BlendRepository>().UpdateAsync(new Blend { BlendId = id, Name = name, IsActive = true });
                    break;
            }
        }

        private async void BtnToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MasterDataRow row)
            {
                try
                {
                    if (row.IsActive) await DeactivateAsync(row.Id);
                    else await ActivateAsync(row);
                    await LoadDataAsync();
                }
                catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error"); }
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: MasterDataRow row }) return;

            var confirmation = MessageBox.Show(
                $"Permanently delete '{row.Name}'? This cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirmation != MessageBoxResult.Yes) return;

            try
            {
                var deleted = await DeletePermanentlyAsync(row.Id);
                if (!deleted)
                    MessageBox.Show("The item could not be deleted.", "Delete", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"This item could not be deleted because it is in use or an error occurred.\n\n{ex.Message}",
                    "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task DeactivateAsync(int id)
        {
            switch (_entity)
            {
                case "Brands": await _services.GetRequiredService<BrandRepository>().DeleteAsync(id); break;
                case "Brokers": await _services.GetRequiredService<BrokerRepository>().DeleteAsync(id); break;
                case "Gardens": await _services.GetRequiredService<GardenRepository>().DeleteAsync(id); break;
                case "Grades": await _services.GetRequiredService<GradeRepository>().DeleteAsync(id); break;
                case "Sheds": await _services.GetRequiredService<ShedRepository>().DeleteAsync(id); break;
                case "Blends": await _services.GetRequiredService<BlendRepository>().DeleteAsync(id); break;
            }
        }

        private Task<bool> DeletePermanentlyAsync(int id) => _entity switch
        {
            "Brands" => _services.GetRequiredService<BrandRepository>().DeletePermanentlyAsync(id),
            "Brokers" => _services.GetRequiredService<BrokerRepository>().DeletePermanentlyAsync(id),
            "Gardens" => _services.GetRequiredService<GardenRepository>().DeletePermanentlyAsync(id),
            "Grades" => _services.GetRequiredService<GradeRepository>().DeletePermanentlyAsync(id),
            "Sheds" => _services.GetRequiredService<ShedRepository>().DeletePermanentlyAsync(id),
            "Blends" => _services.GetRequiredService<BlendRepository>().DeletePermanentlyAsync(id),
            _ => Task.FromResult(false)
        };

        private async Task ActivateAsync(MasterDataRow row)
        {
            switch (_entity)
            {
                case "Brands":
                    await _services.GetRequiredService<BrandRepository>().UpdateAsync(new Brand { BrandId = row.Id, Name = row.Name, Description = row.Description, IsActive = true, SortOrder = row.SortOrder });
                    break;
                case "Brokers":
                    await _services.GetRequiredService<BrokerRepository>().UpdateAsync(new Broker { BrokerId = row.Id, Name = row.Name, FullName = row.FullName, Address = row.Address, IsActive = true, SortOrder = row.SortOrder });
                    break;
                case "Gardens":
                    await _services.GetRequiredService<GardenRepository>().UpdateAsync(new Garden { GardenId = row.Id, Name = row.Name, FullName = row.FullName, Address = row.Address, District = row.District, Acre = row.Acre, GardenType = row.GardenType, MapLink = row.MapLink, IsActive = true, SortOrder = row.SortOrder });
                    break;
                case "Grades":
                    await _services.GetRequiredService<GradeRepository>().UpdateAsync(new Grade { GradeId = row.Id, Name = row.Name, IsActive = true, SortOrder = row.SortOrder });
                    break;
                case "Sheds":
                    await _services.GetRequiredService<ShedRepository>().UpdateAsync(new Shed { ShedId = row.Id, Name = row.Name, Address = row.Address, IsActive = true, SortOrder = row.SortOrder });
                    break;
                case "Blends":
                    await _services.GetRequiredService<BlendRepository>().UpdateAsync(new Blend { BlendId = row.Id, Name = row.Name, IsActive = true, SortOrder = row.SortOrder });
                    break;
            }
        }

        private void PopupDragThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            PopupTransform.X += e.HorizontalChange;
            PopupTransform.Y += e.VerticalChange;
        }
    }
}
