using SQLite;
using Newtonsoft.Json;

namespace Toko2025.Services
{
    public class LocalDatabase
    {
        private readonly SQLiteConnection _database;
        private readonly HttpClient _httpClient;

        public LocalDatabase()
        {
            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "toko2025.db");
            _database = new SQLiteConnection(dbPath);
            _httpClient = new HttpClient();
            
            // Create tables
            _database.CreateTable<Kategori>();
            _database.CreateTable<Merk>();
            _database.CreateTable<SyncStatus>();
        }

        // Get data from local database
        public List<Kategori> GetKategori()
        {
            return _database.Table<Kategori>().ToList();
        }

        public List<Merk> GetMerk()
        {
            // Debug: Check all merk data first
            var allMerk = _database.Table<Merk>().ToList();
            System.Diagnostics.Debug.WriteLine($"=== GetMerk Debug ===");
            System.Diagnostics.Debug.WriteLine($"Total merk in database: {allMerk.Count}");
            
            foreach (var merk in allMerk)
            {
                System.Diagnostics.Debug.WriteLine($"Merk: {merk.nama_merk}, Aktif: {merk.aktif}, Jumlah: {merk.jumlah}");
            }
            
            // Filter for active brands (aktif == 1)
            var activeMerk = allMerk.Where(m => m.aktif == 1).ToList();
            System.Diagnostics.Debug.WriteLine($"Active merk count: {activeMerk.Count}");
            
            // If no active brands found, return all brands (fallback)
            if (activeMerk.Count == 0 && allMerk.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine("No active brands found, returning all brands as fallback");
                return allMerk;
            }
            
            return activeMerk;
        }

        public List<Merk> GetAllMerk()
        {
            // Ambil semua merk (termasuk yang tidak aktif)
            return _database.Table<Merk>().ToList();
        }

        // Get single item by ID
        public Kategori GetKategoriById(int idKategori)
        {
            return _database.Table<Kategori>().FirstOrDefault(k => k.id_kategori == idKategori);
        }

        public Merk GetMerkById(int idMerk)
        {
            return _database.Table<Merk>().FirstOrDefault(m => m.id_merk == idMerk);
        }

        // Check if data already synced
        public bool IsDataInitialized(string tableName)
        {
            var syncStatus = _database.Table<SyncStatus>()
                .FirstOrDefault(s => s.TableName == tableName);
            
            return syncStatus?.IsInitialized ?? false;
        }

        // Sync Kategori
        public async Task<bool> SyncKategoriAsync()
        {
            try
            {
                if (IsDataInitialized("Kategori"))
                {
                    System.Diagnostics.Debug.WriteLine("Kategori already synced, skipping...");
                    return true;
                }

                // Fetch from server
                var response = await _httpClient.GetAsync($"http://{App.IP}:3000/api/kategori");
                response.EnsureSuccessStatusCode();
                
                var jsonContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Kategori JSON Response: {jsonContent}");
                
                // Parse API response with wrapper
                var apiResponse = JsonConvert.DeserializeObject<ApiResponse<List<Kategori>>>(jsonContent);

                if (apiResponse != null && apiResponse.success && apiResponse.data != null && apiResponse.data.Count > 0)
                {
                    // Clear and insert new data
                    _database.DeleteAll<Kategori>();
                    _database.InsertAll(apiResponse.data);

                    // Update sync status
                    UpdateSyncStatus("Kategori");
                    
                    System.Diagnostics.Debug.WriteLine($"Synced {apiResponse.data.Count} kategori successfully");
                    
                    // Log the jumlah values for debugging
                    foreach (var kategori in apiResponse.data)
                    {
                        System.Diagnostics.Debug.WriteLine($"Kategori: {kategori.nama_kategori}, Jumlah: {kategori.jumlah}");
                    }
                    
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"API response failed or no data. Success: {apiResponse?.success}, Data count: {apiResponse?.data?.Count ?? 0}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sync Kategori error: {ex.Message}");
                return false;
            }
        }

        // Sync Merk
        public async Task<bool> SyncMerkAsync()
        {
            try
            {
                if (IsDataInitialized("Merk"))
                {
                    System.Diagnostics.Debug.WriteLine("Merk already synced, skipping...");
                    return true;
                }

                var response = await _httpClient.GetAsync($"http://{App.IP}:3000/api/merk");
                response.EnsureSuccessStatusCode();
                
                var jsonContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Merk JSON Response: {jsonContent}");
                
                // Parse API response with wrapper
                var apiResponse = JsonConvert.DeserializeObject<ApiResponse<List<Merk>>>(jsonContent);

                if (apiResponse != null && apiResponse.success && apiResponse.data != null && apiResponse.data.Count > 0)
                {
                    // If API doesn't provide aktif field, set all brands as active
                    foreach (var merk in apiResponse.data)
                    {
                        if (merk.aktif == 0) // If not explicitly set by API
                        {
                            merk.aktif = 1; // Set as active
                        }
                    }
                    
                    _database.DeleteAll<Merk>();
                    _database.InsertAll(apiResponse.data);

                    UpdateSyncStatus("Merk");
                    
                    System.Diagnostics.Debug.WriteLine($"Synced {apiResponse.data.Count} merk successfully");
                    
                    // Log the jumlah values for debugging
                    foreach (var merk in apiResponse.data)
                    {
                        System.Diagnostics.Debug.WriteLine($"Merk: {merk.nama_merk}, Aktif: {merk.aktif}, Jumlah: {merk.jumlah}");
                    }
                    
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"API response failed or no data. Success: {apiResponse?.success}, Data count: {apiResponse?.data?.Count ?? 0}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sync Merk error: {ex.Message}");
                return false;
            }
        }

        // Product API methods
        public async Task<List<Product>> GetProductsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"http://{App.IP}:3000/api/barang/display");
                response.EnsureSuccessStatusCode();
                
                var jsonContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Products JSON Response: {jsonContent}");
                
                // Parse API response with wrapper
                var apiResponse = JsonConvert.DeserializeObject<ApiResponse<List<Product>>>(jsonContent);

                if (apiResponse != null && apiResponse.success && apiResponse.data != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Retrieved {apiResponse.data.Count} products successfully");
                    return apiResponse.data;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"API response failed or no data. Success: {apiResponse?.success}, Data count: {apiResponse?.data?.Count ?? 0}");
                    return new List<Product>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Get products error: {ex.Message}");
                return new List<Product>();
            }
        }

        public async Task<List<Product>> GetProductsBySearchAsync(string searchTerm)
        {
            try
            {
                var allProducts = await GetProductsAsync();
                
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    return allProducts;
                }

                return allProducts.Where(p => 
                    p.nama_barang.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    p.barcode1.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(p.barcode2) && p.barcode2.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Search products error: {ex.Message}");
                return new List<Product>();
            }
        }

        public async Task<List<Product>> GetProductsByCategoryAsync(int categoryId)
        {
            try
            {
                var allProducts = await GetProductsAsync();
                return allProducts.Where(p => p.id_kategori == categoryId).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Get products by category error: {ex.Message}");
                return new List<Product>();
            }
        }

        public async Task<List<Product>> GetProductsByBrandAsync(int brandId)
        {
            try
            {
                var allProducts = await GetProductsAsync();
                return allProducts.Where(p => p.id_merk == brandId).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Get products by brand error: {ex.Message}");
                return new List<Product>();
            }
        }

        // Update sync status
        private void UpdateSyncStatus(string tableName)
        {
            var syncStatus = _database.Table<SyncStatus>()
                .FirstOrDefault(s => s.TableName == tableName);

            if (syncStatus == null)
            {
                syncStatus = new SyncStatus 
                { 
                    TableName = tableName,
                    LastSync = DateTime.Now,
                    IsInitialized = true
                };
                _database.Insert(syncStatus);
            }
            else
            {
                syncStatus.LastSync = DateTime.Now;
                syncStatus.IsInitialized = true;
                _database.Update(syncStatus);
            }
        }

        // Force refresh data
        public async Task<bool> ForceRefreshAsync()
        {
            try
            {
                // Reset sync status
                var kategoriSync = _database.Table<SyncStatus>()
                    .FirstOrDefault(s => s.TableName == "Kategori");
                var merkSync = _database.Table<SyncStatus>()
                    .FirstOrDefault(s => s.TableName == "Merk");

                if (kategoriSync != null)
                {
                    kategoriSync.IsInitialized = false;
                    _database.Update(kategoriSync);
                }

                if (merkSync != null)
                {
                    merkSync.IsInitialized = false;
                    _database.Update(merkSync);
                }

                // Sync again
                var kategoriResult = await SyncKategoriAsync();
                var merkResult = await SyncMerkAsync();

                return kategoriResult && merkResult;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Force refresh error: {ex.Message}");
                return false;
            }
        }

        // Force refresh only kategori data to get updated jumlah field
        public async Task<bool> ForceRefreshKategoriAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Force refreshing kategori data...");
                
                // Reset sync status for kategori
                var kategoriSync = _database.Table<SyncStatus>()
                    .FirstOrDefault(s => s.TableName == "Kategori");

                if (kategoriSync != null)
                {
                    kategoriSync.IsInitialized = false;
                    _database.Update(kategoriSync);
                }

                // Sync again
                var kategoriResult = await SyncKategoriAsync();
                
                if (kategoriResult)
                {
                    System.Diagnostics.Debug.WriteLine("Kategori force refresh completed successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Kategori force refresh failed");
                }

                return kategoriResult;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Force refresh kategori error: {ex.Message}");
                return false;
            }
        }

        // Force refresh only merk data to get updated jumlah field
        public async Task<bool> ForceRefreshMerkAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Force refreshing merk data...");
                
                // Reset sync status for merk
                var merkSync = _database.Table<SyncStatus>()
                    .FirstOrDefault(s => s.TableName == "Merk");

                if (merkSync != null)
                {
                    merkSync.IsInitialized = false;
                    _database.Update(merkSync);
                }

                // Sync again
                var merkResult = await SyncMerkAsync();
                
                if (merkResult)
                {
                    System.Diagnostics.Debug.WriteLine("Merk force refresh completed successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Merk force refresh failed");
                }

                return merkResult;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Force refresh merk error: {ex.Message}");
                return false;
            }
        }

        // Get last sync time
        public DateTime? GetLastSyncTime(string tableName)
        {
            var syncStatus = _database.Table<SyncStatus>()
                .FirstOrDefault(s => s.TableName == tableName);
            
            return syncStatus?.LastSync;
        }

        // Check if tables have data
        public bool HasData()
        {
            var kategoriCount = _database.Table<Kategori>().Count();
            var merkCount = _database.Table<Merk>().Count();
            
            return kategoriCount > 0 && merkCount > 0;
        }

        // Get counts for debugging
        public (int kategoriCount, int merkCount) GetDataCounts()
        {
            var kategoriCount = _database.Table<Kategori>().Count();
            var merkCount = _database.Table<Merk>().Count();
            
            return (kategoriCount, merkCount);
        }

        // Debug method to check database state
        public void DebugDatabaseState()
        {
            System.Diagnostics.Debug.WriteLine("=== DATABASE STATE DEBUG ===");
            
            // Check all tables
            var allKategori = _database.Table<Kategori>().ToList();
            var allMerk = _database.Table<Merk>().ToList();
            
            System.Diagnostics.Debug.WriteLine($"Total Kategori: {allKategori.Count}");
            foreach (var k in allKategori)
            {
                System.Diagnostics.Debug.WriteLine($"  Kategori: {k.nama_kategori}, Jumlah: {k.jumlah}");
            }
            
            System.Diagnostics.Debug.WriteLine($"Total Merk: {allMerk.Count}");
            foreach (var m in allMerk)
            {
                System.Diagnostics.Debug.WriteLine($"  Merk: {m.nama_merk}, Aktif: {m.aktif}, Jumlah: {m.jumlah}");
            }
            
            // Check active brands specifically
            var activeMerk = GetMerk();
            System.Diagnostics.Debug.WriteLine($"Active Merk: {activeMerk.Count}");
            
            System.Diagnostics.Debug.WriteLine("=== END DATABASE STATE DEBUG ===");
        }
    }

    // API Response wrapper class
    public class ApiResponse<T>
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
        public T data { get; set; }
    }
}