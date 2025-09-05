using System.Text;
using System.Net.Http;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Toko2025.Services;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace Toko2025.OrderHistory;

public partial class DetailList : ContentPage, INotifyPropertyChanged
{
    private string _faktur = string.Empty;
    private int _hutang = 0; // NEW: Store hutang status
    private int _idPenjualan = 0; // NEW: Store id_penjualan for debt payment
    private ObservableCollection<HistoryCartItem> _cartItems = new ObservableCollection<HistoryCartItem>();
    private HistoryCartSummary _cartSummary = new HistoryCartSummary();
    private bool _isLoading = false;
    private string _pesan = string.Empty;

    public ObservableCollection<HistoryCartItem> CartItems
    {
        get => _cartItems;
        set
        {
            _cartItems = value;
            OnPropertyChanged();
            UpdateVisibility();
        }
    }

    public HistoryCartSummary CartSummary
    {
        get => _cartSummary;
        set
        {
            _cartSummary = value;
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
            UpdateVisibility();
        }
    }

    // NEW: Property untuk status hutang
    public bool IsDebt => _hutang == 1;

    public DetailList(string faktur = "", int hutang = 0, int idPenjualan = 0)
    {
        InitializeComponent();
        BindingContext = this;
        
        _faktur = faktur;
        _hutang = hutang; // Store hutang status
        _idPenjualan = idPenjualan; // Store id_penjualan for debt payment
        
        // Update header label with faktur - TANPA indikator HUTANG
        if (!string.IsNullOrEmpty(_faktur))
        {
            L_Faktur.Text = $"Item Order History: {_faktur}";
        }
        
        System.Diagnostics.Debug.WriteLine($"DetailList initialized - Faktur: {_faktur}, Hutang: {_hutang}, ID Penjualan: {_idPenjualan}");
        
        LoadCartDetailData();
    }

    private void UpdateVisibility()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                // Find UI elements with proper names
                var listView = this.FindByName<ListView>("ItemsListView"); 
                var emptyStateLayout = this.FindByName<StackLayout>("EmptyStateLayout");
                
                bool isEmpty = !IsLoading && (CartItems == null || CartItems.Count == 0);
                bool hasItems = !IsLoading && CartItems != null && CartItems.Count > 0;
                
                System.Diagnostics.Debug.WriteLine($"=== UPDATE VISIBILITY ===");
                System.Diagnostics.Debug.WriteLine($"IsLoading: {IsLoading}");
                System.Diagnostics.Debug.WriteLine($"CartItems count: {CartItems?.Count ?? 0}");
                System.Diagnostics.Debug.WriteLine($"isEmpty: {isEmpty}");
                System.Diagnostics.Debug.WriteLine($"hasItems: {hasItems}");
                
                // Show/hide ListView
                if (listView != null)
                {
                    listView.IsVisible = hasItems;
                    System.Diagnostics.Debug.WriteLine($"ListView.IsVisible set to: {hasItems}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("WARNING: ListView 'ItemsListView' not found!");
                }
                
                // Show/hide empty state
                if (emptyStateLayout != null)
                {
                    emptyStateLayout.IsVisible = isEmpty;
                    System.Diagnostics.Debug.WriteLine($"EmptyStateLayout.IsVisible set to: {isEmpty}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("WARNING: EmptyStateLayout not found!");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in UpdateVisibility: {ex.Message}");
            }
        });
    }

    private async void LoadCartDetailData()
    {
        try
        {
            IsLoading = true;
            
            if (string.IsNullOrEmpty(_faktur))
            {
                _pesan = "Faktur is required to load cart details";
                await ShowToast();
                return;
            }

            System.Diagnostics.Debug.WriteLine($"Loading cart detail for faktur: {_faktur}");
            
            // Call API
            var cartDetailResponse = await GetCartDetailAsync(_faktur);
            
            if (cartDetailResponse != null && cartDetailResponse.success)
            {
                // Update UI with data
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CartItems.Clear();
                    
                    // Add items to collection with row index for alternating colors
                    if (cartDetailResponse.data.items != null)
                    {
                        for (int i = 0; i < cartDetailResponse.data.items.Count; i++)
                        {
                            var item = cartDetailResponse.data.items[i];
                            item.RowIndex = i; // Set the row index for alternating colors
                            CartItems.Add(item);
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"Added {cartDetailResponse.data.items.Count} items to CartItems collection with alternating colors");
                    }
                    
                    CartSummary = cartDetailResponse.data.summary ?? new HistoryCartSummary();
                    
                    // Update header with actual faktur from API - TANPA indikator HUTANG
                    if (!string.IsNullOrEmpty(cartDetailResponse.data.faktur))
                    {
                        L_Faktur.Text = $"Item Order History: {cartDetailResponse.data.faktur}";
                    }
                    
                    // Set visibility untuk status hutang dan tombol pay debt
                    var statusHutangLabel = this.FindByName<Label>("L_StatusHutang");
                    var payDebtButton = this.FindByName<Button>("B_PayDebt");
                    
                    if (statusHutangLabel != null)
                    {
                        statusHutangLabel.IsVisible = IsDebt;
                    }
                    
                    if (payDebtButton != null)
                    {
                        payDebtButton.IsVisible = IsDebt;
                    }
                    
                    // Log debt status for debugging
                    System.Diagnostics.Debug.WriteLine($"Transaction {cartDetailResponse.data.faktur} - Debt status: {_hutang} (IsDebt: {IsDebt})");
                    System.Diagnostics.Debug.WriteLine($"Status hutang label visible: {statusHutangLabel?.IsVisible}");
                    System.Diagnostics.Debug.WriteLine($"Pay debt button visible: {payDebtButton?.IsVisible}");
                });
                
                System.Diagnostics.Debug.WriteLine($"Cart detail loaded: {cartDetailResponse.data.items?.Count ?? 0} items");
            }
            else
            {
                _pesan = cartDetailResponse?.message ?? "Failed to load cart detail data";
                await ShowToast();
                
                // Clear UI
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CartItems.Clear();
                    CartSummary = new HistoryCartSummary();
                });
            }
        }
        catch (Exception ex)
        {
            _pesan = $"Error loading cart detail: {ex.Message}";
            await ShowToast();
            System.Diagnostics.Debug.WriteLine($"LoadCartDetailData error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task<HistoryCartResponse> GetCartDetailAsync(string faktur)
    {
        try
        {
            string apiUrl = $"{App.IP}/api/history/cart/{faktur}";
            System.Diagnostics.Debug.WriteLine($"Calling Cart Detail API: {apiUrl}");

            var response = await App.SharedHttpClient.GetAsync(apiUrl);
            var jsonContent = await response.Content.ReadAsStringAsync();
            
            System.Diagnostics.Debug.WriteLine($"Cart Detail API Response Status: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"Cart Detail API Response Content: {jsonContent}");

            if (response.IsSuccessStatusCode)
            {
                var cartDetailResponse = JsonConvert.DeserializeObject<HistoryCartResponse>(jsonContent);
                return cartDetailResponse ?? new HistoryCartResponse { success = false, message = "Invalid response format" };
            }
            else
            {
                return new HistoryCartResponse { success = false, message = $"API Error: {response.StatusCode}" };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetCartDetailAsync error: {ex.Message}");
            return new HistoryCartResponse { success = false, message = $"Network error: {ex.Message}" };
        }
    }

    private async Task ShowToast()
    {
        try
        {
            if (string.IsNullOrEmpty(_pesan))
                _pesan = "Operation completed";

            var toast = Toast.Make(_pesan, ToastDuration.Long, 12);
            await toast.Show(new CancellationTokenSource().Token);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Toast error: {ex.Message}");
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void BackTap_Tapped(object sender, TappedEventArgs e)
    {
        if (sender is Image image)
        {
            await image.FadeTo(0.3, 100); // Turunkan opacity ke 0.3 dalam 100ms
            await image.FadeTo(1, 200);   // Kembalikan opacity ke 1 dalam 200ms
        }

        try
        {
            System.Diagnostics.Debug.WriteLine($"=== BACK NAVIGATION START ===");
            
            // Since DetailList was opened with Navigation.PushAsync from List_History,
            // we can simply use PopAsync to return to List_History
            if (Navigation.NavigationStack.Count > 1)
            {
                System.Diagnostics.Debug.WriteLine("Using PopAsync to return to List_History");
                await Navigation.PopAsync();
                System.Diagnostics.Debug.WriteLine("PopAsync completed successfully");
            }
            else
            {
                // Fallback: Navigate to List_History via Shell if navigation stack is empty
                System.Diagnostics.Debug.WriteLine("Navigation stack empty, using Shell navigation to ListHistory");
                
                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync("//ListHistory");
                    System.Diagnostics.Debug.WriteLine("Shell navigation to ListHistory completed");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Shell.Current is null, using fallback navigation");
                    // Ultimate fallback - navigate back to TabPage
                    Application.Current.MainPage = new TabPage();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== BACK NAVIGATION ERROR ===");
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
            
            // Error fallback - try Shell navigation
            try
            {
                System.Diagnostics.Debug.WriteLine("Attempting fallback Shell navigation");
                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync("//ListHistory");
                    System.Diagnostics.Debug.WriteLine("Fallback navigation successful");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Shell fallback also failed, setting MainPage");
                    Application.Current.MainPage = new TabPage();
                }
            }
            catch (Exception fallbackEx)
            {
                System.Diagnostics.Debug.WriteLine($"Fallback navigation also failed: {fallbackEx.Message}");
                
                // Show error message to user
                _pesan = "Navigation error occurred";
                await ShowToast();
            }
        }
    }

    private async void B_PayDebt_Clicked(object sender, EventArgs e)
    {
        if (sender is Button button)
        {
            await button.FadeTo(0.3, 100); // Visual feedback
            await button.FadeTo(1, 200);
        }

        try
        {
            System.Diagnostics.Debug.WriteLine($"=== PAY DEBT NAVIGATION START ===");
            System.Diagnostics.Debug.WriteLine($"Navigating to TabPage Cart for debt payment");
            
            // Set debt payment mode flag and debt transaction info WITH id_penjualan
            Preferences.Set("debt_payment_mode", true);
            Preferences.Set("debt_faktur", _faktur);
            Preferences.Set("debt_id_penjualan", _idPenjualan); // NEW: Pass id_penjualan for checkout
            Preferences.Set("debt_member_id", 0); // Will be filled from history data
            Preferences.Set("debt_member_phone", ""); // Will be filled from history data
            
            // NEW: Set flag to reset History tab to List_History after debt payment
            Preferences.Set("reset_history_tab_after_debt", true);
            
            System.Diagnostics.Debug.WriteLine($"Set debt payment preferences:");
            System.Diagnostics.Debug.WriteLine($"  debt_faktur: {_faktur}");
            System.Diagnostics.Debug.WriteLine($"  debt_id_penjualan: {_idPenjualan}");
            System.Diagnostics.Debug.WriteLine($"  reset_history_tab_after_debt: true");
            
            // FIXED: Navigate to Cart tab and ensure History tab is reset to List_History
            try
            {
                // Method 1: Direct Shell navigation to TabPage Cart route
                if (Shell.Current != null)
                {
                    System.Diagnostics.Debug.WriteLine("Using Shell navigation to //ListCart");
                    await Shell.Current.GoToAsync("//ListCart");
                    System.Diagnostics.Debug.WriteLine("Shell navigation to ListCart completed");
                    
                    // Reset History tab navigation stack by navigating to ListHistory first
                    await ResetHistoryTabAfterDelay();
                    return;
                }
            }
            catch (Exception shellEx)
            {
                System.Diagnostics.Debug.WriteLine($"Shell navigation failed: {shellEx.Message}");
            }

            try
            {
                // Method 2: Navigate via TabPage if current MainPage is TabPage
                if (Application.Current?.MainPage is TabPage tabPage)
                {
                    System.Diagnostics.Debug.WriteLine("Current MainPage is TabPage, navigating to ListCart");
                    await tabPage.GoToAsync("//ListCart");
                    System.Diagnostics.Debug.WriteLine("TabPage navigation to ListCart completed");
                    
                    // Reset History tab navigation stack
                    await ResetHistoryTabAfterDelay();
                    return;
                }
            }
            catch (Exception tabEx)
            {
                System.Diagnostics.Debug.WriteLine($"TabPage navigation failed: {tabEx.Message}");
            }

            // Method 3: Set MainPage to TabPage and navigate to Cart
            System.Diagnostics.Debug.WriteLine("Setting MainPage to TabPage and navigating to Cart");
            
            // Create new TabPage instance
            var newTabPage = new TabPage();
            Application.Current.MainPage = newTabPage;
            
            // Wait for TabPage to initialize
            await Task.Delay(300);
            
            // Navigate to Cart tab
            await newTabPage.GoToAsync("//ListCart");
            
            // Reset History tab navigation stack
            await ResetHistoryTabAfterDelay();
            
            System.Diagnostics.Debug.WriteLine("Successfully navigated to TabPage Cart");
            
            // Show success message
            _pesan = "Navigated to cart for debt payment";
            await ShowToast();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== PAY DEBT NAVIGATION ERROR ===");
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
            
            _pesan = "Navigation error occurred";
            await ShowToast();
        }
    }
    
    // NEW: Method untuk reset History tab ke List_History setelah delay
    private async Task ResetHistoryTabAfterDelay()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== RESETTING HISTORY TAB NAVIGATION ===");
            
            // Wait a bit to ensure Cart navigation is complete
            await Task.Delay(500);
            
            // Navigate History tab to ListHistory to reset navigation stack
            if (Shell.Current != null)
            {
                // This will reset the History tab to show List_History instead of DetailList
                await Shell.Current.GoToAsync("//ListHistory");
                System.Diagnostics.Debug.WriteLine("History tab reset to ListHistory");
            }
            else if (Application.Current?.MainPage is TabPage tabPage)
            {
                await tabPage.GoToAsync("//ListHistory");
                System.Diagnostics.Debug.WriteLine("History tab reset via TabPage");
            }
            
            // Then navigate back to Cart to continue debt payment
            await Task.Delay(100);
            
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync("//ListCart");
                System.Diagnostics.Debug.WriteLine("Returned to Cart tab for debt payment");
            }
            else if (Application.Current?.MainPage is TabPage tabPage2)
            {
                await tabPage2.GoToAsync("//ListCart");
                System.Diagnostics.Debug.WriteLine("Returned to Cart tab via TabPage");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error resetting History tab: {ex.Message}");
        }
    }
}