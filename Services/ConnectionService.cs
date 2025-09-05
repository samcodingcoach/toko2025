using System;
using System.Threading.Tasks;

namespace Toko2025.Services
{
    public static class ConnectionService
    {
        /// <summary>
        /// Check connection and automatically handle disconnection by showing Connection page
        /// </summary>
        /// <returns>True if connected, False if disconnected</returns>
        public static async Task<bool> CheckConnectionAndHandle()
        {
            try
            {
                bool isConnected = await App.ValidateIPConnection();
                
                if (!isConnected)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        try
                        {
                            // Show Connection page
                            if (Application.Current?.MainPage != null)
                            {
                                await Application.Current.MainPage.DisplayAlert(
                                    "Connection Lost", 
                                    "Unable to connect to the server. Please check your connection settings.", 
                                    "OK"
                                );
                                
                                Application.Current.MainPage = new NavigationPage(new Connection());
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error showing connection page: {ex.Message}");
                        }
                    });
                }
                
                return isConnected;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CheckConnectionAndHandle error: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Test connection without handling disconnection
        /// </summary>
        /// <returns>True if connected, False if disconnected</returns>
        public static async Task<bool> TestConnection()
        {
            return await App.ValidateIPConnection();
        }
        
        /// <summary>
        /// Get current connection information
        /// </summary>
        /// <returns>Connection info string</returns>
        public static string GetConnectionInfo()
        {
            return App.GetConnectionInfo();
        }
        
        /// <summary>
        /// Update IP configuration and test new connection
        /// </summary>
        /// <param name="newIP">New IP address to use</param>
        /// <returns>True if new IP works, False otherwise</returns>
        public static async Task<bool> UpdateAndTestIP(string newIP)
        {
            try
            {
                string oldIP = App.IP;
                
                // Temporarily set new IP for testing
                App.IP = newIP;
                
                bool connectionSuccess = await App.ValidateIPConnection();
                
                if (connectionSuccess)
                {
                    // Save if successful
                    App.UpdateIPConfiguration(newIP);
                    System.Diagnostics.Debug.WriteLine($"IP updated successfully from {oldIP} to {newIP}");
                }
                else
                {
                    // Restore old IP if failed
                    App.IP = oldIP;
                    System.Diagnostics.Debug.WriteLine($"IP update failed, restored to {oldIP}");
                }
                
                return connectionSuccess;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateAndTestIP error: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Get saved network configuration
        /// </summary>
        /// <returns>Tuple with LocalIP, OnlineIP, and NetworkType</returns>
        public static (string LocalIP, string OnlineIP, string NetworkType) GetSavedConfiguration()
        {
            try
            {
                string localIP = Preferences.Get("LocalIP", "192.168.1.2:3000");
                string onlineIP = Preferences.Get("OnlineIP", "domain.com");
                string networkType = Preferences.Get("NetworkType", "Local Network");
                
                return (localIP, onlineIP, networkType);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetSavedConfiguration error: {ex.Message}");
                return ("192.168.1.2:3000", "domain.com", "Local Network");
            }
        }
    }
}