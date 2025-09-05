using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Newtonsoft.Json;
using System.Text;
using Toko2025.Services;

namespace Toko2025;

public partial class Login : ContentPage
{
    private bool _isPasswordVisible = false;
    string apiUrl = $"{App.IP}/api/sesi_kasir/login";
    string pesan=string.Empty;
    public Login()
    {
        InitializeComponent();
        
        // Load store profile data when page is initialized
        LoadStoreProfileAsync();
    }

    private async void LoadStoreProfileAsync()
    {
        try
        {
            // Check connection before loading profile
            bool isConnected = await ConnectionService.TestConnection();
            if (!isConnected)
            {
                System.Diagnostics.Debug.WriteLine("No connection available, using fallback values");
                // Use fallback values when no connection
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StoreNameLabel.Text = "NAMA TOKO";
                    StoreAddressLabel.Text = "Alamat Toko";
                    StoreImageBackground.Source = null;
                    FallbackGradient.IsVisible = true;
                });
                return;
            }

            var storeProfile = await GetStoreProfile();
            if (storeProfile != null && storeProfile.success)
            {
                // Update UI labels with store information
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StoreNameLabel.Text = !string.IsNullOrEmpty(storeProfile.data.nama_usaha) 
                        ? storeProfile.data.nama_usaha.ToUpper() 
                        : "NAMA TOKO";
                    
                    StoreAddressLabel.Text = !string.IsNullOrEmpty(storeProfile.data.alamat) 
                        ? storeProfile.data.alamat 
                        : "Alamat Toko";
                    
                    // Load store image if available
                    if (!string.IsNullOrEmpty(storeProfile.data.foto_usaha))
                    {
                        string imageUrl = storeProfile.data.ImageUrl;
                        System.Diagnostics.Debug.WriteLine($"Loading store image: {imageUrl}");
                        
                        StoreImageBackground.Source = ImageSource.FromUri(new Uri(imageUrl));
                        FallbackGradient.IsVisible = false; // Hide gradient when image is loaded
                    }
                    else
                    {
                        // No image available, use gradient background
                        StoreImageBackground.Source = null;
                        FallbackGradient.IsVisible = true;
                    }
                });
            }
            else
            {
                // Fallback to default values if API fails
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StoreNameLabel.Text = "NAMA TOKO";
                    StoreAddressLabel.Text = "Alamat Toko";
                    // Use gradient background as fallback
                    StoreImageBackground.Source = null;
                    FallbackGradient.IsVisible = true;
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading store profile: {ex.Message}");
            // Use default values on error
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StoreNameLabel.Text = "NAMA TOKO";
                StoreAddressLabel.Text = "Alamat Toko";
                // Use gradient background as fallback on error
                StoreImageBackground.Source = null;
                FallbackGradient.IsVisible = true;
            });
        }
    }

    private async Task<ProfileUsahaResponse?> GetStoreProfile()
    {
        try
        {
            string profileApiUrl = $"{App.IP}/api/profil-usaha";
            
            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromSeconds(10); // Shorter timeout for profile
                
                var response = await httpClient.GetAsync(profileApiUrl);
                string responseContent = await response.Content.ReadAsStringAsync();
                
                System.Diagnostics.Debug.WriteLine($"Store profile API response: {responseContent}");
                
                if (response.IsSuccessStatusCode)
                {
                    var profileResponse = JsonConvert.DeserializeObject<ProfileUsahaResponse>(responseContent);
                    return profileResponse;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Store profile API error: {response.StatusCode}");
                    return null;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Store profile fetch error: {ex.Message}");
            return null;
        }
    }

    private async void toast()
    {
        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        ToastDuration duration = ToastDuration.Long;
        double fontSize = 12;
        var toast = Toast.Make(pesan, duration, fontSize);
        await toast.Show(cancellationTokenSource.Token);
    }

    // Model untuk Login Request
    public class LoginRequest
    {
        public string username { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
    }

    // Model untuk Password (Buffer type)
    public class PasswordBuffer
    {
        public string type { get; set; } = string.Empty;
        public List<int> data { get; set; } = new List<int>();
    }

    // Model untuk User Data
    public class UserData
    {
        public int id_user { get; set; }
        public string username { get; set; } = string.Empty;
        public string nama_lengkap { get; set; } = string.Empty;
        public string id_sesi { get; set; } = string.Empty;
        public string email { get; set; } = string.Empty;
        public string hp { get; set; } = string.Empty;

    }

    // Model untuk Login Response
    public class LoginResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
        public UserData data { get; set; } = new UserData();
    }

    

    private void OnTogglePasswordVisibility(object sender, EventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;
        PasswordEntry.IsPassword = !_isPasswordVisible;
        // TogglePasswordButton.Source = _isPasswordVisible ? "eye_open.png" : "eye_closed.png";
    }

    private async void ButtonLogin_Clicked(object sender, EventArgs e)
    {
        // Validasi koneksi sebelum login
        bool isConnected = await ConnectionService.CheckConnectionAndHandle();
        if (!isConnected)
        {
            // ConnectionService will handle showing Connection page
            return;
        }

        // Disable button saat loading
        if (sender is Button btn)
        {
            btn.IsEnabled = false;
            btn.Text = "Logging in...";
        }

        try
        {
            // Ambil username dan password dari UI
            string username = UsernameEntry.Text ?? string.Empty;
            string password = PasswordEntry.Text ?? string.Empty;

            // Validasi input
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                pesan = "Please enter username and password";
                toast();
                UsernameEntry.Focus();
                return;
            }

            // Lakukan login
            var loginResult = await PerformLogin(username, password);

            if (loginResult.success)
            {
                // Simpan data user untuk session
                SaveUserSession(loginResult.data);
                
                pesan = loginResult.message;
                toast();

                // Sync master data setelah login berhasil
                if (sender is Button button)
                {
                    button.Text = "Syncing data...";
                }
                
                await SyncMasterDataAfterLogin();
                
                // Navigate ke TabPage
                await Navigation.PushAsync(new TabPage());
            }
            else
            {
                pesan = loginResult.message;
                toast();
            }
        }
        catch (Exception ex)
        {
            pesan = $"Login failed: {ex.Message}";
            toast();
        }
        finally
        {
            // Enable button kembali
            if (sender is Button button)
            {
                button.IsEnabled = true;
                button.Text = "Login";
            }
        }
    }

    private async Task<LoginResponse> PerformLogin(string username, string password)
    {
        try
        {
            using (var httpClient = new HttpClient())
            {
                // Set timeout
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                // Buat URL API
              

                // Buat request data
                var loginData = new LoginRequest
                {
                    username = username,
                    password = password
                };

                // Serialize ke JSON
                string jsonData = JsonConvert.SerializeObject(loginData);
                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                // Kirim POST request
                var response = await httpClient.PostAsync(apiUrl, content);

                // Baca response
                string responseContent = await response.Content.ReadAsStringAsync();

                // Deserialize response
                var loginResponse = JsonConvert.DeserializeObject<LoginResponse>(responseContent);

                return loginResponse ?? new LoginResponse { success = false, message = "Invalid response" };
            }
        }
        catch (HttpRequestException ex)
        {
            return new LoginResponse { success = false, message = $"Network error: {ex.Message}" };
        }
        catch (TaskCanceledException)
        {
            return new LoginResponse { success = false, message = "Request timeout" };
        }
        catch (Exception ex)
        {
            return new LoginResponse { success = false, message = $"Error: {ex.Message}" };
        }
    }

    private void SaveUserSession(UserData userData)
    {
        // Simpan data user ke Preferences untuk session
        Preferences.Set("user_id", userData.id_user.ToString());
        Preferences.Set("username", userData.username);
        Preferences.Set("nama_lengkap", userData.nama_lengkap);
        Preferences.Set("id_sesi", userData.id_sesi);
        Preferences.Set("email", userData.email);
        Preferences.Set("hp", userData.hp);
        Preferences.Set("is_logged_in", true);
    }

    // Method untuk mengecek apakah user sudah login
    public static bool IsUserLoggedIn()
    {
        return Preferences.Get("is_logged_in", false);
    }

    // Method untuk mendapatkan data user yang login
    public static (int id_user, string username, string nama_lengkap, string id_sesi,string email,string hp) GetLoggedInUser()
    {
        var id_user = int.Parse(Preferences.Get("user_id", "0"));
        var username = Preferences.Get("username", string.Empty);
        var nama_lengkap = Preferences.Get("nama_lengkap", string.Empty);
        var id_sesi = Preferences.Get("id_sesi", string.Empty);
        var email = Preferences.Get("email", string.Empty);
        var hp = Preferences.Get("hp", string.Empty);


        return (id_user, username, nama_lengkap, id_sesi,email,hp);
    }

    
  

    // Method untuk logout
    public static void LogoutUser()
    {
        Preferences.Remove("user_id");
        Preferences.Remove("username");
        Preferences.Remove("nama_lengkap");
        Preferences.Remove("id_sesi");
        Preferences.Remove("email");
        Preferences.Remove("hp");
        Preferences.Set("is_logged_in", false);
    }

    private async Task SyncMasterDataAfterLogin()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Starting master data sync after login...");
            
            // Force refresh untuk memastikan data terbaru
            bool refreshSuccess = await App.Database.ForceRefreshAsync();
            System.Diagnostics.Debug.WriteLine($"Force refresh result: {refreshSuccess}");
            
            // Get data counts for verification
            var (kategoriCount, merkCount) = App.Database.GetDataCounts();
            System.Diagnostics.Debug.WriteLine($"Data counts after sync - Kategori: {kategoriCount}, Merk: {merkCount}");
            
            // Debug database state
            App.Database.DebugDatabaseState();
            
            if (refreshSuccess)
            {
                System.Diagnostics.Debug.WriteLine("Master data sync completed successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Master data sync completed with errors");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Master data sync error: {ex.Message}");
        }
    }
}