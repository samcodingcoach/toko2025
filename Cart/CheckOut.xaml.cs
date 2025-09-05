﻿﻿using The49.Maui.BottomSheet;
using System.Text;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Toko2025.Services;
using System.Globalization;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Microsoft.Maui.Media;

namespace Toko2025.Cart;

public partial class CheckOut : BottomSheet, INotifyPropertyChanged
{
    private CartSummary _cartSummary = new();
    private decimal _subtotalProduct = 0;
    private decimal _diskon = 0;
    private decimal _feePayment = 0; // Untuk sementara 0
    private decimal _grandTotal = 0;
    private decimal _amountCustomerPayment = 0;
    private decimal _cashReturn = 0;
    private bool _isFormattingAmount = false; // Flag untuk mencegah infinite loop

    // QRIS related fields
    private System.Timers.Timer _countdownTimer;
    private System.Timers.Timer _statusCheckTimer; // Auto-check timer 
    private int _remainingSeconds = 300; // 5 minutes in seconds
    private string _currentOrderId = string.Empty;
    private string _currentTransactionId = string.Empty;
    private int _currentPenjualanId = 0; // Store penjualan ID for status checking

    // Add these fields at the top of the class
    private MemoryStream _photoStream;
    private string _photoFileName = string.Empty;
    private string _photoLocalPath = string.Empty;
    
    // NEW: Field untuk mendeteksi debt payment mode
    private bool _isDebtPaymentMode = false;

    public CartSummary CartSummary
    {
        get => _cartSummary;
        set
        {
            _cartSummary = value;
            OnPropertyChanged();
            UpdateCalculations();
        }
    }

    public string SubtotalProductText => $"Rp {_subtotalProduct:N0}";
    public string DiskonText => $"Rp {_diskon:N0}";
    public string FeePaymentText => $"Rp {_feePayment:N0}";
    public string GrandTotalText => $"Rp {_grandTotal:N0}";
    public string CashReturnText => $"Rp {_cashReturn:N0}";

    public CheckOut()
    {
        InitializeComponent();
        BindingContext = this;
        
        // Subscribe to radio button events
        var cashRadio = this.FindByName<RadioButton>("R_Cash");
        var qrisRadio = this.FindByName<RadioButton>("R_QRIS");
        var bankTransferRadio = this.FindByName<RadioButton>("R_BankTransfer");
        
        cashRadio.CheckedChanged += OnPaymentMethodChanged;
        qrisRadio.CheckedChanged += OnPaymentMethodChanged;
        bankTransferRadio.CheckedChanged += OnPaymentMethodChanged;
        
        // Subscribe to amount customer payment changes
        var amountEntry = this.FindByName<Entry>("E_AmountCustomerPayment");
        amountEntry.TextChanged += OnAmountCustomerPaymentChanged;
        
        // Subscribe to checkbox confirmation changes
        var confirmationCheckbox = this.FindByName<CheckBox>("CB_Confirmation");
        if (confirmationCheckbox != null)
        {
            confirmationCheckbox.CheckedChanged += OnConfirmationChanged;
        }
        
        // NEW: Check if this is debt payment mode
        CheckDebtPaymentMode();
        
        LoadCartData();
    }
    
    // NEW: Method untuk mengecek debt payment mode
    private void CheckDebtPaymentMode()
    {
        try
        {
            // Check if we're in debt payment mode by checking if active_penjualan_id was set from debt payment
            var (id_user, _, _, _, _, _) = Login.GetLoggedInUser();
            
            if (id_user > 0)
            {
                int activePenjualanId = Preferences.Get($"active_penjualan_id_{id_user}", 0);
                
                // Check if there was a recent debt payment navigation
                bool wasDebtPayment = Preferences.Get("was_debt_payment_navigation", false);
                
                if (wasDebtPayment && activePenjualanId > 0)
                {
                    _isDebtPaymentMode = true;
                    System.Diagnostics.Debug.WriteLine($"=== CHECKOUT IN DEBT PAYMENT MODE ===");
                    System.Diagnostics.Debug.WriteLine($"Penjualan ID: {activePenjualanId}");
                    
                    // Clean up the flag
                    Preferences.Remove("was_debt_payment_navigation");
                }
                else
                {
                    _isDebtPaymentMode = false;
                    System.Diagnostics.Debug.WriteLine("=== CHECKOUT IN NORMAL MODE ===");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking debt payment mode in CheckOut: {ex.Message}");
            _isDebtPaymentMode = false;
        }
    }
    
    private void OnPaymentMethodChanged(object sender, CheckedChangedEventArgs e)
    {
        if (e.Value) // Only handle when radio button is checked (not unchecked)
        {
            var cashStack = this.FindByName<StackLayout>("StackLayout_Cash");
            var qrisStack = this.FindByName<ScrollView>("StackLayout_Qris"); // Changed to ScrollView
            var bankTransferStack = this.FindByName<ScrollView>("StackLayout_Banktransfer");
            
            // Hide all StackLayouts first
            cashStack.IsVisible = false;
            qrisStack.IsVisible = false;
            bankTransferStack.IsVisible = false;
            
            var cashRadio = this.FindByName<RadioButton>("R_Cash");
            var qrisRadio = this.FindByName<RadioButton>("R_QRIS");
            var bankTransferRadio = this.FindByName<RadioButton>("R_BankTransfer");
            
            // Show the appropriate StackLayout based on which radio button was checked
            if (sender == cashRadio)
            {
                cashStack.IsVisible = true;
            }
            else if (sender == qrisRadio)
            {
                qrisStack.IsVisible = true;
            }
            else if (sender == bankTransferRadio)
            {
                bankTransferStack.IsVisible = true;
            }
        }
    }
    
    private async void LoadCartData()
    {
        try
        {
            var (id_user, _, _, _, _, _) = Login.GetLoggedInUser();
            
            if (id_user <= 0)
            {
                return;
            }

            int penjualanId = Preferences.Get($"active_penjualan_id_{id_user}", 0);
            
            if (penjualanId <= 0)
            {
                return;
            }

            var cartResponse = await GetCartDataAsync(penjualanId);
            
            if (cartResponse != null && cartResponse.success)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CartSummary = cartResponse.data.summary;
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading cart data in CheckOut: {ex.Message}");
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
    
    private void UpdateCalculations()
    {
        // Update values dari CartSummary
        _subtotalProduct = (decimal)CartSummary.total_amount;
        _diskon = (decimal)CartSummary.total_diskon;
        _feePayment = 0; // Untuk sementara 0
        _grandTotal = _subtotalProduct - _diskon + _feePayment;
        
        // Update UI
        UpdateLabels();
        CalculateCashReturn();
    }
    
    private void UpdateLabels()
    {
        var subtotalLabel = this.FindByName<Label>("L_SubtotalProduct");
        var diskonLabel = this.FindByName<Label>("L_Diskon");
        var feeLabel = this.FindByName<Label>("L_FeePayment");
        var grandTotalLabel = this.FindByName<Label>("L_GrandTotal");
        
        // Update label-label di UI dengan data yang sudah dihitung
        subtotalLabel.Text = SubtotalProductText;
        diskonLabel.Text = DiskonText;
        feeLabel.Text = FeePaymentText;
        grandTotalLabel.Text = GrandTotalText;
        
        // Trigger property changed untuk binding
        OnPropertyChanged(nameof(SubtotalProductText));
        OnPropertyChanged(nameof(DiskonText));
        OnPropertyChanged(nameof(FeePaymentText));
        OnPropertyChanged(nameof(GrandTotalText));
        OnPropertyChanged(nameof(CashReturnText));
    }
    
    private void OnAmountCustomerPaymentChanged(object sender, TextChangedEventArgs e)
    {
        if (_isFormattingAmount) return; // Mencegah infinite loop
        
        var amountEntry = sender as Entry;
        if (amountEntry == null) return;

        // Format input dengan pemisah ribuan
        FormatAmountInput(amountEntry);
        
        // Lakukan kalkulasi
        CalculateCashReturn();
        ValidatePaymentAmount();
    }
    
    private void FormatAmountInput(Entry amountEntry)
    {
        try
        {
            _isFormattingAmount = true;
            
            string input = amountEntry.Text ?? "";
            
            // Hilangkan semua karakter non-digit
            string cleanInput = new string(input.Where(char.IsDigit).ToArray());
            
            if (string.IsNullOrEmpty(cleanInput))
            {
                amountEntry.Text = "";
                return;
            }
            
            // Convert ke decimal untuk formatting
            if (decimal.TryParse(cleanInput, out decimal amount))
            {
                // Format dengan pemisah ribuan (contoh: 60.000)
                string formattedAmount = amount.ToString("N0", CultureInfo.InvariantCulture);
                
                // Update text jika berbeda (untuk menghindari loop)
                if (amountEntry.Text != formattedAmount)
                {
                    int cursorPosition = amountEntry.CursorPosition;
                    amountEntry.Text = formattedAmount;
                    
                    // Usahakan mempertahankan posisi cursor yang masuk akal
                    amountEntry.CursorPosition = Math.Min(cursorPosition, formattedAmount.Length);
                }
                
                System.Diagnostics.Debug.WriteLine($"Amount formatted: {cleanInput} -> {formattedAmount}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error formatting amount: {ex.Message}");
        }
        finally
        {
            _isFormattingAmount = false;
        }
    }
    
    private void CalculateCashReturn()
    {
        var amountEntry = this.FindByName<Entry>("E_AmountCustomerPayment");
        var cashReturnEntry = this.FindByName<Entry>("E_CashReturn");
        
        // Parse amount customer payment (hilangkan pemisah ribuan untuk parsing)
        string cleanAmount = new string((amountEntry.Text ?? "").Where(char.IsDigit).ToArray());
        
        if (decimal.TryParse(cleanAmount, out decimal customerPayment))
        {
            _amountCustomerPayment = customerPayment;
        }
        else
        {
            _amountCustomerPayment = 0;
        }
        
        // Hitung kembalian: customer payment - grand total
        _cashReturn = _amountCustomerPayment - _grandTotal;
        
        // Update UI dengan perhitungan kembalian
        if (_cashReturn >= 0)
        {
            // Pembayaran cukup, tampilkan kembalian normal
            cashReturnEntry.Text = _cashReturn.ToString("N0");
            cashReturnEntry.TextColor = Color.FromArgb("#333333"); // Warna normal
        }
        else
        {
            // Pembayaran tidak cukup, tampilkan defisit dalam warna merah
            var deficit = Math.Abs(_cashReturn);
            cashReturnEntry.Text = $"-{deficit:N0}";
            cashReturnEntry.TextColor = Colors.Red; // Warna merah untuk pembayaran tidak cukup
        }
        
        OnPropertyChanged(nameof(CashReturnText));
    }
    
    private void ValidatePaymentAmount()
    {
        var paymentButton = this.FindByName<Button>("B_Payment");
        
        // Validasi apakah pembayaran cukup
        bool isPaymentSufficient = _amountCustomerPayment >= _grandTotal && _grandTotal > 0;
        
        if (isPaymentSufficient)
        {
            // Pembayaran cukup - aktifkan tombol
            paymentButton.IsEnabled = true;
            paymentButton.Opacity = 1.0;
            
            System.Diagnostics.Debug.WriteLine($"Payment sufficient: {_amountCustomerPayment:N0} >= {_grandTotal:N0}");
        }
        else
        {
            // Pembayaran tidak cukup - disable tombol
            paymentButton.IsEnabled = false;
            paymentButton.Opacity = 0.6;
            
            System.Diagnostics.Debug.WriteLine($"Payment insufficient: {_amountCustomerPayment:N0} < {_grandTotal:N0}");
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void B_Payment_Clicked(object sender, EventArgs e)
    {
        // Disable button saat processing
        if (sender is Button btn)
        {
            btn.IsEnabled = false;
            btn.Text = "Processing...";
        }

        try
        {
            // Button animation
            if (sender is Button image)
            {
                await image.FadeTo(0.3, 100); // Turunkan opacity ke 0.3 dalam 100ms
                await image.FadeTo(1, 200);   // Kembalikan opacity ke 1 dalam 200ms
            }

            // === VALIDASI INPUT ===
            if (_grandTotal <= 0)
            {
                await ShowToast("Invalid grand total");
                return;
            }

            if (_amountCustomerPayment < _grandTotal)
            {
                await ShowToast("Payment amount is insufficient");
                return;
            }

            // === DETERMINE PAYMENT METHOD ===
            int id_pembayaran = GetSelectedPaymentMethod();
            
            if (id_pembayaran <= 0)
            {
                await ShowToast("Please select a payment method");
                return;
            }

            // === VALIDASI CASH BAYAR (hanya untuk pembayaran tunai) ===
            if (id_pembayaran == 1 && _amountCustomerPayment <= 0) // 1 = Cash payment
            {
                await ShowToast("Cash payment amount is required and must be greater than 0");
                return;
            }

            // === GET USER LOGIN ===
            var (id_user, username, nama_lengkap, id_sesi, email, hp) = Login.GetLoggedInUser();
            
            if (id_user <= 0)
            {
                await ShowToast("User not logged in. Please login first.");
                return;
            }

            // === GET ACTIVE PENJUALAN ID ===
            int penjualanId = Preferences.Get($"active_penjualan_id_{id_user}", 0);
            
            if (penjualanId <= 0)
            {
                await ShowToast("No active transaction found");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"=== CHECKOUT SUBMIT START ===");
            System.Diagnostics.Debug.WriteLine($"Penjualan ID: {penjualanId}");
            System.Diagnostics.Debug.WriteLine($"Payment Method ID: {id_pembayaran}");
            System.Diagnostics.Debug.WriteLine($"Grand Total: {_grandTotal}");
            System.Diagnostics.Debug.WriteLine($"Discount: {_diskon}");

            // === SUBMIT CHECKOUT ===
            var checkoutResult = await SubmitCheckoutAsync(penjualanId, id_pembayaran, _diskon, _grandTotal);

            if (checkoutResult.success)
            {
                // === SUCCESS ===
                System.Diagnostics.Debug.WriteLine($"=== CHECKOUT SUCCESS ===");
                
                // NEW: If this is debt payment mode, call bayar_hutang API
                if (_isDebtPaymentMode)
                {
                    System.Diagnostics.Debug.WriteLine($"=== CALLING BAYAR HUTANG API ===");
                    var bayarHutangResult = await BayarHutangAsync(penjualanId);
                    
                    if (bayarHutangResult.success)
                    {
                        System.Diagnostics.Debug.WriteLine($"=== BAYAR HUTANG SUCCESS ===");
                        await ShowToast("Debt payment completed successfully!");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"=== BAYAR HUTANG FAILED ===");
                        System.Diagnostics.Debug.WriteLine($"Error: {bayarHutangResult.message}");
                        await ShowToast($"Checkout completed but debt status update failed: {bayarHutangResult.message}");
                    }
                }
                
                // Clear cart preferences
                Preferences.Remove($"active_penjualan_id_{id_user}");
                Preferences.Remove($"active_faktur_{id_user}");
                
                // Show success message
                string successMessage = _isDebtPaymentMode 
                    ? $"Debt payment completed! Invoice: {checkoutResult.data.faktur}"
                    : $"Transaction completed! Invoice: {checkoutResult.data.faktur}";
                    
                await ShowToast(successMessage);
                
                // Clean up resources before closing
                CleanupResources();
                
                // Close bottom sheet first
                await this.DismissAsync();
                
                // Navigate to PreviewStruk with delay to ensure bottom sheet is closed
                await Task.Delay(500);
                
                // Navigate to PreviewStruk to show receipt
                try
                {
                    System.Diagnostics.Debug.WriteLine($"=== NAVIGATING TO PREVIEW STRUK ===");
                    System.Diagnostics.Debug.WriteLine($"Penjualan ID for receipt: {penjualanId}");
                    
                    // Create PreviewStruk page with penjualan ID
                    var previewStrukPage = new Cart.PreviewStruk(penjualanId);
                    
                    // Navigate to PreviewStruk page
                    if (Application.Current?.MainPage is TabPage tabPage)
                    {
                        await tabPage.Navigation.PushAsync(previewStrukPage);
                        System.Diagnostics.Debug.WriteLine("Navigation to PreviewStruk via TabPage successful");
                    }
                    else if (Application.Current?.MainPage?.Navigation != null)
                    {
                        await Application.Current.MainPage.Navigation.PushAsync(previewStrukPage);
                        System.Diagnostics.Debug.WriteLine("Navigation to PreviewStruk via MainPage successful");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No navigation context available");
                        // Fallback: Set PreviewStruk as MainPage temporarily
                        Application.Current.MainPage = previewStrukPage;
                    }
                }
                catch (Exception navEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Navigation to PreviewStruk error: {navEx.Message}");
                    
                    // Fallback navigation to ListProduct if PreviewStruk fails
                    try
                    {
                        if (Application.Current?.MainPage is TabPage tabPage2)
                        {
                            await tabPage2.GoToAsync("//ListProduct");
                            System.Diagnostics.Debug.WriteLine("Fallback navigation to ListProduct successful");
                        }
                        else
                        {
                            Application.Current.MainPage = new TabPage();
                            System.Diagnostics.Debug.WriteLine("Fallback to TabPage successful");
                        }
                    }
                    catch (Exception fallbackEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Fallback navigation also failed: {fallbackEx.Message}");
                    }
                }
            }
            else
            {
                // === ERROR ===
                System.Diagnostics.Debug.WriteLine($"=== CHECKOUT ERROR ===");
                System.Diagnostics.Debug.WriteLine($"Error: {checkoutResult.message}");
                
                await ShowToast(checkoutResult.message ?? "Failed to complete transaction. Please try again.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== CHECKOUT EXCEPTION ===");
            System.Diagnostics.Debug.WriteLine($"Exception: {ex.Message}");
            
            await ShowToast($"An error occurred: {ex.Message}");
        }
        finally
        {
            // Enable button kembali
            if (sender is Button button)
            {
                button.IsEnabled = true;
                button.Text = "Process Payment";
            }
        }
    }

    private async Task ShowToast(string message)
    {
        try
        {
            if (string.IsNullOrEmpty(message))
                message = "Operation completed";

            var toast = CommunityToolkit.Maui.Alerts.Toast.Make(message, CommunityToolkit.Maui.Core.ToastDuration.Long, 14);
            await toast.Show(new CancellationTokenSource().Token);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Toast error: {ex.Message}");
        }
    }

    private int GetSelectedPaymentMethod()
    {
        var cashRadio = this.FindByName<RadioButton>("R_Cash");
        var qrisRadio = this.FindByName<RadioButton>("R_QRIS");
        var bankTransferRadio = this.FindByName<RadioButton>("R_BankTransfer");
        
        if (cashRadio.IsChecked)
            return 1; // Cash
        else if (qrisRadio.IsChecked)
            return 2; // QRIS
        else if (bankTransferRadio.IsChecked)
            return 3; // Bank Transfer
        else
            return 0; // No payment method selected
    }

    private async Task<CheckoutResponse> SubmitCheckoutAsync(int id_penjualan, int id_pembayaran, decimal potongan, decimal grand_total)
    {
        try
        {
            string apiUrl = $"{App.IP}/api/penjualan/simpan_checkout";
            
            System.Diagnostics.Debug.WriteLine($"=== SUBMIT CHECKOUT API ===");
            System.Diagnostics.Debug.WriteLine($"URL: {apiUrl}");
            System.Diagnostics.Debug.WriteLine($"id_penjualan: {id_penjualan}");
            System.Diagnostics.Debug.WriteLine($"id_pembayaran: {id_pembayaran}");
            System.Diagnostics.Debug.WriteLine($"potongan: {potongan}");
            System.Diagnostics.Debug.WriteLine($"grand_total: {grand_total}");
            // Tentukan nilai cash_bayar dan kembalian berdasarkan metode pembayaran
            decimal cashBayar = (id_pembayaran == 1) ? _amountCustomerPayment : 0; // Hanya untuk Cash (id=1)
            decimal kembalian = (id_pembayaran == 1) ? _cashReturn : 0; // Hanya untuk Cash (id=1)
            
            System.Diagnostics.Debug.WriteLine($"cash_bayar: {cashBayar}");
            System.Diagnostics.Debug.WriteLine($"kembalian: {kembalian}");

            // Prepare form data sesuai dokumentasi endpoint
            var formParams = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("id_penjualan", id_penjualan.ToString()),
                new KeyValuePair<string, string>("id_pembayaran", id_pembayaran.ToString()),
                new KeyValuePair<string, string>("potongan", potongan.ToString("0")), // Format as integer
                new KeyValuePair<string, string>("grand_total", grand_total.ToString("0")), // Format as integer
                new KeyValuePair<string, string>("aktif", "0"), // Set to 0 as specified
                new KeyValuePair<string, string>("biaya_lain", "0"), // Set to 0 as specified
                new KeyValuePair<string, string>("cash_bayar", cashBayar.ToString("0")), // Amount customer payment (hanya untuk Cash)
                new KeyValuePair<string, string>("kembalian", kembalian.ToString("0")) // Cash return/change (hanya untuk Cash)
            };

            // Debug: Print form data yang akan dikirim
            System.Diagnostics.Debug.WriteLine("=== FORM DATA TO SEND ===");
            foreach (var param in formParams)
            {
                System.Diagnostics.Debug.WriteLine($"{param.Key} = '{param.Value}'");
            }

            var formContent = new FormUrlEncodedContent(formParams);

            // Send POST request
            var response = await App.SharedHttpClient.PostAsync(apiUrl, formContent);
            var jsonContent = await response.Content.ReadAsStringAsync();
            
            System.Diagnostics.Debug.WriteLine($"Checkout Response Status: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"Checkout Response Content: {jsonContent}");

            if (response.IsSuccessStatusCode)
            {
                var checkoutResponse = JsonConvert.DeserializeObject<CheckoutResponse>(jsonContent);
                return checkoutResponse ?? new CheckoutResponse { success = false, message = "Invalid response format" };
            }
            else
            {
                return new CheckoutResponse { success = false, message = $"API Error: {response.StatusCode} - {jsonContent}" };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SubmitCheckoutAsync error: {ex.Message}");
            return new CheckoutResponse { success = false, message = $"Network error: {ex.Message}" };
        }
    }

    private async Task<QrisResponse> CreateQrisAsync(string faktur, decimal grossAmount, int idPenjualan)
    {
        try
        {
            string apiUrl = $"{App.IP}/api/pembayaran/create-qris";
            
            System.Diagnostics.Debug.WriteLine($"=== CREATE QRIS API ===");
            System.Diagnostics.Debug.WriteLine($"URL: {apiUrl}");
            System.Diagnostics.Debug.WriteLine($"Faktur: {faktur}");
            System.Diagnostics.Debug.WriteLine($"Gross Amount: {grossAmount}");
            System.Diagnostics.Debug.WriteLine($"ID Penjualan: {idPenjualan}");

            // Prepare form data sesuai dokumentasi endpoint
            var formParams = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("faktur", faktur),
                new KeyValuePair<string, string>("gross_amount", grossAmount.ToString("0")), // Format as integer
                new KeyValuePair<string, string>("id_penjualan", idPenjualan.ToString())
            };

            // Debug: Print form data yang akan dikirim
            System.Diagnostics.Debug.WriteLine("=== QRIS FORM DATA TO SEND ===");
            foreach (var param in formParams)
            {
                System.Diagnostics.Debug.WriteLine($"{param.Key} = '{param.Value}'");
            }

            var formContent = new FormUrlEncodedContent(formParams);

            // Send POST request
            var response = await App.SharedHttpClient.PostAsync(apiUrl, formContent);
            var jsonContent = await response.Content.ReadAsStringAsync();
            
            System.Diagnostics.Debug.WriteLine($"QRIS Response Status: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"QRIS Response Content: {jsonContent}");

            if (response.IsSuccessStatusCode)
            {
                var qrisResponse = JsonConvert.DeserializeObject<QrisResponse>(jsonContent);
                return qrisResponse ?? new QrisResponse { success = false, message = "Invalid response format" };
            }
            else
            {
                return new QrisResponse { success = false, message = $"API Error: {response.StatusCode} - {jsonContent}" };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateQrisAsync error: {ex.Message}");
            return new QrisResponse { success = false, message = $"Network error: {ex.Message}" };
        }
    }

    private async Task UpdateQrisUI(QrisData qrisData)
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // Update Order ID
                var orderIdLabel = this.FindByName<Label>("L_OrderId");
                orderIdLabel.Text = qrisData.order_id;        // Set text
                orderIdLabel.IsVisible = true;                // Make visible

                // Update Status
                var statusLabel = this.FindByName<Label>("L_QrisStatus");
                statusLabel.Text = qrisData.transaction_status.ToUpper();

                // Show status section
                var statusSection = this.FindByName<StackLayout>("StatusSection");
                statusSection.IsVisible = true;

                // Store transaction data
                _currentOrderId = qrisData.order_id;
                _currentTransactionId = qrisData.transaction_id;
                
                // Convert string id_penjualan to int for _currentPenjualanId
                if (int.TryParse(qrisData.id_penjualan, out int penjualanIdValue))
                {
                    _currentPenjualanId = penjualanIdValue;
                    System.Diagnostics.Debug.WriteLine($"Stored penjualan ID: {_currentPenjualanId}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Could not parse id_penjualan '{qrisData.id_penjualan}' to int");
                    _currentPenjualanId = 0; // Set to 0 as fallback
                }

                System.Diagnostics.Debug.WriteLine($"=== UPDATING QRIS IMAGE ===");
            });

            // Load QR Code image from actions URL
            if (qrisData.actions != null && qrisData.actions.Count > 0)
            {
                var qrAction = qrisData.actions.FirstOrDefault(a => a.name == "generate-qr-code");
                if (qrAction != null && !string.IsNullOrEmpty(qrAction.url))
                {
                    System.Diagnostics.Debug.WriteLine($"Loading QR image from: {qrAction.url}");
                    await LoadQrCodeImage(qrAction.url);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No QR code URL found in actions");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating QRIS UI: {ex.Message}");
        }
    }

    private async Task LoadQrCodeImage(string qrCodeUrl)
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var qrImage = this.FindByName<Image>("ImageQris");
                
                // Use UriImageSource for loading from URL
                var uriImageSource = new UriImageSource
                {
                    Uri = new Uri(qrCodeUrl),
                    CachingEnabled = false // Don't cache QR codes as they're transaction-specific
                };

                qrImage.Source = uriImageSource;
                qrImage.IsVisible = true; // Show the QR image
                
                System.Diagnostics.Debug.WriteLine($"QR Code image source set: {qrCodeUrl}");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading QR code image: {ex.Message}");
            await ShowToast("QR code loaded, but image display failed");
        }
    }

    private void StartCountdownTimer()
    {
        try
        {
            // Stop existing timer if any
            StopCountdownTimer();

            // Reset countdown to 5 minutes
            _remainingSeconds = 300;

            // Create and start new timer
            _countdownTimer = new System.Timers.Timer(1000); // 1 second interval
            _countdownTimer.Elapsed += OnCountdownTick;
            _countdownTimer.AutoReset = true;
            _countdownTimer.Enabled = true;

            System.Diagnostics.Debug.WriteLine("Countdown timer started - 5 minutes");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error starting countdown timer: {ex.Message}");
        }
    }

    private void StopCountdownTimer()
    {
        try
        {
            if (_countdownTimer != null)
            {
                _countdownTimer.Stop();
                _countdownTimer.Elapsed -= OnCountdownTick;
                _countdownTimer.Dispose();
                _countdownTimer = null;
                System.Diagnostics.Debug.WriteLine("Countdown timer stopped");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error stopping countdown timer: {ex.Message}");
        }
    }

    private async void OnCountdownTick(object sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            _remainingSeconds--;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var countdownLabel = this.FindByName<Label>("L_Countdown");
                
                if (_remainingSeconds > 0)
                {
                    // Format as MM:SS
                    int minutes = _remainingSeconds / 60;
                    int seconds = _remainingSeconds % 60;
                    countdownLabel.Text = $"{minutes:D2}:{seconds:D2}";
                    
                    // Change color based on time remaining
                    if (_remainingSeconds <= 60)
                    {
                        countdownLabel.TextColor = Colors.Red; // Last minute - red
                    }
                    else if (_remainingSeconds <= 120)
                    {
                        countdownLabel.TextColor = Colors.Orange; // Last 2 minutes - orange
                    }
                    else
                    {
                        countdownLabel.TextColor = Color.FromArgb("#4A90E2"); // Normal - blue
                    }
                }
                else
                {
                    // Time expired - Reset UI to initial state
                    countdownLabel.Text = "EXPIRED";
                    countdownLabel.TextColor = Colors.Red;
                    
                    var statusLabel = this.FindByName<Label>("L_QrisStatus");
                    statusLabel.Text = "EXPIRED";
                    statusLabel.TextColor = Colors.Red;
                    
                    StopCountdownTimer();
                    StopStatusCheckTimer(); // Also stop status checking
                    ResetQrisUI();
                    
                    // Show expired message
                    _ = Task.Run(async () => await ShowToast("QRIS payment has expired. Please create a new QRIS."));
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in countdown tick: {ex.Message}");
        }
    }

    // Auto-check timer methods (MORE FREQUENT for debugging)
    private void StartStatusCheckTimer()
    {
        try
        {
            // Stop existing timer if any
            StopStatusCheckTimer();

            // Create timer for every 5 seconds (more frequent for debugging)
            _statusCheckTimer = new System.Timers.Timer(5000); // 5 second interval for debugging
            _statusCheckTimer.Elapsed += OnStatusCheckTick;
            _statusCheckTimer.AutoReset = true;
            _statusCheckTimer.Enabled = true;

            System.Diagnostics.Debug.WriteLine("Auto status check started - checking every 5 seconds (DEBUG MODE)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error starting status check timer: {ex.Message}");
        }
    }

    private void StopStatusCheckTimer()
    {
        try
        {
            if (_statusCheckTimer != null)
            {
                _statusCheckTimer.Stop();
                _statusCheckTimer.Elapsed -= OnStatusCheckTick;
                _statusCheckTimer.Dispose();
                _statusCheckTimer = null;
                System.Diagnostics.Debug.WriteLine("Auto status check stopped");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error stopping status check timer: {ex.Message}");
        }
    }

    private async void OnStatusCheckTick(object sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            if (_currentPenjualanId <= 0)
            {
                System.Diagnostics.Debug.WriteLine("No active QRIS to check, stopping timer");
                System.Diagnostics.Debug.WriteLine($"Current penjualan ID: {_currentPenjualanId}");
                StopStatusCheckTimer();
                return;
            }

            System.Diagnostics.Debug.WriteLine($"=== AUTO-CHECKING QRIS STATUS (Timer Tick) ===");
            System.Diagnostics.Debug.WriteLine($"Check time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            System.Diagnostics.Debug.WriteLine($"Penjualan ID: {_currentPenjualanId}");
            System.Diagnostics.Debug.WriteLine($"Order ID: {_currentOrderId}");
            System.Diagnostics.Debug.WriteLine($"Transaction ID: {_currentTransactionId}");

            // Check status from database (webhook already updated it)
            var statusResult = await CheckQrisStatusFromDB(_currentPenjualanId);

            System.Diagnostics.Debug.WriteLine($"=== STATUS CHECK RESULT ===");
            System.Diagnostics.Debug.WriteLine($"Result success: {statusResult.success}");
            System.Diagnostics.Debug.WriteLine($"Result message: {statusResult.message}");

            if (statusResult.success)
            {
                int qrisStatus = statusResult.qris_status;
                string transactionStatus = statusResult.transaction_status ?? "pending";

                System.Diagnostics.Debug.WriteLine($"=== STATUS DETAILS ===");
                System.Diagnostics.Debug.WriteLine($"QRIS Status (dari DB): {qrisStatus}");
                System.Diagnostics.Debug.WriteLine($"Transaction Status: {transactionStatus}");
                System.Diagnostics.Debug.WriteLine($"Status meaning: {GetStatusMeaning(qrisStatus)}");

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var statusLabel = this.FindByName<Label>("L_QrisStatus");
                    
                    System.Diagnostics.Debug.WriteLine($"=== UPDATING UI ===");
                    System.Diagnostics.Debug.WriteLine($"Current status label text: {statusLabel?.Text}");
                    
                    if (qrisStatus == 1) // PAID - webhook already updated database
                    {
                        System.Diagnostics.Debug.WriteLine("PAYMENT SUCCESSFUL DETECTED!");
                        
                        // Payment successful!
                        statusLabel.Text = "PAID";
                        statusLabel.TextColor = Colors.Green;
                        
                        // Stop all timers
                        StopCountdownTimer();
                        StopStatusCheckTimer();
                        
                        System.Diagnostics.Debug.WriteLine("Timers stopped, showing success message");
                        
                        // Show success and auto-complete
                        await ShowToast("Payment successful! Completing checkout...");
                        await Task.Delay(2000);
                        await CompleteQrisCheckout();
                    }
                    else if (qrisStatus == 2) // FAILED/EXPIRED
                    {
                        System.Diagnostics.Debug.WriteLine("Payment failed or expired detected");
                        
                        statusLabel.Text = "FAILED";
                        statusLabel.TextColor = Colors.Red;
                        
                        StopCountdownTimer();
                        StopStatusCheckTimer();
                        
                        // Reset UI to initial state after delay
                        await Task.Delay(3000); // Show failed status for 3 seconds
                        ResetQrisUI();
                        
                        await ShowToast("Payment failed or expired");
                    }
                    else
                    {
                        // Still pending - update status text
                        System.Diagnostics.Debug.WriteLine($"Still pending - Status: {transactionStatus}");
                        
                        statusLabel.Text = transactionStatus.ToUpper();
                        statusLabel.TextColor = Colors.Orange;
                        
                        // Continue checking
                        System.Diagnostics.Debug.WriteLine("Will check again in 5 seconds...");
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"UI updated - Status label now: {statusLabel.Text}");
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Status check failed: {statusResult.message}");
                
                // Try to diagnose the issue
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await ShowToast($"Status check error: {statusResult.message}");
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CRITICAL ERROR in auto status check: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Exception type: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    // Helper method to explain status codes
    private string GetStatusMeaning(int qrisStatus)
    {
        return qrisStatus switch
        {
            0 => "Pending - Waiting for payment",
            1 => "Settlement - Payment successful",
            2 => "Failed/Expired - Payment failed or expired",
            _ => $"Unknown status code: {qrisStatus}"
        };
    }

    // Check QRIS status from database (where webhook already updated it)
    private async Task<dynamic> CheckQrisStatusFromDB(int penjualanId)
    {
        try
        {
            // FIXED: Use correct endpoint with order_id instead of id_penjualan
            string apiUrl = $"{App.IP}/api/pembayaran/status/{_currentOrderId}";
            
            System.Diagnostics.Debug.WriteLine($"=== CHECKING QRIS STATUS FROM MIDTRANS ===");
            System.Diagnostics.Debug.WriteLine($"API URL: {apiUrl}");
            System.Diagnostics.Debug.WriteLine($"Order ID: {_currentOrderId}");
            System.Diagnostics.Debug.WriteLine($"Current time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            if (string.IsNullOrEmpty(_currentOrderId))
            {
                System.Diagnostics.Debug.WriteLine("ERROR: Order ID is empty!");
                return new { success = false, message = "Order ID is empty" };
            }

            var response = await App.SharedHttpClient.GetAsync(apiUrl);
            var jsonContent = await response.Content.ReadAsStringAsync();
            
            System.Diagnostics.Debug.WriteLine($"=== MIDTRANS STATUS API RESPONSE ===");
            System.Diagnostics.Debug.WriteLine($"Status Code: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"Response Headers: {response.Headers}");
            System.Diagnostics.Debug.WriteLine($"Content Length: {jsonContent?.Length ?? 0}");
            System.Diagnostics.Debug.WriteLine($"Raw Content: {jsonContent}");

            if (response.IsSuccessStatusCode)
            {
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: Empty response content");
                    return new { success = false, message = "Empty response from server" };
                }

                try
                {
                    dynamic result = JsonConvert.DeserializeObject(jsonContent);
                    
                    System.Diagnostics.Debug.WriteLine($"=== PARSED MIDTRANS STATUS RESULT ===");
                    System.Diagnostics.Debug.WriteLine($"Parsed result type: {result?.GetType()?.Name ?? "null"}");
                    System.Diagnostics.Debug.WriteLine($"Result success: {result?.success}");
                    System.Diagnostics.Debug.WriteLine($"Result data: {result?.data}");
                    System.Diagnostics.Debug.WriteLine($"Result message: {result?.message}");
                    
                    if (result?.data != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Data transaction_status: {result.data.transaction_status}");
                        System.Diagnostics.Debug.WriteLine($"Data payment_status: {result.data.payment_status}");
                        System.Diagnostics.Debug.WriteLine($"Data order_id: {result.data.order_id}");
                        System.Diagnostics.Debug.WriteLine($"Data status_code: {result.data.status_code}");
                    }
                    
                    // Convert Midtrans status to our internal format
                    string transactionStatus = (string)(result?.data?.transaction_status ?? "pending");
                    string paymentStatus = (string)(result?.data?.payment_status ?? "pending");
                    
                    // Determine qris_status based on Midtrans response
                    int qrisStatus = 0; // Default: pending
                    if (transactionStatus == "settlement" && paymentStatus == "paid")
                    {
                        qrisStatus = 1; // Success/Settlement
                    }
                    else if (transactionStatus == "failure" || transactionStatus == "expire")
                    {
                        qrisStatus = 2; // Failed/Expired
                    }
                    
                    var statusResult = new {
                        success = (bool)(result?.success ?? false),
                        qris_status = qrisStatus,
                        transaction_status = transactionStatus,
                        payment_status = paymentStatus,
                        message = (string)(result?.message ?? ""),


                        raw_response = jsonContent
                    };
                    
                    System.Diagnostics.Debug.WriteLine($"=== FINAL STATUS RESULT ===");
                    System.Diagnostics.Debug.WriteLine($"Final success: {statusResult.success}");
                    System.Diagnostics.Debug.WriteLine($"Final qris_status: {statusResult.qris_status}");
                    System.Diagnostics.Debug.WriteLine($"Final transaction_status: {statusResult.transaction_status}");
                    System.Diagnostics.Debug.WriteLine($"Final payment_status: {statusResult.payment_status}");
                    
                    return statusResult;
                }
                catch (JsonException jsonEx)
                {
                    System.Diagnostics.Debug.WriteLine($"JSON Parse Error: {jsonEx.Message}");
                    System.Diagnostics.Debug.WriteLine($"Raw JSON that failed: {jsonContent}");
                    return new { success = false, message = $"JSON parse error: {jsonEx.Message}" };
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"HTTP Error: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"Error content: {jsonContent}");
                return new { success = false, message = $"API Error: {response.StatusCode} - {jsonContent}" };
            }
        }
        catch (HttpRequestException httpEx)
        {
            System.Diagnostics.Debug.WriteLine($"HTTP Request Error: {httpEx.Message}");
            return new { success = false, message = $"HTTP error: {httpEx.Message}" };
        }
        catch (TaskCanceledException timeoutEx)
        {
            System.Diagnostics.Debug.WriteLine($"Request Timeout: {timeoutEx.Message}");
            return new { success = false, message = $"Request timeout: {timeoutEx.Message}" };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Unexpected Error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            return new { success = false, message = $"Network error: {ex.Message}" };
        }
    }

    // Complete QRIS checkout automatically when payment detected
    private async Task CompleteQrisCheckout()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== AUTO-COMPLETING QRIS CHECKOUT ===");

            var (id_user, _, _, _, _, _) = Login.GetLoggedInUser();
            
            if (id_user <= 0 || _currentPenjualanId <= 0)
            {
                await ShowToast("Error completing checkout");
                return;
            }

            // Submit checkout with QRIS payment method (id_pembayaran = 2)
            var checkoutResult = await SubmitCheckoutAsync(_currentPenjualanId, 2, _diskon, _grandTotal);

            if (checkoutResult.success)
            {
                System.Diagnostics.Debug.WriteLine("=== QRIS CHECKOUT COMPLETED ===");
                
                // NEW: If this is debt payment mode, call bayar_hutang API
                if (_isDebtPaymentMode)
                {
                    System.Diagnostics.Debug.WriteLine($"=== CALLING BAYAR HUTANG API FOR QRIS ===");
                    var bayarHutangResult = await BayarHutangAsync(_currentPenjualanId);
                    
                    if (bayarHutangResult.success)
                    {
                        System.Diagnostics.Debug.WriteLine($"=== QRIS BAYAR HUTANG SUCCESS ===");
                        await ShowToast("QRIS debt payment completed successfully!");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"=== QRIS BAYAR HUTANG FAILED ===");
                        await ShowToast($"QRIS completed but debt status update failed: {bayarHutangResult.message}");
                    }
                }
                
                // Clear cart preferences
                Preferences.Remove($"active_penjualan_id_{id_user}");
                Preferences.Remove($"active_faktur_{id_user}");
                
                // Show success message
                string successMessage = _isDebtPaymentMode 
                    ? $"QRIS debt payment completed! Invoice: {checkoutResult.data.faktur}"
                    : $"Transaction completed! Invoice: {checkoutResult.data.faktur}";
                    
                await ShowToast(successMessage);
                
                // Clean up and navigate
                CleanupResources();
                await this.DismissAsync();
                await Task.Delay(500);
                
                // Navigate back to ListProduct
                try
                {
                    // NEW: Check if we need to reset History tab after QRIS debt payment
                    bool resetHistoryTab = Preferences.Get("reset_history_tab_after_debt", false);
                    
                    if (resetHistoryTab)
                    {
                        System.Diagnostics.Debug.WriteLine("=== RESETTING HISTORY TAB AFTER QRIS DEBT PAYMENT ===");
                        Preferences.Remove("reset_history_tab_after_debt");
                        
                        // Reset History tab to List_History first
                        if (Application.Current?.MainPage is TabPage tabPage)
                        {
                            await tabPage.GoToAsync("//ListHistory");
                            System.Diagnostics.Debug.WriteLine("History tab reset to ListHistory after QRIS debt payment");
                            await Task.Delay(200);
                        }
                        else if (Shell.Current != null)
                        {
                            await Shell.Current.GoToAsync("//ListHistory");
                            System.Diagnostics.Debug.WriteLine("History tab reset via Shell after QRIS debt payment");
                            await Task.Delay(200);
                        }
                    }
                    
                    if (Application.Current?.MainPage is TabPage tabPage2)
                    {
                        await tabPage2.GoToAsync("//ListProduct");
                    }
                    else if (Shell.Current != null)
                    {
                        await Shell.Current.GoToAsync("//ListProduct");
                    }
                }
                catch (Exception navEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Navigation error: {navEx.Message}");
                    Application.Current.MainPage = new TabPage();
                }
            }
            else
            {
                await ShowToast($"Checkout error: {checkoutResult.message}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error completing QRIS checkout: {ex.Message}");
            await ShowToast("Error completing checkout");
        }
    }

    private async void B_CreateQris_Clicked(object sender, EventArgs e)
    {
        if (sender is Button btn)
        {
            btn.IsEnabled = false;
            btn.Text = "Creating QRIS...";
        }

        try
        {
            System.Diagnostics.Debug.WriteLine($"=== CREATE QRIS START ===");

            // === VALIDASI INPUT ===
            if (_grandTotal <= 0)
            {
                await ShowToast("Invalid grand total amount");
                return;
            }

            // === GET USER LOGIN & TRANSACTION DATA ===
            var (id_user, _, _, _, _, _) = Login.GetLoggedInUser();
            if (id_user <= 0)
            {
                await ShowToast("User not logged in");
                return;
            }

            int penjualanId = Preferences.Get($"active_penjualan_id_{id_user}", 0);
            string faktur = Preferences.Get($"active_faktur_{id_user}", string.Empty);

            if (penjualanId <= 0 || string.IsNullOrEmpty(faktur))
            {
                await ShowToast("No active transaction found");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"Creating QRIS - Faktur: {faktur}, Amount: {_grandTotal}, ID: {penjualanId}");

            // === CALL QRIS API ===
            var qrisResult = await CreateQrisAsync(faktur, _grandTotal, penjualanId);

            if (qrisResult.success && qrisResult.data != null)
            {
                System.Diagnostics.Debug.WriteLine($"=== QRIS CREATED SUCCESSFULLY ===");
                System.Diagnostics.Debug.WriteLine($"Order ID: {qrisResult.data.order_id}");
                System.Diagnostics.Debug.WriteLine($"Transaction ID: {qrisResult.data.transaction_id}");
                System.Diagnostics.Debug.WriteLine($"Status: {qrisResult.data.transaction_status}");

                // Store penjualan ID for status checking (we already have it from parameter)
                _currentPenjualanId = penjualanId;

                // Hide the Create QRIS button after successful creation
                var createButton = this.FindByName<Button>("B_CreateQris");
                createButton.IsVisible = false;

                // Update UI dengan data QRIS
                await UpdateQrisUI(qrisResult.data);

                // Start countdown timer
                StartCountdownTimer();

                // Start auto-check timer (every 5 seconds)
                StartStatusCheckTimer();

                await ShowToast("QRIS created successfully! Please scan the QR code.");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"=== QRIS CREATION FAILED ===");
                System.Diagnostics.Debug.WriteLine($"Error: {qrisResult.message}");
                await ShowToast(qrisResult.message ?? "Failed to create QRIS");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== QRIS CREATION ERROR ===");
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
            await ShowToast($"Error creating QRIS: {ex.Message}");
        }
        finally
        {
            // Only restore button if QRIS creation failed
            if (sender is Button button && button.IsVisible)
            {
                button.IsEnabled = true;
                button.Text = "Create QRIS";
            }
        }
    }

    // Reset QRIS UI to initial state when expired or failed
    private void ResetQrisUI()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== RESETTING QRIS UI TO INITIAL STATE ===");
            
            // Reset all QRIS-related variables
            _currentOrderId = string.Empty;
            _currentTransactionId = string.Empty;
            _currentPenjualanId = 0;
            
            // Hide Order ID label
            var orderIdLabel = this.FindByName<Label>("L_OrderId");
            orderIdLabel.Text = "-";
            orderIdLabel.IsVisible = false;
            
            // Hide QR Code image
            var qrImage = this.FindByName<Image>("ImageQris");
            qrImage.Source = null;
            qrImage.IsVisible = false;
            
            // Hide status section
            var statusSection = this.FindByName<StackLayout>("StatusSection");
            statusSection.IsVisible = false;
            
            // Reset status labels
            var statusLabel = this.FindByName<Label>("L_QrisStatus");
            statusLabel.Text = "Pending";
            statusLabel.TextColor = Colors.Orange;
            
            var countdownLabel = this.FindByName<Label>("L_Countdown");
            countdownLabel.Text = "05:00";
            countdownLabel.TextColor = Color.FromArgb("#4A90E2");
            
            // Show Create QRIS button again
            var createButton = this.FindByName<Button>("B_CreateQris");
            createButton.IsVisible = true;
            createButton.IsEnabled = true;
            createButton.Text = "Create QRIS";
            
            System.Diagnostics.Debug.WriteLine("QRIS UI reset to initial state completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error resetting QRIS UI: {ex.Message}");
        }
    }

    private string GetMimeType(string fileName)
    {
        // Use the ImageCompressionService for consistent MIME type detection
        return ImageCompressionService.GetMimeType(fileName);
    }

    private async void TapUpload_Tapped(object sender, TappedEventArgs e)
    {
        // Remove old upload methods as they are replaced by new capture design
        // TapUpload_Tapped, ShowInlineImagePreview, OnPreviewImageTapped, B_RemoveImage_Clicked are now replaced
    }

    private async Task ShowCapturedImageState()
    {
        try
        {
            if (_photoStream == null || _photoStream.Length == 0)
                return;

            // Calculate file size in KB
            double fileSizeKB = _photoStream.Length / 1024.0;
            
            // Buat salinan stream untuk preview
            var imageStreamCopy = new MemoryStream();
            _photoStream.Position = 0;
            await _photoStream.CopyToAsync(imageStreamCopy);
            imageStreamCopy.Position = 0;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // Update UI state
                var initialState = this.FindByName<StackLayout>("InitialCaptureState");
                var afterState = this.FindByName<StackLayout>("AfterCaptureState");
                var previewImage = this.FindByName<Image>("CapturedPreviewImage");
                var captureAreaBorder = this.FindByName<Border>("CaptureAreaBorder");
                var actionButtonsGrid = this.FindByName<Grid>("ActionButtonsGrid");
                var imageCapturedButton = this.FindByName<Button>("B_ImageCaptured");
                
                if (initialState != null && afterState != null && previewImage != null)
                {
                    // Hide initial state, show after capture state
                    initialState.IsVisible = false;
                    afterState.IsVisible = true;
                    
                    // Set border stroke thickness to 0 when image is captured
                    if (captureAreaBorder != null)
                    {
                        captureAreaBorder.StrokeThickness = 0;
                        captureAreaBorder.Padding = new Thickness(0); // Remove padding as well
                    }
                    
                    // Show action buttons
                    if (actionButtonsGrid != null)
                    {
                        actionButtonsGrid.IsVisible = true;
                    }
                    
                    // Update button text with file size info
                    if (imageCapturedButton != null)
                    {
                        imageCapturedButton.Text = $"Image captured ({fileSizeKB:F0} KB)";
                        
                        // Color coding based on file size
                        if (fileSizeKB <= 512) // <= 512KB = Green (Good)
                        {
                            imageCapturedButton.BackgroundColor = Color.FromArgb("#34C759"); // Green
                        }
                        else if (fileSizeKB <= 800) // <= 800KB = Orange (OK)  
                        {
                            imageCapturedButton.BackgroundColor = Color.FromArgb("#FF9500"); // Orange
                        }
                        else // > 800KB = Red (Large but still acceptable)
                        {
                            imageCapturedButton.BackgroundColor = Color.FromArgb("#FF3B30"); // Red
                        }
                    }
                    
                    // Set image source
                    previewImage.Source = ImageSource.FromStream(() => new MemoryStream(imageStreamCopy.ToArray()));
                }
                
                // Validate transfer button state after photo is captured
                ValidateTransferButtonState();
            });

            System.Diagnostics.Debug.WriteLine($"Captured image state displayed - Size: {fileSizeKB:F1} KB");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error showing captured state: {ex.Message}");
        }
    }

    private async void OnCapturedImageTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (_photoStream != null && _photoStream.Length > 0)
            {
                await ShowToast("Photo already captured. Use retake button to capture new photo.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnCapturedImageTapped: {ex.Message}");
        }
    }

    private async void B_CloseImage_Clicked(object sender, EventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== CLOSE IMAGE CLICKED ===");
            
            // Reset photo data and UI directly (no confirmation needed for better UX)
            ResetPhotoData();
            await ShowToast("Photo removed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error closing image: {ex.Message}");
        }
    }

    private async void B_RetakePhoto_Clicked(object sender, EventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== RETAKE PHOTO CLICKED ===");
            
            // Reset current photo data
            ResetPhotoData();
            
            // Capture new photo by calling the tap method directly
            await CapturePhotoAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error retaking photo: {ex.Message}");
            await ShowToast($"Failed to retake photo: {ex.Message}");
        }
    }

    private async Task CapturePhotoAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== CAPTURE PHOTO INITIATED ===");
            
            // Check camera permission first
            var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (cameraStatus != PermissionStatus.Granted)
            {
                cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
                
                if (cameraStatus != PermissionStatus.Granted)
                {
                    await ShowToast("Camera permission is required to take photos");
                    return;
                }
            }

            // Check storage permission for saving files
            var storageStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
            if (storageStatus != PermissionStatus.Granted)
            {
                storageStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
                
                if (storageStatus != PermissionStatus.Granted)
                {
                    await ShowToast("Storage permission is required to save photos");
                    return;
                }
            }

            // Show loading toast
            await ShowToast("Taking photo...");

            // Capture photo
            var photo = await MediaPicker.CapturePhotoAsync();
            
            if (photo != null)
            {
                System.Diagnostics.Debug.WriteLine($"Photo captured: {photo.FileName}");
                
                // Show compression toast
                await ShowToast("Processing and compressing image...");
                
                // Process captured photo with compression
                await ProcessCapturedPhoto(photo);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Photo capture cancelled by user");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error capturing photo: {ex.Message}");
            await ShowToast($"Failed to capture photo: {ex.Message}");
        }
    }

    private async Task ProcessCapturedPhoto(FileResult photo)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== PROCESSING CAPTURED PHOTO ===");
            System.Diagnostics.Debug.WriteLine($"File name: {photo.FileName}");
            System.Diagnostics.Debug.WriteLine($"Content type: {photo.ContentType}");

            // Clear existing photo data
            _photoStream?.Dispose();
            
            // Read photo to memory stream
            using var sourceStream = await photo.OpenReadAsync();
            
            System.Diagnostics.Debug.WriteLine($"Original file size: {sourceStream.Length:N0} bytes ({sourceStream.Length / 1024.0:F1} KB)");
            
            // Compress image using SkiaSharp service
            var (compressedStream, originalSize, compressedSize) = await ImageCompressionService.CompressImageAsync(sourceStream, photo.FileName);
            
            // Store compressed stream
            _photoStream = compressedStream;
            _photoStream.Position = 0;
            
            // Generate unique file name with appropriate extension
            string fileExtension = GetCompressedFileExtension(photo.FileName);
            _photoFileName = $"transfer_{DateTime.Now:yyyyMMdd_HHmmss}{fileExtension}";
            
            System.Diagnostics.Debug.WriteLine($"=== COMPRESSION SUMMARY ===");
            System.Diagnostics.Debug.WriteLine($"Original size: {originalSize:N0} bytes ({originalSize / 1024.0:F1} KB)");
            System.Diagnostics.Debug.WriteLine($"Compressed size: {compressedSize:N0} bytes ({compressedSize / 1024.0:F1} KB)");
            System.Diagnostics.Debug.WriteLine($"Compression ratio: {(double)compressedSize / originalSize * 100:F1}%");
            System.Diagnostics.Debug.WriteLine($"Size reduction: {(originalSize - compressedSize) / 1024.0:F1} KB");
            System.Diagnostics.Debug.WriteLine($"Generated filename: {_photoFileName}");
            
            // Show compression info to user via toast
            await ShowToast($"Image compressed: {originalSize / 1024.0:F1} KB → {compressedSize / 1024.0:F1} KB");
            
            // Update UI to show captured image
            await ShowCapturedImageState();
            
            System.Diagnostics.Debug.WriteLine("Photo processed and compressed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error processing captured photo: {ex.Message}");
            await ShowToast($"Failed to process photo: {ex.Message}");
        }
    }

    /// <summary>
    /// Get file extension untuk file yang sudah dikompres
    /// Default ke .jpg karena JPEG memberikan kompresi terbaik
    /// </summary>
    private string GetCompressedFileExtension(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName)?.ToLowerInvariant();
        
        return extension switch
        {
            ".png" => ".jpg", // Convert PNG ke JPEG untuk kompresi lebih baik
            ".webp" => ".jpg", // Convert WebP ke JPEG
            ".jpg" or ".jpeg" => ".jpg", // Keep JPEG
            _ => ".jpg" // Default ke JPEG
        };
    }
    
    private void OnConfirmationChanged(object sender, CheckedChangedEventArgs e)
    {
        try
        {
            var transferButton = this.FindByName<Button>("B_Transfer");
            var accountNameEntry = this.FindByName<Entry>("E_AccountName");
            var bankPicker = this.FindByName<Picker>("P_BankList");
            
            if (transferButton != null)
            {
                // Check if all required fields are filled and photo is taken
                bool hasAccountName = !string.IsNullOrWhiteSpace(accountNameEntry?.Text);
                bool hasBankSelected = bankPicker?.SelectedIndex >= 0;
                bool hasPhoto = _photoStream != null && _photoStream.Length > 0;
                bool isConfirmed = e.Value;
                
                // Enable button only if all conditions are met
                bool canSubmit = hasAccountName && hasBankSelected && hasPhoto && isConfirmed;
                
                transferButton.IsEnabled = canSubmit;
                transferButton.Opacity = canSubmit ? 1.0 : 0.3;
                
                System.Diagnostics.Debug.WriteLine($"Transfer button enabled: {canSubmit} (Account: {hasAccountName}, Bank: {hasBankSelected}, Photo: {hasPhoto}, Confirmed: {isConfirmed})");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnConfirmationChanged: {ex.Message}");
        }
    }

    private void ValidateTransferButtonState()
    {
        try
        {
            var transferButton = this.FindByName<Button>("B_Transfer");
            var accountNameEntry = this.FindByName<Entry>("E_AccountName");
            var bankPicker = this.FindByName<Picker>("P_BankList");
            var confirmationCheckbox = this.FindByName<CheckBox>("CB_Confirmation");
            
            if (transferButton != null)
            {
                // Check if all required fields are filled and photo is taken
                bool hasAccountName = !string.IsNullOrWhiteSpace(accountNameEntry?.Text);
                bool hasBankSelected = bankPicker?.SelectedIndex >= 0;
                bool hasPhoto = _photoStream != null && _photoStream.Length > 0;
                bool isConfirmed = confirmationCheckbox?.IsChecked == true;
                
                // Enable button only if all conditions are met
                bool canSubmit = hasAccountName && hasBankSelected && hasPhoto && isConfirmed;
                
                transferButton.IsEnabled = canSubmit;
                transferButton.Opacity = canSubmit ? 1.0 : 0.3;
                
                System.Diagnostics.Debug.WriteLine($"Transfer button state validated: {canSubmit}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error validating transfer button state: {ex.Message}");
        }
    }

    private void ResetPhotoData()
    {
        try
        {
            // Dispose existing stream
            _photoStream?.Dispose();
            _photoStream = null;
            
            // Clear file paths
            _photoFileName = string.Empty;
            _photoLocalPath = string.Empty;
            
            // Reset UI to initial state
            var initialState = this.FindByName<StackLayout>("InitialCaptureState");
            var afterState = this.FindByName<StackLayout>("AfterCaptureState");
            var previewImage = this.FindByName<Image>("CapturedPreviewImage");
            var captureAreaBorder = this.FindByName<Border>("CaptureAreaBorder");
            var actionButtonsGrid = this.FindByName<Grid>("ActionButtonsGrid");
            
            if (initialState != null && afterState != null)
            {
                initialState.IsVisible = true;
                afterState.IsVisible = false;
            }
            
            // Reset border to initial state with dotted border
            if (captureAreaBorder != null)
            {
                captureAreaBorder.StrokeThickness = 2;
                captureAreaBorder.Padding = new Thickness(40, 60); // Restore original padding
            }
            
            // Hide action buttons
            if (actionButtonsGrid != null)
            {
                actionButtonsGrid.IsVisible = false;
            }
            
            if (previewImage != null)
            {
                previewImage.Source = null;
            }
            
            // Validate transfer button state after photo is removed
            ValidateTransferButtonState();
            
            System.Diagnostics.Debug.WriteLine("Photo data reset successfully with border restored");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error resetting photo data: {ex.Message}");
        }
    }

    private void CleanupResources()
    {
        // Clean up all timers when needed
        StopCountdownTimer();
        StopStatusCheckTimer();
        
        // Clean up photo resources
        ResetPhotoData();
    }

    private async void B_Transfer_Clicked(object sender, EventArgs e)
    {
        // Disable button saat processing
        if (sender is Button button)
        {
            button.IsEnabled = false;
            button.Text = "Saving...";
        }

        try
        {
            // Button animation
            if (sender is Button btn)
            {
                await btn.FadeTo(0.3, 100);
                await btn.FadeTo(1, 200);
            }

            // === VALIDASI INPUT ===
            var accountNameEntry = this.FindByName<Entry>("E_AccountName");
            var bankPicker = this.FindByName<Picker>("P_BankList");
            var confirmationCheckbox = this.FindByName<CheckBox>("CB_Confirmation");

            if (string.IsNullOrWhiteSpace(accountNameEntry?.Text))
            {
                await ShowToast("Please enter account name");
                return;
            }

            if (bankPicker?.SelectedIndex < 0)
            {
                await ShowToast("Please select a bank");
                return;
            }

            if (_photoStream == null || _photoStream.Length == 0)
            {
                await ShowToast("Please capture transfer evidence photo");
                return;
            }

            if (confirmationCheckbox?.IsChecked != true)
            {
                await ShowToast("Please confirm that all information is valid");
                return;
            }

            // === GET USER LOGIN ===
            var (id_user, username, nama_lengkap, id_sesi, email, hp) = Login.GetLoggedInUser();
            
            if (id_user <= 0)
            {
                await ShowToast("User not logged in. Please login first.");
                return;
            }

            // === GET ACTIVE PENJUALAN ID ===
            int penjualanId = Preferences.Get($"active_penjualan_id_{id_user}", 0);
            
            if (penjualanId <= 0)
            {
                await ShowToast("No active transaction found");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"=== TRANSFER BANK START ===");
            System.Diagnostics.Debug.WriteLine($"Penjualan ID: {penjualanId}");
            System.Diagnostics.Debug.WriteLine($"Account Name: {accountNameEntry.Text}");
            System.Diagnostics.Debug.WriteLine($"Bank: {bankPicker.Items[bankPicker.SelectedIndex]}");
            System.Diagnostics.Debug.WriteLine($"Photo size: {_photoStream.Length} bytes");

            // === SUBMIT TRANSFER DATA ===
            var transferResult = await SubmitTransferBankAsync(
                penjualanId, 
                accountNameEntry.Text, 
                bankPicker.Items[bankPicker.SelectedIndex]);

            if (transferResult.success)
            {
                System.Diagnostics.Debug.WriteLine($"=== TRANSFER BANK SUCCESS ===");
                
                // Show success message
                await ShowToast($"Transfer data saved successfully! ID: {transferResult.data.id_transfer}");
                
                // === PROCEED WITH CHECKOUT ===
                // Submit checkout with Bank Transfer payment method (id_pembayaran = 3)
                var checkoutResult = await SubmitCheckoutAsync(penjualanId, 3, _diskon, _grandTotal);

                if (checkoutResult.success)
                {
                    System.Diagnostics.Debug.WriteLine($"=== TRANSFER CHECKOUT SUCCESS ===");
                    
                    // NEW: If this is debt payment mode, call bayar_hutang API
                    if (_isDebtPaymentMode)
                    {
                        System.Diagnostics.Debug.WriteLine($"=== CALLING BAYAR HUTANG API FOR TRANSFER ===");
                        var bayarHutangResult = await BayarHutangAsync(penjualanId);
                        
                        if (bayarHutangResult.success)
                        {
                            System.Diagnostics.Debug.WriteLine($"=== TRANSFER BAYAR HUTANG SUCCESS ===");
                            await ShowToast("Transfer debt payment completed successfully!");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"=== TRANSFER BAYAR HUTANG FAILED ===");
                            await ShowToast($"Transfer completed but debt status update failed: {bayarHutangResult.message}");
                        }
                    }
                    
                    // Clear cart preferences
                    Preferences.Remove($"active_penjualan_id_{id_user}");
                    Preferences.Remove($"active_faktur_{id_user}");
                    
                    // Show final success message
                    string successMessage = _isDebtPaymentMode 
                        ? $"Transfer debt payment completed! Invoice: {checkoutResult.data.faktur}"
                        : $"Transaction completed! Invoice: {checkoutResult.data.faktur}";
                        
                    await ShowToast(successMessage);
                    
                    // Clean up resources
                    CleanupResources();
                    
                    // Close bottom sheet and navigate
                    await this.DismissAsync();
                    await Task.Delay(500);
                    
                    // Navigate back to ListProduct
                    try
                    {
                        // NEW: Check if we need to reset History tab after debt payment
                        bool resetHistoryTab = Preferences.Get("reset_history_tab_after_debt", false);
                        
                        if (resetHistoryTab)
                        {
                            System.Diagnostics.Debug.WriteLine("=== RESETTING HISTORY TAB AFTER TRANSFER DEBT PAYMENT ===");
                            Preferences.Remove("reset_history_tab_after_debt");
                            
                            // Reset History tab to List_History first
                            if (Application.Current?.MainPage is TabPage tabPage)
                            {
                                await tabPage.GoToAsync("//ListHistory");
                                System.Diagnostics.Debug.WriteLine("History tab reset to ListHistory after transfer debt payment");
                                await Task.Delay(200);
                            }
                            else if (Shell.Current != null)
                            {
                                await Shell.Current.GoToAsync("//ListHistory");
                                System.Diagnostics.Debug.WriteLine("History tab reset via Shell after transfer debt payment");
                                await Task.Delay(200);
                            }
                        }
                        
                        if (Application.Current?.MainPage is TabPage tabPage2)
                        {
                            await tabPage2.GoToAsync("//ListProduct");
                        }
                        else if (Shell.Current != null)
                        {
                            await Shell.Current.GoToAsync("//ListProduct");
                        }
                        else
                        {
                            Application.Current.MainPage = new TabPage();
                        }
                    }
                    catch (Exception navEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Navigation error: {navEx.Message}");
                        Application.Current.MainPage = new TabPage();
                    }
                }
                else
                {
                    await ShowToast($"Transfer saved but checkout failed: {checkoutResult.message}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"=== TRANSFER BANK ERROR ===");
                System.Diagnostics.Debug.WriteLine($"Error: {transferResult.message}");
                await ShowToast(transferResult.message ?? "Failed to save transfer data");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== TRANSFER BANK EXCEPTION ===");
            System.Diagnostics.Debug.WriteLine($"Exception: {ex.Message}");
            await ShowToast($"An error occurred: {ex.Message}");
        }
        finally
        {
            // Enable button kembali
            if (sender is Button finalBtn)
            {
                finalBtn.IsEnabled = true;
                finalBtn.Text = "Save Transfer Data";
            }
        }
    }

    private async Task<BayarHutangResponse> BayarHutangAsync(int idPenjualan)
    {
        try
        {
            string apiUrl = $"{App.IP}/api/penjualan/bayar_hutang";
            
            System.Diagnostics.Debug.WriteLine($"=== BAYAR HUTANG API ===");
            System.Diagnostics.Debug.WriteLine($"URL: {apiUrl}");
            System.Diagnostics.Debug.WriteLine($"id_penjualan: {idPenjualan}");

            // Prepare form data sesuai dokumentasi endpoint
            var formParams = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("id_penjualan", idPenjualan.ToString())
            };

            // Debug: Print form data yang akan dikirim
            System.Diagnostics.Debug.WriteLine("=== BAYAR HUTANG FORM DATA ===");
            foreach (var param in formParams)
            {
                System.Diagnostics.Debug.WriteLine($"{param.Key} = '{param.Value}'");
            }

            var formContent = new FormUrlEncodedContent(formParams);

            // Send POST request
            var response = await App.SharedHttpClient.PostAsync(apiUrl, formContent);
            var jsonContent = await response.Content.ReadAsStringAsync();
            
            System.Diagnostics.Debug.WriteLine($"Bayar Hutang Response Status: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"Bayar Hutang Response Content: {jsonContent}");

            if (response.IsSuccessStatusCode)
            {
                var bayarHutangResponse = JsonConvert.DeserializeObject<BayarHutangResponse>(jsonContent);
                return bayarHutangResponse ?? new BayarHutangResponse { success = false, message = "Invalid response format" };
            }
            else
            {
                return new BayarHutangResponse { success = false, message = $"API Error: {response.StatusCode} - {jsonContent}" };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BayarHutangAsync error: {ex.Message}");
            return new BayarHutangResponse { success = false, message = $"Network error: {ex.Message}" };
        }
    }

    private async Task<TransferBankResponse> SubmitTransferBankAsync(int id_penjualan, string nama_pemilik, string nama_bank)
    {
        try
        {
            string apiUrl = $"{App.IP}/api/pembayaran/transfer_bank";
            
            System.Diagnostics.Debug.WriteLine($"=== SUBMIT TRANSFER BANK API ===");
            System.Diagnostics.Debug.WriteLine($"URL: {apiUrl}");
            System.Diagnostics.Debug.WriteLine($"id_penjualan: {id_penjualan}");
            System.Diagnostics.Debug.WriteLine($"nama_pemilik: {nama_pemilik}");
            System.Diagnostics.Debug.WriteLine($"nama_bank: {nama_bank}");

            // Create multipart form data
            using (var content = new MultipartFormDataContent())
            {
                // Add form fields
                content.Add(new StringContent(id_penjualan.ToString()), "id_penjualan");
                content.Add(new StringContent(nama_pemilik), "nama_pemilik");
                content.Add(new StringContent(nama_bank), "nama_bank");

                // Add image file
                if (_photoStream != null && _photoStream.Length > 0)
                {
                    _photoStream.Position = 0;
                    
                    // Create byte array from stream
                    byte[] imageBytes = new byte[_photoStream.Length];
                    await _photoStream.ReadAsync(imageBytes, 0, imageBytes.Length);
                    
                    // Create content from bytes
                    var imageContent = new ByteArrayContent(imageBytes);
                    
                    // Set content type based on file extension (default to jpeg)
                    string mimeType = GetMimeType(_photoFileName);
                    imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
                    
                    // Add to form with field name expected by backend: url_image
                    content.Add(imageContent, "url_image", _photoFileName);
                    
                    System.Diagnostics.Debug.WriteLine($"Image added: {_photoFileName}, Size: {imageBytes.Length}, MIME: {mimeType}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: No photo stream available");
                    return new TransferBankResponse { success = false, message = "No transfer image available" };
                }

                // Debug: Print all form data
                System.Diagnostics.Debug.WriteLine("=== MULTIPART FORM DATA ===");
                foreach (var item in content)
                {
                    if (item.Headers.ContentDisposition?.Name != null)
                    {
                        string fieldName = item.Headers.ContentDisposition.Name.Trim('"');
                        if (fieldName != "url_image")
                        {
                            string value = await item.ReadAsStringAsync();
                            System.Diagnostics.Debug.WriteLine($"{fieldName} = '{value}'");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"{fieldName} = [IMAGE FILE]");
                        }
                    }
                }

                // Send POST request
                var response = await App.SharedHttpClient.PostAsync(apiUrl, content);
                var jsonContent = await response.Content.ReadAsStringAsync();
                
                System.Diagnostics.Debug.WriteLine($"Transfer Response Status: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"Transfer Response Content: {jsonContent}");

                if (response.IsSuccessStatusCode)
                {
                    var transferResponse = JsonConvert.DeserializeObject<TransferBankResponse>(jsonContent);
                    return transferResponse ?? new TransferBankResponse { success = false, message = "Invalid response format" };
                }
                else
                {
                    return new TransferBankResponse { success = false, message = $"API Error: {response.StatusCode} - {jsonContent}" };
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SubmitTransferBankAsync error: {ex.Message}");
            return new TransferBankResponse { success = false, message = $"Network error: {ex.Message}" };
        }
    }

    private async void TapCapture_Tapped(object sender, TappedEventArgs e)
    {
        try
        {
            await CapturePhotoAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in TapCapture_Tapped: {ex.Message}");
            await ShowToast($"Failed to capture photo: {ex.Message}");
        }
    }
}