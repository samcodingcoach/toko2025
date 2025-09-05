using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Toko2025.Services;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace Toko2025.Cart;

public partial class List_Cart : ContentPage, INotifyPropertyChanged
{
    private ObservableCollection<CartItem> _cartItems = new();
    private CartSummary _cartSummary = new();
    private bool _isLoading = false;
    private bool _isEmpty = true;
    private bool _isDebtEnabled = false;
    private int _currentMemberId = 0; // Store current member ID
    private bool _isDebtPaymentMode = false; // NEW: Detect if in debt payment mode
    private string pesan = string.Empty;

    public ObservableCollection<CartItem> CartItems
    {
        get => _cartItems;
        set
        {
            _cartItems = value;
            OnPropertyChanged();
        }
    }

    public CartSummary CartSummary
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
        }
    }

    public bool IsEmpty
    {
        get => _isEmpty;
        set
        {
            _isEmpty = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasItems));
        }
    }

    public bool HasItems => !IsEmpty;

    public bool IsDebtEnabled
    {
        get => _isDebtEnabled;
        set
        {
            _isDebtEnabled = value;
            OnPropertyChanged();
        }
    }

    // NEW: Property untuk mode pembayaran hutang
    public bool IsDebtPaymentMode
    {
        get => _isDebtPaymentMode;
        set
        {
            _isDebtPaymentMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanDeleteCart)); // Update delete cart permission
        }
    }

    // NEW: Property untuk mengontrol permission delete cart
    public bool CanDeleteCart => !IsDebtPaymentMode;

    public List_Cart()
    {
        InitializeComponent();
        BindingContext = this;
        
        IsLoading = false;
        IsEmpty = true;
        CartItems.Clear();
        CartSummary = new CartSummary();
        
        LoadCartData();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        System.Diagnostics.Debug.WriteLine("=== LIST_CART OnAppearing ===");
        
        // Check if coming from debt payment mode
        CheckDebtPaymentMode();
        
        // Only load regular cart data if not in debt payment mode
        if (!IsDebtPaymentMode)
        {
            LoadCartData();
        }
    }

    private void CheckDebtPaymentMode()
    {
        try
        {
            // Check if there's a flag indicating debt payment mode
            bool debtPaymentFlag = Preferences.Get("debt_payment_mode", false);
            
            System.Diagnostics.Debug.WriteLine($"=== CHECKING DEBT PAYMENT MODE ===");
            System.Diagnostics.Debug.WriteLine($"Debt payment flag: {debtPaymentFlag}");
            
            if (debtPaymentFlag)
            {
                System.Diagnostics.Debug.WriteLine("=== DEBT PAYMENT MODE DETECTED ===");
                IsDebtPaymentMode = true;
                
                // Remove the flag after detection
                Preferences.Remove("debt_payment_mode");
                
                // Load debt transaction data immediately
                LoadDebtTransactionData();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("=== NORMAL CART MODE ===");
                IsDebtPaymentMode = false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking debt payment mode: {ex.Message}");
            IsDebtPaymentMode = false;
        }
    }

    private async void LoadCartData()
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsLoading = true;
                IsEmpty = false;
                // Reset debt status on cart reload
                _currentMemberId = 0;
                IsDebtEnabled = false;
            });

            var (id_user, _, _, _, _, _) = Login.GetLoggedInUser();
            
            if (id_user <= 0)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CartItems.Clear();
                    CartSummary = new CartSummary();
                    IsEmpty = true;
                    // Show discount section when cart is empty
                    BorderDiscount.IsVisible = true;
                });
                return;
            }

            int penjualanId = Preferences.Get($"active_penjualan_id_{id_user}", 0);
            
            if (penjualanId <= 0)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CartItems.Clear();
                    CartSummary = new CartSummary();
                    IsEmpty = true;
                    // Show discount section when no active penjualan
                    BorderDiscount.IsVisible = true;
                });
                return;
            }

            var cartResponse = await GetCartDataAsync(penjualanId);
            
            if (cartResponse != null && cartResponse.success)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CartItems = new ObservableCollection<CartItem>(cartResponse.data.items);
                    CartSummary = cartResponse.data.summary;
                    IsEmpty = !CartItems.Any();
                    
                    // Show discount section if cart has items and no discount applied yet
                    // Hide if discount is already applied (total_diskon > 0)
                    BorderDiscount.IsVisible = CartItems.Any() && CartSummary.total_diskon <= 0;
                });
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CartItems.Clear();
                    CartSummary = new CartSummary();
                    IsEmpty = true;
                    // Show discount section when cart load fails
                    BorderDiscount.IsVisible = true;
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading cart: {ex.Message}");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                CartItems.Clear();
                CartSummary = new CartSummary();
                IsEmpty = true;
                // Show discount section on error
                BorderDiscount.IsVisible = true;
            });
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsLoading = false;
            });
        }
    }

    private async Task<CartResponse> GetCartDataAsync(int penjualanId)
    {
        try
        {
            string apiUrl = $"{App.IP}/api/penjualan/cart/{penjualanId}";
            var response = await App.SharedHttpClient.GetAsync(apiUrl);
            var jsonContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var cartResponse = JsonConvert.DeserializeObject<CartResponse>(jsonContent);
                return cartResponse ?? new CartResponse { success = false, message = "Invalid response format" };
            }
            else
            {
                return new CartResponse { success = false, message = $"API Error: {response.StatusCode}" };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetCartDataAsync error: {ex.Message}");
            return new CartResponse { success = false, message = $"Network error: {ex.Message}" };
        }
    }

    private async void B_Checkout_Clicked(object sender, EventArgs e)
    {
        if (sender is Button image)
        {
            await image.FadeTo(0.3, 100);
            await image.FadeTo(1, 200);
        }

        if (IsEmpty || !CartItems.Any())
        {
            await DisplayAlert("Cart Empty", "Please add items to cart before checkout", "OK");
            return;
        }

        //await Navigation.PushAsync(new Cart.Payment());
        var cartBottomSheet = new Cart.CheckOut();
        await cartBottomSheet.ShowAsync();
    }

    // SwipeView Event Handler - Delete Only
    private async void OnDeleteItemSwipe(object sender, EventArgs e)
    {
        try
        {
            if (sender is SwipeItem swipeItem && swipeItem.BindingContext is CartItem cartItem)
            {
                bool confirmDelete = await DisplayAlert(
                    "Remove Item", 
                    $"Remove {cartItem.nama_barang} from cart?", 
                    "Remove", 
                    "Cancel");

                if (confirmDelete)
                {
                    await DeleteCartItem(cartItem);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in delete item swipe: {ex.Message}");
            pesan = "Error removing item";
            toast();
        }
    }

    private async Task DeleteCartItem(CartItem cartItem)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== DELETE CART ITEM ===");
            System.Diagnostics.Debug.WriteLine($"Item: {cartItem.nama_barang}");
            System.Diagnostics.Debug.WriteLine($"id_penjualan: {cartItem.id_penjualan}");
            System.Diagnostics.Debug.WriteLine($"id_barang: {cartItem.id_barang}");

            string apiUrl = $"{App.IP}/api/penjualan/detail";
            
            // Prepare form data sesuai endpoint yang diberikan
            var formParams = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("id_penjualan", cartItem.id_penjualan.ToString()),
                new KeyValuePair<string, string>("id_barang", cartItem.id_barang.ToString())
            };

            var formContent = new FormUrlEncodedContent(formParams);

            System.Diagnostics.Debug.WriteLine("Sending DELETE request with form data:");
            System.Diagnostics.Debug.WriteLine($"id_penjualan={cartItem.id_penjualan}&id_barang={cartItem.id_barang}");

            var response = await App.SharedHttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Delete, apiUrl)
            {
                Content = formContent
            });

            var jsonContent = await response.Content.ReadAsStringAsync();
            
            System.Diagnostics.Debug.WriteLine($"Delete item response: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"Response content: {jsonContent}");

            if (response.IsSuccessStatusCode)
            {
                // Reload cart data to reflect changes and update discount visibility
                LoadCartData();
                
                pesan = $"{cartItem.nama_barang} removed from cart";
                toast();
            }
            else
            {
                pesan = "Failed to remove item";
                toast();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DeleteCartItem error: {ex.Message}");
            pesan = "Error removing item";
            toast();
        }
    }

    private async void TapDel_Penjualan_Tapped(object sender, TappedEventArgs e)
    {
        // Check if delete is allowed in debt payment mode
        if (IsDebtPaymentMode)
        {
            await DisplayAlert("Action Not Allowed", "Cannot clear cart in debt payment mode", "OK");
            return;
        }

        if (sender is Image image)
        {
            await image.FadeTo(0.3, 100);
            await image.FadeTo(1, 200);
        }

        bool confirm = await DisplayAlert("Clear Cart", "Are you sure you want to clear your cart?", "Yes", "No");
        
        if (confirm)
        {
            var (id_user, _, _, _, _, _) = Login.GetLoggedInUser();
            
            if (id_user <= 0)
            {
                await DisplayAlert("Error", "User not logged in", "OK");
                return;
            }

            int penjualanId = Preferences.Get($"active_penjualan_id_{id_user}", 0);
            
            if (penjualanId <= 0)
            {
                await DisplayAlert("Info", "Cart is already empty", "OK");
                return;
            }

            bool deleteSuccess = await DeletePenjualanAsync(penjualanId);
            
            if (deleteSuccess)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CartItems.Clear();
                    CartSummary = new CartSummary();
                    IsEmpty = true;
                    // Show discount section when cart is cleared
                    BorderDiscount.IsVisible = true;
                    // Clear discount entry
                    T_NoHp.Text = string.Empty;
                });
                
                Preferences.Remove($"active_penjualan_id_{id_user}");
                Preferences.Remove($"active_faktur_{id_user}");
                
                pesan = "Cart cleared successfully";
                toast();
                
                // Navigate to ListProduct using TabPage navigation pattern
                if (Application.Current?.MainPage is TabPage tabPage)
                {
                    await tabPage.GoToAsync("//ListProduct");
                }
                else
                {
                    Application.Current.MainPage = new TabPage();
                }
            }
            else
            {
                await DisplayAlert("Error", "Failed to clear cart. Please try again.", "OK");
            }
        }
    }

    private async Task<bool> DeletePenjualanAsync(int penjualanId)
    {
        try
        {
            string apiUrl = $"{App.IP}/api/penjualan/{penjualanId}";
            var response = await App.SharedHttpClient.DeleteAsync(apiUrl);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DeletePenjualanAsync error: {ex.Message}");
            return false;
        }
    }

    private async void toast()
    {
        try
        {
            if (string.IsNullOrEmpty(pesan))
                pesan = "Operation completed";

            var toast = Toast.Make(pesan, ToastDuration.Long, 12);
            await toast.Show(new CancellationTokenSource().Token);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Toast error: {ex.Message}");
        }
    }

    private async void B_ApplyDiscountMember_Clicked(object sender, EventArgs e)
    {
        // Check if in debt payment mode
        if (IsDebtPaymentMode)
        {
            await DisplayAlert("Action Not Allowed", "Member discount already applied for debt payment", "OK");
            return;
        }

        // Disable button saat processing
        if (sender is Button btn)
        {
            btn.IsEnabled = false;
            btn.Text = "Applying...";
        }

        try
        {
            // Validasi input
            if (string.IsNullOrWhiteSpace(T_NoHp.Text))
            {
                pesan = "Please enter member phone number";
                toast();
                return;
            }

            // Validasi user login
            var (id_user, _, _, _, _, _) = Login.GetLoggedInUser();
            if (id_user <= 0)
            {
                pesan = "User not logged in";
                toast();
                return;
            }

            // Get active penjualan ID
            int penjualanId = Preferences.Get($"active_penjualan_id_{id_user}", 0);
            if (penjualanId <= 0)
            {
                pesan = "No active cart found";
                toast();
                return;
            }

            // Call member search API
            var memberResult = await SearchMemberAndApplyDiscountAsync(T_NoHp.Text, penjualanId);

            if (memberResult.success)
            {
                // Success - hide discount border and refresh cart
                BorderDiscount.IsVisible = false;
                LoadCartData();
                
                // Enable debt button if member found and id_member >= 2
                if (memberResult.data?.member != null && memberResult.data.member.id_member >= 2)
                {
                    _currentMemberId = memberResult.data.member.id_member;
                    IsDebtEnabled = true;
                    System.Diagnostics.Debug.WriteLine($"Debt enabled for member ID: {_currentMemberId}");
                }
                else
                {
                    _currentMemberId = 0;
                    IsDebtEnabled = false;
                    System.Diagnostics.Debug.WriteLine($"Debt disabled - member ID: {memberResult.data?.member?.id_member ?? 0}");
                }
                
                // Show success message with member name
                if (memberResult.data?.member != null)
                {
                    pesan = $"Member discount applied for {memberResult.data.member.nama_member}";
                }
                else
                {
                    pesan = "Member discount applied successfully";
                }
                toast();
            }
            else
            {
                // Reset debt availability on failure
                _currentMemberId = 0;
                IsDebtEnabled = false;
                
                pesan = memberResult.message ?? "Failed to apply member discount";
                toast();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Apply discount error: {ex.Message}");
            pesan = "Error applying discount";
            toast();
        }
        finally
        {
            // Enable button kembali
            if (sender is Button button)
            {
                button.IsEnabled = true;
                button.Text = "Apply";
            }
        }
    }

    private async Task<MemberSearchResponse> SearchMemberAndApplyDiscountAsync(string hp, int idPenjualan)
    {
        try
        {
            string apiUrl = $"{App.IP}/api/member/search";
            
            System.Diagnostics.Debug.WriteLine($"=== MEMBER SEARCH API ===");
            System.Diagnostics.Debug.WriteLine($"URL: {apiUrl}");
            System.Diagnostics.Debug.WriteLine($"HP: {hp}");
            System.Diagnostics.Debug.WriteLine($"ID Penjualan: {idPenjualan}");

            // Prepare form data
            var formParams = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("hp", hp),
                new KeyValuePair<string, string>("id_penjualan", idPenjualan.ToString())
            };

            var formContent = new FormUrlEncodedContent(formParams);

            // Send POST request
            var response = await App.SharedHttpClient.PostAsync(apiUrl, formContent);
            var jsonContent = await response.Content.ReadAsStringAsync();
            
            System.Diagnostics.Debug.WriteLine($"Member Search Response Status: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"Member Search Response Content: {jsonContent}");

            if (response.IsSuccessStatusCode)
            {
                var memberResponse = JsonConvert.DeserializeObject<MemberSearchResponse>(jsonContent);
                return memberResponse ?? new MemberSearchResponse { success = false, message = "Invalid response format" };
            }
            else
            {
                return new MemberSearchResponse { success = false, message = $"API Error: {response.StatusCode}" };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SearchMemberAndApplyDiscountAsync error: {ex.Message}");
            return new MemberSearchResponse { success = false, message = $"Network error: {ex.Message}" };
        }
    }

    private async void B_Debt_Clicked(object sender, EventArgs e)
    {
        if (sender is Button btn)
        {
            await btn.FadeTo(0.3, 100);
            await btn.FadeTo(1, 200);
            
            // Disable button saat processing
            btn.IsEnabled = false;
            btn.Text = "Processing...";
        }

        try
        {
            // Validasi member sudah ada dan valid
            if (_currentMemberId < 2)
            {
                pesan = "Please apply member discount first. Member ID must be valid.";
                toast();
                return;
            }

            // Validasi user login
            var (id_user, _, _, _, _, _) = Login.GetLoggedInUser();
            if (id_user <= 0)
            {
                pesan = "User not logged in";
                toast();
                return;
            }

            // Get active penjualan ID
            int penjualanId = Preferences.Get($"active_penjualan_id_{id_user}", 0);
            if (penjualanId <= 0)
            {
                pesan = "No active cart found";
                toast();
                return;
            }

            // Validasi cart tidak kosong
            if (IsEmpty || !CartItems.Any())
            {
                pesan = "Cart is empty. Please add items before saving as debt.";
                toast();
                return;
            }

            // Konfirmasi dengan user
            bool confirmDebt = await DisplayAlert(
                "Save as Debt", 
                $"Save this transaction as debt for member ID {_currentMemberId}?", 
                "Save as Debt", 
                "Cancel");

            if (!confirmDebt)
            {
                return;
            }

            // Call API simpan hutang
            var debtResult = await SaveDebtAsync(penjualanId, _currentMemberId);

            if (debtResult != null && debtResult.success)
            {
                // Clear cart preferences
                Preferences.Remove($"active_penjualan_id_{id_user}");
                Preferences.Remove($"active_faktur_{id_user}");
                
                // Reset member data
                _currentMemberId = 0;
                IsDebtEnabled = false;
                
                // Show success message
                pesan = "Transaction saved as debt successfully";
                toast();
                
                // Wait for toast to show
                await Task.Delay(2000);
                
                // Navigate to ListProduct using TabPage navigation pattern
                if (Application.Current?.MainPage is TabPage tabPage)
                {
                    await tabPage.GoToAsync("//ListProduct");
                }
                else
                {
                    Application.Current.MainPage = new TabPage();
                }
            }
            else
            {
                pesan = debtResult?.message ?? "Failed to save debt";
                toast();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Debt save error: {ex.Message}");
            pesan = "Error saving debt";
            toast();
        }
        finally
        {
            // Enable button kembali
            if (sender is Button button)
            {
                button.IsEnabled = true;
                button.Text = "Debt";
            }
        }
    }

    private async Task<DebtResponse> SaveDebtAsync(int penjualanId, int memberId)
    {
        try
        {
            string apiUrl = $"{App.IP}/api/penjualan/simpan_hutang";
            
            System.Diagnostics.Debug.WriteLine($"=== SAVE DEBT API ===");
            System.Diagnostics.Debug.WriteLine($"URL: {apiUrl}");
            System.Diagnostics.Debug.WriteLine($"Penjualan ID: {penjualanId}");
            System.Diagnostics.Debug.WriteLine($"Member ID: {memberId}");

            // Calculate grand_total from cart summary
            double grandTotal = CartSummary.total_amount - CartSummary.total_diskon;
            
            // Prepare form data sesuai spesifikasi lengkap
            var formParams = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("id_penjualan", penjualanId.ToString()),
                new KeyValuePair<string, string>("id_member", memberId.ToString()),
                new KeyValuePair<string, string>("biaya_lain", "0"), // Default 0 untuk debt
                new KeyValuePair<string, string>("potongan", CartSummary.total_diskon.ToString("F2")), // Gunakan discount yang sudah ada
                new KeyValuePair<string, string>("id_pembayaran", "1"), // Default payment method untuk debt (misalnya cash)
                new KeyValuePair<string, string>("aktif", "0"), // Set aktif = 0 untuk debt status
                new KeyValuePair<string, string>("grand_total", grandTotal.ToString("F2"))
            };

            System.Diagnostics.Debug.WriteLine($"=== REQUEST PARAMETERS ===");
            foreach (var param in formParams)
            {
                System.Diagnostics.Debug.WriteLine($"{param.Key}: {param.Value}");
            }

            var formContent = new FormUrlEncodedContent(formParams);

            // Send POST request
            var response = await App.SharedHttpClient.PostAsync(apiUrl, formContent);
            var jsonContent = await response.Content.ReadAsStringAsync();
            
            System.Diagnostics.Debug.WriteLine($"Save Debt Response Status: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"Save Debt Response Content: {jsonContent}");

            if (response.IsSuccessStatusCode)
            {
                var debtResponse = JsonConvert.DeserializeObject<DebtResponse>(jsonContent);
                return debtResponse ?? new DebtResponse { success = false, message = "Invalid response format" };
            }
            else
            {
                return new DebtResponse { success = false, message = $"API Error: {response.StatusCode}" };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SaveDebtAsync error: {ex.Message}");
            return new DebtResponse { success = false, message = $"Network error: {ex.Message}" };
        }
    }

    private async void LoadDebtTransactionData()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== LOADING DEBT TRANSACTION DATA ===");
            
            // Get debt transaction info from preferences
            string debtFaktur = Preferences.Get("debt_faktur", "");
            int debtIdPenjualan = Preferences.Get("debt_id_penjualan", 0); // NEW: Get id_penjualan for checkout
            
            System.Diagnostics.Debug.WriteLine($"Debt payment info:");
            System.Diagnostics.Debug.WriteLine($"  Faktur: {debtFaktur}");
            System.Diagnostics.Debug.WriteLine($"  ID Penjualan: {debtIdPenjualan}");
            
            if (!string.IsNullOrEmpty(debtFaktur))
            {
                System.Diagnostics.Debug.WriteLine($"Loading debt transaction: {debtFaktur}");
                
                // FIXED: Set the debt id_penjualan for checkout to work
                if (debtIdPenjualan > 0)
                {
                    var (id_user, _, _, _, _, _) = Login.GetLoggedInUser();
                    if (id_user > 0)
                    {
                        // Set active penjualan ID for this debt payment session
                        Preferences.Set($"active_penjualan_id_{id_user}", debtIdPenjualan);
                        
                        // NEW: Set flag for debt payment mode detection in CheckOut
                        Preferences.Set("was_debt_payment_navigation", true);
                        
                        System.Diagnostics.Debug.WriteLine($"Set active_penjualan_id_{id_user} = {debtIdPenjualan} for debt payment checkout");
                        System.Diagnostics.Debug.WriteLine("Set debt payment navigation flag for CheckOut");
                    }
                }
                
                // First, get transaction info from main history API to get member info
                var historyItem = await GetDebtTransactionInfoAsync(debtFaktur);
                
                // Then load cart data from history API
                var historyData = await GetDebtTransactionFromHistoryAsync(debtFaktur);
                
                if (historyData != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        // Convert history items to cart items with correct id_penjualan
                        var cartItems = ConvertHistoryToCartItems(historyData.items, debtIdPenjualan);
                        CartItems = new ObservableCollection<CartItem>(cartItems);
                        
                        // Use history summary data
                        CartSummary = ConvertHistorySummaryToCartSummary(historyData.summary);
                        
                        IsEmpty = !CartItems.Any();
                        
                        // Auto-fill member info if available from history
                        if (historyItem != null && !string.IsNullOrEmpty(historyItem.nama_member))
                        {
                            // Extract phone from member name or use placeholder
                            string memberPhone = ExtractPhoneFromMemberName(historyItem.nama_member);
                            T_NoHp.Text = memberPhone;
                            T_NoHp.IsReadOnly = true; // Make read-only in debt payment mode
                            
                            System.Diagnostics.Debug.WriteLine($"Auto-filled member: {historyItem.nama_member}, Phone: {memberPhone}");
                        }
                        
                        BorderDiscount.IsVisible = false; // Hide discount section in debt mode
                        
                        // Disable debt button in payment mode (can't create debt from debt)
                        IsDebtEnabled = false;
                        
                        System.Diagnostics.Debug.WriteLine($"Debt transaction loaded: {CartItems.Count} items");
                        System.Diagnostics.Debug.WriteLine($"Total Amount: {CartSummary.FormattedTotalAmount}");
                        System.Diagnostics.Debug.WriteLine($"Total Discount: {CartSummary.FormattedTotalDiskon}");
                    });
                }
            }
            
            // Clean up preferences
            Preferences.Remove("debt_faktur");
            Preferences.Remove("debt_id_penjualan"); // NEW: Clean up id_penjualan
            Preferences.Remove("debt_member_id");
            Preferences.Remove("debt_member_phone");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading debt transaction data: {ex.Message}");
        }
    }

    private async Task<HistoryItem> GetDebtTransactionInfoAsync(string faktur)
    {
        try
        {
            // We need to get this from the main history API that contains member info
            var (id_user, _, _, _, _, _) = Login.GetLoggedInUser();
            
            if (id_user <= 0) return null;
            
            string startDate = DateTime.Now.ToString("yyyy-MM-dd");
            string apiUrl = $"{App.IP}/api/history?start_date={startDate}&id_user={id_user}";
            
            System.Diagnostics.Debug.WriteLine($"Getting transaction info from history API: {apiUrl}");

            var response = await App.SharedHttpClient.GetAsync(apiUrl);
            var jsonContent = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var historyResponse = JsonConvert.DeserializeObject<HistoryResponse>(jsonContent);
                
                if (historyResponse != null && historyResponse.success)
                {
                    // Find the transaction with matching faktur
                    var transaction = historyResponse.data.FirstOrDefault(x => x.faktur == faktur);
                    return transaction;
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting debt transaction info: {ex.Message}");
            return null;
        }
    }

    private string ExtractPhoneFromMemberName(string memberName)
    {
        // This is a placeholder implementation
        // You might need to adjust based on how member names are stored
        // For now, return a placeholder that indicates it's from debt
        return "[Debt Payment]";
    }

    private async Task<HistoryCartData> GetDebtTransactionFromHistoryAsync(string faktur)
    {
        try
        {
            string apiUrl = $"{App.IP}/api/history/cart/{faktur}";
            System.Diagnostics.Debug.WriteLine($"Calling History Cart API for debt: {apiUrl}");

            var response = await App.SharedHttpClient.GetAsync(apiUrl);
            var jsonContent = await response.Content.ReadAsStringAsync();
            
            System.Diagnostics.Debug.WriteLine($"History Cart API Response: {response.StatusCode}");
            
            if (response.IsSuccessStatusCode)
            {
                var historyResponse = JsonConvert.DeserializeObject<HistoryCartResponse>(jsonContent);
                return historyResponse?.data;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting debt transaction from history: {ex.Message}");
            return null;
        }
    }

    private List<CartItem> ConvertHistoryToCartItems(List<HistoryCartItem> historyItems, int idPenjualan = 0)
    {
        var cartItems = new List<CartItem>();
        
        foreach (var historyItem in historyItems)
        {
            // Calculate new subtotal: price * quantity (ignoring history subtotal)
            double calculatedSubtotal = historyItem.harga_jual * historyItem.jumlah_jual;
            
            cartItems.Add(new CartItem
            {
                id_detail_penjualan = 0, // Not applicable for debt payment
                id_penjualan = idPenjualan, // FIXED: Use actual id_penjualan for checkout
                id_barang = 0, // Not applicable for debt payment
                nama_barang = historyItem.nama_barang,
                gambar1 = !string.IsNullOrEmpty(historyItem.gambar1) ? historyItem.gambar1 : "default_product.png",
                gambar2 = "",
                jumlah_jual = historyItem.jumlah_jual,
                harga_jual = historyItem.harga_jual,
                diskon = historyItem.diskon, // Keep original discount per item
                subtotal = calculatedSubtotal, // FIXED: Calculate fresh subtotal (price * quantity)
                nama_merk = !string.IsNullOrEmpty(historyItem.nama_merk) ? historyItem.nama_merk : "Unknown Brand"
            });
            
            System.Diagnostics.Debug.WriteLine($"Item: {historyItem.nama_barang}");
            System.Diagnostics.Debug.WriteLine($"  Original subtotal from history: {historyItem.subtotal:N0}");
            System.Diagnostics.Debug.WriteLine($"  Calculated subtotal (price * qty): {calculatedSubtotal:N0}");
            System.Diagnostics.Debug.WriteLine($"  Price: {historyItem.harga_jual:N0}, Qty: {historyItem.jumlah_jual}");
            System.Diagnostics.Debug.WriteLine($"  ID Penjualan: {idPenjualan}");
        }
        
        return cartItems;
    }

    private CartSummary ConvertHistorySummaryToCartSummary(HistoryCartSummary historySummary)
    {
        // Calculate fresh total_amount from recalculated cart items
        double recalculatedTotalAmount = CartItems.Sum(item => item.subtotal);
        
        System.Diagnostics.Debug.WriteLine($"=== CART SUMMARY CONVERSION ===");
        System.Diagnostics.Debug.WriteLine($"Original total_amount from history: {historySummary.total_amount:N0}");
        System.Diagnostics.Debug.WriteLine($"Recalculated total_amount (from items): {recalculatedTotalAmount:N0}");
        System.Diagnostics.Debug.WriteLine($"Total discount from history: {historySummary.total_diskon:N0}");
        
        return new CartSummary
        {
            total_items = historySummary.total_items,
            total_qty = historySummary.total_qty,
            total_amount = recalculatedTotalAmount, // FIXED: Use recalculated total from items
            total_diskon = historySummary.total_diskon // Keep original discount from history
        };
    }
}

// DebtResponse model untuk API response
public class DebtResponse
{
    public bool success { get; set; }
    public string message { get; set; } = string.Empty;
    public int affectedRows { get; set; }
}