using System.Text;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Toko2025.Services;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace Toko2025.OrderHistory;

public partial class List_History : ContentPage, INotifyPropertyChanged
{
    private ObservableCollection<HistoryItem> _historyItems = new ObservableCollection<HistoryItem>();
    private ObservableCollection<PaymentMethod> _paymentMethods = new ObservableCollection<PaymentMethod>();
    private HistorySummary _historySummary = new HistorySummary();
    private string _openingBalance = "Rp 0";
    private string _totalSalesWithOpening = "Rp 0";
    private bool _isLoading = false;
    private string _pesan = string.Empty;
    private string _selectedPaymentMethod = "All"; // Default to show all payment methods

    public ObservableCollection<HistoryItem> HistoryItems
    {
        get => _historyItems;
        set
        {
            _historyItems = value;
            OnPropertyChanged();
            UpdateEmptyState();
        }
    }

    public ObservableCollection<PaymentMethod> PaymentMethods
    {
        get => _paymentMethods;
        set
        {
            _paymentMethods = value;
            OnPropertyChanged();
        }
    }

    public HistorySummary HistorySummary
    {
        get => _historySummary;
        set
        {
            _historySummary = value;
            OnPropertyChanged();
        }
    }

    public string OpeningBalance
    {
        get => _openingBalance;
        set
        {
            _openingBalance = value;
            OnPropertyChanged();
        }
    }

    public string TotalSalesWithOpening
    {
        get => _totalSalesWithOpening;
        set
        {
            _totalSalesWithOpening = value;
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

    public List_History()
    {
        InitializeComponent();
        
        System.Diagnostics.Debug.WriteLine($"=== LIST_HISTORY CONSTRUCTOR START ===");
        
        // Set binding context
        BindingContext = this;
        System.Diagnostics.Debug.WriteLine($"BindingContext set to: {BindingContext?.GetType().Name}");
        
        // Set initial values for debugging
        TotalSalesWithOpening = "Rp 0";
        OpeningBalance = "Rp 0";
        System.Diagnostics.Debug.WriteLine($"Initial TotalSalesWithOpening: {TotalSalesWithOpening}");
        System.Diagnostics.Debug.WriteLine($"Initial OpeningBalance: {OpeningBalance}");
        
        // Set default date to today
        DatePicker1.Date = DateTime.Now;
        System.Diagnostics.Debug.WriteLine($"Default date set to: {DatePicker1.Date:yyyy-MM-dd}");
        
        // Load payment methods and history data
        LoadPaymentMethods();
        LoadHistoryData();
        
        System.Diagnostics.Debug.WriteLine($"=== LIST_HISTORY CONSTRUCTOR END ===");
    }

    private void UpdateVisibility()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var loadingIndicator = this.FindByName<ActivityIndicator>("LoadingIndicator");
            var historyListView = this.FindByName<ListView>("HistoryListView");
            
            if (loadingIndicator != null)
                loadingIndicator.IsVisible = IsLoading;
                
            if (historyListView != null)
                historyListView.IsVisible = !IsLoading;
                
            UpdateEmptyState();
        });
    }

    private void UpdateEmptyState()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var emptyStateLayout = this.FindByName<StackLayout>("EmptyStateLayout");
            var historyListView = this.FindByName<ListView>("HistoryListView");
            
            bool isEmpty = !IsLoading && (HistoryItems == null || HistoryItems.Count == 0);
            
            if (emptyStateLayout != null)
                emptyStateLayout.IsVisible = isEmpty;
                
            if (historyListView != null)
                historyListView.IsVisible = !IsLoading && !isEmpty;
        });
    }

    private async void LoadPaymentMethods()
    {
        try
        {
            var paymentMethodsResponse = await GetPaymentMethodsAsync();
            
            if (paymentMethodsResponse != null && paymentMethodsResponse.success)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    PaymentMethods.Clear();
                    
                    // Add "All" option first
                    PaymentMethods.Add(new PaymentMethod 
                    { 
                        id_pembayaran = 0, 
                        nama_pembayaran = "All Payment Methods",
                        aktif = 1 
                    });
                    
                    // Add payment methods from API
                    foreach (var method in paymentMethodsResponse.data.Where(x => x.aktif == 1))
                    {
                        PaymentMethods.Add(method);
                    }
                    
                    // Set picker items
                    var picker = this.FindByName<Picker>("SortJenisPembayaran");
                    picker.ItemsSource = PaymentMethods;
                    picker.ItemDisplayBinding = new Binding("nama_pembayaran");
                    picker.SelectedIndex = 0; // Select "All" by default
                });
                
                System.Diagnostics.Debug.WriteLine($"Payment methods loaded: {paymentMethodsResponse.data.Count} items");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Failed to load payment methods");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadPaymentMethods error: {ex.Message}");
        }
    }

    private async Task<PaymentMethodResponse> GetPaymentMethodsAsync()
    {
        try
        {
            string apiUrl = $"http://{App.IP}:3000/api/pembayaran";
            System.Diagnostics.Debug.WriteLine($"Calling Payment Methods API: {apiUrl}");

            var response = await App.SharedHttpClient.GetAsync(apiUrl);
            var jsonContent = await response.Content.ReadAsStringAsync();
            
            System.Diagnostics.Debug.WriteLine($"Payment Methods API Response Status: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"Payment Methods API Response Content: {jsonContent}");

            if (response.IsSuccessStatusCode)
            {
                var paymentMethodsResponse = JsonConvert.DeserializeObject<PaymentMethodResponse>(jsonContent);
                return paymentMethodsResponse ?? new PaymentMethodResponse { success = false, message = "Invalid response format" };
            }
            else
            {
                return new PaymentMethodResponse { success = false, message = $"API Error: {response.StatusCode}" };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetPaymentMethodsAsync error: {ex.Message}");
            return new PaymentMethodResponse { success = false, message = $"Network error: {ex.Message}" };
        }
    }

    private async void LoadHistoryData()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== LOAD HISTORY DATA START ===");
            System.Diagnostics.Debug.WriteLine($"Current date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            System.Diagnostics.Debug.WriteLine($"Selected date: {DatePicker1.Date:yyyy-MM-dd}");
            System.Diagnostics.Debug.WriteLine($"Is today? {DatePicker1.Date.Date == DateTime.Now.Date}");
            
            IsLoading = true;
            
            // Get user login info
            var (id_user, _, _, _, _, _) = Login.GetLoggedInUser();
            if (id_user <= 0)
            {
                System.Diagnostics.Debug.WriteLine("User not logged in");
                _pesan = "User not logged in. Please login first.";
                await ShowToast();
                return;
            }

            // Format date for API (assuming DatePicker1 is bound to start_date)
            string startDate = DatePicker1.Date.ToString("yyyy-MM-dd");
            System.Diagnostics.Debug.WriteLine($"Loading history for user {id_user} on date {startDate}");
            
            // Call API
            var historyResponse = await GetHistoryDataAsync(startDate, id_user);
            
            if (historyResponse != null && historyResponse.success)
            {
                System.Diagnostics.Debug.WriteLine($"=== API SUCCESS ===");
                System.Diagnostics.Debug.WriteLine($"Raw data count: {historyResponse.data?.Count ?? 0}");
                System.Diagnostics.Debug.WriteLine($"API summary - count: {historyResponse.summary?.count ?? 0}, grand_total: {historyResponse.summary?.grand_total ?? 0}");
                
                // Debug raw data
                if (historyResponse.data != null && historyResponse.data.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine("=== RAW TRANSACTION DATA ===");
                    foreach (var item in historyResponse.data.Take(3))
                    {
                        System.Diagnostics.Debug.WriteLine($"Faktur: {item.faktur}, Grand Total: {item.grand_total}, Hutang: {item.hutang}, Opening: {item.uang_awal}, Payment: {item.nama_pembayaran}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No raw transaction data from API");
                }
                
                // Update UI with data
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    System.Diagnostics.Debug.WriteLine("=== UPDATING UI ON MAIN THREAD ===");
                    
                    HistoryItems.Clear();
                    
                    // Filter by payment method if selected
                    var filteredData = FilterByPaymentMethod(historyResponse.data);
                    System.Diagnostics.Debug.WriteLine($"After payment filter '{_selectedPaymentMethod}': {filteredData.Count} items");
                    
                    foreach (var item in filteredData)
                    {
                        HistoryItems.Add(item);
                    }
                    
                    HistorySummary = historyResponse.summary;
                    System.Diagnostics.Debug.WriteLine($"History summary set: count={HistorySummary.count}, total={HistorySummary.grand_total}");
                    
                    // Calculate Opening Balance and Total Sales including opening balance
                    System.Diagnostics.Debug.WriteLine("About to call CalculateBalanceAndTotals...");
                    CalculateBalanceAndTotals(filteredData);
                    
                    System.Diagnostics.Debug.WriteLine($"After calculation:");
                    System.Diagnostics.Debug.WriteLine($"  OpeningBalance property: {OpeningBalance}");
                    System.Diagnostics.Debug.WriteLine($"  TotalSalesWithOpening property: {TotalSalesWithOpening}");
                });
                
                System.Diagnostics.Debug.WriteLine($"=== FINAL STATUS ===");
                System.Diagnostics.Debug.WriteLine($"History loaded: {historyResponse.data.Count} items");
                System.Diagnostics.Debug.WriteLine($"Opening Balance: {OpeningBalance}");
                System.Diagnostics.Debug.WriteLine($"Total Sales with Opening: {TotalSalesWithOpening}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"=== API FAILED ===");
                System.Diagnostics.Debug.WriteLine($"Success: {historyResponse?.success ?? false}");
                System.Diagnostics.Debug.WriteLine($"Message: {historyResponse?.message ?? "Unknown error"}");
                System.Diagnostics.Debug.WriteLine($"Data is null: {historyResponse?.data == null}");
                System.Diagnostics.Debug.WriteLine($"Data count: {historyResponse?.data?.Count ?? 0}");
                
                _pesan = historyResponse?.message ?? "Failed to load history data";
                await ShowToast();
                
                // Clear UI but still try to calculate with empty data (untuk opening balance default)
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    HistoryItems.Clear();
                    HistorySummary = new HistorySummary();
                    
                    // Even if API fails, try to calculate default values for current date
                    CalculateBalanceAndTotals(new List<HistoryItem>());
                    
                    System.Diagnostics.Debug.WriteLine($"After empty calculation:");
                    System.Diagnostics.Debug.WriteLine($"  OpeningBalance property: {OpeningBalance}");
                    System.Diagnostics.Debug.WriteLine($"  TotalSalesWithOpening property: {TotalSalesWithOpening}");
                    
                    // Force property updates
                    OnPropertyChanged(nameof(OpeningBalance));
                    OnPropertyChanged(nameof(TotalSalesWithOpening));
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== LOAD HISTORY DATA ERROR ===");
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            
            _pesan = $"Error loading history: {ex.Message}";
            await ShowToast();
        }
        finally
        {
            IsLoading = false;
            System.Diagnostics.Debug.WriteLine($"=== LOAD HISTORY DATA END ===");
        }
    }

    private List<HistoryItem> FilterByPaymentMethod(List<HistoryItem> data)
    {
        if (string.IsNullOrEmpty(_selectedPaymentMethod) || _selectedPaymentMethod == "All Payment Methods")
        {
            return data.OrderByDescending(x => x.jam).ToList(); // Show all, newest first
        }
        
        // Filter by selected payment method
        var filtered = data.Where(x => x.nama_pembayaran.Equals(_selectedPaymentMethod, StringComparison.OrdinalIgnoreCase))
                          .OrderByDescending(x => x.jam)
                          .ToList();
        
        System.Diagnostics.Debug.WriteLine($"Filtered by payment method '{_selectedPaymentMethod}': {filtered.Count} items");
        
        return filtered;
    }

    private void CalculateBalanceAndTotals(List<HistoryItem> historyData)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== CALCULATE BALANCE AND TOTALS START ===");
            System.Diagnostics.Debug.WriteLine($"History data count: {historyData?.Count ?? 0}");
            System.Diagnostics.Debug.WriteLine($"Selected date: {DatePicker1.Date:yyyy-MM-dd}");
            System.Diagnostics.Debug.WriteLine($"Is today? {DatePicker1.Date.Date == DateTime.Now.Date}");
            
            // FIXED: Untuk current date tanpa transaksi, tetap ambil opening balance dari API/default
            if (historyData == null || historyData.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("No history data for this date");
                
                // PERBAIKAN: Untuk current date, coba ambil opening balance dari user preferences atau default
                // Jangan langsung set ke Rp 0, karena opening balance bisa ada meskipun belum ada transaksi
                var (id_user, _, _, _, _, _) = Login.GetLoggedInUser();
                
                // Default opening balance untuk current date (bisa disesuaikan dengan kebutuhan bisnis)
                int defaultOpeningBalance = 0;
                
                // Jika current date, coba ambil opening balance dari preferences atau database
                if (DatePicker1.Date.Date == DateTime.Now.Date && id_user > 0)
                {
                    // Untuk hari ini, opening balance bisa diambil dari saldo akhir kemarin atau setting default
                    defaultOpeningBalance = Preferences.Get($"daily_opening_balance_{id_user}", 0);
                    System.Diagnostics.Debug.WriteLine($"Using saved opening balance for today: {defaultOpeningBalance}");
                }
                
                string emptyFormattedOpeningBalance = $"Rp {defaultOpeningBalance:N0}";
                string emptyFormattedTotalSales = $"Rp {defaultOpeningBalance:N0}"; // Total sales = opening balance jika belum ada transaksi
                
                OpeningBalance = emptyFormattedOpeningBalance;
                TotalSalesWithOpening = emptyFormattedTotalSales;
                
                System.Diagnostics.Debug.WriteLine($"=== CALCULATION FOR EMPTY DATA ===");
                System.Diagnostics.Debug.WriteLine($"Opening Balance: {emptyFormattedOpeningBalance}");
                System.Diagnostics.Debug.WriteLine($"Total Sales: {emptyFormattedTotalSales}");
                
                // Force UI update on main thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"=== FORCING UI UPDATE FOR EMPTY DATA ===");
                    System.Diagnostics.Debug.WriteLine($"Setting OpeningBalance to: {OpeningBalance}");
                    System.Diagnostics.Debug.WriteLine($"Setting TotalSalesWithOpening to: {TotalSalesWithOpening}");
                    
                    OnPropertyChanged(nameof(OpeningBalance));
                    OnPropertyChanged(nameof(TotalSalesWithOpening));
                    
                    // Direct label update sebagai backup
                    try
                    {
                        var openingBalanceLabel = this.FindByName<Label>("L_UangAwal");
                        var totalSalesLabel = this.FindByName<Label>("L_SumGrandtotal");
                        
                        if (openingBalanceLabel != null)
                        {
                            openingBalanceLabel.Text = OpeningBalance;
                            System.Diagnostics.Debug.WriteLine($"Direct opening balance label set to: {OpeningBalance}");
                        }
                        
                        if (totalSalesLabel != null)
                        {
                            totalSalesLabel.Text = TotalSalesWithOpening;
                            System.Diagnostics.Debug.WriteLine($"Direct total sales label set to: {TotalSalesWithOpening}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error updating labels directly: {ex.Message}");
                    }
                });
                
                return; // Exit early untuk data kosong
            }

            // Ada data, hitung nilai sebenarnya
            System.Diagnostics.Debug.WriteLine("Has data, calculating actual values...");
            
            // Get opening balance from first transaction
            var firstTransaction = historyData.First();
            int openingBalanceValue = firstTransaction.uang_awal;
            
            // PERBAIKAN: Save opening balance untuk current date agar bisa digunakan besok
            var (user_id, _, _, _, _, _) = Login.GetLoggedInUser();
            if (DatePicker1.Date.Date == DateTime.Now.Date && user_id > 0)
            {
                Preferences.Set($"daily_opening_balance_{user_id}", openingBalanceValue);
                System.Diagnostics.Debug.WriteLine($"Saved opening balance for today: {openingBalanceValue}");
            }
            
            // Calculate total of NON-DEBT transactions only (filtered)
            // PERBAIKAN: Exclude hutang (hutang = 1) dari perhitungan total sales
            var nonDebtTransactions = historyData.Where(x => x.hutang != 1).ToList();
            var debtTransactions = historyData.Where(x => x.hutang == 1).ToList();
            
            int totalNonDebtTransactions = nonDebtTransactions.Sum(x => x.grand_total);
            
            // Total Sales = Opening Balance + Non-Debt Transaction Totals
            int totalSalesValue = openingBalanceValue + totalNonDebtTransactions;
            
            // Format values
            string formattedOpeningBalance = $"Rp {openingBalanceValue:N0}";
            string formattedTotalSales = $"Rp {totalSalesValue:N0}";
            
            System.Diagnostics.Debug.WriteLine($"=== CALCULATION BREAKDOWN (FIXED FOR HUTANG) ===");
            System.Diagnostics.Debug.WriteLine($"Opening Balance: {openingBalanceValue:N0} -> {formattedOpeningBalance}");
            System.Diagnostics.Debug.WriteLine($"Total transactions: {historyData.Count}");
            System.Diagnostics.Debug.WriteLine($"Non-debt transactions: {nonDebtTransactions.Count} (sum: {totalNonDebtTransactions:N0})");
            System.Diagnostics.Debug.WriteLine($"Debt transactions: {debtTransactions.Count} (excluded from total)");
            System.Diagnostics.Debug.WriteLine($"Total Sales (Opening + Non-Debt Transactions): {totalSalesValue:N0} -> {formattedTotalSales}");
            System.Diagnostics.Debug.WriteLine($"Filter: {_selectedPaymentMethod}");
            System.Diagnostics.Debug.WriteLine($"Date: {DatePicker1.Date:yyyy-MM-dd}");
            
            // Debug individual transactions (max 5 for readability)
            foreach (var transaction in historyData.Take(5))
            {
                string debtStatus = transaction.hutang == 1 ? "(DEBT - EXCLUDED)" : "(INCLUDED)";
                System.Diagnostics.Debug.WriteLine($"Transaction {transaction.faktur} ({transaction.nama_pembayaran}): {transaction.grand_total:N0} {debtStatus}");
            }
            
            // Set calculated properties
            OpeningBalance = formattedOpeningBalance;
            TotalSalesWithOpening = formattedTotalSales;
            
            // Force property change notifications on main thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                System.Diagnostics.Debug.WriteLine($"=== FORCING UI UPDATE FOR DATA ===");
                System.Diagnostics.Debug.WriteLine($"Setting OpeningBalance to: {OpeningBalance}");
                System.Diagnostics.Debug.WriteLine($"Setting TotalSalesWithOpening to: {TotalSalesWithOpening}");
                
                OnPropertyChanged(nameof(OpeningBalance));
                OnPropertyChanged(nameof(TotalSalesWithOpening));
                
                // Direct label update sebagai backup
                try
                {
                    var openingBalanceLabel = this.FindByName<Label>("L_UangAwal");
                    var totalSalesLabel = this.FindByName<Label>("L_SumGrandtotal");
                    
                    if (openingBalanceLabel != null)
                    {
                        openingBalanceLabel.Text = OpeningBalance;
                        System.Diagnostics.Debug.WriteLine($"Direct opening balance label update: {OpeningBalance}");
                    }
                    
                    if (totalSalesLabel != null)
                    {
                        totalSalesLabel.Text = TotalSalesWithOpening;
                        System.Diagnostics.Debug.WriteLine($"Direct total sales label update: {TotalSalesWithOpening}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating labels directly: {ex.Message}");
                }
            });
            
            System.Diagnostics.Debug.WriteLine($"=== CALCULATE BALANCE AND TOTALS END ===");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== CALCULATION ERROR ===");
            System.Diagnostics.Debug.WriteLine($"Error calculating totals: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            
            // Set safe defaults pada error
            OpeningBalance = "Rp 0";
            TotalSalesWithOpening = "Rp 0";
            
            // Force UI update even on error
            MainThread.BeginInvokeOnMainThread(() =>
            {
                OnPropertyChanged(nameof(OpeningBalance));
                OnPropertyChanged(nameof(TotalSalesWithOpening));
            });
        }
    }

    private async Task<HistoryResponse> GetHistoryDataAsync(string startDate, int idUser)
    {
        try
        {
            string apiUrl = $"http://{App.IP}:3000/api/history?start_date={startDate}&id_user={idUser}";
            System.Diagnostics.Debug.WriteLine($"=== CALLING HISTORY API ===");
            System.Diagnostics.Debug.WriteLine($"URL: {apiUrl}");
            System.Diagnostics.Debug.WriteLine($"Start Date: {startDate}");
            System.Diagnostics.Debug.WriteLine($"User ID: {idUser}");
            System.Diagnostics.Debug.WriteLine($"Request time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            var response = await App.SharedHttpClient.GetAsync(apiUrl);
            var jsonContent = await response.Content.ReadAsStringAsync();
            
            System.Diagnostics.Debug.WriteLine($"=== HISTORY API RESPONSE ===");
            System.Diagnostics.Debug.WriteLine($"Status Code: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"Response Size: {jsonContent?.Length ?? 0} characters");
            System.Diagnostics.Debug.WriteLine($"Content Preview: {(jsonContent?.Length > 200 ? jsonContent.Substring(0, 200) + "..." : jsonContent)}");

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var historyResponse = JsonConvert.DeserializeObject<HistoryResponse>(jsonContent);
                    
                    if (historyResponse != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"=== PARSED RESPONSE ===");
                        System.Diagnostics.Debug.WriteLine($"Success: {historyResponse.success}");
                        System.Diagnostics.Debug.WriteLine($"Message: {historyResponse.message}");
                        System.Diagnostics.Debug.WriteLine($"Data count: {historyResponse.data?.Count ?? 0}");
                        System.Diagnostics.Debug.WriteLine($"Summary count: {historyResponse.summary?.count ?? 0}");
                        System.Diagnostics.Debug.WriteLine($"Summary grand_total: {historyResponse.summary?.grand_total ?? 0}");
                        
                        return historyResponse;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Deserialized response is null");
                        return new HistoryResponse { success = false, message = "Invalid response format" };
                    }
                }
                catch (JsonException jsonEx)
                {
                    System.Diagnostics.Debug.WriteLine($"JSON Deserialization Error: {jsonEx.Message}");
                    System.Diagnostics.Debug.WriteLine($"JSON Content: {jsonContent}");
                    return new HistoryResponse { success = false, message = $"JSON parse error: {jsonEx.Message}" };
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"HTTP Error: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"Error Content: {jsonContent}");
                return new HistoryResponse { success = false, message = $"API Error: {response.StatusCode}" };
            }
        }
        catch (HttpRequestException httpEx)
        {
            System.Diagnostics.Debug.WriteLine($"HTTP Request Exception: {httpEx.Message}");
            return new HistoryResponse { success = false, message = $"Network error: {httpEx.Message}" };
        }
        catch (TaskCanceledException timeoutEx)
        {
            System.Diagnostics.Debug.WriteLine($"Request Timeout: {timeoutEx.Message}");
            return new HistoryResponse { success = false, message = $"Request timeout: {timeoutEx.Message}" };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Unexpected Error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            return new HistoryResponse { success = false, message = $"Unexpected error: {ex.Message}" };
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

    private void DatePicker1_DateSelected(object sender, DateChangedEventArgs e)
    {
        // Reload data when date changes
        LoadHistoryData();
    }

    private void SortJenisPembayaran_SelectedIndexChanged(object sender, EventArgs e)
    {
        try
        {
            var picker = sender as Picker;
            if (picker?.SelectedItem is PaymentMethod selectedMethod)
            {
                _selectedPaymentMethod = selectedMethod.nama_pembayaran;
                
                System.Diagnostics.Debug.WriteLine($"Payment method filter changed to: {_selectedPaymentMethod}");
                
                // Reload data with new filter
                LoadHistoryData();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SortJenisPembayaran_SelectedIndexChanged error: {ex.Message}");
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void TapDetail_Tapped(object sender, TappedEventArgs e)
    {
        if (sender is Image image)
        {
            await image.FadeTo(0.3, 100); // Turunkan opacity ke 0.3 dalam 100ms
            await image.FadeTo(1, 200);   // Kembalikan opacity ke 1 dalam 200ms
        }

        // Get faktur, hutang, and id_penjualan from the tapped item's BindingContext
        if (sender is Image img && img.BindingContext is HistoryItem selectedItem)
        {
            System.Diagnostics.Debug.WriteLine($"Navigating to DetailList for faktur: {selectedItem.faktur}, hutang: {selectedItem.hutang}, id_penjualan: {selectedItem.id_penjualan}");
            
            try
            {
                // Navigate to DetailList with faktur, hutang, and id_penjualan parameters
                await Navigation.PushAsync(new DetailList(selectedItem.faktur, selectedItem.hutang, selectedItem.id_penjualan));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
                _pesan = $"Navigation error: {ex.Message}";
                await ShowToast();
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("No HistoryItem context found for tap detail");
            _pesan = "Unable to open details - no transaction selected";
            await ShowToast();
        }
    }
}