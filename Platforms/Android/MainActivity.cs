using Android;
using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using System.Collections.Generic;
using System.Linq;

namespace Toko2025
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ScreenOrientation = ScreenOrientation.Portrait, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        const int RequestBluetoothPermission = 1;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            
            System.Diagnostics.Debug.WriteLine("=== MAINACTIVITY ONCREATE ===");
            System.Diagnostics.Debug.WriteLine($"Android SDK Version: {Build.VERSION.SdkInt}");
            
            RequestBluetoothPermissions();
        }

        void RequestBluetoothPermissions()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== REQUESTING BLUETOOTH PERMISSIONS ===");
                
                var permissions = new List<string>();

                if (Build.VERSION.SdkInt >= BuildVersionCodes.S) // Android 12+ (API 31+)
                {
                    System.Diagnostics.Debug.WriteLine("Android 12+ detected, checking new permissions...");
                    
                    if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.BluetoothConnect) != Permission.Granted)
                    {
                        permissions.Add(Manifest.Permission.BluetoothConnect);
                        System.Diagnostics.Debug.WriteLine("Need BLUETOOTH_CONNECT permission");
                    }
                        
                    if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.BluetoothScan) != Permission.Granted)
                    {
                        permissions.Add(Manifest.Permission.BluetoothScan);
                        System.Diagnostics.Debug.WriteLine("Need BLUETOOTH_SCAN permission");
                    }
                }
                else // Android 11 and below
                {
                    System.Diagnostics.Debug.WriteLine("Android 11 or below detected, checking legacy permissions...");
                    
                    if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.Bluetooth) != Permission.Granted)
                    {
                        permissions.Add(Manifest.Permission.Bluetooth);
                        System.Diagnostics.Debug.WriteLine("Need BLUETOOTH permission");
                    }
                        
                    if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.BluetoothAdmin) != Permission.Granted)
                    {
                        permissions.Add(Manifest.Permission.BluetoothAdmin);
                        System.Diagnostics.Debug.WriteLine("Need BLUETOOTH_ADMIN permission");
                    }
                }

                if (permissions.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"Requesting {permissions.Count} permissions...");
                    ActivityCompat.RequestPermissions(this, permissions.ToArray(), RequestBluetoothPermission);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("All Bluetooth permissions already granted");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error requesting Bluetooth permissions: {ex.Message}");
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            try
            {
                base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

                System.Diagnostics.Debug.WriteLine($"=== PERMISSION RESULT ===");
                System.Diagnostics.Debug.WriteLine($"Request code: {requestCode}");
                System.Diagnostics.Debug.WriteLine($"Permissions: {string.Join(", ", permissions)}");
                System.Diagnostics.Debug.WriteLine($"Results: {string.Join(", ", grantResults)}");

                if (requestCode == RequestBluetoothPermission)
                {
                    bool allGranted = grantResults.All(r => r == Permission.Granted);
                    
                    if (allGranted)
                    {
                        System.Diagnostics.Debug.WriteLine("✅ All Bluetooth permissions granted");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("❌ Some Bluetooth permissions denied");
                        
                        // Log which permissions were denied
                        for (int i = 0; i < permissions.Length; i++)
                        {
                            if (grantResults[i] != Permission.Granted)
                            {
                                System.Diagnostics.Debug.WriteLine($"❌ Permission denied: {permissions[i]}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling permission result: {ex.Message}");
            }
        }
    }
}
