using System.Collections.ObjectModel;
using System.ComponentModel;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
using Toko2025.Services;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace Toko2025.Cart;

public partial class PreviewStruk : ContentPage, INotifyPropertyChanged
{
    private readonly IBluetoothService _bluetoothService;
    private StrukData _strukData;
    private ObservableCollection<StrukItem> _items;
    
    public StrukData StrukData
    {
        get => _strukData;
        set
        {
            _strukData = value;
            OnPropertyChanged();
            UpdateUI();
        }
    }
    
    public ObservableCollection<StrukItem> Items
    {
        get => _items;
        set
        {
            _items = value;
            OnPropertyChanged();
        }
    }
    
    public PreviewStruk()
    {
        InitializeComponent();
        Items = new ObservableCollection<StrukItem>();
        BindingContext = this;
        
#if ANDROID
        // Initialize Bluetooth Service
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
    }
    
    public PreviewStruk(int penjualanId) : this()
    {
        LoadStrukData(penjualanId);
    }
    
    private async void LoadStrukData(int penjualanId)
    {
        try
        {
            string apiUrl = $"{App.IP}/api/struk/penjualan/{penjualanId}";
            
            System.Diagnostics.Debug.WriteLine($"=== LOADING STRUK DATA ===");
            System.Diagnostics.Debug.WriteLine($"API URL: {apiUrl}");
            System.Diagnostics.Debug.WriteLine($"Penjualan ID: {penjualanId}");
            
            var response = await App.SharedHttpClient.GetAsync(apiUrl);
            var jsonContent = await response.Content.ReadAsStringAsync();
            
            System.Diagnostics.Debug.WriteLine($"Struk API Response Status: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"Struk API Response Content: {jsonContent}");
            
            if (response.IsSuccessStatusCode)
            {
                var apiResponse = JsonConvert.DeserializeObject<StrukApiResponse>(jsonContent);
                
                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    StrukData = apiResponse.Data;
                    
                    // Update UI on main thread
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        UpdateUI();
                    });
                    
                    System.Diagnostics.Debug.WriteLine("Struk data loaded and UI updated successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("API response indicates failure or no data");
                    await DisplayAlert("Error", "Failed to load struk data", "OK");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"API request failed with status: {response.StatusCode}");
                await DisplayAlert("Error", $"Failed to load struk data: {response.StatusCode}", "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadStrukData error: {ex.Message}");
            await DisplayAlert("Error", $"Error loading struk data: {ex.Message}", "OK");
        }
    }
    
    private void UpdateUI()
    {
        if (StrukData == null) return;
        
        // Update header info
        L_NamaUsaha.Text = StrukData.NamaUsaha ?? "";
        L_Alamat.Text = StrukData.Alamat ?? "";
        L_WhatsApp.Text = StrukData.WhatsApp ?? "";
        
        // Update transaction info
        var tanggal = StrukData.Tanggal?.ToString("dd-MM-yyyy HH:mm") ?? "";
        L_NoFaktur.Text = $"No: {StrukData.Faktur}/ {tanggal}";
        L_Member.Text = $"Member: {StrukData.NamaMember}";
        
        // Update items
        Items.Clear();
        if (StrukData.Items != null)
        {
            foreach (var item in StrukData.Items)
            {
                Items.Add(item);
            }
        }
        
        // Calculate subtotal from items
        var subtotalProduk = StrukData.Items?.Sum(x => x.Subtotal) ?? 0;
        L_SubtotalProduk.Text = subtotalProduk.ToString("N0");
        L_BiayaLain.Text = StrukData.BiayaLain.ToString("N0");
        L_Potongan.Text = StrukData.Potongan.ToString("N0");
        
        // Update totals
        L_TotalHarga.Text = StrukData.GrandTotal.ToString("N0");
        
        // Update payment info
        L_Cash.Text = StrukData.CashBayar.ToString("N0");
        L_Kembalian.Text = StrukData.Kembalian.ToString("N0");
        
        // Update footer
        L_Hutang.Text = StrukData.Hutang?.ToUpper() ?? "LUNAS";
        L_Kasir.Text = StrukData.Kasir ?? "";
    }
    
    private async void B_Print_Clicked(object sender, EventArgs e)
    {
        try
        {
            // Check if bluetooth service is available
            if (_bluetoothService == null)
            {
                await DisplayAlert("Error", "Bluetooth service not available", "OK");
                return;
            }
            
            // Get saved printer info
            string defaultPrinter = Preferences.Get("default_printer", string.Empty);
            
            if (string.IsNullOrEmpty(defaultPrinter))
            {
                await DisplayAlert("Error", "No default printer set. Please configure printer in Settings > Bluetooth", "OK");
                return;
            }
            
            // Extract printer name from format "Name|MacAddress" or use as-is for old format
            string savedPrinterName = defaultPrinter.Contains("|") ? defaultPrinter.Split('|')[0] : defaultPrinter;
            
            // Disable button during printing
            B_Print.IsEnabled = false;
            B_Print.Text = "Printing...";
            
            await PrintStrukAsync(savedPrinterName);
            
            // Clear cart preferences after successful print
            var loginData = Preferences.Get("login_data", "");
            if (!string.IsNullOrEmpty(loginData))
            {
                var loginInfo = JsonConvert.DeserializeObject<dynamic>(loginData);
                int id_user = loginInfo.id_user;
                
                Preferences.Remove($"active_penjualan_id_{id_user}");
                Preferences.Remove($"active_faktur_{id_user}");
                
                System.Diagnostics.Debug.WriteLine("Cart preferences cleared after print");
            }
            
            await Toast.Make("Struk berhasil dicetak", ToastDuration.Short).Show();
            
            // Navigate back to home (ListProduct)
            await Task.Delay(1000); // Small delay to show toast
            
            try
            {
                // Try TabPage navigation first
                if (Application.Current?.MainPage is TabPage tabPage)
                {
                    await tabPage.GoToAsync("//ListProduct");
                    System.Diagnostics.Debug.WriteLine("Navigation to ListProduct via TabPage successful");
                }
                else
                {
                    // Fallback: Replace with new TabPage
                    Application.Current.MainPage = new TabPage();
                    System.Diagnostics.Debug.WriteLine("Navigation to ListProduct via MainPage replacement successful");
                }
            }
            catch (Exception navEx)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation error: {navEx.Message}");
                // Final fallback
                Application.Current.MainPage = new TabPage();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Print error: {ex.Message}");
            await DisplayAlert("Error", $"Print failed: {ex.Message}", "OK");
        }
        finally
        {
            // Re-enable button
            B_Print.IsEnabled = true;
            B_Print.Text = "Print Struk";
        }
    }
    
    private async Task PrintStrukAsync(string printerName)
    {
        if (StrukData == null)
        {
            throw new Exception("No struk data available");
        }
        
        const int CHAR_PER_LINE = 32;
        string garis = new string('-', CHAR_PER_LINE) + "\r\n";
        var sb = new System.Text.StringBuilder();
        
        // HEADER
        sb.Append(
            "\x1B\x61\x01" +          // Center align
            "\x1B\x21\x08" +          // Font A Bold
            $"{StrukData.NamaUsaha}\r\n" +
            "\x1B\x21\x00" +          // Reset font normal
            $"{StrukData.Alamat}\r\n" +
            $"{StrukData.WhatsApp}\r\n\r\n" +
            "\x1B\x61\x00" +          // Align left
            garis
        );
        
        // TRANSACTION INFO
        sb.Append(
            $"No: {StrukData.Faktur}/ {StrukData.Tanggal?.ToString("dd-MM-yyyy HH:mm")}\r\n" +
            $"Member: {StrukData.NamaMember}\r\n" +
            garis
        );
        
        // RINCIAN - Bold
        sb.Append(
            "\x1B\x21\x08" +
            "RINCIAN\r\n" +
            "\x1B\x21\x00"
        );
        
        // ITEMS
        if (StrukData.Items != null)
        {
            foreach (var item in StrukData.Items)
            {
                sb.AppendLine(item.NamaBarang);
                sb.AppendLine(AlignRight($"{item.JumlahJual} x {item.HargaJual:N0}", item.Subtotal.ToString("N0"), CHAR_PER_LINE));
                sb.AppendLine();
            }
        }
        
        // SUBTOTAL
        sb.Append(
            "\x1B\x21\x08" + "SUBTOTAL\r\n" + "\x1B\x21\x00" +
            AlignRight("Produk", StrukData.Items?.Sum(x => x.Subtotal).ToString("N0") ?? "0", CHAR_PER_LINE) + "\r\n" +
            AlignRight("Biaya Lain", StrukData.BiayaLain.ToString("N0"), CHAR_PER_LINE) + "\r\n" +
            AlignRight("Potongan", StrukData.Potongan.ToString("N0"), CHAR_PER_LINE) + "\r\n\r\n" +
            garis
        );
        
        // TOTAL HARGA - besar dan center
        sb.Append(
            "\x1B\x21\x08" + "TOTAL HARGA\r\n" +
            "\x1B\x61\x01" +
            "\x1D\x21\x11" +
            $"{StrukData.GrandTotal:N0}\r\n" +
            "\x1D\x21\x00" +
            "\x1B\x61\x00" +
            garis
        );
        
        // PAYMENT INFO
        sb.Append(
            "\x1B\x21\x08" + "TUNAI\r\n" + "\x1B\x21\x00" +
            AlignRight("Cash", StrukData.CashBayar.ToString("N0"), CHAR_PER_LINE) + "\r\n" +
            AlignRight("Kembalian", StrukData.Kembalian.ToString("N0"), CHAR_PER_LINE) + "\r\n" +
            garis
        );
        
        // FOOTER INFO
        sb.Append(
            AlignRight("Hutang", StrukData.Hutang?.ToUpper() ?? "LUNAS", CHAR_PER_LINE) + "\r\n" +
            AlignRight("Kasir", StrukData.Kasir ?? "", CHAR_PER_LINE) + "\r\n\r\n"
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
    private string AlignRight(string label, string value, int totalLength)
    {
        string combined = $"{label} {value}";
        if (combined.Length >= totalLength)
            return combined;
        
        int spaces = totalLength - combined.Length;
        return label + new string(' ', spaces) + value;
    }
    
    public event PropertyChangedEventHandler PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

// Data Models
public class StrukApiResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }
    
    [JsonProperty("message")]
    public string Message { get; set; }
    
    [JsonProperty("data")]
    public StrukData Data { get; set; }
}

public class StrukData
{
    [JsonProperty("nama_usaha")]
    public string NamaUsaha { get; set; }
    
    [JsonProperty("alamat")]
    public string Alamat { get; set; }
    
    [JsonProperty("whatsapp")]
    public string WhatsApp { get; set; }
    
    [JsonProperty("id_penjualan")]
    public int IdPenjualan { get; set; }
    
    [JsonProperty("faktur")]
    public string Faktur { get; set; }
    
    [JsonProperty("tanggal")]
    public DateTime? Tanggal { get; set; }
    
    [JsonProperty("nama_member")]
    public string NamaMember { get; set; }
    
    [JsonProperty("nama_pembayaran")]
    public string NamaPembayaran { get; set; }
    
    [JsonProperty("biaya_lain")]
    public decimal BiayaLain { get; set; }
    
    [JsonProperty("potongan")]
    public decimal Potongan { get; set; }
    
    [JsonProperty("grand_total")]
    public decimal GrandTotal { get; set; }
    
    [JsonProperty("cash_bayar")]
    public decimal CashBayar { get; set; }
    
    [JsonProperty("kembalian")]
    public decimal Kembalian { get; set; }
    
    [JsonProperty("hutang")]
    public string Hutang { get; set; }
    
    [JsonProperty("kasir")]
    public string Kasir { get; set; }
    
    [JsonProperty("items")]
    public List<StrukItem> Items { get; set; }
}

public class StrukItem
{
    [JsonProperty("id_barang")]
    public int IdBarang { get; set; }
    
    [JsonProperty("nama_barang")]
    public string NamaBarang { get; set; }
    
    [JsonProperty("jumlah_jual")]
    public int JumlahJual { get; set; }
    
    [JsonProperty("simbol")]
    public string Simbol { get; set; }
    
    [JsonProperty("harga_jual")]
    public decimal HargaJual { get; set; }
    
    [JsonProperty("subtotal")]
    public decimal Subtotal { get; set; }
    
    [JsonProperty("id_penjualan")]
    public int IdPenjualan { get; set; }
}