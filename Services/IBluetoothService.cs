using System.Collections.Generic;
using System.Threading.Tasks;

namespace Toko2025.Services
{
    public interface IBluetoothService
    {
        IList<BluetoothDeviceInfo> GetDeviceList();
        Task Print(string deviceName, string text);
    }
    
    // Model untuk menyimpan informasi device termasuk MAC address
    public class BluetoothDeviceInfo
    {
        public string Name { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
    }
}