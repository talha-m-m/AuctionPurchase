using System.Windows;
using System.Windows.Controls;
using AuctionPurchase.Core.Models;
using AuctionPurchase.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace AuctionPurchase.UI.Pages;

public partial class CompaniesPage : Page
{
    private readonly IServiceProvider _services;
    private List<Company> _companies = new();
    private List<Garden> _gardens = new();
    private List<GardenChoice> _choices = new();
    private int _editingId;

    public CompaniesPage(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
        Loaded += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            _companies = await _services.GetRequiredService<CompanyRepository>().GetAllAsync();
            _gardens = (await _services.GetRequiredService<GardenRepository>().GetAllAsync()).OrderBy(x => x.Name).ToList();
            FilterCompanies();
            Edit(_companies.FirstOrDefault(x => x.CompanyId == _editingId));
        }
        catch (Exception ex) { TxtStatus.Text = ex.Message; }
    }

    private void FilterCompanies()
    {
        if (CompanyList == null) return;
        string query = TxtCompanySearch.Text.Trim();
        CompanyList.ItemsSource = _companies.Where(x => x.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            x.GardenNames.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void Edit(Company? company)
    {
        _editingId = company?.CompanyId ?? 0;
        TxtName.Text = company?.Name ?? "";
        ChkActive.IsChecked = company?.IsActive ?? true;
        TxtEditorTitle.Text = company == null ? "New Company" : "Edit Company";
        BtnDelete.IsEnabled = company != null;
        TxtStatus.Text = "";
        _choices = _gardens.Select(garden =>
        {
            var owner = _companies.FirstOrDefault(x => x.GardenIds.Contains(garden.GardenId));
            bool available = owner == null || owner.CompanyId == _editingId;
            return new GardenChoice
            {
                GardenId = garden.GardenId, Name = garden.Name,
                Label = available ? garden.Name : $"{garden.Name} — {owner!.Name}",
                IsAvailable = available, IsSelected = company?.GardenIds.Contains(garden.GardenId) == true
            };
        }).ToList();
        TxtGardenSearch.Text = "";
        FilterGardens();
    }

    private void FilterGardens()
    {
        if (GardenList == null) return;
        string query = TxtGardenSearch.Text.Trim();
        GardenList.ItemsSource = _choices.Where(x => x.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }
    private void Search_Changed(object sender, TextChangedEventArgs e) => FilterCompanies();
    private void GardenSearch_Changed(object sender, TextChangedEventArgs e) => FilterGardens();
    private void New_Click(object sender, RoutedEventArgs e) { CompanyList.SelectedItem = null; Edit(null); TxtName.Focus(); }
    private void Company_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (CompanyList.SelectedItem is Company company) Edit(company);
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            BtnSave.IsEnabled = false;
            _editingId = await _services.GetRequiredService<CompanyRepository>().SaveAsync(new Company
            {
                CompanyId = _editingId, Name = TxtName.Text, IsActive = ChkActive.IsChecked == true,
                GardenIds = _choices.Where(x => x.IsSelected && x.IsAvailable).Select(x => x.GardenId).ToList()
            });
            await ReloadAsync();
            TxtStatus.Text = "Company saved.";
        }
        catch (Exception ex) { TxtStatus.Text = ex.Message; }
        finally { BtnSave.IsEnabled = true; }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_editingId == 0 || MessageBox.Show("Delete this company and its garden links? Existing sale accounts will be kept.",
            "Delete Company", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            await _services.GetRequiredService<CompanyRepository>().DeleteAsync(_editingId);
            _editingId = 0;
            await ReloadAsync();
        }
        catch (Exception ex) { TxtStatus.Text = ex.Message; }
    }

    public class GardenChoice
    {
        public int GardenId { get; set; }
        public string Name { get; set; } = "";
        public string Label { get; set; } = "";
        public bool IsAvailable { get; set; }
        public bool IsSelected { get; set; }
    }
}
