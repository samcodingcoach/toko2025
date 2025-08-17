using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.Bluetooth;
using Java.Util;
using Toko2025.Services;

namespace Toko2025.Platforms.Android
{
    public class AndroidBluetoothService : IBluetoothService
    {
        private static readonly UUID SPP_UUID = UUID.FromString("00001101-0000-1000-8000-00805f9b34fb");

        public IList<BluetoothDeviceInfo> GetDeviceList()
        {
            try
            {
                Debug.WriteLine("=== GETTING BLUETOOTH DEVICE LIST ===");
                
                var bluetoothAdapter = BluetoothAdapter.DefaultAdapter;
                
                if (bluetoothAdapter == null)
                {
                    Debug.WriteLine("Bluetooth adapter not available on this device");
                    return new List<BluetoothDeviceInfo>();
                }

                if (!bluetoothAdapter.IsEnabled)
                {
                    Debug.WriteLine("Bluetooth is not enabled");
                    return new List<BluetoothDeviceInfo>();
                }

                var bondedDevices = bluetoothAdapter.BondedDevices;
                
                if (bondedDevices == null || bondedDevices.Count == 0)
                {
                    Debug.WriteLine("No bonded/paired devices found");
                    return new List<BluetoothDeviceInfo>();
                }

                var deviceList = new List<BluetoothDeviceInfo>();
                
                foreach (var device in bondedDevices)
                {
                    if (device?.Name != null)
                    {
                        // Create BluetoothDeviceInfo with both name and MAC address
                        var deviceInfo = new BluetoothDeviceInfo
                        {
                            Name = device.Name,
                            MacAddress = device.Address ?? "Unknown"
                        };
                        
                        deviceList.Add(deviceInfo);
                        Debug.WriteLine($"Found bonded device: {device.Name} ({device.Address})");
                    }
                }

                Debug.WriteLine($"Total devices found: {deviceList.Count}");
                return deviceList;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting device list: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return new List<BluetoothDeviceInfo>();
            }
        }

        public async Task Print(string deviceName, string text)
        {
            Debug.WriteLine($"=== STARTING PRINT TO {deviceName} ===");
            
            var bluetoothAdapter = BluetoothAdapter.DefaultAdapter;
            
            if (bluetoothAdapter == null)
            {
                throw new Exception("Bluetooth adapter not available");
            }

            if (!bluetoothAdapter.IsEnabled)
            {
                throw new Exception("Bluetooth is not enabled");
            }

            BluetoothDevice device = bluetoothAdapter.BondedDevices?
                .FirstOrDefault(bd => bd?.Name == deviceName);

            if (device == null)
            {
                Debug.WriteLine($"Device {deviceName} not found in bonded devices");
                throw new Exception($"Bluetooth device '{deviceName}' not found or not paired");
            }

            Debug.WriteLine($"Found device: {device.Name} ({device.Address})");

            BluetoothSocket bluetoothSocket = null;
            try
            {
                Debug.WriteLine("Creating RFCOMM socket...");
                
                // Give time for any previous connections to close
                await Task.Delay(1000);
                
                bluetoothSocket = device.CreateRfcommSocketToServiceRecord(SPP_UUID);
                
                if (bluetoothSocket == null)
                {
                    throw new Exception("Failed to create Bluetooth socket");
                }

                Debug.WriteLine("Connecting to device...");
                await bluetoothSocket.ConnectAsync();
                
                if (!bluetoothSocket.IsConnected)
                {
                    throw new Exception("Failed to connect to device");
                }

                Debug.WriteLine("Connected successfully, preparing to send data...");
                
                // Convert text to bytes using Code Page 437 (printer charset)
                byte[] buffer = Encoding.GetEncoding(437).GetBytes(text);
                
                Debug.WriteLine($"Sending {buffer.Length} bytes to printer...");
                
                // Send data in chunks to prevent buffer overflow
                const int chunkSize = 512;
                for (int i = 0; i < buffer.Length; i += chunkSize)
                {
                    int size = Math.Min(chunkSize, buffer.Length - i);
                    
                    await bluetoothSocket.OutputStream.WriteAsync(buffer, i, size);
                    await Task.Delay(50); // Small delay between chunks
                    
                    Debug.WriteLine($"Sent chunk {i / chunkSize + 1}, bytes {i}-{i + size - 1}");
                }
                
                await bluetoothSocket.OutputStream.FlushAsync();
                
                // Give printer time to process
                await Task.Delay(500);
                
                Debug.WriteLine("Print completed successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Print error: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw new Exception($"Failed to print: {ex.Message}");
            }
            finally
            {
                try
                {
                    bluetoothSocket?.Close();
                    Debug.WriteLine("Bluetooth socket closed");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error closing socket: {ex.Message}");
                }
            }
        }
    }
}