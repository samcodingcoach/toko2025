using The49.Maui.BottomSheet;
using System.Collections.ObjectModel;
using Toko2025.Services;
using System.ComponentModel;

namespace Toko2025.Home;

public partial class Categories_BottomSheet : BottomSheet, INotifyPropertyChanged
{
    private ObservableCollection<CategoryItem> _categories = new();
    private string _searchText = string.Empty;
    private List<Kategori> _allKategori = new();

    public ObservableCollection<CategoryItem> Categories
    {
        get => _categories;
        set
        {
            _categories = value;
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
            FilterCategories();
        }
    }

    // Event for category selection
    public event EventHandler<int> CategorySelected;

    public Categories_BottomSheet()
    {
        InitializeComponent();
        BindingContext = this;
        LoadCategories();
    }

    private async void LoadCategories()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== Loading categories ===");
            
            // Check if database is initialized
            if (App.Database == null)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: Database is null");
                
                // Show error message
                Categories = new ObservableCollection<CategoryItem>
                {
                    new CategoryItem { Id = 0, Name = "Database not initialized", ProductCount = 0 }
                };
                return;
            }

            // Force sync if no data
            await EnsureDataLoaded();

            // Load kategori from local database
            _allKategori = App.Database.GetKategori();
            System.Diagnostics.Debug.WriteLine($"Retrieved {_allKategori.Count} categories from database");

            // Convert to CategoryItem for display
            if (_allKategori.Count > 0)
            {
                var categoryItems = _allKategori.Select(k => new CategoryItem
                {
                    Id = k.id_kategori,
                    Name = k.nama_kategori,
                    ProductCount = GetProductCountForCategory(k.id_kategori)
                }).ToList();

                Categories = new ObservableCollection<CategoryItem>(categoryItems);
                System.Diagnostics.Debug.WriteLine($"Categories loaded: {Categories.Count} items");
            }
            else
            {
                // Show message if no categories
                Categories = new ObservableCollection<CategoryItem>
                {
                    new CategoryItem { Id = 0, Name = "No categories found", ProductCount = 0 }
                };
                System.Diagnostics.Debug.WriteLine("No categories found in database");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR loading categories: {ex.Message}");
            
            // Show error message in UI
            Categories = new ObservableCollection<CategoryItem>
            {
                new CategoryItem { Id = 0, Name = $"Error: {ex.Message}", ProductCount = 0 }
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
            if (kategoriCount == 0)
            {
                System.Diagnostics.Debug.WriteLine("No categories in SQLite database");
                // Tampilkan pesan bahwa belum ada data
                Categories = new ObservableCollection<CategoryItem>
                {
                    new CategoryItem { Id = 0, Name = "No categories available. Please login to sync data.", ProductCount = 0 }
                };
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Categories found in SQLite, reading local data...");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading from SQLite: {ex.Message}");
        }
    }

    private int GetProductCountForCategory(int categoryId)
    {
        try
        {
            // Get the actual count from the kategori record
            var kategori = _allKategori.FirstOrDefault(k => k.id_kategori == categoryId);
            return kategori?.jumlah ?? 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting product count for category {categoryId}: {ex.Message}");
            return 0;
        }
    }

    private void FilterCategories()
    {
        if (_allKategori == null || _allKategori.Count == 0) 
        {
            System.Diagnostics.Debug.WriteLine("No categories to filter");
            return;
        }

        var filtered = _allKategori.Where(k => 
            string.IsNullOrEmpty(SearchText) || 
            k.nama_kategori.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
        ).Select(k => new CategoryItem
        {
            Id = k.id_kategori,
            Name = k.nama_kategori,
            ProductCount = GetProductCountForCategory(k.id_kategori)
        }).ToList();

        Categories = new ObservableCollection<CategoryItem>(filtered);
        System.Diagnostics.Debug.WriteLine($"Filtered categories: {Categories.Count} items");
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        SearchText = e.NewTextValue ?? string.Empty;
        System.Diagnostics.Debug.WriteLine($"Search text changed: {SearchText}");
    }

    private async void OnCategoryTapped(object sender, TappedEventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is CategoryItem category)
        {
            System.Diagnostics.Debug.WriteLine($"Category tapped: {category.Name}");
            
            // Skip if this is an error/loading message
            if (category.Id == 0)
            {
                System.Diagnostics.Debug.WriteLine("Error/loading item tapped, ignoring");
                return;
            }
            
            // Animation
            await grid.ScaleTo(0.95, 100);
            await grid.ScaleTo(1, 100);

            // Handle category selection
            System.Diagnostics.Debug.WriteLine($"Selected category: {category.Name} (ID: {category.Id})");
            
            // Fire the CategorySelected event
            CategorySelected?.Invoke(this, category.Id);
            
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

public class CategoryItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ProductCount { get; set; }
}