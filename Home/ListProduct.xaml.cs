using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Toko2025.Services;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace Toko2025.Home;

public partial class ListProduct : ContentPage, INotifyPropertyChanged
{
    private ObservableCollection<Product> _products = new();
    private bool _isLoading = false;
    private string _searchText = string.Empty;
    private List<Product> _allProducts = new();
    private int? _selectedCategoryId = null;
    private int? _selectedBrandId = null;

    public ObservableCollection<Product> Products
    {
        get => _products;
        set
        {
            _products = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public ICommand RefreshCommand { get; }

    public ListProduct()
    {
        InitializeComponent();
        RefreshCommand = new Command(async () => await LoadProductsAsync());
        BindingContext = this;
        
        // Set initial loading state
        IsLoading = true;
        
        // Load products when page appears - dengan loading indicator
        _ = Task.Run(async () => await LoadProductsAsync());
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Check if there's a stored barcode result from ScanBarcode page
        CheckForStoredBarcodeResult();
    }

    private void CheckForStoredBarcodeResult()
    {
        try
        {
            var storedBarcode = Preferences.Get("scanned_barcode_result", string.Empty);
            
            if (!string.IsNullOrEmpty(storedBarcode))
            {
                System.Diagnostics.Debug.WriteLine($"=== FOUND STORED BARCODE ===");
                System.Diagnostics.Debug.WriteLine($"Stored barcode: {storedBarcode}");
                
                // Clear the stored barcode first
                Preferences.Remove("scanned_barcode_result");
                
                // Set to SearchEntry
                SetSearchEntryText(storedBarcode);
                
                System.Diagnostics.Debug.WriteLine("Barcode set from stored preferences");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking stored barcode: {ex.Message}");
        }
    }

    /// <summary>
    /// Public method to set SearchEntry text with barcode result
    /// Called from ScanBarcode popup when barcode is scanned
    /// </summary>
    public void SetSearchEntryText(string barcode)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== SETTING SEARCH ENTRY TEXT ===");
            System.Diagnostics.Debug.WriteLine($"Barcode: {barcode}");
            
            if (string.IsNullOrEmpty(barcode))
            {
                System.Diagnostics.Debug.WriteLine("Barcode is empty, skipping");
                return;
            }

            // Set the text on main thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    // Clear any existing filters first
                    _selectedCategoryId = null;
                    _selectedBrandId = null;
                    
                    // Set the search text
                    _searchText = barcode;
                    SearchEntry.Text = barcode;
                    
                    System.Diagnostics.Debug.WriteLine($"SearchEntry.Text set to: {SearchEntry.Text}");
                    
                    // Trigger search with the barcode
                    _ = Task.Run(async () => await LoadProductsAsync());
                    
                    System.Diagnostics.Debug.WriteLine("Search triggered with barcode");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error setting SearchEntry text on UI thread: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in SetSearchEntryText: {ex.Message}");
        }
    }

    private async Task LoadProductsAsync()
    {
        try
        {
            // Set IsLoading = true pada main thread dulu untuk immediate feedback
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsLoading = true;
            });
            
            System.Diagnostics.Debug.WriteLine("=== LOADING PRODUCTS START ===");
            System.Diagnostics.Debug.WriteLine("Loading products...");
            
            // Start timing untuk minimum loading duration
            var startTime = DateTime.Now;
            
            if (App.Database == null)
            {
                System.Diagnostics.Debug.WriteLine("Database is null!");
                return;
            }

            List<Product> products;

            // Apply filters based on selection
            if (_selectedCategoryId.HasValue)
            {
                products = await App.Database.GetProductsByCategoryAsync(_selectedCategoryId.Value);
                System.Diagnostics.Debug.WriteLine($"Loaded {products.Count} products for category {_selectedCategoryId}");
            }
            else if (_selectedBrandId.HasValue)
            {
                products = await App.Database.GetProductsByBrandAsync(_selectedBrandId.Value);
                System.Diagnostics.Debug.WriteLine($"Loaded {products.Count} products for brand {_selectedBrandId}");
            }
            else if (!string.IsNullOrWhiteSpace(_searchText))
            {
                products = await App.Database.GetProductsBySearchAsync(_searchText);
                System.Diagnostics.Debug.WriteLine($"Loaded {products.Count} products for search '{_searchText}'");
            }
            else
            {
                products = await App.Database.GetProductsAsync();
                System.Diagnostics.Debug.WriteLine($"Loaded {products.Count} products");
            }

            _allProducts = products;
            
            // Update UI on main thread - data sudah siap
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Products = new ObservableCollection<Product>(products);
                System.Diagnostics.Debug.WriteLine("=== PRODUCTS UI UPDATED ===");
            });
            
            // Calculate elapsed time dan ensure minimum 3 second loading
            var elapsedTime = DateTime.Now - startTime;
            var remainingTime = TimeSpan.FromSeconds(3) - elapsedTime;
            
            if (remainingTime > TimeSpan.Zero)
            {
                System.Diagnostics.Debug.WriteLine($"Waiting additional {remainingTime.TotalMilliseconds}ms to reach minimum 3 seconds");
                await Task.Delay(remainingTime);
            }
            
            // Pastikan semua UI sudah ter-render dengan delay tambahan
            await Task.Delay(200); // 200ms final delay untuk memastikan UI smooth
            
            System.Diagnostics.Debug.WriteLine("=== LOADING PRODUCTS COMPLETE ===");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading products: {ex.Message}");
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlert("Error", $"Failed to load products: {ex.Message}", "OK");
            });
        }
        finally
        {
            // IsLoading = false hanya setelah semua proses selesai
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsLoading = false;
                System.Diagnostics.Debug.WriteLine("=== LOADING INDICATOR HIDDEN ===");
            });
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = e.NewTextValue ?? string.Empty;
        
        // Clear filters when searching
        _selectedCategoryId = null;
        _selectedBrandId = null;
        
        // Debounce search
        _ = Task.Run(async () =>
        {
            await Task.Delay(500); // 500ms delay
            if (_searchText == (e.NewTextValue ?? string.Empty))
            {
                await LoadProductsAsync();
            }
        });
    }

    private async void TapShowCategories_Tapped(object sender, TappedEventArgs e)
    {
        if (sender is HorizontalStackLayout image)
        {
            await image.FadeTo(0.3, 100);
            await image.FadeTo(1, 200);
        }

        try
        {
            System.Diagnostics.Debug.WriteLine("Opening Categories Bottom Sheet...");
            
            if (App.Database == null)
            {
                System.Diagnostics.Debug.WriteLine("Database is null!");
                return;
            }

            var categoriesBottomSheet = new Categories_BottomSheet();
            
            // Subscribe to category selection
            categoriesBottomSheet.CategorySelected += OnCategorySelected;
            
            await categoriesBottomSheet.ShowAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error showing categories: {ex.Message}");
            await DisplayAlert("Error", $"Failed to load categories: {ex.Message}", "OK");
        }
    }

    private async void TapShowBrands_Tapped(object sender, TappedEventArgs e)
    {
        if (sender is HorizontalStackLayout image)
        {
            await image.FadeTo(0.3, 100);
            await image.FadeTo(1, 200);
        }

        try
        {
            System.Diagnostics.Debug.WriteLine("Opening Brands Bottom Sheet...");
            
            if (App.Database == null)
            {
                System.Diagnostics.Debug.WriteLine("Database is null!");
                return;
            }

            var brandsBottomSheet = new Merk_BottomSheet();
            
            // Subscribe to brand selection
            brandsBottomSheet.BrandSelected += OnBrandSelected;
            
            await brandsBottomSheet.ShowAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error showing brands: {ex.Message}");
            await DisplayAlert("Error", $"Failed to load brands: {ex.Message}", "OK");
        }
    }

    private async void OnCategorySelected(object sender, int categoryId)
    {
        _selectedCategoryId = categoryId;
        _selectedBrandId = null; // Clear brand filter
        _searchText = string.Empty;
        SearchEntry.Text = string.Empty;
        
        await LoadProductsAsync();
    }

    private async void OnBrandSelected(object sender, int brandId)
    {
        _selectedBrandId = brandId;
        _selectedCategoryId = null; // Clear category filter
        _searchText = string.Empty;
        SearchEntry.Text = string.Empty;
        
        await LoadProductsAsync();
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void TapDetailProduct_Tapped(object sender, TappedEventArgs e)
    {
        // Prevent multiple taps during loading
        if (IsLoading)
        {
            System.Diagnostics.Debug.WriteLine("Already loading, ignoring tap");
            return;
        }

        if (sender is Image image)
        {
            await image.FadeTo(0.3, 100); // Turunkan opacity ke 0.3 dalam 100ms
            await image.FadeTo(1, 200);   // Kembalikan opacity ke 1 dalam 200ms
        }

        // Cek apakah ada product yang dipilih dari BindingContext
        if (sender is Image img && img.BindingContext is Product selectedProduct)
        {
            System.Diagnostics.Debug.WriteLine($"=== PRODUCT TAP DETECTED ===");
            System.Diagnostics.Debug.WriteLine($"Navigating to detail for product ID: {selectedProduct.id_barang} - {selectedProduct.nama_barang}");
            
            try
            {
                // Show loading during navigation dengan minimum 3 detik
                IsLoading = true;
                var startTime = DateTime.Now;
                
                // Show loading toast
                var toast = Toast.Make($"Loading {selectedProduct.nama_barang}...", 
                    ToastDuration.Short, 12);
                await toast.Show(new CancellationTokenSource().Token);
                
                // Navigate to detail
                await Navigation.PushAsync(new Home.DetailProduct(selectedProduct.id_barang));
                
                // Ensure minimum 3 second loading untuk navigation
                var elapsedTime = DateTime.Now - startTime;
                var remainingTime = TimeSpan.FromSeconds(3) - elapsedTime;
                
                if (remainingTime > TimeSpan.Zero)
                {
                    System.Diagnostics.Debug.WriteLine($"Navigation loading: waiting additional {remainingTime.TotalMilliseconds}ms");
                    await Task.Delay(remainingTime);
                }
                
                System.Diagnostics.Debug.WriteLine($"=== NAVIGATION TO DETAIL COMPLETED ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
                await DisplayAlert("Error", "Failed to open product detail", "OK");
            }
            finally
            {
                // Hide loading after navigation
                IsLoading = false;
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("No product context found, using default ID");
            try
            {
                IsLoading = true;
                var startTime = DateTime.Now;
                
                await Navigation.PushAsync(new Home.DetailProduct());
                
                // Ensure minimum 3 second loading
                var elapsedTime = DateTime.Now - startTime;
                var remainingTime = TimeSpan.FromSeconds(3) - elapsedTime;
                
                if (remainingTime > TimeSpan.Zero)
                {
                    await Task.Delay(remainingTime);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Default navigation error: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    private async void TapBarcode_Tapped(object sender, TappedEventArgs e)
    {
        if (sender is HorizontalStackLayout image)
        {
            await image.FadeTo(0.3, 100);
            await image.FadeTo(1, 200);
        }

        try
        {
            System.Diagnostics.Debug.WriteLine("Opening ScanBarcode popup...");
            
            // Buat instance popup ScanBarcode
            var scanBarcodePopup = new ScanBarcode();
            
            // Subscribe ke event BarcodeScanned
            scanBarcodePopup.BarcodeScanned += OnBarcodeScanned;
            
            // Tampilkan popup
            await this.ShowPopupAsync(scanBarcodePopup);
            
            System.Diagnostics.Debug.WriteLine("ScanBarcode popup closed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error opening ScanBarcode popup: {ex.Message}");
            await DisplayAlert("Error", $"Failed to open barcode scanner: {ex.Message}", "OK");
        }
    }

    private void OnBarcodeScanned(object sender, string barcode)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== BARCODE SCANNED EVENT RECEIVED ===");
            System.Diagnostics.Debug.WriteLine($"Barcode: {barcode}");
            
            // Set barcode ke SearchEntry dan trigger search
            SetSearchEntryText(barcode);
            
            System.Diagnostics.Debug.WriteLine("Barcode processed from popup event");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error processing scanned barcode: {ex.Message}");
        }
    }
}