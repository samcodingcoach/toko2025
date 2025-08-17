using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using Toko2025.Services;

namespace Toko2025.Home;

public partial class DetailProduct : ContentPage, INotifyPropertyChanged
{
    private int _productId;
    private Product _currentProduct;
    private bool _isImageLoading = false;
    private bool _isAmountEventAttached = false;
    private string pesan = string.Empty;
    private ObservableCollection<Product> _similarProducts = new ObservableCollection<Product>();
    private bool _isNavigatingToSimilarProduct = false;

    // Property untuk Similar Products
    public ObservableCollection<Product> SimilarProducts
    {
        get => _similarProducts;
        set
        {
            _similarProducts = value;
            OnPropertyChanged();
        }
    }

    // Property untuk Similar Product Loading
    public bool IsNavigatingToSimilarProduct
    {
        get => _isNavigatingToSimilarProduct;
        set
        {
            _isNavigatingToSimilarProduct = value;
            OnPropertyChanged();
        }
    }

    // INotifyPropertyChanged implementation
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public DetailProduct(int productId = 4) // Default ke id 4 sesuai contoh
    {
        InitializeComponent();
        _productId = productId;
        
        // Set binding context
        BindingContext = this;
        
        // Set placeholder images immediately
        I_ProductImage1.Source = "p300x300.svg";
        I_ProductImage2.Source = "p300x300.svg";
        
        // Hide loading indicators initially
        AI_Image1Loading.IsVisible = false;
        AI_Image1Loading.IsRunning = false;
        AI_Image2Loading.IsVisible = false;
        AI_Image2Loading.IsRunning = false;
        
        LoadProductDetail();
    }

    private async void toast()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== TOAST START ===");
            System.Diagnostics.Debug.WriteLine($"Toast message: '{pesan}'");
            
            if (string.IsNullOrEmpty(pesan))
            {
                System.Diagnostics.Debug.WriteLine("Warning: Toast message is empty, using default");
                pesan = "Operation completed";
            }

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            ToastDuration duration = ToastDuration.Long;
            double fontSize = 12;
            
            var toast = Toast.Make(pesan, duration, fontSize);
            
            if (toast != null)
            {
                System.Diagnostics.Debug.WriteLine($"=== SHOWING TOAST ===");
                await toast.Show(cancellationTokenSource.Token);
                System.Diagnostics.Debug.WriteLine($"=== TOAST SHOWN SUCCESSFULLY ===");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("ERROR: Toast.Make returned null");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== TOAST ERROR ===");
            System.Diagnostics.Debug.WriteLine($"Toast error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Toast error type: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"Toast stack trace: {ex.StackTrace}");
        }
    }

    private async void LoadProductDetail()
    {
        try
        {
            // Tampilkan loading state
            L_ProductTitle.Text = "Loading...";
            
            System.Diagnostics.Debug.WriteLine($"Loading product detail for ID: {_productId}");

            // Call API
            var product = await GetProductDetailAsync(_productId);

            if (product != null)
            {
                _currentProduct = product;
                
                // Fill UI dengan data produk (text data dulu, gambar belakangan)
                L_ProductTitle.Text = product.nama_barang;
                L_Brand.Text = $"Brand : {product.nama_merk}";
                L_Categories.Text = $"Categories : {product.nama_kategori}";

                // Barcode information
                L_Barcode1.Text = product.barcode1;
                L_Barcode2.Text = product.barcode2 ?? "N/A";

                // Price information
                L_RegularPrice.Text = product.FormattedPrice;
                L_MembershipPrice.Text = product.FormattedMemberPrice;
                L_RemainingStock.Text = product.stok_aktif.ToString();
                L_Unit.Text = product.nama_satuan ?? "Pieces";

                // Setup amount dan subtotal dengan event handler dinamis
                SetupAmountCalculation();

                // Load gambar secara async setelah UI data selesai
                _ = Task.Run(async () => await LoadProductImagesAsync(product));

                // Load similar products
                _ = Task.Run(async () => await LoadSimilarProductsAsync(product.id_kategori, product.id_barang));

                System.Diagnostics.Debug.WriteLine($"Product detail loaded successfully: {product.nama_barang}");
            }
            else
            {
                L_ProductTitle.Text = "Product not found";
                System.Diagnostics.Debug.WriteLine($"Product with ID {_productId} not found");
                
                pesan = $"Product with ID {_productId} not found";
                toast();
            }
        }
        catch (Exception ex)
        {
            L_ProductTitle.Text = "Error loading product";
            System.Diagnostics.Debug.WriteLine($"Error loading product detail: {ex.Message}");
            
            pesan = $"Failed to load product: {ex.Message}";
            toast();
        }
    }

    private async Task LoadProductImagesAsync(Product product)
    {
        if (_isImageLoading) return;
        _isImageLoading = true;

        try
        {
            System.Diagnostics.Debug.WriteLine("Loading product images with caching...");

            // Show loading indicators
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                AI_Image1Loading.IsVisible = true;
                AI_Image1Loading.IsRunning = true;
                AI_Image2Loading.IsVisible = true;
                AI_Image2Loading.IsRunning = true;
            });

            // Load gambar 1 dengan UriImageSource dan cache
            if (!string.IsNullOrEmpty(product.gambar1))
            {
                await LoadImageWithCaching(product.gambar1, I_ProductImage1, AI_Image1Loading);
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    AI_Image1Loading.IsVisible = false;
                    AI_Image1Loading.IsRunning = false;
                });
            }

            // Load gambar 2 dengan UriImageSource dan cache
            if (!string.IsNullOrEmpty(product.gambar2))
            {
                await LoadImageWithCaching(product.gambar2, I_ProductImage2, AI_Image2Loading);
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    AI_Image2Loading.IsVisible = false;
                    AI_Image2Loading.IsRunning = false;
                });
            }

            System.Diagnostics.Debug.WriteLine("Product images loaded with caching successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading images: {ex.Message}");
            
            // Hide loading indicators on error
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                AI_Image1Loading.IsVisible = false;
                AI_Image1Loading.IsRunning = false;
                AI_Image2Loading.IsVisible = false;
                AI_Image2Loading.IsRunning = false;
            });
        }
        finally
        {
            _isImageLoading = false;
        }
    }

    private async Task LoadImageWithCaching(string imageName, Image imageControl, ActivityIndicator loadingIndicator)
    {
        try
        {
            string imageUrl = $"http://{App.IP}:3000/images/{imageName}";
            System.Diagnostics.Debug.WriteLine($"Loading image with cache: {imageUrl}");

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // Gunakan UriImageSource dengan CacheValidity untuk auto-caching
                var uriImageSource = new UriImageSource
                {
                    Uri = new Uri(imageUrl),
                    CacheValidity = new TimeSpan(24, 0, 0, 0), // Cache selama 24 jam
                    CachingEnabled = true
                };

                imageControl.Source = uriImageSource;
                
                // Hide loading indicator setelah set source
                loadingIndicator.IsVisible = false;
                loadingIndicator.IsRunning = false;
                
                System.Diagnostics.Debug.WriteLine($"Image source set with 24-hour cache: {imageName}");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading image {imageName}: {ex.Message}");
            
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // Fallback ke placeholder
                imageControl.Source = "p300x300.svg";
                loadingIndicator.IsVisible = false;
                loadingIndicator.IsRunning = false;
            });
        }
    }

    private async Task<ImageSource> LoadImageWithFallbackAsync(string imageUrl)
    {
        try
        {
            // Timeout untuk image loading
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            
            // Pre-check if URL is reachable
            var response = await App.SharedHttpClient.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            
            if (response.IsSuccessStatusCode)
            {
                var stream = await response.Content.ReadAsStreamAsync();
                return ImageSource.FromStream(() => stream);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Image not found at: {imageUrl}");
                return ImageSource.FromFile("p300x300.svg");
            }
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"Image loading timeout: {imageUrl}");
            return ImageSource.FromFile("p300x300.svg");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading image {imageUrl}: {ex.Message}");
            return ImageSource.FromFile("p300x300.svg");
        }
    }

    private async Task<Product> GetProductDetailAsync(int productId)
    {
        try
        {
            string apiUrl = $"http://{App.IP}:3000/api/barang?id_barang={productId}";
            System.Diagnostics.Debug.WriteLine($"Calling API: {apiUrl}");

            // Gunakan SharedHttpClient
            var response = await App.SharedHttpClient.GetAsync(apiUrl);
            
            var jsonContent = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"API Response Status: {response.StatusCode}");

            // Cek format response pertama kali
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                System.Diagnostics.Debug.WriteLine("Empty response from API");
                return null;
            }

            // Parse sebagai ApiResponse<List<Product>> karena data dalam array
            var apiResponse = JsonConvert.DeserializeObject<ApiResponse<List<Product>>>(jsonContent);
            
            if (apiResponse != null && apiResponse.success && apiResponse.data != null && apiResponse.data.Count > 0)
            {
                var product = apiResponse.data[0]; // Ambil produk pertama dari array
                System.Diagnostics.Debug.WriteLine($"Product found: {product.nama_barang}");
                return product;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"API returned error or no data");
                return null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"API call error: {ex.Message}");
            throw;
        }
    }

    private void SetupAmountCalculation()
    {
        if (_currentProduct == null) return;

        // Check stock availability dan setup initial amount
        if (_currentProduct.stok_aktif <= 0)
        {
            // Jika stock habis, disable entry dan set ke 0
            E_Amount.Text = "0";
            E_Amount.IsEnabled = false;
            E_Amount.BackgroundColor = Colors.LightGray;
            
            // Update remaining stock color
            L_RemainingStock.TextColor = Colors.Red;
            
            System.Diagnostics.Debug.WriteLine("Product out of stock - Amount entry disabled");
        }
        else
        {
            // Stock tersedia, enable entry dan set initial amount
            E_Amount.Text = "1";
            E_Amount.IsEnabled = true;
            E_Amount.BackgroundColor = Colors.White;
            
            // Update remaining stock color based on stock level
            if (_currentProduct.stok_aktif <= 5)
            {
                L_RemainingStock.TextColor = Colors.Orange; // Low stock warning
            }
            else
            {
                L_RemainingStock.TextColor = Color.FromArgb("#333333"); // Normal
            }
        }

        // Hitung initial subtotal
        CalculateAndUpdateSubtotal();

        // Attach event handler untuk real-time calculation (hanya sekali)
        if (!_isAmountEventAttached)
        {
            E_Amount.TextChanged += OnAmountChanged;
            
            // Tambahkan event untuk validasi input
            E_Amount.Unfocused += OnAmountUnfocused;
            
            _isAmountEventAttached = true;
        }

        System.Diagnostics.Debug.WriteLine($"Amount calculation setup completed - Stock: {_currentProduct.stok_aktif}");
    }

    private void OnAmountChanged(object sender, TextChangedEventArgs e)
    {
        // Real-time validation dan calculation saat user mengetik
        ValidateAmountRealTime();
        CalculateAndUpdateSubtotal();
    }

    private void OnAmountUnfocused(object sender, FocusEventArgs e)
    {
        // Validasi dan perbaiki input saat user selesai edit
        ValidateAndFixAmount();
        CalculateAndUpdateSubtotal();
    }

    private void ValidateAmountRealTime()
    {
        if (_currentProduct == null) return;

        string amountText = E_Amount.Text ?? "";
        
        // Jika kosong, biarkan sementara (user sedang mengetik)
        if (string.IsNullOrWhiteSpace(amountText))
        {
            return;
        }

        // Parse dan validasi angka real-time
        if (int.TryParse(amountText, out int amount))
        {
            // Cek jika melebihi stock secara real-time
            if (amount > _currentProduct.stok_aktif)
            {
                // Auto-correct ke maximum stock
                E_Amount.Text = _currentProduct.stok_aktif.ToString();
                
                // Set cursor ke akhir text
                E_Amount.CursorPosition = E_Amount.Text.Length;
                
                // Show warning
                ShowStockWarning();
                
                System.Diagnostics.Debug.WriteLine($"Amount auto-corrected from {amount} to {_currentProduct.stok_aktif} (max stock)");
            }
        }
    }

    private void ValidateAndFixAmount()
    {
        if (_currentProduct == null) return;

        string amountText = E_Amount.Text ?? "1";
        
        // Validasi input amount
        if (string.IsNullOrWhiteSpace(amountText))
        {
            E_Amount.Text = "1";
            System.Diagnostics.Debug.WriteLine("Empty amount fixed to 1");
            return;
        }

        // Parse dan validasi angka
        if (int.TryParse(amountText, out int amount))
        {
            bool wasChanged = false;
            
            // Pastikan amount tidak negatif atau nol
            if (amount <= 0)
            {
                E_Amount.Text = "1";
                amount = 1;
                wasChanged = true;
                System.Diagnostics.Debug.WriteLine("Negative/zero amount fixed to 1");
            }
            
            // Cek stock availability - ini adalah validasi utama
            if (amount > _currentProduct.stok_aktif)
            {
                E_Amount.Text = _currentProduct.stok_aktif.ToString();
                amount = _currentProduct.stok_aktif;
                wasChanged = true;
                
                // Show warning hanya jika user benar-benar mencoba input melebihi stock
                ShowStockWarning();
                
                System.Diagnostics.Debug.WriteLine($"Amount exceeds stock, fixed to maximum: {_currentProduct.stok_aktif}");
            }

            // Cek jika stock habis
            if (_currentProduct.stok_aktif <= 0)
            {
                E_Amount.Text = "0";
                E_Amount.IsEnabled = false;
                ShowOutOfStockWarning();
                System.Diagnostics.Debug.WriteLine("Product out of stock, amount disabled");
            }

            if (wasChanged)
            {
                // Set cursor ke akhir text setelah auto-correction
                E_Amount.CursorPosition = E_Amount.Text.Length;
            }
        }
        else
        {
            // Jika input bukan angka valid, reset ke 1
            E_Amount.Text = "1";
            E_Amount.CursorPosition = E_Amount.Text.Length;
            System.Diagnostics.Debug.WriteLine("Invalid number input fixed to 1");
        }
    }

    private void ShowStockWarning()
    {
        if (_currentProduct == null) return;

        pesan = $"Max stock: {_currentProduct.stok_aktif} {_currentProduct.nama_satuan ?? "pieces"}. Auto-adjusted.";
        toast();
        
        System.Diagnostics.Debug.WriteLine($"Stock warning toast shown: {pesan}");
    }

    private void ShowOutOfStockWarning()
    {
        if (_currentProduct == null) return;

        pesan = "Product out of stock. Please check back later.";
        toast();
        
        System.Diagnostics.Debug.WriteLine($"Out of stock warning toast shown: {pesan}");
    }

    private void CalculateAndUpdateSubtotal()
    {
        if (_currentProduct == null)
        {
            E_Subtotal.Text = "0";
            L_SaveMember.Text = "";
            return;
        }

        string amountText = E_Amount.Text ?? "1";
        
        // Parse amount, default ke 1 jika invalid
        if (!int.TryParse(amountText, out int amount) || amount <= 0)
        {
            amount = 1;
        }

        // Double-check: pastikan amount tidak melebihi stock
        if (amount > _currentProduct.stok_aktif)
        {
            amount = _currentProduct.stok_aktif;
            E_Amount.Text = amount.ToString();
        }

        // Jika stock habis, set amount ke 0
        if (_currentProduct.stok_aktif <= 0)
        {
            amount = 0;
            E_Amount.Text = "0";
        }

        // Hitung subtotal: amount × harga_jual (regular price)
        long subtotalRegular = (long)amount * _currentProduct.harga_jual;
        
        // Hitung subtotal member: amount × harga_jual_member
        long subtotalMember = (long)amount * _currentProduct.harga_jual_member;
        
        // Update subtotal dengan format currency (selalu tampilkan regular price)
        E_Subtotal.Text = $"{subtotalRegular:N0}";
        
        // Hitung penghematan jika jadi member
        CalculateAndDisplayMemberSavings(subtotalRegular, subtotalMember);
        
        System.Diagnostics.Debug.WriteLine($"Calculated subtotal: {amount} × {_currentProduct.harga_jual:N0} = {subtotalRegular:N0}");
        System.Diagnostics.Debug.WriteLine($"Member subtotal: {amount} × {_currentProduct.harga_jual_member:N0} = {subtotalMember:N0}");
        System.Diagnostics.Debug.WriteLine($"Available stock: {_currentProduct.stok_aktif}");
    }

    private void CalculateAndDisplayMemberSavings(long regularTotal, long memberTotal)
    {
        if (_currentProduct == null)
        {
            L_SaveMember.Text = "";
            return;
        }

        // Hitung selisih harga (penghematan)
        long savings = regularTotal - memberTotal;
        
        if (savings > 0)
        {
            // Jika ada penghematan, tampilkan informasi member savings
            var formattedSavings = $"{savings:N0}";
            
            // Create FormattedString untuk styling berbeda
            var formattedString = new FormattedString();
            
            // Bagian "If Member, Save " dengan warna normal
            formattedString.Spans.Add(new Span
            {
                Text = "If Member, Save ",
                TextColor = Colors.Gray,
                FontFamily = "FontRegular"
            });
            
            // Bagian "Rp [amount]" dengan warna SeaGreen dan FontBold
            formattedString.Spans.Add(new Span
            {
                Text = $"Rp {formattedSavings}",
                TextColor = Color.FromArgb("#20B2AA"), // SeaGreen color
                FontFamily = "FontBold"
            });
            
            L_SaveMember.FormattedText = formattedString;
            L_SaveMember.IsVisible = true;
            
            System.Diagnostics.Debug.WriteLine($"Member savings: Rp {formattedSavings}");
        }
        else
        {
            // Jika tidak ada penghematan, sembunyikan label
            L_SaveMember.Text = "";
            L_SaveMember.IsVisible = false;
        }
    }

    // Method helper untuk mendapatkan member subtotal (untuk keperluan cart)
    public int GetMemberSubtotal()
    {
        if (_currentProduct == null) return 0;
        
        if (!int.TryParse(E_Amount.Text, out int amount) || amount <= 0)
            amount = 1;
            
        return amount * _currentProduct.harga_jual_member;
    }

    // Method helper untuk mendapatkan total savings
    public int GetMemberSavings()
    {
        if (_currentProduct == null) return 0;
        
        if (!int.TryParse(E_Amount.Text, out int amount) || amount <= 0)
            amount = 1;
            
        int regularTotal = amount * _currentProduct.harga_jual;
        int memberTotal = amount * _currentProduct.harga_jual_member;
        
        return Math.Max(0, regularTotal - memberTotal);
    }

    private void UpdateSubtotal(int subtotal)
    {
        E_Subtotal.Text = $"{subtotal:N0}";
    }

    /// <summary>
    /// Memastikan ada penjualan (faktur) untuk user ini. 
    /// Jika belum ada, buat baru. Jika sudah ada, gunakan yang existing.
    /// DIPERBAIKI: Selalu validasi database state sebelum menggunakan existing ID
    /// </summary>
    private async Task<int> EnsurePenjualanExists(int id_user)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== ENSURE PENJUALAN EXISTS ===");
            System.Diagnostics.Debug.WriteLine($"User ID: {id_user}");

            // Cek apakah sudah ada penjualan aktif untuk user ini
            int existingPenjualanId = Preferences.Get($"active_penjualan_id_{id_user}", 0);
            string existingFaktur = Preferences.Get($"active_faktur_{id_user}", string.Empty);

            System.Diagnostics.Debug.WriteLine($"Existing Penjualan ID from Preferences: {existingPenjualanId}");
            System.Diagnostics.Debug.WriteLine($"Existing Faktur from Preferences: '{existingFaktur}'");

            // PERBAIKAN: Jika ada existing ID, VALIDASI dulu apakah masih valid di database
            if (existingPenjualanId > 0 && !string.IsNullOrEmpty(existingFaktur))
            {
                System.Diagnostics.Debug.WriteLine($"=== VALIDATING EXISTING PENJUALAN ===");
                
                // Validasi dengan memanggil cart API - jika sukses berarti ID masih valid
                var validationResult = await GetCartDataAsync(existingPenjualanId);
                
                if (validationResult != null && validationResult.success)
                {
                    // ID masih valid di database
                    System.Diagnostics.Debug.WriteLine($"=== EXISTING PENJUALAN VALIDATED ===");
                    System.Diagnostics.Debug.WriteLine($"Reusing valid Penjualan ID: {existingPenjualanId}");
                    return existingPenjualanId;
                }
                else
                {
                    // ID tidak valid lagi di database, clear preferences dan buat baru
                    System.Diagnostics.Debug.WriteLine($"=== EXISTING PENJUALAN INVALID ===");
                    System.Diagnostics.Debug.WriteLine($"Validation failed: {validationResult?.message}");
                    System.Diagnostics.Debug.WriteLine("Clearing invalid preferences and creating new penjualan...");
                    
                    // Clear invalid preferences
                    Preferences.Remove($"active_penjualan_id_{id_user}");
                    Preferences.Remove($"active_faktur_{id_user}");
                }
            }

            // Belum ada penjualan aktif atau existing tidak valid, buat baru
            System.Diagnostics.Debug.WriteLine($"=== CREATING NEW PENJUALAN ===");
            System.Diagnostics.Debug.WriteLine("Creating new penjualan...");

            // Step 1: Get next suggested faktur
            System.Diagnostics.Debug.WriteLine("Step 1: Getting last faktur...");
            var lastFakturResult = await GetLastFakturAsync();
            
            if (!lastFakturResult.success)
            {
                System.Diagnostics.Debug.WriteLine($"=== GET LAST FAKTUR FAILED ===");
                System.Diagnostics.Debug.WriteLine($"Error: {lastFakturResult.message}");
                
                // PERBAIKAN: Jangan langsung return 0, coba dengan fallback faktur
                System.Diagnostics.Debug.WriteLine("Attempting to create penjualan with fallback faktur...");
                
                // Generate fallback faktur berdasarkan timestamp
                string fallbackFaktur = GenerateFallbackFaktur();
                System.Diagnostics.Debug.WriteLine($"Generated fallback faktur: {fallbackFaktur}");
                
                // Langsung ke step 2 dengan fallback faktur
                var fallbackResult = await CreatePenjualanAsync(id_user, fallbackFaktur);
                if (fallbackResult.success && fallbackResult.data != null && fallbackResult.data.id_penjualan > 0)
                {
                    int fallbackPenjualanId = fallbackResult.data.id_penjualan;
                    
                    // Save to preferences
                    Preferences.Set($"active_penjualan_id_{id_user}", fallbackPenjualanId);
                    Preferences.Set($"active_faktur_{id_user}", fallbackFaktur);
                    
                    System.Diagnostics.Debug.WriteLine($"=== FALLBACK PENJUALAN CREATED SUCCESSFULLY ===");
                    System.Diagnostics.Debug.WriteLine($"Fallback Penjualan ID: {fallbackPenjualanId}");
                    
                    return fallbackPenjualanId;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"=== FALLBACK PENJUALAN ALSO FAILED ===");
                    return 0;
                }
            }

            string newFaktur = lastFakturResult.data.next_suggested;
            System.Diagnostics.Debug.WriteLine($"Next suggested faktur: '{newFaktur}'");

            if (string.IsNullOrEmpty(newFaktur))
            {
                System.Diagnostics.Debug.WriteLine("ERROR: Next suggested faktur is empty!");
                
                // PERBAIKAN: Generate fallback faktur jika API tidak memberikan suggestion
                newFaktur = GenerateFallbackFaktur();
                System.Diagnostics.Debug.WriteLine($"Using fallback faktur: {newFaktur}");
            }

            // Step 2: Create new penjualan
            System.Diagnostics.Debug.WriteLine("Step 2: Creating penjualan...");
            var penjualanResult = await CreatePenjualanAsync(id_user, newFaktur);
            
            if (!penjualanResult.success)
            {
                System.Diagnostics.Debug.WriteLine($"=== CREATE PENJUALAN FAILED ===");
                System.Diagnostics.Debug.WriteLine($"Error: {penjualanResult.message}");
                
                // PERBAIKAN: Coba lagi dengan faktur yang berbeda jika gagal
                string retryFaktur = GenerateFallbackFaktur();
                System.Diagnostics.Debug.WriteLine($"Retrying with different faktur: {retryFaktur}");
                
                var retryResult = await CreatePenjualanAsync(id_user, retryFaktur);
                if (!retryResult.success)
                {
                    System.Diagnostics.Debug.WriteLine($"=== RETRY ALSO FAILED ===");
                    System.Diagnostics.Debug.WriteLine($"Retry Error: {retryResult.message}");
                    return 0;
                }
                penjualanResult = retryResult;
                newFaktur = retryFaktur;
            }

            // Step 3: Validate response data
            if (penjualanResult.data == null)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: Penjualan response data is null!");
                return 0;
            }

            int newPenjualanId = penjualanResult.data.id_penjualan;
            
            if (newPenjualanId <= 0)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR: Invalid penjualan ID received: {newPenjualanId}");
                return 0;
            }

            // Step 4: Save to preferences untuk session ini
            System.Diagnostics.Debug.WriteLine("Step 4: Saving to preferences...");
            Preferences.Set($"active_penjualan_id_{id_user}", newPenjualanId);
            Preferences.Set($"active_faktur_{id_user}", newFaktur);

            // Step 5: Verification step - pastikan tersimpan dengan benar
            System.Diagnostics.Debug.WriteLine("Step 5: Verifying preferences...");
            int savedId = Preferences.Get($"active_penjualan_id_{id_user}", 0);
            string savedFaktur = Preferences.Get($"active_faktur_{id_user}", string.Empty);
            
            System.Diagnostics.Debug.WriteLine($"=== PREFERENCES SAVED ===");
            System.Diagnostics.Debug.WriteLine($"Saved Penjualan ID: {savedId}");
            System.Diagnostics.Debug.WriteLine($"Saved Faktur: '{savedFaktur}'");

            if (savedId != newPenjualanId || savedFaktur != newFaktur)
            {
                System.Diagnostics.Debug.WriteLine("WARNING: Preferences save verification failed!");
                // Coba simpan ulang
                Preferences.Set($"active_penjualan_id_{id_user}", newPenjualanId);
                Preferences.Set($"active_faktur_{id_user}", newFaktur);
            }

            System.Diagnostics.Debug.WriteLine($"=== NEW PENJUALAN CREATED SUCCESSFULLY ===");
            System.Diagnostics.Debug.WriteLine($"New Penjualan ID: {newPenjualanId}");
            System.Diagnostics.Debug.WriteLine($"New Faktur: '{newFaktur}'");
            
            return newPenjualanId;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== ENSURE PENJUALAN EXISTS ERROR ===");
            System.Diagnostics.Debug.WriteLine($"Error Type: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"Error Message: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            return 0;
        }
    }

    /// <summary>
    /// Generate fallback faktur ketika API last-faktur gagal
    /// </summary>
    private string GenerateFallbackFaktur()
    {
        try
        {
            // Format: INV-YYYYMMDD-HHMMSS-RND
            var now = DateTime.Now;
            var random = new Random().Next(100, 999);
            string fallbackFaktur = $"INV-{now:yyyyMMdd}-{now:HHmmss}-{random}";
            
            System.Diagnostics.Debug.WriteLine($"Generated fallback faktur: {fallbackFaktur}");
            return fallbackFaktur;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error generating fallback faktur: {ex.Message}");
            // Ultimate fallback
            return $"INV-{DateTime.Now.Ticks}";
        }
    }

    private async Task<CartResponse> GetCartDataAsync(int penjualanId)
    {
        try
        {
            string apiUrl = $"http://{App.IP}:3000/api/penjualan/cart/{penjualanId}";
            System.Diagnostics.Debug.WriteLine($"=== VALIDATING PENJUALAN ID ===");
            System.Diagnostics.Debug.WriteLine($"URL: {apiUrl}");

            var response = await App.SharedHttpClient.GetAsync(apiUrl);
            var jsonContent = await response.Content.ReadAsStringAsync();
            
            System.Diagnostics.Debug.WriteLine($"Validation API Response Status: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"Validation API Response Content: {jsonContent}");

            if (response.IsSuccessStatusCode)
            {
                var cartResponse = JsonConvert.DeserializeObject<CartResponse>(jsonContent);
                
                // PERBAIKAN: Tambahan pengecekan untuk response yang valid
                if (cartResponse != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Cart validation success: {cartResponse.success}");
                    System.Diagnostics.Debug.WriteLine($"Cart validation message: {cartResponse.message}");
                    
                    return cartResponse;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Cart response deserialization failed");
                    return new CartResponse { success = false, message = "Invalid response format" };
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Cart validation HTTP error: {response.StatusCode}");
                
                // PERBAIKAN: Jika 404 atau error lain, anggap sebagai tidak valid (bukan error fatal)
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new CartResponse { success = false, message = "Penjualan not found (404)" };
                }
                else
                {
                    return new CartResponse { success = false, message = $"API Error: {response.StatusCode}" };
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetCartDataAsync validation error: {ex.Message}");
            
            // PERBAIKAN: Tidak langsung return error fatal, bisa jadi network issue
            return new CartResponse { success = false, message = $"Network/validation error: {ex.Message}" };
        }
    }

    // Debug method untuk clear preferences jika diperlukan
    public static void ClearCartPreferences(int id_user)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== CLEARING CART PREFERENCES ===");
            System.Diagnostics.Debug.WriteLine($"User ID: {id_user}");
            
            Preferences.Remove($"active_penjualan_id_{id_user}");
            Preferences.Remove($"active_faktur_{id_user}");
            
            System.Diagnostics.Debug.WriteLine("Cart preferences cleared successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error clearing preferences: {ex.Message}");
        }
    }

    // BARU: Method untuk debug dan reset cart state jika ada masalah
    public static void DebugAndResetCartState(int id_user)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== DEBUG CART STATE ===");
            System.Diagnostics.Debug.WriteLine($"User ID: {id_user}");
            
            var currentPenjualanId = Preferences.Get($"active_penjualan_id_{id_user}", 0);
            var currentFaktur = Preferences.Get($"active_faktur_{id_user}", string.Empty);
            
            System.Diagnostics.Debug.WriteLine($"Current Penjualan ID: {currentPenjualanId}");
            System.Diagnostics.Debug.WriteLine($"Current Faktur: '{currentFaktur}'");
            
            // Force clear jika ada data yang tidak konsisten
            if (currentPenjualanId > 0 && string.IsNullOrEmpty(currentFaktur))
            {
                System.Diagnostics.Debug.WriteLine("Inconsistent state detected: ID exists but no faktur. Clearing...");
                ClearCartPreferences(id_user);
            }
            else if (currentPenjualanId <= 0 && !string.IsNullOrEmpty(currentFaktur))
            {
                System.Diagnostics.Debug.WriteLine("Inconsistent state detected: Faktur exists but no ID. Clearing...");
                ClearCartPreferences(id_user);
            }
            else if (currentPenjualanId > 0 && !string.IsNullOrEmpty(currentFaktur))
            {
                System.Diagnostics.Debug.WriteLine("Cart state appears consistent, but might be stale. Consider clearing if problems persist.");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Cart state is clean (no active transaction)");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in debug cart state: {ex.Message}");
        }
    }

    private async Task<LastFakturResponse> GetLastFakturAsync()
    {
        try
        {
            string apiUrl = $"http://{App.IP}:3000/api/penjualan/last-faktur";
            System.Diagnostics.Debug.WriteLine($"Getting last faktur from: {apiUrl}");

            // Send GET request
            var response = await App.SharedHttpClient.GetAsync(apiUrl);
            var jsonContent = await response.Content.ReadAsStringAsync();
            
            System.Diagnostics.Debug.WriteLine($"Last Faktur API Response Status: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"Last Faktur API Response Content: {jsonContent}");

            if (response.IsSuccessStatusCode)
            {
                var lastFakturResponse = JsonConvert.DeserializeObject<LastFakturResponse>(jsonContent);
                return lastFakturResponse ?? new LastFakturResponse { success = false, message = "Invalid response format" };
            }
            else
            {
                return new LastFakturResponse { success = false, message = $"API Error: {response.StatusCode}" };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetLastFakturAsync error: {ex.Message}");
            return new LastFakturResponse { success = false, message = $"Network error: {ex.Message}" };
        }
    }

    private async Task<PenjualanResponse> CreatePenjualanAsync(int id_user, string faktur)
    {
        try
        {
            string apiUrl = $"http://{App.IP}:3000/api/penjualan";
            System.Diagnostics.Debug.WriteLine($"=== CREATE PENJUALAN DEBUG ===");
            System.Diagnostics.Debug.WriteLine($"URL: {apiUrl}");
            System.Diagnostics.Debug.WriteLine($"User: {id_user}, Faktur: '{faktur}'");
            System.Diagnostics.Debug.WriteLine($"App.IP: {App.IP}");

            // Validasi input sebelum kirim
            if (string.IsNullOrEmpty(faktur))
            {
                System.Diagnostics.Debug.WriteLine("ERROR: Faktur is null or empty!");
                return new PenjualanResponse { success = false, message = "Faktur tidak boleh kosong" };
            }

            if (id_user <= 0)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR: Invalid user ID: {id_user}");
                return new PenjualanResponse { success = false, message = "User ID tidak valid" };
            }

            // Prepare form data dengan lebih eksplisit
            var formParams = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("id_user", id_user.ToString()),
                new KeyValuePair<string, string>("faktur", faktur)
            };

            // Debug: Print form data yang akan dikirim
            System.Diagnostics.Debug.WriteLine("=== FORM DATA TO SEND ===");
            foreach (var param in formParams)
            {
                System.Diagnostics.Debug.WriteLine($"{param.Key} = '{param.Value}'");
            }

            var formContent = new FormUrlEncodedContent(formParams);
            
            // Set explicit headers
            formContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");

            System.Diagnostics.Debug.WriteLine($"Content-Type: {formContent.Headers.ContentType}");
            
            // Test network connectivity dulu
            System.Diagnostics.Debug.WriteLine("=== SENDING POST REQUEST ===");
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30))) // 30 second timeout
            {
                var response = await App.SharedHttpClient.PostAsync(apiUrl, formContent, cts.Token);
                var jsonContent = await response.Content.ReadAsStringAsync();
                
                System.Diagnostics.Debug.WriteLine($"=== RESPONSE RECEIVED ===");
                System.Diagnostics.Debug.WriteLine($"Status Code: {response.StatusCode} ({(int)response.StatusCode})");
                System.Diagnostics.Debug.WriteLine($"Status Description: {response.ReasonPhrase}");
                System.Diagnostics.Debug.WriteLine($"Raw Response Content: {jsonContent}");
                System.Diagnostics.Debug.WriteLine($"Content Length: {jsonContent?.Length ?? 0} characters");

                if (response.IsSuccessStatusCode)
                {
                    if (string.IsNullOrWhiteSpace(jsonContent))
                    {
                        System.Diagnostics.Debug.WriteLine("ERROR: Response content is empty!");
                        return new PenjualanResponse { success = false, message = "Server returned empty response" };
                    }

                    try
                    {
                        var penjualanResponse = JsonConvert.DeserializeObject<PenjualanResponse>(jsonContent);
                        
                        if (penjualanResponse == null)
                        {
                            System.Diagnostics.Debug.WriteLine("ERROR: Failed to deserialize response to PenjualanResponse");
                            return new PenjualanResponse { success = false, message = "Invalid response format" };
                        }

                        System.Diagnostics.Debug.WriteLine($"=== PARSED RESPONSE ===");
                        System.Diagnostics.Debug.WriteLine($"Success: {penjualanResponse.success}");
                        System.Diagnostics.Debug.WriteLine($"Message: '{penjualanResponse.message}'");
                        
                        if (penjualanResponse.data != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"Data.id_penjualan: {penjualanResponse.data.id_penjualan}");
                            System.Diagnostics.Debug.WriteLine($"Data.faktur: '{penjualanResponse.data.faktur}'");
                            System.Diagnostics.Debug.WriteLine($"Data.id_user: '{penjualanResponse.data.id_user}'");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("WARNING: Response data is null");
                        }

                        if (penjualanResponse.success)
                        {
                            System.Diagnostics.Debug.WriteLine($"=== PENJUALAN CREATED SUCCESSFULLY ===");
                            return penjualanResponse;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"=== API RETURNED ERROR ===");
                            System.Diagnostics.Debug.WriteLine($"Error message: {penjualanResponse.message}");
                            return penjualanResponse;
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"=== JSON DESERIALIZATION ERROR ===");
                        System.Diagnostics.Debug.WriteLine($"JSON Error: {jsonEx.Message}");
                        System.Diagnostics.Debug.WriteLine($"Raw JSON: {jsonContent}");
                        return new PenjualanResponse { success = false, message = $"JSON parse error: {jsonEx.Message}" };
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"=== HTTP ERROR ===");
                    System.Diagnostics.Debug.WriteLine($"Status: {response.StatusCode}");
                    System.Diagnostics.Debug.WriteLine($"Reason: {response.ReasonPhrase}");
                    System.Diagnostics.Debug.WriteLine($"Content: {jsonContent}");
                    
                    return new PenjualanResponse 
                    { 
                        success = false, 
                        message = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase} - {jsonContent}" 
                    };
                }
            }
        }
        catch (TaskCanceledException timeoutEx)
        {
            System.Diagnostics.Debug.WriteLine($"=== REQUEST TIMEOUT ===");
            System.Diagnostics.Debug.WriteLine($"Timeout Error: {timeoutEx.Message}");
            return new PenjualanResponse { success = false, message = "Request timeout - server tidak merespon dalam 30 detik" };
        }
        catch (HttpRequestException httpEx)
        {
            System.Diagnostics.Debug.WriteLine($"=== HTTP REQUEST ERROR ===");
            System.Diagnostics.Debug.WriteLine($"HTTP Error: {httpEx.Message}");
            return new PenjualanResponse { success = false, message = $"Network error: {httpEx.Message}" };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== UNEXPECTED ERROR ===");
            System.Diagnostics.Debug.WriteLine($"Error Type: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"Error Message: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            return new PenjualanResponse { success = false, message = $"Unexpected error: {ex.Message}" };
        }
    }

    private async Task<PenjualanDetailResponse> AddItemToCartAsync(int id_penjualan, int id_barang, int jumlah_jual, int harga_jual)
    {
        try
        {
            string apiUrl = $"http://{App.IP}:3000/api/penjualan/detail";
            System.Diagnostics.Debug.WriteLine($"=== ADD ITEM TO CART ===");
            System.Diagnostics.Debug.WriteLine($"URL: {apiUrl}");
            System.Diagnostics.Debug.WriteLine($"Penjualan ID: {id_penjualan}, Barang ID: {id_barang}, Jumlah: {jumlah_jual}, Harga: {harga_jual}");

            // Prepare form data sesuai backend expectation
            var formParams = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("id_penjualan", id_penjualan.ToString()),
                new KeyValuePair<string, string>("id_barang", id_barang.ToString()),
                new KeyValuePair<string, string>("jumlah_jual", jumlah_jual.ToString()),
                new KeyValuePair<string, string>("harga_jual", harga_jual.ToString()),
                new KeyValuePair<string, string>("diskon", "0") // Default diskon = 0
            };

            var formContent = new FormUrlEncodedContent(formParams);

            // Send POST request
            var response = await App.SharedHttpClient.PostAsync(apiUrl, formContent);
            var jsonContent = await response.Content.ReadAsStringAsync();
            
            System.Diagnostics.Debug.WriteLine($"Response Status: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"Response Content: {jsonContent}");

            if (response.IsSuccessStatusCode)
            {
                var detailResponse = JsonConvert.DeserializeObject<PenjualanDetailResponse>(jsonContent);
                
                if (detailResponse != null && detailResponse.success)
                {
                    System.Diagnostics.Debug.WriteLine($"Item added to cart - Action: {detailResponse.action}, ID: {detailResponse.data.id}");
                    return detailResponse;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"API returned success=false: {detailResponse?.message}");
                    return new PenjualanDetailResponse { success = false, message = detailResponse?.message ?? "Unknown error" };
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"HTTP Error: {response.StatusCode}");
                return new PenjualanDetailResponse { success = false, message = $"HTTP {response.StatusCode}: {jsonContent}" };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AddItemToCartAsync error: {ex.Message}");
            return new PenjualanDetailResponse { success = false, message = $"Network error: {ex.Message}" };
        }
    }

    private async Task LoadSimilarProductsAsync(int categoryId, int excludeProductId)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== LOADING SIMILAR PRODUCTS ===");
            System.Diagnostics.Debug.WriteLine($"Category ID: {categoryId}, Current Product ID: {excludeProductId}");

            var similarProducts = await GetSimilarProductsAsync(categoryId, excludeProductId);

            if (similarProducts != null && similarProducts.Count > 0)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    SimilarProducts.Clear();
                    foreach (var product in similarProducts)
                    {
                        SimilarProducts.Add(product);
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Similar products loaded: {SimilarProducts.Count} items");
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No similar products found");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    SimilarProducts.Clear();
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading similar products: {ex.Message}");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SimilarProducts.Clear();
            });
        }
    }

    private async Task<List<Product>> GetSimilarProductsAsync(int idKategori, int idBarang)
    {
        try
        {
            string apiUrl = $"http://{App.IP}:3000/api/barang/sekategori?id_kategori={idKategori}&id_barang={idBarang}";
            System.Diagnostics.Debug.WriteLine($"Calling Similar Products API: {apiUrl}");

            var response = await App.SharedHttpClient.GetAsync(apiUrl);
            var jsonContent = await response.Content.ReadAsStringAsync();
            
            System.Diagnostics.Debug.WriteLine($"Similar Products API Response Status: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"Similar Products API Response Content: {jsonContent}");

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = JsonConvert.DeserializeObject<ApiResponse<List<Product>>>(jsonContent);
                
                if (apiResponse != null && apiResponse.success && apiResponse.data != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Similar products found: {apiResponse.data.Count} items");
                    return apiResponse.data;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("API returned no similar products");
                    return new List<Product>();
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Similar Products API Error: {response.StatusCode}");
                return new List<Product>();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetSimilarProductsAsync error: {ex.Message}");
            return new List<Product>();
        }
    }
    
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Cancel image loading jika user keluar dari halaman
        _isImageLoading = false;
    }

    private async void TapBack_Tapped(object sender, TappedEventArgs e)
    {
        // Visual feedback
        if (sender is Image image)
        {
            await image.FadeTo(0.3, 100);
            await image.FadeTo(1, 200);
        }

        // Gunakan logika yang sama dengan B_Cancel_Clicked
        bool result = await DisplayAlert("Confirmation", "Are you sure you want to go back?", "Yes", "No");

        if (result)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== BACK IMAGE - NAVIGATING BACK ===");
                
                // DetailProduct dipanggil dengan Navigation.PushAsync(), jadi kita harus PopAsync() untuk kembali
                if (Navigation.NavigationStack.Count > 1)
                {
                    await Navigation.PopAsync();
                    System.Diagnostics.Debug.WriteLine($"=== NAVIGATION POP COMPLETED ===");
                }
                else
                {
                    // Fallback: jika stack kosong, gunakan Shell navigation ke TabPage
                    if (Shell.Current != null)
                    {
                        await Shell.Current.GoToAsync("//ListProduct");
                        System.Diagnostics.Debug.WriteLine($"=== SHELL NAVIGATION COMPLETED ===");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("ERROR: Both Navigation and Shell.Current failed");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"=== BACK IMAGE NAVIGATION ERROR ===");
                System.Diagnostics.Debug.WriteLine($"Back image navigation error: {ex.Message}");
            }
        }
    }

    private async void TapAddTocart_Tapped(object sender, TappedEventArgs e)
    {
        // Visual feedback
        if (sender is Image image)
        {
            await image.FadeTo(0.3, 100);
            await image.FadeTo(1, 200);
        }

        // Gunakan logika yang sama dengan B_AddToCart_Clicked
        try
        {
            // === VALIDASI INPUT ===
            if (_currentProduct == null)
            {
                pesan = "Product data not loaded";
                toast();
                return;
            }

            if (!int.TryParse(E_Amount.Text, out int amount) || amount <= 0)
            {
                pesan = "Please enter valid amount";
                toast();
                E_Amount.Focus();
                return;
            }

            if (amount > _currentProduct.stok_aktif)
            {
                pesan = $"Amount exceeds available stock ({_currentProduct.stok_aktif})";
                toast();
                return;
            }

            // === GET USER LOGIN ===
            var (id_user, username, nama_lengkap, id_sesi, email, hp) = Login.GetLoggedInUser();
            
            if (id_user <= 0)
            {
                pesan = "User not logged in. Please login first.";
                toast();
                return;
            }

            System.Diagnostics.Debug.WriteLine($"=== ADD TO CART IMAGE START ===");
            System.Diagnostics.Debug.WriteLine($"User: {id_user}, Product: {_currentProduct.id_barang}, Amount: {amount}");

            // BARU: Debug cart state sebelum memulai proses
            DebugAndResetCartState(id_user);

            // === STEP 1: ENSURE PENJUALAN EXISTS ===
            int penjualanId = await EnsurePenjualanExists(id_user);
            
            if (penjualanId <= 0)
            {
                System.Diagnostics.Debug.WriteLine($"=== PENJUALAN CREATION FAILED ===");
                System.Diagnostics.Debug.WriteLine($"User ID: {id_user}");
                System.Diagnostics.Debug.WriteLine($"Returned Penjualan ID: {penjualanId}");
                
                // PERBAIKAN: Berikan pesan error yang lebih spesifik
                var existingId = Preferences.Get($"active_penjualan_id_{id_user}", 0);
                var existingFaktur = Preferences.Get($"active_faktur_{id_user}", string.Empty);
                
                System.Diagnostics.Debug.WriteLine($"Current preferences - ID: {existingId}, Faktur: '{existingFaktur}'");
                
                pesan = $"Failed to create transaction. Please try again. (User: {id_user})";
                toast();
                return;
            }

            System.Diagnostics.Debug.WriteLine($"Penjualan ID secured: {penjualanId}");

            // === STEP 2: ADD ITEM TO CART ===
            var detailResult = await AddItemToCartAsync(penjualanId, _currentProduct.id_barang, amount, _currentProduct.harga_jual);

            if (!detailResult.success)
            {
                pesan = detailResult.message ?? "Failed to add item to cart";
                toast();
                return;
            }

            // === SUCCESS ===
            System.Diagnostics.Debug.WriteLine($"=== ADD TO CART IMAGE SUCCESS ===");
            
            // Safe null checks untuk detailResult
            string actionText = detailResult?.action ?? "unknown";
            string itemIdText = detailResult?.data?.id.ToString() ?? "unknown";
            
            System.Diagnostics.Debug.WriteLine($"Action: {actionText}, Item ID: {itemIdText}");

            // Safe null checks untuk _currentProduct
            string productName = _currentProduct?.nama_barang ?? "Unknown Product";
            string unitName = _currentProduct?.nama_satuan ?? "pcs";
            
            pesan = $"Successfully added {amount} {unitName} of {productName} to cart!";
            
            System.Diagnostics.Debug.WriteLine($"=== SUCCESS MESSAGE PREPARED ===");
            System.Diagnostics.Debug.WriteLine($"Message: {pesan}");
            
            toast();
            
            System.Diagnostics.Debug.WriteLine($"=== TOAST DISPLAYED ===");
            
            // Navigate back after success - FIXED: Always go to ListProduct
            System.Diagnostics.Debug.WriteLine($"=== STARTING NAVIGATION TO LISTPRODUCT ===");
            await Task.Delay(1500);
            
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== NAVIGATING BACK TO ListProduct ===");
                
                // PERBAIKAN: Setelah add to cart berhasil, selalu kembali ke ListProduct
                // Tidak lagi menggunakan PopAsync() yang bisa kembali ke similar product sebelumnya
                await Shell.Current.GoToAsync("//ListProduct");
                System.Diagnostics.Debug.WriteLine($"=== SHELL NAVIGATION TO ListProduct COMPLETED ===");
            }
            catch (Exception navEx)
            {
                System.Diagnostics.Debug.WriteLine($"=== NAVIGATION ERROR ===");
                System.Diagnostics.Debug.WriteLine($"Navigation error: {navEx.Message}");
                
                // Fallback: Gunakan PopToRootAsync untuk clear navigation stack, lalu Shell navigation
                try
                {
                    System.Diagnostics.Debug.WriteLine($"=== ATTEMPTING FALLBACK NAVIGATION ===");
                    await Navigation.PopToRootAsync();
                    await Shell.Current.GoToAsync("//ListProduct");
                    System.Diagnostics.Debug.WriteLine($"=== FALLBACK NAVIGATION COMPLETED ===");
                }
                catch (Exception fallbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"=== FALLBACK NAVIGATION ALSO FAILED ===");
                    System.Diagnostics.Debug.WriteLine($"Fallback error: {fallbackEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== ADD TO CART IMAGE ERROR ===");
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
            pesan = $"Error adding to cart: {ex.Message}";
            toast();
        }
    }

    private async void OnSimilarProductTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (sender is Image image && image.BindingContext is Product selectedProduct)
            {
                System.Diagnostics.Debug.WriteLine($"=== SIMILAR PRODUCT TAPPED ===");
                System.Diagnostics.Debug.WriteLine($"Product: {selectedProduct.nama_barang} (ID: {selectedProduct.id_barang})");

                // Prevent multiple taps during loading
                if (_isNavigatingToSimilarProduct)
                {
                    System.Diagnostics.Debug.WriteLine("Already navigating, ignoring tap");
                    return;
                }

                _isNavigatingToSimilarProduct = true;

                try
                {
                    // Visual feedback
                    await image.ScaleTo(0.95, 100);
                    await image.ScaleTo(1.0, 100);

                    // Show loading toast
                    pesan = $"Loading {selectedProduct.nama_barang}...";
                    toast();

                    // Create and show loading overlay dynamically
                    var loadingOverlay = new Grid
                    {
                        BackgroundColor = Color.FromArgb("#80000000"),
                        IsVisible = true
                    };

                    Grid.SetRowSpan(loadingOverlay, 2);

                    var loadingContent = new StackLayout
                    {
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.Center,
                        Spacing = 15,
                        Children =
                        {
                            new ActivityIndicator
                            {
                                Color = Colors.White,
                                IsRunning = true,
                                HeightRequest = 60,
                                WidthRequest = 60
                            },
                            new Label
                            {
                                Text = "Loading Product...",
                                TextColor = Colors.White,
                                FontSize = 18,
                                FontFamily = "FontBold",
                                HorizontalOptions = LayoutOptions.Center
                            },
                            new Label
                            {
                                Text = "Please wait",
                                TextColor = Colors.White,
                                FontSize = 14,
                                HorizontalOptions = LayoutOptions.Center,
                                Opacity = 0.8
                            }
                        }
                    };

                    loadingOverlay.Children.Add(loadingContent);

                    // Add overlay to main grid
                    if (this.Content is Grid mainGrid)
                    {
                        mainGrid.Children.Add(loadingOverlay);
                    }

                    // Loading delay (1-3 seconds for smooth UX)
                    var random = new Random();
                    int loadingDelay = random.Next(1000, 3000);
                    System.Diagnostics.Debug.WriteLine($"Loading delay: {loadingDelay}ms");

                    await Task.Delay(loadingDelay);

                    // Remove loading overlay before navigation
                    if (this.Content is Grid grid)
                    {
                        grid.Children.Remove(loadingOverlay);
                    }

                    // Navigate to new DetailProduct
                    System.Diagnostics.Debug.WriteLine($"Navigating to DetailProduct for ID: {selectedProduct.id_barang}");
                    await Navigation.PushAsync(new DetailProduct(selectedProduct.id_barang));

                    System.Diagnostics.Debug.WriteLine("=== NAVIGATION COMPLETED ===");
                }
                catch (Exception navEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Navigation error: {navEx.Message}");

                    // Remove loading overlay on error
                    if (this.Content is Grid grid)
                    {
                        var overlay = grid.Children.FirstOrDefault(x => x is Grid g && g.BackgroundColor == Color.FromArgb("#80000000"));
                        if (overlay != null)
                            grid.Children.Remove(overlay);
                    }

                    pesan = "Error loading product details";
                    toast();
                }
                finally
                {
                    _isNavigatingToSimilarProduct = false;
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No product context found for tap");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling similar product tap: {ex.Message}");
            _isNavigatingToSimilarProduct = false;
            pesan = "Error opening product details";
            toast();
        }
    }
}