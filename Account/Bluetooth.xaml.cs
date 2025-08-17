using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Toko2025.Services;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace Toko2025.Account;

public partial class Bluetooth : ContentPage, INotifyPropertyChanged
{
    private readonly IBluetoothService _bluetoothService;
    private ObservableCollection<BluetoothDeviceItem> _devices = new ObservableCollection<BluetoothDeviceItem>();
    private BluetoothDeviceItem _selectedDevice;
    private bool _isRefreshing = false;
    private string _savedPrinterName = string.Empty;

    public ObservableCollection<BluetoothDeviceItem> Devices
    {
        get => _devices;
        set
        {
            _devices = value;
            OnPropertyChanged();
        }
    }

    public BluetoothDeviceItem SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            _selectedDevice = value;
            OnPropertyChanged();
        }
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set
        {
            _isRefreshing = value;
            OnPropertyChanged();
        }
    }

    public Bluetooth()
    {
        InitializeComponent();
        BindingContext = this;
        
#if ANDROID
        // FIXED: Proper way to get service in .NET MAUI
        try
        {
            _bluetoothService = IPlatformApplication.Current?.Services?.GetService<IBluetoothService>();
            
            // Fallback - create instance directly if service not found
            if (_bluetoothService == null)
            {
                System.Diagnostics.Debug.WriteLine("Creating AndroidBluetoothService directly");
                _bluetoothService = new Toko2025.Platforms.Android.AndroidBluetoothService();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting bluetooth service: {ex.Message}");
            // Create instance directly as fallback
            _bluetoothService = new Toko2025.Platforms.Android.AndroidBluetoothService();
        }
#endif

        // Load saved printer
        _savedPrinterName = Preferences.Get("default_printer", string.Empty);
        System.Diagnostics.Debug.WriteLine($"Saved printer name: {_savedPrinterName}");
        
        // Setup event handlers
        SetupEventHandlers();
        
        System.Diagnostics.Debug.WriteLine("=== BLUETOOTH PAGE CONSTRUCTOR COMPLETE ===");
    }
    
    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        System.Diagnostics.Debug.WriteLine("=== BLUETOOTH PAGE APPEARING ===");
        
        // Load devices when page appears to ensure UI is ready
        Task.Run(async () =>
        {
            await Task.Delay(100); // Small delay to ensure UI is fully loaded
            LoadBluetoothDevices();
        });
    }

    private void SetupEventHandlers()
    {
        var backTap = this.FindByName<TapGestureRecognizer>("BackTap");
        if (backTap != null)
            backTap.Tapped += async (s, e) => await Navigation.PopAsync();

        var refreshTap = this.FindByName<TapGestureRecognizer>("RefreshTap");
        if (refreshTap != null)
            refreshTap.Tapped += async (s, e) => await RefreshDevices();

        var saveButton = this.FindByName<Button>("SaveButton");
        if (saveButton != null)
            saveButton.Clicked += SaveButton_Clicked;
    }

    private async void LoadBluetoothDevices()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== LOADING BLUETOOTH DEVICES ===");
            
            // Set loading state on main thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsRefreshing = true;
                Devices.Clear();
                
                // Control visibility manually
                var scrollView = this.FindByName<ScrollView>("DeviceListScrollView");
                if (scrollView != null)
                    scrollView.IsVisible = false;
            });

            if (_bluetoothService == null)
            {
                System.Diagnostics.Debug.WriteLine("Bluetooth service is null");
                await ShowToast("Bluetooth service not available");
                return;
            }

            System.Diagnostics.Debug.WriteLine("Bluetooth service found, checking permissions...");

            // Check Bluetooth permission (only for Android API 31+)
#if ANDROID
            try
            {
                var bluetoothStatus = await Permissions.CheckStatusAsync<Permissions.Bluetooth>();
                System.Diagnostics.Debug.WriteLine($"Bluetooth permission status: {bluetoothStatus}");
                
                if (bluetoothStatus != PermissionStatus.Granted)
                {
                    System.Diagnostics.Debug.WriteLine("Requesting Bluetooth permission...");
                    bluetoothStatus = await Permissions.RequestAsync<Permissions.Bluetooth>();
                    
                    if (bluetoothStatus != PermissionStatus.Granted)
                    {
                        await ShowToast("Bluetooth permission is required");
                        return;
                    }
                }
            }
            catch (Exception permEx)
            {
                System.Diagnostics.Debug.WriteLine($"Permission check error: {permEx.Message}");
                // Continue anyway for older Android versions
            }
#endif

            System.Diagnostics.Debug.WriteLine("Getting device list...");
            var deviceList = _bluetoothService.GetDeviceList();
            
            System.Diagnostics.Debug.WriteLine($"Device list count: {deviceList?.Count ?? 0}");
            
            if (deviceList == null || !deviceList.Any())
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await ShowToast("No paired Bluetooth devices found. Please pair your printer first.");
                });
                return;
            }

            // Process devices on main thread
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                foreach (var deviceInfo in deviceList)
                {
                    System.Diagnostics.Debug.WriteLine($"Adding device to UI: {deviceInfo.Name} ({deviceInfo.MacAddress})");
                    
                    var deviceItem = new BluetoothDeviceItem
                    {
                        Name = deviceInfo.Name,
                        MacAddress = $"MAC Address: {deviceInfo.MacAddress}", // Format dengan prefix "MAC Address:"
                        IsSelected = deviceInfo.Name == _savedPrinterName
                    };

                    if (deviceItem.IsSelected)
                    {
                        SelectedDevice = deviceItem;
                        System.Diagnostics.Debug.WriteLine($"Selected saved device: {deviceInfo.Name}");
                    }

                    Devices.Add(deviceItem);
                }

                System.Diagnostics.Debug.WriteLine($"=== UI UPDATE COMPLETE ===");
                System.Diagnostics.Debug.WriteLine($"Total devices in collection: {Devices.Count}");
                System.Diagnostics.Debug.WriteLine($"IsRefreshing: {IsRefreshing}");
                
                // Force property change notifications
                OnPropertyChanged(nameof(Devices));
                
                await ShowToast($"Found {Devices.Count} Bluetooth devices");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadBluetoothDevices error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await ShowToast($"Error loading devices: {ex.Message}");
            });
        }
        finally
        {
            // Always reset loading state on main thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsRefreshing = false;
                
                // Show device list
                var scrollView = this.FindByName<ScrollView>("DeviceListScrollView");
                if (scrollView != null)
                    scrollView.IsVisible = true;
                
                System.Diagnostics.Debug.WriteLine($"Loading finished. IsRefreshing = {IsRefreshing}");
                System.Diagnostics.Debug.WriteLine($"ScrollView visible = {scrollView?.IsVisible}");
            });
        }
    }

    private async Task RefreshDevices()
    {
        if (IsRefreshing) return;
        
        // Animation for refresh icon
        var refreshImage = this.FindByName<Image>("RefreshImage");
        if (refreshImage != null)
        {
            await refreshImage.RotateTo(360, 500);
            refreshImage.Rotation = 0;
        }

        LoadBluetoothDevices(); // Call async void method directly instead of await
    }

    private void OnDeviceTapped(object sender, TappedEventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is BluetoothDeviceItem device)
        {
            // Deselect all
            foreach (var d in Devices)
                d.IsSelected = false;

            // Select tapped device
            device.IsSelected = true;
            SelectedDevice = device;
            
            System.Diagnostics.Debug.WriteLine($"Device selected: {device.Name}");
        }
    }

    private async void SaveButton_Clicked(object sender, EventArgs e)
    {
        // Ensure code page 437 encoding is available
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        
        try
        {
            System.Diagnostics.Debug.WriteLine("=== SAVE BUTTON CLICKED ===");
            
            if (SelectedDevice == null)
            {
                await ShowToast("Please select a printer");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"Testing connection to: {SelectedDevice.Name}");

            // Disable button during test
            var button = sender as Button;
            if (button != null)
            {
                button.IsEnabled = false;
                button.Text = "Testing...";
            }

            try
            {
                // Test print dengan format struk ESC/POS
                await PrintReceiptAsync(SelectedDevice.Name);
                // Save as default printer
                Preferences.Set("default_printer", SelectedDevice.Name);
                _savedPrinterName = SelectedDevice.Name;
                System.Diagnostics.Debug.WriteLine($"Printer saved as default: {SelectedDevice.Name}");
                await ShowToast($"Printer '{SelectedDevice.Name}' saved as default");
                // Navigate back
                await Navigation.PopAsync();
            }
            catch (Exception printEx)
            {
                System.Diagnostics.Debug.WriteLine($"Print test failed: {printEx.Message}");
                await ShowToast($"Connection failed: {printEx.Message}");
            }
            finally
            {
                // Re-enable button
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Text = "Save Configuration";
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SaveButton_Clicked error: {ex.Message}");
            await ShowToast($"Error saving configuration: {ex.Message}");
        }
    }

    // ESC/POS receipt printing method
    private async Task PrintReceiptAsync(string printerName)
    {
        const int CHAR_PER_LINE = 32; // Sesuaikan dengan printer Anda
        string garis = new string('-', CHAR_PER_LINE) + "\r\n";
        var sb = new System.Text.StringBuilder();

        // HEADER
        sb.Append(
            "\x1B\x61\x01" +          // Center align
            "\x1B\x21\x08" +          // Font A Bold
            "STRUK TEST PRINTER\r\n" +
            "\x1B\x21\x00" +          // Reset font normal
            $"No: 12345 | TEST\r\n" +
            $"2024-06-01. Konsumen\r\n\r\n" +
            $"No. Antrian: 1\r\n" +
            garis
        );

        // MODE PESANAN - Besar dan Center
        sb.Append(
            "\x1B\x61\x01" +    // Center
            "\x1D\x21\x11" +    // Font double width & height
            $"DINE-IN\r\n" +
            "\x1D\x21\x00" +    // Reset font
            "\x1B\x61\x00" +    // Align kiri
            garis
        );

        // RINCIAN - Bold normal
        sb.Append(
            "\x1B\x21\x08" +
            "RINCIAN\r\n" +
            "\x1B\x21\x00"
        );

        // DETAIL PRODUK (contoh)
        sb.AppendLine("Produk A");
        sb.AppendLine(AlignRight("2x 10000", "20000", CHAR_PER_LINE));
        sb.AppendLine();
        sb.AppendLine("Produk B");
        sb.AppendLine(AlignRight("1x 5000", "5000", CHAR_PER_LINE));
        sb.AppendLine();

        // SUBTOTAL & LAINNYA
        sb.Append(
            "\x1B\x21\x08" + "SUBTOTAL\r\n" + "\x1B\x21\x00" +
            AlignRight("Produk", $"25000", CHAR_PER_LINE) + "\r\n" +
            AlignRight("Take Away", $"0", CHAR_PER_LINE) + "\r\n" +
            AlignRight("Service Charge", $"0", CHAR_PER_LINE) + "\r\n" +
            AlignRight("PPN Resto", $"0", CHAR_PER_LINE) + "\r\n" +
            AlignRight("DISKON", $"0", CHAR_PER_LINE) + "\r\n" +
            AlignRight("Promo", $"0", CHAR_PER_LINE) + "\r\n\r\n" +
            garis
        );

        // TOTAL HARGA - besar dan center
        sb.Append(
            "\x1B\x21\x08" + "TOTAL HARGA\r\n" +
            "\x1B\x61\x01" +
            "\x1D\x21\x11" +
            $"25000\r\n" +
            "\x1D\x21\x00" +
            "\x1B\x61\x00" +
            garis
        );

        // CASH & KEMBALIAN
        sb.Append(
            AlignRight("Cash", $"30000", CHAR_PER_LINE) + "\r\n" +
            AlignRight("Kembalian", $"5000", CHAR_PER_LINE) + "\r\n" +
            $"Kasir: Admin\r\n\r\n"
        );

        // FOOTER - Centered thanks message
        sb.Append(
            "\x1B\x61\x01" +
            "\x1B\x21\x08" + "Terimakasih Atas\r\nPembayaran Anda\r\n" +
            "\x1B\x21\x00" +
            DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss") +
            "\r\n"
        );

        // FEED & CUT
        sb.Append("\x1B\x64\x02");     // Feed 2 lines
        sb.Append("\x1D\x56\x42\x00"); // Partial cut

        // Buka laci (jika perlu)
        sb.Append("\x1B\x70\x00\x19\xFA");

        string struk = sb.ToString();
        byte[] buffer = System.Text.Encoding.GetEncoding(437).GetBytes(struk);

        // Kirim ke printer
        string struk437 = System.Text.Encoding.GetEncoding(437).GetString(buffer);
        await _bluetoothService.Print(printerName, struk437);
    }

    // Helper untuk align kanan
    private string AlignRight(string label, string value, int? totalLength = null)
    {
        int len = totalLength ?? 32;
        int spacing = len - (label.Length + value.Length);
        spacing = spacing < 0 ? 0 : spacing;
        return label + new string(' ', spacing) + value;
    }

    private async Task ShowToast(string message)
    {
        try
        {
            var toast = Toast.Make(message, ToastDuration.Long, 14);
            await toast.Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Toast error: {ex.Message}");
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

// Model untuk Bluetooth device
public class BluetoothDeviceItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public string Name { get; set; }
    public string MacAddress { get; set; }
    
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}