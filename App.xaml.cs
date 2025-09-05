using System.Net;
using System.Net.NetworkInformation;
using Toko2025.Services;

namespace Toko2025
{
    public partial class App : Application
    { 
        // Default IP Address - now loaded from Preferences
        public static string IP { get; set; } = LoadIPFromPreferences();
        
        // Local Database instance
        public static LocalDatabase Database { get; private set; }
        
        // Shared HttpClient untuk performance
        public static HttpClient SharedHttpClient { get; private set; }
        
        // Connection monitoring
        public static bool IsConnected { get; private set; } = true;
        public static event Action<bool> ConnectionChanged;
        
        // Load IP from Preferences
        private static string LoadIPFromPreferences()
        {
            try
            {
                string savedIP = Preferences.Get("SelectedIP", "http://192.168.77.8:3000");
                System.Diagnostics.Debug.WriteLine($"Loaded IP from Preferences: {savedIP}");
                return savedIP;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading IP from Preferences: {ex.Message}");
                return "http://192.168.77.8:3000"; // Default fallback
            }
        }
        
        // Update IP configuration
        public static void UpdateIPConfiguration(string newIP)
        {
            IP = newIP;
            Preferences.Set("SelectedIP", newIP);
            System.Diagnostics.Debug.WriteLine($"IP configuration updated to: {newIP}");
            
            // Re-initialize HttpClient with new IP
            InitializeHttpClient();
            
            // Test new connection
            Task.Run(async () => await MonitorConnection());
        }
        
        // Monitor connection status
        public static async Task MonitorConnection()
        {
            try
            {
                bool wasConnected = IsConnected;
                bool currentlyConnected = await ValidateIPConnection();
                
                if (wasConnected != currentlyConnected)
                {
                    IsConnected = currentlyConnected;
                    ConnectionChanged?.Invoke(currentlyConnected);
                    
                    System.Diagnostics.Debug.WriteLine($"Connection status changed: {(currentlyConnected ? "Connected" : "Disconnected")}");
                    
                    // If disconnected, optionally navigate to Connection page
                    if (!currentlyConnected)
                    {
                        await HandleConnectionLoss();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Connection monitoring error: {ex.Message}");
            }
        }
        
        // Handle connection loss
        private static async Task HandleConnectionLoss()
        {
            try
            {
                // You can customize this behavior based on your needs
                System.Diagnostics.Debug.WriteLine("Connection lost - consider showing Connection page");
                
                // Optional: Auto-navigate to Connection page on main thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    try
                    {
                        if (Application.Current?.MainPage != null)
                        {
                            // Only navigate if not already on Connection page
                            if (!(Application.Current.MainPage is NavigationPage navPage && 
                                  navPage.CurrentPage is Connection))
                            {
                                Application.Current.MainPage = new NavigationPage(new Connection());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Handle connection loss error: {ex.Message}");
            }
        }
        
        // Enhanced IP validation method
        public static async Task<bool> ValidateIPConnection()
        {
            try
            {
                // Test API endpoint instead of ping for better reliability
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(5);
                    
                    string testUrl = $"{IP}/api/kategori";
                    System.Diagnostics.Debug.WriteLine($"Testing connection to: {testUrl}");
                    
                    var response = await httpClient.GetAsync(testUrl);
                    bool isConnected = response.IsSuccessStatusCode;
                    
                    System.Diagnostics.Debug.WriteLine($"Connection test result: {isConnected} (Status: {response.StatusCode})");
                    return isConnected;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Connection validation failed: {ex.Message}");
                return false;
            }
        }

        // Method untuk test API endpoint
        public static async Task<(bool success, string message)> TestAPIEndpoint()
        {
            try
            {
                string testUrl = $"{IP}/api/kategori";
                System.Diagnostics.Debug.WriteLine($"Testing API endpoint: {testUrl}");
                
                var response = await SharedHttpClient.GetAsync(testUrl);
                var content = await response.Content.ReadAsStringAsync();
                
                System.Diagnostics.Debug.WriteLine($"API Response Status: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"API Response Content: {content}");
                
                if (response.IsSuccessStatusCode)
                {
                    return (true, $"API accessible. Content: {content}");
                }
                else
                {
                    return (false, $"API returned {response.StatusCode}: {content}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"API test error: {ex.Message}");
                return (false, $"API test failed: {ex.Message}");
            }
        }

        public App()
        {
            InitializeComponent();
            
            // Initialize shared HttpClient dengan optimasi
            InitializeHttpClient();
            
            // Initialize database
            try
            {
                Database = new LocalDatabase();
                System.Diagnostics.Debug.WriteLine("Database initialized successfully");
                
                // Test database creation
                var (kategoriCount, merkCount) = Database.GetDataCounts();
                System.Diagnostics.Debug.WriteLine($"Initial database counts - Kategori: {kategoriCount}, Merk: {merkCount}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database initialization error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            // Start connection monitoring
            Task.Run(async () => 
            {
                await Task.Delay(2000); // Wait 2 seconds
                await MonitorConnection();
                await TestAPIEndpoint();
            });
            
            // Check if user is logged in
            if (Login.IsUserLoggedIn())
            {
                MainPage = new TabPage(); // Langsung ke TabPage
            }
            else
            {
                MainPage = new NavigationPage(new Connection());
            }
        }

        private static void InitializeHttpClient()
        {
            var handler = new HttpClientHandler()
            {
                // Enable automatic decompression
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            SharedHttpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30) // Kembali ke 30 detik
            };

            System.Diagnostics.Debug.WriteLine("HttpClient initialized");
        }

        // Method untuk force sync dari page manapun
        public static async Task<bool> ForceSyncMasterDataAsync()
        {
            try
            {
                if (Database == null)
                {
                    System.Diagnostics.Debug.WriteLine("Database not initialized");
                    return false;
                }
                
                return await Database.ForceRefreshAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Force sync error: {ex.Message}");
                return false;
            }
        }

        // Method untuk force sync kategori saja (untuk mendapatkan jumlah terbaru)
        public static async Task<bool> ForceSyncKategoriAsync()
        {
            try
            {
                if (Database == null)
                {
                    System.Diagnostics.Debug.WriteLine("Database not initialized");
                    return false;
                }
                
                return await Database.ForceRefreshKategoriAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Force sync kategori error: {ex.Message}");
                return false;
            }
        }

        // Method untuk force sync merk saja (untuk mendapatkan jumlah terbaru)
        public static async Task<bool> ForceSyncMerkAsync()
        {
            try
            {
                if (Database == null)
                {
                    System.Diagnostics.Debug.WriteLine("Database not initialized");
                    return false;
                }
                
                return await Database.ForceRefreshMerkAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Force sync merk error: {ex.Message}");
                return false;
            }
        }

        // Method untuk debug database status
        public static string GetDatabaseStatus()
        {
            try
            {
                if (Database == null)
                {
                    return "Database: NULL";
                }
                
                var (kategoriCount, merkCount) = Database.GetDataCounts();
                var kategoriLastSync = Database.GetLastSyncTime("Kategori");
                var merkLastSync = Database.GetLastSyncTime("Merk");
                
                return $"Database Status:\n" +
                       $"Kategori: {kategoriCount} items\n" +
                       $"Merk: {merkCount} items\n" +
                       $"Kategori Last Sync: {kategoriLastSync}\n" +
                       $"Merk Last Sync: {merkLastSync}";
            }
            catch (Exception ex)
            {
                return $"Database Status Error: {ex.Message}";
            }
        }
        
        // Start periodic connection monitoring
        public static void StartConnectionMonitoring(int intervalSeconds = 30)
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await MonitorConnection();
                        await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Connection monitoring loop error: {ex.Message}");
                        await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
                    }
                }
            });
        }
        
        // Get current connection info
        public static string GetConnectionInfo()
        {
            return $"Current IP: {IP}\n" +
                   $"Status: {(IsConnected ? "Connected" : "Disconnected")}\n" +
                   $"Network Type: {Preferences.Get("NetworkType", "Unknown")}";
        }
    }
}
