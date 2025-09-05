# Dynamic IP Configuration and Connection Detection

## Overview
The application now supports dynamic IP configuration with automatic connection detection. The IP address is no longer hardcoded and can be changed through the Connection interface.

## Features

### 1. Dynamic IP Configuration
- **Local Network**: For local IP addresses (e.g., 192.168.1.2:3000)
- **Online Network**: For domain names (e.g., domain.com)
- **Preferences Storage**: Configuration is saved and persists between app sessions
- **Connection Testing**: Validates connection before applying new settings

### 2. Connection Detection
- **Automatic Monitoring**: Detects when connection is lost
- **Graceful Handling**: Shows Connection page when disconnected
- **API-based Testing**: Uses actual API endpoints for more reliable detection

## How to Use

### Changing IP Configuration
1. Open the **Connection** page
2. Enter your **IP Local Network** (e.g., `192.168.1.2:3000`)
3. Enter your **Domain Online Network** (e.g., `domain.com`)
4. Select the network type from the picker:
   - **Local Network**: Uses the local IP
   - **Online Network**: Uses the domain
5. Click **Apply Connection**
6. The app will test the connection and save settings if successful

### Connection Page Features
- **Real-time Validation**: Tests connection before applying
- **Auto-formatting**: Adds `http://` for local IPs and `https://` for domains
- **Error Handling**: Shows clear error messages if connection fails
- **Navigation**: Returns to appropriate page after successful configuration

## Technical Implementation

### Files Modified
- `App.xaml.cs`: Dynamic IP loading and connection monitoring
- `Connection.xaml`: UI for IP configuration
- `Connection.xaml.cs`: Logic for IP testing and saving
- `Services/ConnectionService.cs`: Utility service for connection management
- `Login.xaml.cs`: Example integration of connection checking

### Key Classes and Methods

#### App.xaml.cs
```csharp
// Dynamic IP loading
public static string IP { get; set; } = LoadIPFromPreferences();

// Connection monitoring
public static async Task MonitorConnection()
public static async Task<bool> ValidateIPConnection()
public static void UpdateIPConfiguration(string newIP)
```

#### ConnectionService.cs
```csharp
// Check connection and handle disconnection
public static async Task<bool> CheckConnectionAndHandle()

// Test connection without handling
public static async Task<bool> TestConnection()

// Update IP and test
public static async Task<bool> UpdateAndTestIP(string newIP)

// Get saved configuration
public static (string LocalIP, string OnlineIP, string NetworkType) GetSavedConfiguration()
```

#### Connection.xaml.cs
```csharp
// Load saved configuration from Preferences
private void LoadSavedConfiguration()

// Apply new configuration with validation
private async void B_Apply_Clicked(object sender, EventArgs e)

// Navigate back to appropriate page
private async Task NavigateBack()
```

## Usage Examples

### Basic Connection Check
```csharp
// Simple connection test
bool isConnected = await ConnectionService.TestConnection();

// Check connection and auto-handle disconnection
bool isConnected = await ConnectionService.CheckConnectionAndHandle();
```

### Integration in Pages
```csharp
private async void SomeButton_Clicked(object sender, EventArgs e)
{
    // Validate connection before proceeding
    bool isConnected = await ConnectionService.CheckConnectionAndHandle();
    if (!isConnected)
    {
        return; // ConnectionService handles showing Connection page
    }
    
    // Proceed with API calls...
}
```

### Getting Current Configuration
```csharp
// Get current IP and status
string connectionInfo = ConnectionService.GetConnectionInfo();

// Get saved configuration
var (localIP, onlineIP, networkType) = ConnectionService.GetSavedConfiguration();
```

## Configuration Storage
All settings are stored in `Microsoft.Maui.Essentials.Preferences`:

- **LocalIP**: Local network IP address
- **OnlineIP**: Online domain address  
- **NetworkType**: Selected network type ("Local Network" or "Online Network")
- **SelectedIP**: Currently active IP address

## Error Handling
The system includes comprehensive error handling:

- **Connection Failures**: Shows user-friendly error messages
- **Invalid URLs**: Automatic URL formatting and validation
- **Network Issues**: Graceful fallback to offline mode
- **Configuration Errors**: Safe defaults and error recovery

## Automatic Features
- **URL Formatting**: Automatically adds protocol prefixes
- **Connection Monitoring**: Periodic background checking
- **Fallback Handling**: Graceful degradation when offline
- **Preference Persistence**: Settings survive app restarts

This implementation provides a robust, user-friendly way to manage dynamic IP configuration while maintaining reliable connection detection throughout the application.