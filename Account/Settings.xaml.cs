using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using System.Text;
using Newtonsoft.Json;
using Toko2025.Services;
namespace Toko2025.Account;

public partial class Settings : ContentPage
{
    string pesan = string.Empty;

    public Settings()
    {
        InitializeComponent();
        LoadUserData();
        
        // Load closing session data saat halaman pertama kali dibuka
        _ = Task.Run(async () => await LoadClosingSessionData());
    }

    private async void toast()
    {
        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        ToastDuration duration = ToastDuration.Long;
        double fontSize = 12;
        var toast = Toast.Make(pesan, duration, fontSize);
        await toast.Show(cancellationTokenSource.Token);
    }

    // Model untuk Closing Session Data Response
    public class ClosingSessionDataResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
        public ClosingSessionData data { get; set; } = new ClosingSessionData();
    }

    public class ClosingSessionData
    {
        public int id_user { get; set; }
        public string tanggal { get; set; } = string.Empty;
        public long total_uang { get; set; }
    }

    // Model untuk Closing Session Response
    public class ClosingSessionResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
    }

    // Model untuk Update User Request
    public class UpdateUserRequest
    {
        public int id_user { get; set; }
        public string username { get; set; } = string.Empty;
        public string nama_lengkap { get; set; } = string.Empty;
        public string role { get; set; } = "Kasir";
        public string email { get; set; } = string.Empty;
        public string hp { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
        public string aktif { get; set; } = "1";
    }

    // Model untuk Update User Response
    public class UpdateUserResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
    }

    private async Task LoadClosingSessionData()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== LOADING CLOSING SESSION DATA ===");
            
            // Cek apakah user sudah login
            if (!Login.IsUserLoggedIn())
            {
                System.Diagnostics.Debug.WriteLine("User not logged in, skipping closing session data load");
                return;
            }

            var (id_user, username, nama_lengkap, id_sesi, email, hp) = Login.GetLoggedInUser();
            
            // Get current date in YYYY-MM-DD format
            string currentDate = DateTime.Now.ToString("yyyy-MM-dd");
            
            // Call API untuk mendapatkan data closing session
            var closingData = await GetClosingSessionDataAsync(id_user, currentDate);
            
            if (closingData.success && closingData.data != null)
            {
                // Update UI pada main thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    try
                    {
                        // Update tanggal menggunakan FindByName
                        var sessionDateLabel = this.FindByName<Label>("SessionDateLabel");
                        if (sessionDateLabel != null)
                        {
                            sessionDateLabel.Text = $"Session Date: {closingData.data.tanggal}";
                            System.Diagnostics.Debug.WriteLine($"Updated session date: {closingData.data.tanggal}");
                        }
                        
                        // Update total uang menggunakan FindByName
                        var totalUangLabel = this.FindByName<Label>("TotalUangLabel");
                        if (totalUangLabel != null)
                        {
                            totalUangLabel.Text = $"Rp {closingData.data.total_uang:N0}";
                            System.Diagnostics.Debug.WriteLine($"Updated total uang: Rp {closingData.data.total_uang:N0}");
                        }
                    }
                    catch (Exception uiEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error updating UI: {uiEx.Message}");
                    }
                });
                
                System.Diagnostics.Debug.WriteLine($"Closing session data loaded successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load closing session data: {closingData.message}");
                
                // Set default values jika API gagal
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    try
                    {
                        var sessionDateLabel = this.FindByName<Label>("SessionDateLabel");
                        if (sessionDateLabel != null)
                        {
                            sessionDateLabel.Text = $"Session Date: {DateTime.Now:dd MMMM yyyy}";
                        }
                        
                        var totalUangLabel = this.FindByName<Label>("TotalUangLabel");
                        if (totalUangLabel != null)
                        {
                            totalUangLabel.Text = "Rp 0";
                        }
                    }
                    catch (Exception defaultEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error setting default values: {defaultEx.Message}");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading closing session data: {ex.Message}");
        }
    }

    private async Task<ClosingSessionDataResponse> GetClosingSessionDataAsync(int id_user, string tanggal)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== CALLING CLOSING SESSION API ===");
            System.Diagnostics.Debug.WriteLine($"User ID: {id_user}");
            System.Diagnostics.Debug.WriteLine($"Date: {tanggal}");
            
            // Buat URL API
            string apiUrl = $"{App.IP}/api/sesi_kasir/closing?id_user={id_user}&tanggal={tanggal}";
            System.Diagnostics.Debug.WriteLine($"API URL: {apiUrl}");

            // Kirim GET request menggunakan SharedHttpClient
            var response = await App.SharedHttpClient.GetAsync(apiUrl);
            
            // Baca response content
            string responseContent = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"API Response Status: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"API Response Content: {responseContent}");

            if (response.IsSuccessStatusCode)
            {
                // Deserialize response
                var closingResponse = JsonConvert.DeserializeObject<ClosingSessionDataResponse>(responseContent);
                
                if (closingResponse != null)
                {
                    System.Diagnostics.Debug.WriteLine($"API call successful: {closingResponse.success}");
                    return closingResponse;
                }
                else
                {
                    return new ClosingSessionDataResponse { success = false, message = "Invalid response format" };
                }
            }
            else
            {
                return new ClosingSessionDataResponse { success = false, message = $"API Error: {response.StatusCode}" };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"API call error: {ex.Message}");
            return new ClosingSessionDataResponse { success = false, message = $"Error: {ex.Message}" };
        }
    }

    private void LoadUserData()
    {
        // Tampilkan data user yang sedang login
        if (Login.IsUserLoggedIn())
        {
            var (id_user, username, nama_lengkap, id_sesi, email, hp) = Login.GetLoggedInUser();
            
            // Jika ada label untuk menampilkan nama user di UI
            T_NamaLengkap.Text = nama_lengkap;
            T_Username.Text = username;
            T_Email.Text = email;
            // Format phone number: replace 08 with +628
            if (!string.IsNullOrEmpty(hp) && hp.StartsWith("08"))
            {
                T_Hp.Text = "+628" + hp.Substring(2);
            }
            else
            {
                T_Hp.Text = hp;
            }

            // Load dan tampilkan informasi printer aktif
            LoadActivePrinterInfo();
            
            // Load dan tampilkan informasi connection aktif
            LoadActiveConnectionInfo();

            System.Diagnostics.Debug.WriteLine($"Logged in user: {nama_lengkap} ({username}) - Session ID: {id_sesi}");
        }
    }

    private void LoadActivePrinterInfo()
    {
        try
        {
            string defaultPrinter = Preferences.Get("default_printer", "");
            
            if (string.IsNullOrEmpty(defaultPrinter))
            {
                L_ActivePrinter.Text = "No printer configured";
                return;
            }
            
            // Jika format "NamaPrinter|MacAddress"
            if (defaultPrinter.Contains("|"))
            {
                string[] parts = defaultPrinter.Split('|');
                if (parts.Length == 2)
                {
                    string printerName = parts[0];
                    string macAddress = parts[1];
                    L_ActivePrinter.Text = $"{printerName}\nMAC: {macAddress}";
                }
                else
                {
                    L_ActivePrinter.Text = defaultPrinter;
                }
            }
            else
            {
                // Format lama, hanya nama printer
                L_ActivePrinter.Text = defaultPrinter;
            }
        }
        catch (Exception ex)
        {
            L_ActivePrinter.Text = "Error loading printer info";
            System.Diagnostics.Debug.WriteLine($"Error loading printer info: {ex.Message}");
        }
    }
    
    private async void LoadActiveConnectionInfo()
    {
        try
        {
            // Get current IP configuration
            string currentIP = App.IP;
            string networkType = Preferences.Get("NetworkType", "Unknown");
            string localIP = Preferences.Get("LocalIP", "");
            string onlineIP = Preferences.Get("OnlineIP", "");
            
            if (string.IsNullOrEmpty(currentIP))
            {
                L_ActiveConnection.Text = "No connection configured";
                return;
            }
            
            // Format connection info based on network type
            if (networkType == "Local Network" && !string.IsNullOrEmpty(localIP))
            {
                L_ActiveConnection.Text = $"[Local]: {localIP}";
            }
            else if (networkType == "Online Network" && !string.IsNullOrEmpty(onlineIP))
            {
                L_ActiveConnection.Text = $"[Online]: {onlineIP}";
            }
            else
            {
                // Fallback to showing the current IP without prefix
                string displayIP = currentIP;
                
                // Remove protocol prefix for cleaner display
                if (displayIP.StartsWith("http://"))
                {
                    displayIP = displayIP.Substring(7);
                }
                else if (displayIP.StartsWith("https://"))
                {
                    displayIP = displayIP.Substring(8);
                }
                
                L_ActiveConnection.Text = $"[Hardcoded]: {displayIP}";
            }
            
            System.Diagnostics.Debug.WriteLine($"Active connection info loaded: {L_ActiveConnection.Text}");
        }
        catch (Exception ex)
        {
            L_ActiveConnection.Text = "Error loading connection info";
            System.Diagnostics.Debug.WriteLine($"Error loading connection info: {ex.Message}");
        }
    }

    private async void TapLogout_Tapped(object sender, TappedEventArgs e)
    {
        // Animasi tap
        if (sender is Image image)
        {
            await image.FadeTo(0.3, 100); // Turunkan opacity ke 0.3 dalam 100ms
            await image.FadeTo(1, 200);   // Kembalikan opacity ke 1 dalam 200ms
        }

        try
        {
            // Check if user has items in cart before logout
            if (await HasActiveCart())
            {
                bool logoutWithCart = await DisplayAlert(
                    "Logout Warning", 
                    "You have items in your cart. Logging out will clear your cart. Do you want to continue?", 
                    "Logout & Clear Cart", 
                    "Cancel");

                if (!logoutWithCart)
                {
                    // User chose to cancel logout
                    return;
                }

                // User chose to logout and clear cart
                await ClearCartBeforeLogout();
            }

            // Konfirmasi logout normal (jika tidak ada cart atau user sudah confirm clear cart)
            bool confirmLogout = await DisplayAlert("Logout", "Are you sure you want to logout?", "Yes", "No");
            
            if (confirmLogout)
            {
                // Lakukan logout
                Login.LogoutUser();
                
                // Navigate ke Login page dan reset navigation stack
                Application.Current.MainPage = new NavigationPage(new Login());
            }
        }
        catch (Exception ex)
        {
            pesan = $"Logout failed: {ex.Message}";
            toast();
        }
    }

    private async Task<bool> HasActiveCart()
    {
        try
        {
            // Get current user
            var (id_user, _, _, _, _, _) = Login.GetLoggedInUser();
            
            if (id_user <= 0)
                return false;

            // Check if there's active penjualan in preferences
            int penjualanId = Preferences.Get($"active_penjualan_id_{id_user}", 0);
            
            if (penjualanId <= 0)
                return false;

            // Check if cart has items by calling API
            string apiUrl = $"{App.IP}/api/penjualan/cart/{penjualanId}";
            var response = await App.SharedHttpClient.GetAsync(apiUrl);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var cartResponse = JsonConvert.DeserializeObject<dynamic>(jsonContent);
                
                // Check if cart has items
                if (cartResponse?.success == true && cartResponse?.data?.items != null)
                {
                    var items = cartResponse.data.items;
                    return items.Count > 0;
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking cart in Settings: {ex.Message}");
            return false;
        }
    }

    private async Task ClearCartBeforeLogout()
    {
        try
        {
            // Get current user
            var (id_user, _, _, _, _, _) = Login.GetLoggedInUser();
            
            if (id_user > 0)
            {
                // Get penjualan ID
                int penjualanId = Preferences.Get($"active_penjualan_id_{id_user}", 0);
                
                if (penjualanId > 0)
                {
                    // Call DELETE API to clear cart
                    string apiUrl = $"{App.IP}/api/penjualan/{penjualanId}";
                    await App.SharedHttpClient.DeleteAsync(apiUrl);
                    
                    System.Diagnostics.Debug.WriteLine($"Cart cleared before logout for user {id_user}");
                }
                
                // Clear preferences
                Preferences.Remove($"active_penjualan_id_{id_user}");
                Preferences.Remove($"active_faktur_{id_user}");
                
                // Show toast notification
                pesan = "Cart cleared before logout";
                toast();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error clearing cart before logout: {ex.Message}");
        }
    }

    private async void B_Closing_Clicked(object sender, EventArgs e)
    {
        // Disable button saat loading
        if (sender is Button btn)
        {
            btn.IsEnabled = false;
            btn.Text = "Closing...";
        }

        try
        {
            // Ambil id_sesi dari user yang login
            if (!Login.IsUserLoggedIn())
            {
                pesan = "User not logged in";
                toast();
                return;
            }

            var (id_user, username, nama_lengkap, id_sesi, email, hp) = Login.GetLoggedInUser();
            
            if (string.IsNullOrEmpty(id_sesi))
            {
                pesan = "Session ID not found";
                toast();
                return;
            }

            // Check if user has items in cart before closing session
            if (await HasActiveCart())
            {
                bool closeWithCart = await DisplayAlert(
                    "Close Session Warning", 
                    "You have items in your cart. Closing session will clear your cart. Do you want to continue?", 
                    "Close & Clear Cart", 
                    "Cancel");

                if (!closeWithCart)
                {
                    // User chose to cancel close session
                    return;
                }

                // User chose to close session and clear cart
                await ClearCartBeforeLogout();
            }

            // Konfirmasi closing session
            bool confirmClosing = await DisplayAlert("Close Session", "Are you sure you want to close the current session?", "Yes", "No");
            
            if (confirmClosing)
            {
                // Lakukan closing session
                var closingResult = await PerformClosingSession(id_sesi);

                if (closingResult.success)
                {
                    // Logout user setelah berhasil closing session
                    Login.LogoutUser();
                    
                    pesan = closingResult.message;
                    toast();
                    
                    // Navigate ke Login page
                    Application.Current.MainPage = new NavigationPage(new Login());
                }
                else
                {
                    pesan = closingResult.message;
                    toast();
                }
            }
        }
        catch (Exception ex)
        {
            pesan = $"Closing session failed: {ex.Message}";
            toast();
        }
        finally
        {
            // Enable button kembali
            if (sender is Button button)
            {
                button.IsEnabled = true;
                button.Text = "Close Session";
            }
        }
    }

    private async Task<ClosingSessionResponse> PerformClosingSession(string idSesi)
    {
        try
        {
            using (var httpClient = new HttpClient())
            {
                // Set timeout
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                // Buat URL API
                string apiUrl = $"{App.IP}/api/sesi_kasir/off/{idSesi}";

                // Kirim POST request (tidak perlu body data)
                var response = await httpClient.PostAsync(apiUrl, null);

                // Baca response
                string responseContent = await response.Content.ReadAsStringAsync();

                // Deserialize response
                var closingResponse = JsonConvert.DeserializeObject<ClosingSessionResponse>(responseContent);

                return closingResponse ?? new ClosingSessionResponse { success = false, message = "Invalid response" };
            }
        }
        catch (HttpRequestException ex)
        {
            return new ClosingSessionResponse { success = false, message = $"Network error: {ex.Message}" };
        }
        catch (TaskCanceledException)
        {
            return new ClosingSessionResponse { success = false, message = "Request timeout" };
        }
        catch (Exception ex)
        {
            return new ClosingSessionResponse { success = false, message = $"Error: {ex.Message}" };
        }
    }

    private async void TapFullname_Tapped(object sender, TappedEventArgs e)
    {
        if (sender is Grid image)
        {
            await image.FadeTo(0.3, 100); // Turunkan opacity ke 0.3 dalam 100ms
            await image.FadeTo(1, 200);   // Kembalikan opacity ke 1 dalam 200ms
        }

        // Tampilkan dialog untuk mengubah nama lengkap
        string result = await DisplayPromptAsync("Fullname", "Edit fullname?", initialValue: T_NamaLengkap.Text, maxLength: 30, keyboard: Keyboard.Text);
        if (result != null) 
        { 
            T_NamaLengkap.Text = result;
        }
    }

    private async void TapUsername_Tapped(object sender, TappedEventArgs e)
    {
        if (sender is Grid grid)
        {
            await grid.FadeTo(0.3, 100);
            await grid.FadeTo(1, 200);
        }

        // Tampilkan dialog untuk mengubah username
        string result = await DisplayPromptAsync("Username", "Edit username:", initialValue: T_Username.Text, maxLength: 20, keyboard: Keyboard.Text);
        if (result != null)
        {
            T_Username.Text = result;
        }
    }

    private async void TapEmail_Tapped(object sender, TappedEventArgs e)
    {
        if (sender is Grid grid)
        {
            await grid.FadeTo(0.3, 100);
            await grid.FadeTo(1, 200);
        }

        // Tampilkan dialog untuk mengubah email
        string result = await DisplayPromptAsync("Email", "Edit email:", initialValue: T_Email.Text, maxLength: 50, keyboard: Keyboard.Email);
        if (result != null)
        {
            T_Email.Text = result;
        }
    }

    private async void TapHP_Tapped(object sender, TappedEventArgs e)
    {
        if (sender is Grid grid)
        {
            await grid.FadeTo(0.3, 100);
            await grid.FadeTo(1, 200);
        }

        // Tampilkan dialog untuk mengubah HP
        string result = await DisplayPromptAsync("Phone Number", "Edit phone number:", initialValue: T_Hp.Text, maxLength: 15, keyboard: Keyboard.Telephone);
        if (result != null)
        {
            // Format phone number: ensure it starts with +628 or 08
            if (!string.IsNullOrEmpty(result))
            {
                if (result.StartsWith("08"))
                {
                    T_Hp.Text = "+628" + result.Substring(2);
                }
                else if (result.StartsWith("+628"))
                {
                    T_Hp.Text = result;
                }
                else if (result.StartsWith("628"))
                {
                    T_Hp.Text = "+" + result;
                }
                else
                {
                    T_Hp.Text = result;
                }
            }
        }
    }

    private async void B_UpdateProfile_Clicked(object sender, EventArgs e)
    {
        // Disable button saat loading
        if (sender is Button btn)
        {
            btn.IsEnabled = false;
            btn.Text = "Updating...";
        }

        try
        {
            // Validasi apakah user sudah login
            if (!Login.IsUserLoggedIn())
            {
                pesan = "User not logged in";
                toast();
                return;
            }

            // Validasi input fields
            if (string.IsNullOrWhiteSpace(T_NamaLengkap.Text) || 
                string.IsNullOrWhiteSpace(T_Username.Text) || 
                string.IsNullOrWhiteSpace(T_Email.Text) || 
                string.IsNullOrWhiteSpace(T_Hp.Text))
            {
                pesan = "Please fill in all required fields";
                toast();
                return;
            }

            // Validasi format email
            if (!IsValidEmail(T_Email.Text))
            {
                pesan = "Please enter a valid email address";
                toast();
                return;
            }

            // Validasi format HP
            if (!IsValidPhoneNumber(T_Hp.Text))
            {
                pesan = "Please enter a valid phone number";
                toast();
                return;
            }

            // Dialog untuk memasukkan password baru
            string newPassword = await DisplayPromptAsync("New Password", "Enter new password:", 
                placeholder: "Enter new password", maxLength: 50, keyboard: Keyboard.Default);
            
            if (string.IsNullOrWhiteSpace(newPassword))
            {
                pesan = "Password is required";
                toast();
                return;
            }

            // Konfirmasi password
            string confirmPassword = await DisplayPromptAsync("Confirm Password", "Confirm new password:", 
                placeholder: "Confirm new password", maxLength: 50, keyboard: Keyboard.Default);
            
            if (newPassword != confirmPassword)
            {
                pesan = "Passwords do not match";
                toast();
                return;
            }

            // Konfirmasi update profile
            bool confirmUpdate = await DisplayAlert("Update Profile", "Are you sure you want to update your profile?", "Yes", "No");
            
            if (confirmUpdate)
            {
                // Lakukan update profile
                var updateResult = await PerformUpdateProfile(newPassword);

                if (updateResult.success)
                {
                    // Logout user setelah berhasil update
                    Login.LogoutUser();
                    
                    pesan = updateResult.message;
                    toast();
                    
                    // Navigate ke Login page
                    await Task.Delay(2000); // Tunggu 2 detik agar toast terlihat
                    Application.Current.MainPage = new NavigationPage(new Login());
                }
                else
                {
                    pesan = updateResult.message;
                    toast();
                }
            }
        }
        catch (Exception ex)
        {
            pesan = $"Update profile failed: {ex.Message}";
            toast();
        }
        finally
        {
            // Enable button kembali
            if (sender is Button button)
            {
                button.IsEnabled = true;
                button.Text = "Update Profile";
            }
        }
    }

    private async Task<UpdateUserResponse> PerformUpdateProfile(string newPassword)
    {
        try
        {
            using (var httpClient = new HttpClient())
            {
                // Set timeout
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                // Buat URL API
                string apiUrl = $"{App.IP}/api/users/update_users";

                // Ambil data user yang login
                var (id_user, username, nama_lengkap, id_sesi, email, hp) = Login.GetLoggedInUser();

                // Format HP: remove +628 and replace with 08
                string formattedHp = T_Hp.Text;
                if (formattedHp.StartsWith("+628"))
                {
                    formattedHp = "08" + formattedHp.Substring(4);
                }

                // Buat request data
                var updateData = new UpdateUserRequest
                {
                    id_user = id_user,
                    username = T_Username.Text.Trim(),
                    nama_lengkap = T_NamaLengkap.Text.Trim(),
                    role = "Kasir",
                    email = T_Email.Text.Trim(),
                    hp = formattedHp,
                    password = newPassword,
                    aktif = "1" // Asumsi aktif = 1 untuk user yang masih aktif

                };

                // Serialize ke JSON
                string jsonData = JsonConvert.SerializeObject(updateData);
                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                // Kirim POST request
                var response = await httpClient.PostAsync(apiUrl, content);

                // Baca response
                string responseContent = await response.Content.ReadAsStringAsync();

                // Deserialize response
                var updateResponse = JsonConvert.DeserializeObject<UpdateUserResponse>(responseContent);

                return updateResponse ?? new UpdateUserResponse { success = false, message = "Invalid response" };
            }
        }
        catch (HttpRequestException ex)
        {
            return new UpdateUserResponse { success = false, message = $"Network error: {ex.Message}" };
        }
        catch (TaskCanceledException)
        {
            return new UpdateUserResponse { success = false, message = "Request timeout" };
        }
        catch (Exception ex)
        {
            return new UpdateUserResponse { success = false, message = $"Error: {ex.Message}" };
        }
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private bool IsValidPhoneNumber(string phoneNumber)
    {
        // Basic validation untuk format phone number
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return false;

        // Remove all non-digit characters
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        
        // Check if it's a valid Indonesian phone number format
        if (phoneNumber.StartsWith("+628") && digits.Length >= 10 && digits.Length <= 15)
            return true;
        
        if (phoneNumber.StartsWith("08") && digits.Length >= 10 && digits.Length <= 13)
            return true;

        return false;
    }

    private async void Tap_Reloaduang_Tapped(object sender, TappedEventArgs e)
    {
        // Animasi tap untuk feedback visual
        if (sender is Label label)
        {
            await label.FadeTo(0.3, 100);
            await label.FadeTo(1, 200);
        }

        try
        {
            System.Diagnostics.Debug.WriteLine("=== RELOAD UANG TAPPED ===");
            
            // Refresh data closing session
            await LoadClosingSessionData();
            
            // Show toast untuk konfirmasi refresh
            pesan = "Data refreshed successfully";
            toast();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error refreshing closing session data: {ex.Message}");
            pesan = "Failed to refresh data";
            toast();
        }
    }

    private async void PrinterSetting_Tapped(object sender, TappedEventArgs e)
    {

        if (sender is Image image)
        {
            await image.FadeTo(0.3, 100); // Turunkan opacity ke 0.3 dalam 100ms
            await image.FadeTo(1, 200);   // Kembalikan opacity ke 1 dalam 200ms
        }

        await Navigation.PushAsync(new Account.Bluetooth());

    }

    private async void ConnectionSetup_Tapped(object sender, TappedEventArgs e)
    {
        if (sender is Image image)
        {
            await image.FadeTo(0.3, 100); // Turunkan opacity ke 0.3 dalam 100ms
            await image.FadeTo(1, 200);   // Kembalikan opacity ke 1 dalam 200ms
        }

        await Navigation.PushAsync(new Connection());
    }
    
    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Refresh connection info when page appears (e.g., when returning from Connection page)
        LoadActiveConnectionInfo();
    }
}