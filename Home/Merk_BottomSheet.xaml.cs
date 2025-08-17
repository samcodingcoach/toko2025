using The49.Maui.BottomSheet;
using System.Collections.ObjectModel;
using Toko2025.Services;
using System.ComponentModel;

namespace Toko2025.Home;

public partial class Merk_BottomSheet : BottomSheet, INotifyPropertyChanged
{
    private ObservableCollection<BrandItem> _brands = new();
    private string _searchText = string.Empty;
    private List<Merk> _allMerk = new();

    public ObservableCollection<BrandItem> Brands
    {
        get => _brands;
        set
        {
            _brands = value;
            OnPropertyChanged();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
            FilterBrands();
        }
    }

    // Event for brand selection
    public event EventHandler<int> BrandSelected;

    public Merk_BottomSheet()
    {
        InitializeComponent();
        BindingContext = this;
        LoadBrands();
    }

    private async void LoadBrands()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== Loading brands ===");
            
            // Check if database is initialized
            if (App.Database == null)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: Database is null");
                
                // Show error message
                Brands = new ObservableCollection<BrandItem>
                {
                    new BrandItem { Id = 0, Name = "Database not initialized", ProductCount = 0 }
                };
                return;
            }

            await EnsureDataLoaded();

            // Load merk from local database
            _allMerk = App.Database.GetMerk();
            System.Diagnostics.Debug.WriteLine($"Retrieved {_allMerk.Count} brands from database");

            // Convert to BrandItem for display
            if (_allMerk.Count > 0)
            {
                var brandItems = _allMerk.Select(m => new BrandItem
                {
                    Id = m.id_merk,
                    Name = m.nama_merk,
                    ProductCount = GetProductCountForBrand(m.id_merk)
                }).ToList();

                Brands = new ObservableCollection<BrandItem>(brandItems);
                System.Diagnostics.Debug.WriteLine($"Brands loaded: {Brands.Count} items");
            }
            else
            {
                // Show message if no brands
                Brands = new ObservableCollection<BrandItem>
                {
                    new BrandItem { Id = 0, Name = "No brands found", ProductCount = 0 }
                };
                System.Diagnostics.Debug.WriteLine("No brands found in database");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR loading brands: {ex.Message}");
            
            // Show error message in UI
            Brands = new ObservableCollection<BrandItem>
            {
                new BrandItem { Id = 0, Name = $"Error: {ex.Message}", ProductCount = 0 }
            };
        }
    }

    private async Task EnsureDataLoaded()
    {
        try
        {
            // Hanya cek jumlah data, TIDAK melakukan sync sama sekali
            var (kategoriCount, merkCount) = App.Database.GetDataCounts();
            System.Diagnostics.Debug.WriteLine($"Reading from SQLite - Kategori: {kategoriCount}, Merk: {merkCount}");

            // Tidak ada sync sama sekali, hanya informasi
            if (merkCount == 0)
            {
                System.Diagnostics.Debug.WriteLine("No brands in SQLite database");
                // Tampilkan pesan bahwa belum ada data
                Brands = new ObservableCollection<BrandItem>
                {
                    new BrandItem { Id = 0, Name = "No brands available. Please login to sync data.", ProductCount = 0 }
                };
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Brands found in SQLite, reading local data...");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading from SQLite: {ex.Message}");
        }
    }

    private int GetProductCountForBrand(int brandId)
    {
        try
        {
            // Get the actual count from the merk record
            var merk = _allMerk.FirstOrDefault(m => m.id_merk == brandId);
            return merk?.jumlah ?? 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting product count for brand {brandId}: {ex.Message}");
            return 0;
        }
    }

    private void FilterBrands()
    {
        if (_allMerk == null || _allMerk.Count == 0) 
        {
            System.Diagnostics.Debug.WriteLine("No brands to filter");
            return;
        }

        var filtered = _allMerk.Where(m => 
            string.IsNullOrEmpty(SearchText) || 
            m.nama_merk.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
        ).Select(m => new BrandItem
        {
            Id = m.id_merk,
            Name = m.nama_merk,
            ProductCount = GetProductCountForBrand(m.id_merk)
        }).ToList();

        Brands = new ObservableCollection<BrandItem>(filtered);
        System.Diagnostics.Debug.WriteLine($"Filtered brands: {Brands.Count} items");
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        SearchText = e.NewTextValue ?? string.Empty;
        System.Diagnostics.Debug.WriteLine($"Search text changed: {SearchText}");
    }

    private async void OnBrandTapped(object sender, TappedEventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is BrandItem brand)
        {
            System.Diagnostics.Debug.WriteLine($"Brand tapped: {brand.Name}");
            
            // Skip if this is an error/loading message
            if (brand.Id == 0)
            {
                System.Diagnostics.Debug.WriteLine("Error/loading item tapped, ignoring");
                return;
            }
            
            // Animation
            await grid.ScaleTo(0.95, 100);
            await grid.ScaleTo(1, 100);

            // Handle brand selection
            System.Diagnostics.Debug.WriteLine($"Selected brand: {brand.Name} (ID: {brand.Id})");
            
            // Fire the BrandSelected event
            BrandSelected?.Invoke(this, brand.Id);
            
            // Close bottom sheet
            await this.DismissAsync();
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class BrandItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ProductCount { get; set; }
}