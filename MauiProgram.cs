using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using The49.Maui.BottomSheet;
using Toko2025.Services;
using ZXing.Net.Maui.Controls;
using DotNet.Meteor.HotReload.Plugin;

namespace Toko2025;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
            .UseBottomSheet()
            .UseBarcodeReader()
            .UseMauiCommunityToolkit()

            .ConfigureFonts(fonts =>
			{
				fonts.AddFont("Lexend-Light.ttf", "FontRegular");
				fonts.AddFont("Lexend-SemiBold.ttf", "FontBold");
			});

#if ANDROID
        // Register Bluetooth service for Android
        builder.Services.AddSingleton<IBluetoothService, Toko2025.Platforms.Android.AndroidBluetoothService>();
#endif


#if DEBUG
		builder.EnableHotReload();
        builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
