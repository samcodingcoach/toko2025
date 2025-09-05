using System.Net;
using System.Net.NetworkInformation;
using Toko2025.Services;

namespace Toko2025
{
    public partial class App : Application
    { 
        // Default IP Address
        public static string IP { get; set; } = "http://192.168.77.8:3000";
        
        // Local Database instance
        public static LocalDatabase Database { get; private set; }
        
        // Shared HttpClient untuk performance
        public static HttpClient SharedHttpClient { get; private set; }
        
        // Ping validation method
        public static async Task<bool> ValidateIPConnection()
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(IP, 3000); // 3 second timeout
                    return reply.Status == IPStatus.Success;
                }
            }
            catch (Exception)
            {
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
            
            // Test API endpoint (tanpa sync)
            Task.Run(async () => 
            {
                await Task.Delay(2000); // Wait 2 seconds
                await TestAPIEndpoint();
            });
            
            // Check if user is logged in
            if (Login.IsUserLoggedIn())
            {
               MainPage = new TabPage(); // Langsung ke TabPage

              // MainPage = new NavigationPage(new Account.Bluetooth());
             
            }
            else
            {
                MainPage = new NavigationPage(new Login());
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
                       $"Kategori Last Sync: {kategoriLastSync?.ToString("dd/MM/yyyy HH:mm:ss") ?? "Never"}\n" +
                       $"Merk: {merkCount} items\n" +
                       $"Merk Last Sync: {merkLastSync?.ToString("dd/MM/yyyy HH:mm:ss") ?? "Never"}";
            }
            catch (Exception ex)
            {
                return $"Database Status Error: {ex.Message}";
            }
        }
    }
}
