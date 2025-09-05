using Toko2025.Services;

namespace Toko2025;

public partial class TabPage : Shell
{
	public TabPage()
	{
		InitializeComponent();
	}

	protected override bool OnBackButtonPressed()
	{
		// Handle back button press for exit protection
		_ = Task.Run(async () => await HandleExitProtection());
		return true; // Prevent default back button behavior
	}

	private async Task HandleExitProtection()
	{
		try
		{
			// Check if user has items in cart
			if (await HasActiveCart())
			{
				await MainThread.InvokeOnMainThreadAsync(async () =>
				{
					bool exitConfirm = await DisplayAlert(
						"Exit Application", 
						"You have items in your cart. Exiting will clear your cart. Do you want to continue?", 
						"Exit & Clear Cart", 
						"Stay");

					if (exitConfirm)
					{
						// Clear cart and exit
						await ClearCartAndExit();
					}
					// If user chooses "Stay", do nothing (stay in app)
				});
			}
			else
			{
				// No cart items, exit normally
				await MainThread.InvokeOnMainThreadAsync(() =>
				{
					Application.Current?.Quit();
				});
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error in exit protection: {ex.Message}");
			// If error, exit normally
			await MainThread.InvokeOnMainThreadAsync(() =>
			{
				Application.Current?.Quit();
			});
		}
	}

	private async Task<bool> HasActiveCart()
	{
		try
		{
			// Get current user
			var (id_user, _, _, _, _, _) = Login.GetLoggedInUser();
			
			if (id_user <= 0)
				return false;

			// Check if there's active penjualan in preferences
			int penjualanId = Preferences.Get($"active_penjualan_id_{id_user}", 0);
			
			if (penjualanId <= 0)
				return false;

			// Check if cart has items by calling API
			string apiUrl = $"{App.IP}/api/penjualan/cart/{penjualanId}";
			var response = await App.SharedHttpClient.GetAsync(apiUrl);
			
			if (response.IsSuccessStatusCode)
			{
				var jsonContent = await response.Content.ReadAsStringAsync();
				var cartResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(jsonContent);
				
				// Check if cart has items
				if (cartResponse?.success == true && cartResponse?.data?.items != null)
				{
					var items = cartResponse.data.items;
					return items.Count > 0;
				}
			}
			
			return false;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error checking cart: {ex.Message}");
			return false;
		}
	}

	private async Task ClearCartAndExit()
	{
		try
		{
			// Get current user
			var (id_user, _, _, _, _, _) = Login.GetLoggedInUser();
			
			if (id_user > 0)
			{
				// Get penjualan ID
				int penjualanId = Preferences.Get($"active_penjualan_id_{id_user}", 0);
				
				if (penjualanId > 0)
				{
					// Call DELETE API to clear cart
					string apiUrl = $"{App.IP}/api/penjualan/{penjualanId}";
					await App.SharedHttpClient.DeleteAsync(apiUrl);
					
					System.Diagnostics.Debug.WriteLine($"Cart cleared on exit for user {id_user}");
				}
				
				// Clear preferences
				Preferences.Remove($"active_penjualan_id_{id_user}");
				Preferences.Remove($"active_faktur_{id_user}");
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error clearing cart on exit: {ex.Message}");
		}
		finally
		{
			// Exit application
			Application.Current?.Quit();
		}
	}
}