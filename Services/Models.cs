﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;

namespace Toko2025.Services
{
    /// <summary>
    /// Service untuk kompresi gambar menggunakan SkiaSharp
    /// Maksimal ukuran: 1024KB (1MB) sesuai requirement backend
    /// </summary>
    public static class ImageCompressionService
    {
        private const int MAX_FILE_SIZE_BYTES = 1024 * 1024; // 1MB dalam bytes
        private const int MAX_DIMENSION = 1920; // Maksimal lebar/tinggi 1920px
        private const int MIN_QUALITY = 50; // Kualitas minimum
        private const int MAX_QUALITY = 95; // Kualitas maksimum

        /// <summary>
        /// Kompres gambar agar ukurannya tidak melebihi 1MB
        /// </summary>
        /// <param name="originalStream">Stream gambar asli</param>
        /// <param name="fileName">Nama file untuk menentukan format</param>
        /// <returns>Stream gambar yang sudah dikompres</returns>
        public static async Task<(MemoryStream compressedStream, long originalSize, long compressedSize)> CompressImageAsync(Stream originalStream, string fileName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== IMAGE COMPRESSION START ===");
                System.Diagnostics.Debug.WriteLine($"Original file: {fileName}");
                System.Diagnostics.Debug.WriteLine($"Original size: {originalStream.Length:N0} bytes ({originalStream.Length / 1024.0:F1} KB)");

                long originalSize = originalStream.Length;

                // Jika ukuran sudah di bawah 1MB, kembalikan stream asli
                if (originalSize <= MAX_FILE_SIZE_BYTES)
                {
                    var directStream = new MemoryStream();
                    originalStream.Position = 0;
                    await originalStream.CopyToAsync(directStream);
                    directStream.Position = 0;
                    
                    System.Diagnostics.Debug.WriteLine("Image already under 1MB, no compression needed");
                    return (directStream, originalSize, directStream.Length);
                }

                // Load gambar menggunakan SkiaSharp
                originalStream.Position = 0;
                using var originalBitmap = SKBitmap.Decode(originalStream);
                
                if (originalBitmap == null)
                {
                    throw new InvalidOperationException("Failed to decode image");
                }

                System.Diagnostics.Debug.WriteLine($"Original dimensions: {originalBitmap.Width}x{originalBitmap.Height}");

                // Hitung ukuran target berdasarkan rasio aspek
                var (targetWidth, targetHeight) = CalculateTargetDimensions(originalBitmap.Width, originalBitmap.Height);
                
                System.Diagnostics.Debug.WriteLine($"Target dimensions: {targetWidth}x{targetHeight}");

                // Resize gambar jika perlu
                SKBitmap resizedBitmap;
                if (targetWidth != originalBitmap.Width || targetHeight != originalBitmap.Height)
                {
                    resizedBitmap = originalBitmap.Resize(new SKImageInfo(targetWidth, targetHeight), SKFilterQuality.High);
                    if (resizedBitmap == null)
                    {
                        throw new InvalidOperationException("Failed to resize image");
                    }
                }
                else
                {
                    resizedBitmap = originalBitmap.Copy();
                }

                try
                {
                    // Tentukan format berdasarkan ekstensi file
                    var format = GetImageFormat(fileName);
                    System.Diagnostics.Debug.WriteLine($"Output format: {format}");

                    // Kompres dengan kualitas yang bervariasi hingga ukuran <= 1MB
                    var compressedStream = await CompressWithQualityAsync(resizedBitmap, format);
                    
                    System.Diagnostics.Debug.WriteLine($"=== COMPRESSION COMPLETE ===");
                    System.Diagnostics.Debug.WriteLine($"Final size: {compressedStream.Length:N0} bytes ({compressedStream.Length / 1024.0:F1} KB)");
                    System.Diagnostics.Debug.WriteLine($"Compression ratio: {(double)compressedStream.Length / originalSize * 100:F1}%");

                    return (compressedStream, originalSize, compressedStream.Length);
                }
                finally
                {
                    if (resizedBitmap != originalBitmap)
                    {
                        resizedBitmap?.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"=== COMPRESSION ERROR ===");
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Hitung dimensi target dengan mempertahankan aspect ratio
        /// </summary>
        private static (int width, int height) CalculateTargetDimensions(int originalWidth, int originalHeight)
        {
            // Jika kedua dimensi sudah di bawah maksimal, tidak perlu resize
            if (originalWidth <= MAX_DIMENSION && originalHeight <= MAX_DIMENSION)
            {
                return (originalWidth, originalHeight);
            }

            // Hitung scale factor berdasarkan dimensi yang lebih besar
            double scaleFactorWidth = (double)MAX_DIMENSION / originalWidth;
            double scaleFactorHeight = (double)MAX_DIMENSION / originalHeight;
            double scaleFactor = Math.Min(scaleFactorWidth, scaleFactorHeight);

            int targetWidth = (int)(originalWidth * scaleFactor);
            int targetHeight = (int)(originalHeight * scaleFactor);

            // Pastikan dimensi tidak bernilai 0
            targetWidth = Math.Max(1, targetWidth);
            targetHeight = Math.Max(1, targetHeight);

            return (targetWidth, targetHeight);
        }

        /// <summary>
        /// Kompres gambar dengan kualitas yang bervariasi hingga ukuran <= 1MB
        /// </summary>
        private static async Task<MemoryStream> CompressWithQualityAsync(SKBitmap bitmap, SKEncodedImageFormat format)
        {
            for (int quality = MAX_QUALITY; quality >= MIN_QUALITY; quality -= 10)
            {
                var stream = new MemoryStream();
                
                // Encode gambar dengan kualitas tertentu
                using (var image = SKImage.FromBitmap(bitmap))
                using (var data = image.Encode(format, quality))
                {
                    data.SaveTo(stream);
                }

                stream.Position = 0;
                
                System.Diagnostics.Debug.WriteLine($"Quality {quality}%: {stream.Length:N0} bytes ({stream.Length / 1024.0:F1} KB)");

                // Jika ukuran sudah <= 1MB, return stream ini
                if (stream.Length <= MAX_FILE_SIZE_BYTES)
                {
                    return stream;
                }

                // Jika masih terlalu besar, dispose dan coba kualitas lebih rendah
                stream.Dispose();
            }

            // Jika sampai kualitas minimum masih terlalu besar, coba resize lebih kecil lagi
            System.Diagnostics.Debug.WriteLine("Even minimum quality is too large, trying smaller dimensions");
            
            // Reduce dimensions by 75%
            int smallerWidth = (int)(bitmap.Width * 0.75);
            int smallerHeight = (int)(bitmap.Height * 0.75);
            
            using var smallerBitmap = bitmap.Resize(new SKImageInfo(smallerWidth, smallerHeight), SKFilterQuality.Medium);
            if (smallerBitmap != null)
            {
                return await CompressWithQualityAsync(smallerBitmap, format);
            }

            // Last resort: return dengan kualitas minimum
            var finalStream = new MemoryStream();
            using (var image = SKImage.FromBitmap(bitmap))
            using (var data = image.Encode(format, MIN_QUALITY))
            {
                data.SaveTo(finalStream);
            }

            finalStream.Position = 0;
            System.Diagnostics.Debug.WriteLine($"Final attempt: {finalStream.Length:N0} bytes ({finalStream.Length / 1024.0:F1} KB)");
            
            return finalStream;
        }

        /// <summary>
        /// Tentukan format gambar berdasarkan ekstensi file
        /// </summary>
        private static SKEncodedImageFormat GetImageFormat(string fileName)
        {
            var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
            
            return extension switch
            {
                ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
                ".png" => SKEncodedImageFormat.Png,
                ".webp" => SKEncodedImageFormat.Webp,
                _ => SKEncodedImageFormat.Jpeg // Default ke JPEG untuk kompresi terbaik
            };
        }

        /// <summary>
        /// Get MIME type berdasarkan format SkiaSharp
        /// </summary>
        public static string GetMimeType(SKEncodedImageFormat format)
        {
            return format switch
            {
                SKEncodedImageFormat.Jpeg => "image/jpeg",
                SKEncodedImageFormat.Png => "image/png",
                SKEncodedImageFormat.Webp => "image/webp",
                _ => "image/jpeg"
            };
        }

        /// <summary>
        /// Get MIME type berdasarkan nama file
        /// </summary>
        public static string GetMimeType(string fileName)
        {
            var format = GetImageFormat(fileName);
            return GetMimeType(format);
        }
    }

    [Table("Kategori")]
    public class Kategori
    {
        [PrimaryKey]
        [Column("id_kategori")]
        public int id_kategori { get; set; }
        
        [Column("nama_kategori")]
        public string nama_kategori { get; set; }
        
        [Column("jumlah")]
        public int jumlah { get; set; }
    }

    [Table("Merk")]
    public class Merk
    {
        [PrimaryKey]
        [Column("id_merk")]
        public int id_merk { get; set; }
        
        [Column("nama_merk")]
        public string nama_merk { get; set; }
        
        [Column("aktif")]
        public int aktif { get; set; } = 1; // Default to 1 (active) if not provided by API
        
        [Column("jumlah")]
        public int jumlah { get; set; }
    }

    [Table("SyncStatus")]
    public class SyncStatus
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        
        [Column("table_name")]
        public string TableName { get; set; }
        
        [Column("last_sync")]
        public DateTime LastSync { get; set; }
        
        [Column("is_initialized")]
        public bool IsInitialized { get; set; }
    }

    // Product model for API response
    public class Product
    {
        public int id_barang { get; set; }
        public int id_merk { get; set; }
        public int id_kategori { get; set; }
        public string barcode1 { get; set; } = string.Empty;
        public string barcode2 { get; set; } = string.Empty;
        public string nama_barang { get; set; } = string.Empty;
        public string gambar1 { get; set; } = string.Empty;
        public string gambar2 { get; set; } = string.Empty;
        public int harga_jual { get; set; }
        public int harga_jual_member { get; set; }
        public int stok_aktif { get; set; }
        public string nama_kategori { get; set; } = string.Empty;
        public string nama_merk { get; set; } = string.Empty;
        public string nama_satuan { get; set; } = string.Empty;
        public string simbol { get; set; } = string.Empty;
        
        // Helper properties untuk UI
        public string FormattedPrice => $"Rp {harga_jual:N0}";
        public string FormattedMemberPrice => $"Rp {harga_jual_member:N0}";
        public string FormattedStock => $"{stok_aktif}";
        public string ImageUrl => !string.IsNullOrEmpty(gambar1) ? $"{App.IP}/images/{gambar1}" : string.Empty;
    }

    // Models untuk Penjualan (Cart/Sales) - DIPERBAIKI berdasarkan backend
    public class PenjualanRequest
    {
        public int id_user { get; set; }
        public string faktur { get; set; } = string.Empty;
    }

    public class PenjualanResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
        public PenjualanData data { get; set; } = new PenjualanData();
    }

    public class PenjualanData
    {
        // Response dari backend: { id_penjualan: result.insertId, faktur: faktur, id_user: id_user }
        public int id_penjualan { get; set; }  // Backend mengembalikan id_penjualan langsung
        public string faktur { get; set; } = string.Empty;
        public string id_user { get; set; } = string.Empty;
    }

    public class PenjualanDetailRequest
    {
        public int id_penjualan { get; set; }
        public int id_barang { get; set; }
        public int jumlah_jual { get; set; }
        public int diskon { get; set; } = 0;
        public int harga_jual { get; set; }
    }

    public class PenjualanDetailResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
        public string action { get; set; } = string.Empty; // Backend mengembalikan "insert" atau "update"
        public PenjualanDetailData data { get; set; } = new PenjualanDetailData();
    }

    public class PenjualanDetailData
    {
        // Response dari backend bervariasi tergantung insert/update
        public int id { get; set; }  // ID dari detail penjualan
        public int id_penjualan { get; set; }
        public int id_barang { get; set; }
        public double jumlah_jual { get; set; }  // Backend menggunakan double
        public double harga_jual { get; set; }   // Backend menggunakan double
        public double diskon { get; set; }       // Backend menggunakan double
        public double subtotal { get; set; }     // Backend menggunakan double
        
        // Properties tambahan untuk update response
        public double jumlah_jual_sebelum { get; set; }
        public double jumlah_jual_baru { get; set; }
        public double jumlah_jual_sesudah { get; set; }
    }

    // Models untuk Last Faktur API
    public class LastFakturResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
        public LastFakturData data { get; set; } = new LastFakturData();
    }

    public class LastFakturData
    {
        public string last_faktur { get; set; } = string.Empty;
        public string next_suggested { get; set; } = string.Empty;
    }

    // Models untuk Cart API - BARU
    public class CartResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
        public CartData data { get; set; } = new CartData();
    }

    public class CartData
    {
        public string id_penjualan { get; set; } = string.Empty;
        public List<CartItem> items { get; set; } = new List<CartItem>();
        public CartSummary summary { get; set; } = new CartSummary();
    }

    public class CartItem
    {
        public int id_detail_penjualan { get; set; }
        public int id_penjualan { get; set; }
        public int id_barang { get; set; }
        public string nama_barang { get; set; } = string.Empty;
        public string gambar1 { get; set; } = string.Empty;
        public string gambar2 { get; set; } = string.Empty;
        public double jumlah_jual { get; set; }
        public double harga_jual { get; set; }
        public double diskon { get; set; }
        public double subtotal { get; set; }
        public string nama_merk { get; set; } = string.Empty;

        // Helper properties untuk UI
        public string FormattedPrice => $"Rp {harga_jual:N0}";
        public string FormattedSubtotal => $"Rp {subtotal:N0}";
        public string ImageUrl => !string.IsNullOrEmpty(gambar1) ? $"{App.IP}/images/{gambar1}" : string.Empty;
        public int QuantityInt => (int)jumlah_jual;
    }

    public class CartSummary
    {
        public int total_items { get; set; }
        public double total_qty { get; set; }
        public double total_amount { get; set; }
        public double total_diskon { get; set; }  // ← NEW PROPERTY

        // Helper properties untuk UI
        public string FormattedTotalAmount => $"Rp {total_amount:N0}";
        public string FormattedTotalDiskon => $"Rp {total_diskon:N0}";  // ← NEW
        public string FormattedFinalTotal => $"Rp {(total_amount - total_diskon):N0}";  // ← NEW
        public string CartTitle => $"Shopping Bag ({total_items})";
    }

    // Models untuk Member Search API
    public class MemberSearchResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
        public MemberSearchData data { get; set; } = new MemberSearchData();
    }

    public class MemberSearchData
    {
        public string hp { get; set; } = string.Empty;
        public bool member_found { get; set; }
        public MemberInfo member { get; set; } = new MemberInfo();
        public PenjualanUpdateInfo penjualan_update { get; set; } = new PenjualanUpdateInfo();
    }

    public class MemberInfo
    {
        public int id_member { get; set; }
        public string nama_member { get; set; } = string.Empty;
    }

    public class PenjualanUpdateInfo
    {
        public bool success { get; set; }
        public string id_penjualan { get; set; } = string.Empty;
        public int affected_rows { get; set; }
    }

    // Models untuk Checkout API - NEW
    public class CheckoutRequest
    {
        public int id_penjualan { get; set; }
        public int id_pembayaran { get; set; }
        public decimal potongan { get; set; }
        public decimal grand_total { get; set; }
        public int aktif { get; set; } = 0;
        public decimal biaya_lain { get; set; } = 0;
    }

    public class CheckoutResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
        public CheckoutData data { get; set; } = new CheckoutData();
    }

    public class CheckoutData
    {
        public int id_penjualan { get; set; }
        public string faktur { get; set; } = string.Empty;
        public decimal grand_total { get; set; }
        public int id_pembayaran { get; set; }
        public string payment_method { get; set; } = string.Empty;
    }

    // Models untuk QRIS API - NEW
    public class QrisRequest
    {
        public string faktur { get; set; } = string.Empty;
        public decimal gross_amount { get; set; }
        public int id_penjualan { get; set; }
    }

    public class QrisResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
        public QrisData data { get; set; } = new QrisData();
    }

    public class QrisData
    {
        public string faktur { get; set; } = string.Empty;
        public string order_id { get; set; } = string.Empty;
        public string transaction_id { get; set; } = string.Empty;
        public string transaction_status { get; set; } = string.Empty;
        public string qr_string { get; set; } = string.Empty;
        public string gross_amount { get; set; } = string.Empty;
        public string id_penjualan { get; set; } = string.Empty;
        public List<QrisAction> actions { get; set; } = new List<QrisAction>();
    }

    public class QrisAction
    {
        public string name { get; set; } = string.Empty;
        public string method { get; set; } = string.Empty;
        public string url { get; set; } = string.Empty;
    }

    // Models untuk Midtrans Status Check API - NEW
    public class MidtransStatusResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
        public MidtransStatusData data { get; set; } = new MidtransStatusData();
    }

    public class MidtransStatusData
    {
        public string order_id { get; set; } = string.Empty;
        public string transaction_status { get; set; } = string.Empty;
        public string payment_status { get; set; } = string.Empty;
        public string gross_amount { get; set; } = string.Empty;
        public string payment_type { get; set; } = string.Empty;
        public string transaction_time { get; set; } = string.Empty;
        public string fraud_status { get; set; } = string.Empty;
        public string status_code { get; set; } = string.Empty;
        public string status_message { get; set; } = string.Empty;
    }

    // Models untuk Payment Method API - NEW
    public class PaymentMethodResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
        public List<PaymentMethod> data { get; set; } = new List<PaymentMethod>();
    }

    public class PaymentMethod
    {
        public int id_pembayaran { get; set; }
        public string nama_pembayaran { get; set; } = string.Empty;
        public string nomor_pembayaran { get; set; } = string.Empty;
        public decimal biaya_pembayaran { get; set; }
        public int aktif { get; set; }

        // Helper property for UI display
        public string DisplayName => nama_pembayaran;
    }

    // Models untuk Order History API - NEW
    public class HistoryResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
        public HistorySummary summary { get; set; } = new HistorySummary();
        public List<HistoryItem> data { get; set; } = new List<HistoryItem>();
    }

    public class HistorySummary
    {
        public int count { get; set; }
        public int grand_total { get; set; }
        
        // Helper properties untuk UI
        public string FormattedGrandTotal => $"Rp {grand_total:N0}";
        public string CountText => $"{count} Transaction{(count > 1 ? "s" : "")}";
    }

    public class HistoryItem
    {
        public int id_penjualan { get; set; }
        public string faktur { get; set; } = string.Empty;
        public string tanggal { get; set; } = string.Empty;
        public string nama_member { get; set; } = string.Empty;
        public int uang_awal { get; set; }
        public string nama_pembayaran { get; set; } = string.Empty;
        public int grand_total { get; set; }
        public int hutang { get; set; }
        public int? qris_status { get; set; }
        public int aktif { get; set; }
        public int id_user { get; set; }
        public string jam { get; set; } = string.Empty;
        
        // Helper properties untuk UI
        public string FormattedGrandTotal => hutang == 1 
            ? $"-Rp {grand_total:N0}" 
            : $"Rp {grand_total:N0}";
            
        public Color GrandTotalColor => hutang == 1 ? Colors.Red : Color.FromArgb("#4A90E2");
        
        public string FormattedUangAwal => $"Rp {uang_awal:N0}";
        public string TransactionTime => $"Time: {jam}";
        public string MemberInfo => $"{nama_member}";
        public string PaymentIcon => nama_pembayaran?.ToLower() switch
        {
            "qris" => "qr.png",
            "tunai" => "tunai.png",
            _ => "tunai.png"
        };
    }

    // Models untuk History Cart Detail API - NEW
    public class HistoryCartResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
        public HistoryCartData data { get; set; } = new HistoryCartData();
    }

    public class HistoryCartData
    {
        public string faktur { get; set; } = string.Empty;
        public int hutang { get; set; } = 0; // NEW: Tambahkan informasi hutang
        public List<HistoryCartItem> items { get; set; } = new List<HistoryCartItem>();
        public HistoryCartSummary summary { get; set; } = new HistoryCartSummary();
        
        // Helper property for UI
        public bool IsDebt => hutang == 1;
        public string DebtStatusText => hutang == 1 ? "DEBT TRANSACTION" : "";
        public Color DebtStatusColor => hutang == 1 ? Colors.Red : Colors.Transparent;
    }

    public class HistoryCartItem
    {
        public string nama_barang { get; set; } = string.Empty;
        public string gambar1 { get; set; } = string.Empty; // NEW: Add image field
        public string nama_merk { get; set; } = string.Empty; // NEW: Add brand field
        public double jumlah_jual { get; set; }
        public double harga_jual { get; set; }
        public double diskon { get; set; }
        public double subtotal { get; set; }

        // Property untuk menentukan index row (akan diset dari code-behind)
        public int RowIndex { get; set; }

        // Helper properties untuk UI
        public string FormattedPrice => $"Rp {harga_jual:N0}";
        public string FormattedDiskon => $"Rp {diskon:N0}";
        public string FormattedSubtotal => $"Rp {subtotal:N0}";
        public string QuantityDisplay => $"{jumlah_jual:N0}";
        public string PriceQuantityDisplay => $"{FormattedPrice} X {QuantityDisplay}";
        public string DiskonDisplay => diskon > 0 ? $"Discount: {FormattedDiskon}" : "";
        
        // NEW: Add ImageUrl helper property
        public string ImageUrl => !string.IsNullOrEmpty(gambar1) ? $"{App.IP}/images/{gambar1}" : string.Empty;
        
        // Property untuk alternating background color
        public Color RowBackgroundColor => RowIndex % 2 == 0 ? Color.FromArgb("#f6f6f6") : Colors.White;
    }

    public class HistoryCartSummary
    {
        public int total_items { get; set; }
        public double total_qty { get; set; }
        public double total_amount { get; set; }
        public double total_diskon { get; set; }

        // Helper properties untuk UI
        public string FormattedTotalQty => $"{total_qty:N0}";
        public string FormattedTotalAmount => $"Rp {total_amount:N0}";
        public string FormattedTotalDiskon => $"Rp {total_diskon:N0}";
        public string TotalItemsDisplay => $"{total_items}";
    }

    // Models untuk Transfer Bank API - NEW
    public class TransferBankResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
        public TransferBankData data { get; set; } = new TransferBankData();
    }

    public class TransferBankData
    {
        public int id_transfer { get; set; }
        public int id_penjualan { get; set; }
        public string nama_pemilik { get; set; } = string.Empty;
        public string nama_bank { get; set; } = string.Empty;
        public string url_image { get; set; } = string.Empty;
        public string full_image_url { get; set; } = string.Empty;
    }

    // Value Converter untuk alternating background colors
    public class AlternatingColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int index)
            {
                // Row 0 (1st) = #f6f6f6, Row 1 (2nd) = White, Row 2 (3rd) = #f6f6f6, etc.
                return index % 2 == 0 ? Color.FromArgb("#f6f6f6") : Colors.White;
            }
            return Color.FromArgb("#f6f6f6"); // Default fallback
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Value Converter untuk count to boolean (untuk visibility)
    public class CountToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int count)
            {
                return count > 0;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Value Converter untuk inverting boolean (untuk visibility)
    public class InvertedBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return true; // Default to true if not boolean
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }

    // Models untuk Debt API - NEW
    public class DebtRequest
    {
        public int id_penjualan { get; set; }
        public int id_member { get; set; }
        public decimal biaya_lain { get; set; } = 0;
        public decimal potongan { get; set; } = 0;
        public int id_pembayaran { get; set; } = 1; // Default payment method
        public int aktif { get; set; } = 2; // Debt status
        public decimal grand_total { get; set; }
    }

    public class DebtResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
        public int affectedRows { get; set; }
    }
    
    // Models untuk Bayar Hutang API - NEW
    public class BayarHutangResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
        public int affectedRows { get; set; }
    }

    // Models untuk Profile Usaha API - NEW
    public class ProfileUsahaResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
        public ProfileUsahaData data { get; set; } = new ProfileUsahaData();
    }

    public class ProfileUsahaData
    {
        public string nama_usaha { get; set; } = string.Empty;
        public string alamat { get; set; } = string.Empty;
        public string foto_usaha { get; set; } = string.Empty;
        public string tgl_exp { get; set; } = string.Empty;
        
        // Helper property for image URL
        public string ImageUrl => !string.IsNullOrEmpty(foto_usaha) ? $"{App.IP}{foto_usaha}" : string.Empty;
    }

    
}
