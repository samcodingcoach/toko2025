using System.Net.NetworkInformation;
using Toko2025.Services;

namespace Toko2025;

public partial class Connection : ContentPage
{
	public Connection()
	{
		InitializeComponent();
		LoadSavedConfiguration();
	}

    private void LoadSavedConfiguration()
    {
        try
        {
            var (localIP, onlineIP, networkType) = ConnectionService.GetSavedConfiguration();

            // Set the entry values
            Entry_IPLocal.Text = localIP;
            Entry_IPOnline.Text = onlineIP;

            // Set the picker selection
            if (networkType == "Local Network")
            {
                Picker_Network.SelectedIndex = 0;
            }
            else if (networkType == "Online Network")
            {
                Picker_Network.SelectedIndex = 1;
            }

            System.Diagnostics.Debug.WriteLine($"Loaded configuration: Local={localIP}, Online={onlineIP}, Type={networkType}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading saved configuration: {ex.Message}");
        }
    }

    private async void B_Apply_Clicked(object sender, EventArgs e)
    {
        try
        {
            // Disable button during processing
            B_Apply.IsEnabled = false;
            B_Apply.Text = "Applying...";

            // Validate inputs
            if (string.IsNullOrWhiteSpace(Entry_IPLocal.Text) || string.IsNullOrWhiteSpace(Entry_IPOnline.Text))
            {
                await DisplayAlert("Error", "Please fill in both IP addresses", "OK");
                return;
            }

            if (Picker_Network.SelectedIndex == -1)
            {
                await DisplayAlert("Error", "Please select a network type", "OK");
                return;
            }

            string selectedNetwork = Picker_Network.Items[Picker_Network.SelectedIndex];
            string selectedIP = "";

            // Determine which IP to use based on selection
            if (selectedNetwork == "Local Network")
            {
                selectedIP = Entry_IPLocal.Text.Trim();
                // Add http:// if not present
                if (!selectedIP.StartsWith("http://") && !selectedIP.StartsWith("https://"))
                {
                    selectedIP = "http://" + selectedIP;
                }
            }
            else if (selectedNetwork == "Online Network")
            {
                selectedIP = Entry_IPOnline.Text.Trim();
                // Add https:// for domain names if not present
                if (!selectedIP.StartsWith("http://") && !selectedIP.StartsWith("https://"))
                {
                    selectedIP = "https://" + selectedIP;
                }
            }

            System.Diagnostics.Debug.WriteLine($"Selected IP: {selectedIP}");

            // Test connection using ConnectionService
            bool connectionSuccess = await ConnectionService.UpdateAndTestIP(selectedIP);

            if (connectionSuccess)
            {
                // Save configuration to Preferences
                Preferences.Set("LocalIP", Entry_IPLocal.Text.Trim());
                Preferences.Set("OnlineIP", Entry_IPOnline.Text.Trim());
                Preferences.Set("NetworkType", selectedNetwork);
                Preferences.Set("SelectedIP", selectedIP);

                System.Diagnostics.Debug.WriteLine($"Configuration saved and applied: {selectedIP}");

                await DisplayAlert("Success", $"Connection successful!\nUsing: {selectedIP}", "OK");

                // Navigate back to previous page or main page
                await NavigateBack();
            }
            else
            {
                await DisplayAlert("Connection Failed", $"Unable to connect to {selectedIP}\nPlease check the IP address and try again.", "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Apply connection error: {ex.Message}");
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }
        finally
        {
            // Re-enable button
            B_Apply.IsEnabled = true;
            B_Apply.Text = "Apply Connection";
        }
    }

    private async Task NavigateBack()
    {
        try
        {
            // Check if user is logged in to determine where to navigate
            if (Login.IsUserLoggedIn())
            {
                // If logged in, go to TabPage
                Application.Current.MainPage = new TabPage();
            }
            else
            {
                // If not logged in, go to Login page
                Application.Current.MainPage = new NavigationPage(new Login());
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
            // Fallback - just pop the current page
            if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync();
            }
        }
    }
}