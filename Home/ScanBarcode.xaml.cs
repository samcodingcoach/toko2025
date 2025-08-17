using CommunityToolkit.Maui.Views;
using ZXing.Net.Maui;

namespace Toko2025.Home;

public partial class ScanBarcode : Popup
{
    string result_barcode = string.Empty;
    
    // Event untuk mengirim hasil barcode ke parent page
    public event EventHandler<string> BarcodeScanned;
    
    public ScanBarcode()
	{
		InitializeComponent();
        barcodeReader.CameraLocation = CameraLocation.Rear;
        barcodeReader.IsTorchOn = false;

        barcodeReader.Options = new ZXing.Net.Maui.BarcodeReaderOptions
        {
            Formats = ZXing.Net.Maui.BarcodeFormats.OneDimensional,   // atau pecahan seperti BarcodeFormat.Code128 | BarcodeFormat.Ean13
            AutoRotate = true,
            Multiple = false,
            TryHarder = true
        };
    }

    private void B_Scan_Clicked(object sender, EventArgs e)
    {
        barcodeReader.IsDetecting = true;
        System.Diagnostics.Debug.WriteLine("Scanning started");

        Layout_Camera.IsVisible = true; // Tampilkan kamera   
        B_Scan.IsVisible = false; // Sembunyikan tombol scan
        Layout_Info.IsVisible = false; // Sembunyikan info
        System.Diagnostics.Debug.WriteLine("Camera layout is now visible");
    }

    private async void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        // Ambil hasil pertama
        var first = e.Results?.FirstOrDefault();
        if (first is null) return;

        await Dispatcher.DispatchAsync(async () =>
        {
            string format = first.Format.ToString();
            string numbervalue = first.Value;   
            result_barcode = numbervalue;

            System.Diagnostics.Debug.WriteLine($"Barcode detected: {numbervalue} with format {format}");
            barcodeReader.IsDetecting = false; // berhenti setelah scan

            // Kirim hasil barcode ke parent page melalui event
            BarcodeScanned?.Invoke(this, result_barcode);
            
            // Tutup popup
            await ClosePopupAsync();
        });
    }

    private async Task ClosePopupAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== CLOSING POPUP ===");
            
            // Stop camera detection
            barcodeReader.IsDetecting = false;
            
            // Close popup
            await this.CloseAsync();
            
            System.Diagnostics.Debug.WriteLine("Popup closed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error closing popup: {ex.Message}");
        }
    }

    private async void Back_Tapped(object sender, TappedEventArgs e)
    {
        if (sender is Label image)
        {
            await image.FadeTo(0.3, 100); // Turunkan opacity ke 0.3 dalam 100ms
            await image.FadeTo(1, 200);   // Kembalikan opacity ke 1 dalam 200ms
        }

        try
        {
            System.Diagnostics.Debug.WriteLine("=== MANUAL CLOSE POPUP ===");
            
            // Stop camera detection
            barcodeReader.IsDetecting = false;
            
            // Close popup tanpa mengirim hasil barcode
            await this.CloseAsync();
            
            System.Diagnostics.Debug.WriteLine("Popup closed manually");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Manual close popup error: {ex.Message}");
        }
    }

    private async void ImageBack_Clicked(object sender, EventArgs e)
    {
        if (sender is Label image)
        {
            await image.FadeTo(0.3, 100); // Turunkan opacity ke 0.3 dalam 100ms
            await image.FadeTo(1, 200);   // Kembalikan opacity ke 1 dalam 200ms
        }

        try
        {
            System.Diagnostics.Debug.WriteLine("=== MANUAL CLOSE POPUP ===");

            // Stop camera detection
            barcodeReader.IsDetecting = false;

            // Close popup tanpa mengirim hasil barcode
            await this.CloseAsync();

            System.Diagnostics.Debug.WriteLine("Popup closed manually");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Manual close popup error: {ex.Message}");
        }
    }
}